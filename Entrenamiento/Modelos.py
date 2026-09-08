import torch.nn as nn
import torch 
# RNN
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
        x_flat = x.view(B, T, -1)                          # [B, T, V*F]
        h_t = torch.zeros(B, self.hidden_size, device=x.device)
        for t in range(T):
            h_t = self.rnn_cell(x_flat[:, t, :], h_t)
        pred_flat  = self.fc(h_t)                          # [B, V*3]
        pred_delta = pred_flat.view(B, self.num_vertices, self.num_outputs)
        return pred_delta

# RNN con PCA
class FlatRNNClothUnityPCA(nn.Module):
    def __init__(self, n_pca_components, hidden_size):
        """
        n_pca_components : número de componentes PCA.
                           Es tanto la dimensión de entrada (por frame)
                           como la dimensión de salida (delta PCA predicho).
        """
        super().__init__()
        self.n_pca = n_pca_components
        self.hidden_size = hidden_size

        self.rnn_cell = nn.RNNCell( # el LSTM la verdad que creo que hay que cambiarlo por GRU, en las pruebas el tiempo fue bastante peor
            input_size=self.n_pca,
            hidden_size=self.hidden_size,
            nonlinearity = 'relu'
        )
        # La salida la regresión sobre los componentes, no xyz
        self.fc = nn.Linear(hidden_size, n_pca_components)

    def forward(self, x):
        """
        x      : [Batch, Seq_Len, 1, n_pca_components]
        return : [Batch, n_pca_components]  — delta PCA normalizado
        """
        B   = x.size(0)
        T   = x.size(1)
        x_flat = x.view(B, T, -1)         # [B, T, n_pca]
        h_t = torch.zeros(B, self.hidden_size, device=x.device) # Inicialización del estado oculto
        for t in range(T):
                    h_t = self.rnn_cell(x_flat[:, t, :], h_t)
        pred_delta  = self.fc(h_t)  
        return pred_delta               # [B, n_pca] — delta PCA normalizado

# MLP
class FlatMLPClothUnity(nn.Module):
    """
    MLP feed-forward que toma toda la ventana temporal aplanada como entrada.

    Input:  [B, seq_len, V, F]  →  se aplana a  [B, seq_len * V * F]
    Output: [B, V, 3]           →  delta XYZ por vértice

    Al no tener estado oculto, el grafo ONNX es una secuencia de
    operaciones matriciales simples, compatible con cualquier versión
    de Unity Inference Engine sin problemas de operadores recurrentes.
    """
    def __init__(self, seq_len, num_vertices, num_features,
                 hidden_size, num_layers=4, num_outputs=3):
        super().__init__()
        self.num_vertices = num_vertices
        self.num_outputs  = num_outputs
        self.seq_len      = seq_len

        input_size = seq_len * num_vertices * num_features  # ventana completa aplanada

        # Bloque de capas ocultas
        layers = [nn.Linear(input_size, hidden_size), nn.ReLU()]
        for _ in range(num_layers - 1):
            layers += [nn.Linear(hidden_size, hidden_size), nn.ReLU()]
        layers.append(nn.Linear(hidden_size, num_vertices * num_outputs))

        self.net = nn.Sequential(*layers)

    def forward(self, x):
        """
        x: [Batch, seq_len, Vertices, Features]
        returns pred_delta: [Batch, Vertices, 3]
        """
        B = x.shape[0]
        x_flat = x.view(B, -1)                              # [B, seq_len*V*F]
        out    = self.net(x_flat)                           # [B, V*3]
        return out.view(B, self.num_vertices, self.num_outputs)
