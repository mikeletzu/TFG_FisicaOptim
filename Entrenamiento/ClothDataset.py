# Dataset de la tela
from torch.utils.data import Dataset
import os
os.environ["KMP_DUPLICATE_LIB_OK"] = "TRUE"
import torch


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


class ClothPCADataset(Dataset):
    def __init__(self, pca_frames, xyz_frames, sdf_frames, seq_len=5, rollout_steps=24):
        """
        pca_frames : Tensor [N, 1, n_components]  — frames PCA de toda la secuencia
        xyz_frames : Tensor [N, V, 3]             — posiciones XYZ reales
        sdf_frames : Tensor [N, V]                — SDF real por vértice
        seq_len    : frames históricos 
        """
        self.pca_frames = pca_frames if isinstance(pca_frames, torch.Tensor) \
                           else torch.tensor(pca_frames, dtype=torch.float32)
        self.xyz_frames = xyz_frames if isinstance(xyz_frames, torch.Tensor) \
                           else torch.tensor(xyz_frames, dtype=torch.float32)
        self.sdf_frames = sdf_frames if isinstance(sdf_frames, torch.Tensor) \
                           else torch.tensor(sdf_frames, dtype=torch.float32)
        self.seq_len = seq_len
        self.rollout_steps = rollout_steps

    def __len__(self):
        return len(self.pca_frames) - self.seq_len - self.rollout_steps

    def __getitem__(self, idx):
        seq_pca      = self.pca_frames[idx : idx + self.seq_len]
        future_start = idx + self.seq_len - 1
        future_end   = future_start + self.rollout_steps + 1
        future_xyz   = self.xyz_frames[future_start : future_end]
        future_sdf   = self.sdf_frames[future_start : future_end]
        future_pca   = self.pca_frames[future_start : future_end] # Ahora se queda en subespacio
        return seq_pca, future_xyz, future_sdf, future_pca