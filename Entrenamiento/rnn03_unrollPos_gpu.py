import json
import os

import numpy as np
import pandas as pd
import torch
import torch.nn as nn
from torch.utils.data import DataLoader, Dataset

# Ejecutamos en GPU si está disponible
if torch.cuda.is_available():
    device = torch.device("cuda")
    print("GPU is available")
else:
    device = torch.device("cpu")
    print("GPU is not available, using CPU")


# Parámetros
SEQ_LEN       = 8 
ROLLOUT_STEPS = 12
BATCH_SIZE    = 64
HIDDEN_SIZE   = 128
EPOCHS        = 500
NOISE_SIGMA   = 0.03
VEL_WEIGHT    = 1.0
N_EVAL_STEPS  = 30   # pasos de rollout autoregresivo en evaluación final


# Dataset de la tela
class ClothUnrolledDataset(Dataset):
    """
    Devuelve:
      - sequence:       [seq_len, Vertices, Features]          
      - future_frames:  [rollout_steps, Vertices, Features]      
    """
    def __init__(self, data_frames, seq_len=5, rollout_steps=8):
        if not isinstance(data_frames, torch.Tensor):
            self.data = torch.tensor(data_frames, dtype=torch.float32)
        else:
            self.data = data_frames
        self.seq_len       = seq_len
        self.rollout_steps = rollout_steps

    def __len__(self):
        return len(self.data) - self.seq_len - self.rollout_steps

    def __getitem__(self, idx):
        sequence      = self.data[idx : idx + self.seq_len]
        future_frames = self.data[idx + self.seq_len : idx + self.seq_len + self.rollout_steps]
        return sequence, future_frames


# Modelo RNN con rollout
class FlatRNNClothUnity(nn.Module):
    def __init__(self, num_vertices, num_features, hidden_size, num_outputs=3):
        super().__init__()
        self.num_vertices = num_vertices
        self.num_outputs  = num_outputs
        self.input_size   = num_vertices * num_features
        self.hidden_size  = hidden_size

        self.rnn_cell = nn.RNNCell(
            input_size=self.input_size,
            hidden_size=self.hidden_size,
            nonlinearity='relu'
        )
        self.fc = nn.Linear(self.hidden_size, num_vertices * num_outputs)

    def forward(self, x):
        """
        x: [Batch, Seq_Len, Vertices, Features]
        returns pred_delta: [Batch, Vertices, 3]
        """
        B, T, V, F = x.shape
        x_flat = x.view(B, T, -1)
        h_t = torch.zeros(B, self.hidden_size, device=x.device)
        for t in range(T):
            h_t = self.rnn_cell(x_flat[:, t, :], h_t)
        pred_flat  = self.fc(h_t)
        pred_delta = pred_flat.view(B, self.num_vertices, self.num_outputs)
        return pred_delta


# Métodos auxiliares de normalización
def norm_input(x, mean, std):
    return (x - mean) / std

def denorm_delta(d, mean, std):
    return d * std + mean

def norm_delta(d, mean, std):
    return (d - mean) / std


# Rollout
def run_rollout(model, batch_seq, batch_future,
                input_mean, input_std, target_mean, target_std,
                criterion, add_noise=False):
    B, W, V, F = batch_future.shape
    window = batch_seq.clone()

    if add_noise:
        noise = torch.randn_like(window[:, -2:, :, :]) * NOISE_SIGMA
        window[:, -2:, :, :] = window[:, -2:, :, :] + noise

    total_loss    = torch.tensor(0.0, device=batch_seq.device)
    prev_pred_pos = window[:, -1, :, 0:3]

    for k in range(W):
        window_norm     = norm_input(window, input_mean, input_std)
        pred_delta_norm = model(window_norm)

        last_pos        = window[:, -1, :, 0:3]
        pred_delta_real = denorm_delta(pred_delta_norm, target_mean, target_std)
        pred_pos        = last_pos + pred_delta_real

        real_pos      = batch_future[:, k, :, 0:3]
        real_prev_pos = (
            batch_future[:, k-1, :, 0:3] if k > 0
            else batch_seq[:, -1, :, 0:3]
        )

        real_delta      = real_pos - last_pos
        real_delta_norm = norm_delta(real_delta, target_mean, target_std)
        loss_pos        = criterion(pred_delta_norm, real_delta_norm)

        pred_vel = pred_pos - prev_pred_pos
        real_vel = real_pos - real_prev_pos
        loss_vel = criterion(
            pred_vel / target_std.clamp(min=1e-8),
            real_vel / target_std.clamp(min=1e-8)
        )

        total_loss    = total_loss + loss_pos + VEL_WEIGHT * loss_vel

        new_frame = window[:, -1, :, :].clone()
        new_frame[:, :, 0:3] = pred_pos.detach() if not model.training else pred_pos
        window = torch.cat([window[:, 1:, :, :], new_frame.unsqueeze(1)], dim=1)

        prev_pred_pos = pred_pos

    return total_loss / W



