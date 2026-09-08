'''
Funciones para facilitar la visualización de los entrenamientos y métricas
'''
import numpy as np
import torch
from matplotlib import pyplot as plt
import Normalizacion

def plot_loss_curves(history):
    """
    Curvas de train/test loss + error medio por vértice a lo largo del training.
    """

    fig, axes = plt.subplots(1, 2, figsize=(14, 5))

    axes[0].plot(history["train"], label="Train Loss")
    axes[0].plot(history["test"], label="Test Loss")
    axes[0].set_title("Unrolled Training Loss")
    axes[0].set_xlabel("Epoch")
    axes[0].set_ylabel("Loss")
    axes[0].legend()
    axes[0].grid(True)

    axes[1].plot(history["vertex_error"], color="orange", label="Avg Vertex Error")
    axes[1].set_title("Avg Vertex Position Error")
    axes[1].set_xlabel("Epoch")
    axes[1].set_ylabel("Error (units)")
    axes[1].legend()
    axes[1].grid(True)

    plt.tight_layout()
    plt.show()



def plot_rollout_error(rollout_preds, rollout_reals, N_STEPS):
    '''
    Visualización de la progresión de error en el rollout.
    '''
    # Error medio por vértice en cada paso del rollout
    rollout_errors = [
        np.linalg.norm(rollout_preds[i] - rollout_reals[i], axis=-1).mean()
        for i in range(N_STEPS)
    ]
    n_steps = len(rollout_errors)
    plt.figure(figsize=(10, 4))
    plt.plot(range(1, n_steps + 1), rollout_errors, marker="o")
    plt.title(f"Error medio por vértice a lo largo de {n_steps} pasos de rollout")
    plt.xlabel("Paso futuro predicho")
    plt.ylabel("Avg Vertex Error (units)")
    plt.grid(True)
    plt.show()
    print("Unrolling correcto con el aumento progresivo de la curva. Si explota aumentar los pasos de rollout o noise.")

# mover a otro utils 
def get_rollout_params(model, test_loader, train_size, SEQ_LEN, data_tensor , norm_data = Normalizacion.Norm):
    '''
    Función auxiliar para obtener los rollout steps 
    '''
    model.eval()
    N_STEPS = 30  # cuántos pasos predecir en cadena

    # Primer batch del test
    seq_sample, future_sample = next(iter(test_loader))
    seq_sample    = seq_sample[:1].float()     # solo 1 ejemplo
    future_sample = future_sample[:1].float()

    # Datos reales que seguirán (para comparar)
    # Buscamos los siguientes N_STEPS frames desde el dataset
    start_idx = train_size  # primer índice del test set
    end_idx   = start_idx + SEQ_LEN + N_STEPS
    real_sequence = data_tensor[start_idx:end_idx].unsqueeze(0).float()  # [1, SEQ+N, V, F]

    rollout_preds = []   # posiciones predichas
    rollout_reals = []   # posiciones reales

    window = seq_sample.clone()  # [1, seq_len, V, F]

    with torch.no_grad():
        for step in range(N_STEPS):
            window_norm  = norm_data.norm_input(window)
            pred_dn      = model(window_norm)                        # [1, V, 3]
            last_pos     = window[:, -1, :, 0:3]
            pred_pos     = last_pos + norm_data.denorm_delta(pred_dn)          # [1, V, 3]

            rollout_preds.append(pred_pos.squeeze(0).numpy())        # [V, 3]
            real_pos = real_sequence[:, SEQ_LEN + step, :, 0:3]
            rollout_reals.append(real_pos.squeeze(0).numpy())

            # Feedback: insertar predicción en la ventana
            new_frame = window[:, -1, :, :].clone()
            new_frame[:, :, 0:3] = pred_pos
            window = torch.cat([window[:, 1:, :, :], new_frame.unsqueeze(1)], dim=1)
    return rollout_preds, rollout_reals


def plot_3d_rollout(
    rollout_reals: list[np.ndarray],
    rollout_preds: list[np.ndarray],
    num_vertices: int,
    steps_to_show: list[int] = (0, 4, 9, 19),
) -> None:
    """
    Comparación 3D real vs predicho en varios pasos futuros del rollout.
    """
    fig = plt.figure(figsize=(16, 4))

    for col, step in enumerate(steps_to_show):
        ax = fig.add_subplot(1, len(steps_to_show), col + 1, projection="3d")
        real = rollout_reals[step]
        pred = rollout_preds[step]

        ax.scatter(real[:, 0], real[:, 1], real[:, 2], c="blue", s=30, label="Real", marker="o")
        ax.scatter(pred[:, 0], pred[:, 1], pred[:, 2], c="red", s=30, label="Pred", marker="x")

        for i in range(num_vertices):
            ax.plot(
                [real[i, 0], pred[i, 0]],
                [real[i, 1], pred[i, 1]],
                [real[i, 2], pred[i, 2]],
                color="gray",
                linestyle="dotted",
                linewidth=0.5,
            )

        ax.set_title(f"Paso +{step + 1}")
        ax.set_xlabel("X")
        ax.set_ylabel("Y")
        ax.set_zlabel("Z")
        if col == 0:
            ax.legend(fontsize=7)

    plt.suptitle("Rollout autoregresivo: Real (azul) vs Predicho (rojo)", y=1.01)
    plt.tight_layout()
    plt.show()


def plot_pred_vs_real_scatter(preds_pos: np.ndarray, targets_pos: np.ndarray) -> None:
    """
    Scatter de todas las coordenadas predichas vs reales, con la línea y=x.
    """
    true_flat = targets_pos.reshape(-1)
    pred_flat = preds_pos.reshape(-1)

    plt.figure(figsize=(12, 12))
    plt.scatter(true_flat, pred_flat, alpha=0.1, color="purple", s=2)

    min_val = min(true_flat.min(), pred_flat.min())
    max_val = max(true_flat.max(), pred_flat.max())
    plt.plot([min_val, max_val], [min_val, max_val], color="black", linestyle="--", linewidth=2, label="Perfect Prediction")

    plt.title("Predicted vs. Real Positions (All Coordinates)")
    plt.xlabel("Real Position Coordinate")
    plt.ylabel("Predicted Position Coordinate")
    plt.legend()
    plt.grid(True)
    plt.axis("equal")
    plt.show()
