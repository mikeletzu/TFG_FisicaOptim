# Normalizacion
import torch
import json
import numpy as np

class Norm:
    """Medias/desviaciones de input (x,y,z,sdf) y de target (delta xyz)."""
    input_mean: torch.Tensor
    input_std: torch.Tensor
    target_mean: torch.Tensor
    target_std: torch.Tensor

    def __init__(self, train_loader, num_features):
        all_inputs  = []
        all_targets = []

        for batch_seq, batch_future in train_loader:
            all_inputs.append(batch_seq.view(-1, num_features))
            # Usamos el primer frame futuro como referencia del delta para normalización
            last_pos   = batch_seq[:, -1, :, 0:3]
            first_future_pos = batch_future[:, 0, :, 0:3]
            delta = first_future_pos - last_pos
            all_targets.append(delta.view(-1, 3))

        full_input  = torch.cat(all_inputs, dim=0)
        full_target = torch.cat(all_targets, dim=0)

        self.input_mean  = full_input.mean(dim=0)
        self.input_std   = full_input.std(dim=0).clamp(min=1e-8)
        self.target_mean = full_target.mean(dim=0)
        self.target_std  = full_target.std(dim=0).clamp(min=1e-8)

    def save_stats_JSON(self, output_name):
        norm_data = {
            "mean":        self.input_mean.tolist(),
            "std":         self.input_std.tolist(),
            "target_mean": self.target_mean.tolist(),
            "target_std":  self.target_std.tolist()
        }
        with open(output_name + ".json", "w") as f:
            json.dump(norm_data, f)
        print(output_name + ".json")

    def norm_input(self, x):
        """ x: [..., F=4] """
        return (x - self.input_mean) / self.input_std

    def denorm_delta(self, d):
        """ d: [..., 3] """
        return d * self.target_std + self.target_mean

    def norm_delta(self, d):
        return (d - self.target_mean) / self.target_std


# Futuro, partir del anterior, generalizar si posible
class NormPCA:
    """Medias/desviaciones de input (x,y,z,sdf) y de target (delta xyz)."""
    input_scaler_mean: torch.Tensor
    input_scaler_std: torch.Tensor
    target_scaler_mean: torch.Tensor
    target_scaler_std: torch.Tensor
    pca_components: torch.Tensor
    pca_mean: torch.Tensor
    target_pca_mean: torch.Tensor
    target_pca_std: torch.Tensor
    optimal_n: int
    num_vertices: int
    num_features: int
    pca_min: torch.Tensor
    pca_max: torch.Tensor

    def __init__(self, train_loader, input_scaler, input_pca, optimal_n, num_vertices, num_features, data_train_pca):
        # La media y std del delta en espacio PCA es lo que se usa para entrenar el modelo 
        all_pca_deltas = []
        for batch_seq, batch_future_xyz, batch_future_sdf, batch_future_pca in train_loader:
            for k in range(batch_future_pca.shape[1] - 1):
                pca_cur  = batch_future_pca[:, k,   0, :]   # [B, optimal_n]
                pca_next = batch_future_pca[:, k+1, 0, :]   # [B, optimal_n]
                all_pca_deltas.append((pca_next - pca_cur))

        full_pca_delta = torch.cat(all_pca_deltas, dim=0)   # [N, optimal_n]

        self.pca_mean = torch.from_numpy(input_pca.mean_).float()
        self.input_scaler_mean = torch.from_numpy(input_scaler.mean_).float()
        self.input_scaler_std = torch.from_numpy(input_scaler.scale_).float()
        self.pca_components = torch.from_numpy(input_pca.components_).float()

        self.pca_min = torch.from_numpy(data_train_pca.min(axis=0)).float()
        self.pca_max = torch.from_numpy(data_train_pca.max(axis=0)).float()

        self.target_pca_mean = full_pca_delta.mean(dim=0)
        self.target_pca_std  = full_pca_delta.std(dim=0).clamp(min=1e-8)
        self.num_vertices = num_vertices
        self.num_features = num_features
        self.optimal_n = optimal_n
        


    def save_stats_JSON(self, output_name):
        norm_data = {
            "input_scaler_mean":  self.input_scaler_mean.tolist(),
            "input_scaler_std":   self.input_scaler_std.tolist(),
            "pca_components":     self.pca_components.flatten().tolist(),
            "pca_mean":           self.pca_mean.tolist(),
            # Ahora son vectores de longitud optimal_n (no 3 como antes)
            "target_pca_mean":    self.target_pca_mean.cpu().tolist(),
            "target_pca_std":     self.target_pca_std.cpu().tolist(),
            "optimal_n":          self.optimal_n,
            "num_vertices":       self.num_vertices,
            "num_features":       self.num_features,
            "pca_min":  self.pca_min.tolist(),
            "pca_max":  self.pca_max.tolist() # Para clipping :p
        }
        with open(output_name + ".json", "w") as f:
            json.dump(norm_data, f)
        print(output_name + ".json")

    def norm_input(self, x):
        """ x: [..., F=4] """
        return (x - self.input_mean) / self.input_std

    def denorm_delta(self, d):
        """ d: [..., 3] """
        return d * self.target_std + self.target_mean

    def norm_delta(self, d):
        return (d - self.target_mean) / self.target_std

    '''
    def pca_to_xyz(self, pca_vec):
            """
            pca_vec : [B, optimal_n]
            return  : [B, V, 3]
            """
            scaled = pca_vec @ pca_comp_t + pca_mean_t     # [B, raw_size]  inverse PCA
            raw    = scaled * scaler_std_t + scaler_mean_t  # [B, raw_size]  inverse scaler
            return raw.view(-1, num_vertices, num_features)[:, :, :3]  # [B, V, 3]
    '''