def main():
    # Carga dataset
    num_vertices = 25
    num_features = 4  # x, y, z, sdf

    csv_path = 'data/clothDataset_107_.csv'
    df = pd.read_csv(csv_path)

    selected_columns = []
    for i in range(num_vertices):
        selected_columns.extend([f'x{i}', f'y{i}', f'z{i}', f'sdf{i}'])

    data_flat  = df[selected_columns].values
    num_frames = data_flat.shape[0]
    data_3d    = data_flat.reshape((num_frames, num_vertices, num_features))
    data_tensor = torch.tensor(data_3d, dtype=torch.float32)

    print(f"Frames totales: {num_frames}")
    print(f"Tensor shape: {data_tensor.shape}  # [Frames, Vertices, Features]")

    # Cloth dataset y separación en train y test
    dataset    = ClothUnrolledDataset(data_tensor, seq_len=SEQ_LEN, rollout_steps=ROLLOUT_STEPS)
    train_size = int(0.8 * len(dataset))

    train_dataset = torch.utils.data.Subset(dataset, range(train_size))
    test_dataset  = torch.utils.data.Subset(dataset, range(train_size, len(dataset)))

    pin = device.type == "cuda"
    train_loader = DataLoader(train_dataset, batch_size=BATCH_SIZE, shuffle=True,
                              num_workers=4, pin_memory=pin, persistent_workers=True)
    test_loader  = DataLoader(test_dataset,  batch_size=BATCH_SIZE, shuffle=False,
                              num_workers=4, pin_memory=pin, persistent_workers=True)

    seq_b, fut_b = next(iter(train_loader))
    print(f"sequence shape:      {seq_b.shape}   # [B, seq_len, V, F]")
    print(f"future_frames shape: {fut_b.shape}  # [B, rollout, V, F]")

    # Obtención de parámetros de normalización
    all_inputs  = []
    all_targets = []

    for batch_seq, batch_future in train_loader:
        all_inputs.append(batch_seq.view(-1, num_features))
        last_pos         = batch_seq[:, -1, :, 0:3]
        first_future_pos = batch_future[:, 0, :, 0:3]
        delta = first_future_pos - last_pos
        all_targets.append(delta.view(-1, 3))

    full_input  = torch.cat(all_inputs,  dim=0)
    full_target = torch.cat(all_targets, dim=0)

    input_mean  = full_input.mean(dim=0).to(device)
    input_std   = full_input.std(dim=0).clamp(min=1e-8).to(device)
    target_mean = full_target.mean(dim=0).to(device)
    target_std  = full_target.std(dim=0).clamp(min=1e-8).to(device)

    norm_data = {
        "mean":        input_mean.cpu().tolist(),
        "std":         input_std.cpu().tolist(),
        "target_mean": target_mean.cpu().tolist(),
        "target_std":  target_std.cpu().tolist(),
    }
    with open("rnd25unroll.json", "w") as f:
        json.dump(norm_data, f)
    print("Norm params guardados en rnd25unroll.json")

    # Creación del modelo 
    model = FlatRNNClothUnity(num_vertices=num_vertices, num_features=num_features, hidden_size=HIDDEN_SIZE, num_outputs=3).to(device)

    total_params = sum(p.numel() for p in model.parameters())
    print(f"Parámetros totales: {total_params:,}")
    print(f"Modelo en: {next(model.parameters()).device}")

    # Entrenamiento
    criterion = nn.L1Loss()
    optimizer = torch.optim.Adam(model.parameters(), lr=1e-4)
    scheduler = torch.optim.lr_scheduler.ExponentialLR(optimizer, gamma=0.999)

    history = {'train': [], 'test': [], 'vertex_error': []}

    for epoch in range(1, EPOCHS + 1):

        # Train
        model.train()
        train_loss = 0.0

        for batch_seq, batch_future in train_loader:
            batch_seq    = batch_seq.float().to(device, non_blocking=True)
            batch_future = batch_future.float().to(device, non_blocking=True)

            loss = run_rollout(model, batch_seq, batch_future,
                               input_mean, input_std, target_mean, target_std,
                               criterion, add_noise=True)

            optimizer.zero_grad()
            loss.backward()
            torch.nn.utils.clip_grad_norm_(model.parameters(), 1.0)
            optimizer.step()
            train_loss += loss.item()

        scheduler.step()
        avg_train = train_loss / len(train_loader)

        # Test
        model.eval()
        test_loss   = 0.0
        total_err   = 0.0
        total_verts = 0

        with torch.no_grad():
            for batch_seq, batch_future in test_loader:
                batch_seq    = batch_seq.float().to(device, non_blocking=True)
                batch_future = batch_future.float().to(device, non_blocking=True)

                loss = run_rollout(model, batch_seq, batch_future,
                                   input_mean, input_std, target_mean, target_std,
                                   criterion, add_noise=False)
                test_loss += loss.item()

                window_norm = norm_input(batch_seq, input_mean, input_std)
                pred_dn     = model(window_norm)
                last_pos    = batch_seq[:, -1, :, 0:3]
                pred_pos    = last_pos + denorm_delta(pred_dn, target_mean, target_std)
                target_pos  = batch_future[:, 0, :, 0:3]

                distances    = torch.norm(pred_pos - target_pos, dim=-1)
                total_err   += distances.sum().item()
                total_verts += distances.numel()

        avg_test     = test_loss / len(test_loader)
        avg_vert_err = total_err / total_verts

        history['train'].append(avg_train)
        history['test'].append(avg_test)
        history['vertex_error'].append(avg_vert_err)

        if epoch % 100 == 0 or epoch == 1:
            print(f"Epoch {epoch:03d} | Train: {avg_train:.6f} | "
                  f"Test: {avg_test:.6f} | VertErr: {avg_vert_err:.6f}")

    print("\nEntrenamiento completado.")

    # Guardamos evolución del entrenamiento (ahora que no estamos con el notebook)
    with open("training_history.json", "w") as f:
        json.dump(history, f)
    print("History guardado en training_history.json")

    # Evaluación
    model.eval()

    seq_sample, _ = next(iter(test_loader))
    seq_sample    = seq_sample[:1].float().to(device)

    start_idx     = train_size
    end_idx       = start_idx + SEQ_LEN + N_EVAL_STEPS
    real_sequence = data_tensor[start_idx:end_idx].unsqueeze(0).float().to(device)

    rollout_preds  = []
    rollout_reals  = []
    window         = seq_sample.clone()

    with torch.no_grad():
        for step in range(N_EVAL_STEPS):
            window_norm  = norm_input(window, input_mean, input_std)
            pred_dn      = model(window_norm)
            last_pos     = window[:, -1, :, 0:3]
            pred_pos     = last_pos + denorm_delta(pred_dn, target_mean, target_std)

            rollout_preds.append(pred_pos.squeeze(0).cpu().numpy())
            real_pos = real_sequence[:, SEQ_LEN + step, :, 0:3]
            rollout_reals.append(real_pos.squeeze(0).cpu().numpy())

            new_frame             = window[:, -1, :, :].clone()
            new_frame[:, :, 0:3]  = pred_pos
            window = torch.cat([window[:, 1:, :, :], new_frame.unsqueeze(1)], dim=1)

    rollout_errors = [
        np.linalg.norm(rollout_preds[i] - rollout_reals[i], axis=-1).mean()
        for i in range(N_EVAL_STEPS)
    ]
    print("\nError por paso del rollout:")
    for i, err in enumerate(rollout_errors):
        print(f"  Paso {i+1:02d}: {err:.6f}")

    # Exportación a formato ONNX
    import onnx
    import onnxruntime

    model_cpu   = model.cpu()
    dummy_input = torch.randn(1, SEQ_LEN, num_vertices, num_features)

    torch.onnx.export(
        model_cpu,
        dummy_input,
        "rnd25unrolled_rnn.onnx",
        export_params=True,
        opset_version=15,
        input_names=['input'],
        output_names=['output'],
    )

    onnx_model = onnx.load("rnd25unrolled_rnn.onnx")
    onnx.checker.check_model(onnx_model)

    ort_session = onnxruntime.InferenceSession("rnd25unrolled_rnn.onnx")
    ort_out     = ort_session.run(None, {'input': dummy_input.numpy()})
    print(f"\nONNX output shape: {ort_out[0].shape}")
    print("Export exitoso — compatible con Unity Barracuda/Sentis.")

    model = model_cpu.to(device)


if __name__ == "__main__":
    main()
