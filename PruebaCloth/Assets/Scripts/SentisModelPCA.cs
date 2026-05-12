using System;
using System.IO;
using Unity.InferenceEngine; // O Unity.Sentis dependiendo de tu versión exacta
using UnityEngine;
using Newtonsoft.Json;

public class ClothMLpca : MonoBehaviour
{
    [SerializeField]
    public GameObject ball;
    public SphereCollider ballCollider;
    public ModelAsset modelAsset;
    public MeshFilter clothMeshFilter;

    private float[] maxDistance;

    Worker worker;
    Tensor<float> inputTensor;

    public int contador = 0;
    int vertexCount;

    // --- NUEVO: Parámetros de la Secuencia ---
    private int seqLen = 5; 
    // Buffer en espacio PCA: [tiempo, n_components]
    // (ya no es por vértice — el PCA aplana todo el frame en un vector)
    private float[,] historyBuffer; // [seqLen, optimal_n]
    private int optimalN;           // leído del JSON

    private Vector3[] lastVertexPositions;

    public TextAsset jsonFile;

    [System.Serializable]
    public class NormalizationData
    {
        // StandardScaler aplicado antes del PCA
        public float[] input_scaler_mean;   // longitud = vertices * 4
        public float[] input_scaler_std;    // longitud = vertices * 4

        // PCA: components [optimal_n, vertices*4], mean [vertices*4]
        public float[] pca_components;      // aplanado: optimal_n × (vertices*4)
        public float[] pca_mean;            // longitud = vertices*4

        // Normalización del target (delta XYZ)
        public float[] target_mean;         // longitud = 3
        public float[] target_std;          // longitud = 3

        // Metadatos
        public int optimal_n;
        public int num_vertices;
    }
    public NormalizationData normData;

    // Dimensión del vector de entrada plano por frame: vertices * 4 (x,y,z,sdf)
    private int rawFeatureSize;


    void Awake()
    {
        if (jsonFile != null)
            normData = JsonConvert.DeserializeObject<NormalizationData>(jsonFile.text);
    }

    void Start()
    {
        var model = ModelLoader.Load(modelAsset);
        worker = new Worker(model, BackendType.GPUCompute);

        clothMeshFilter.mesh.MarkDynamic();
        vertexCount = clothMeshFilter.mesh.vertexCount;

        optimalN = normData.optimal_n;
        rawFeatureSize = normData.num_vertices * 13; // x,y,z,sdf × vértices

        maxDistance = new float[vertexCount];
        historyBuffer = new float[seqLen, optimalN];

        // Definir puntos anclados (0 = se mueve)
        int i = 0;
        //MINI
        //for (; i < 4; i++)
        //{
        //    maxDistance[i] = 1.0f;
        //}
        while (i < vertexCount)
        {
            maxDistance[i] = 0.2f;
            i++;
        }
        //MAX
        maxDistance[11] = 0f;
        maxDistance[12] = 0f;
        maxDistance[18] = 0f;
        maxDistance[22] = 0f;
        maxDistance[24] = 0f;

        /* // Falda 32 v
        maxDistance[2] = 0f;
        maxDistance[3] = 0f;
        maxDistance[4] = 0f;
        maxDistance[6] = 0f;
        maxDistance[8] = 0f;
        maxDistance[10] = 0f;
        maxDistance[12] = 0f;
        maxDistance[14] = 0f;
        */


        int expectedSize = optimalN * rawFeatureSize;
        int actualSize = normData.pca_components.Length;
        Debug.Log($"PCA components — esperado: {expectedSize}, cargado: {actualSize}");

        if (expectedSize != actualSize)
            Debug.LogError("¡El tamaño de pca_components no coincide! Regenera el JSON.");

        // Llenar el buffer histórico con el frame inicial (tela en reposo)
        lastVertexPositions = clothMeshFilter.mesh.vertices;
        float[] initialPCA = ComputePCAFrame(lastVertexPositions);

        for (int t = 0; t < seqLen; t++)
            for (int c = 0; c < optimalN; c++)
                historyBuffer[t, c] = initialPCA[c];

    }

    void FixedUpdate()
    {
        var mesh = clothMeshFilter.mesh;
        var vertices = mesh.vertices;

        // 1. Desplazar la historia hacia atrás (t=0 desaparece, todo se mueve a la izquierda)
        for (int t = 0; t < seqLen - 1; t++)
            for (int c = 0; c < optimalN; c++)
                historyBuffer[t, c] = historyBuffer[t + 1, c];


        // 2. Calcular los features del frame actual y ponerlos al final de la historia (t = seqLen - 1)
        float[] currentPCA = ComputePCAFrame(vertices);
        for (int c = 0; c < optimalN; c++)
            historyBuffer[seqLen - 1, c] = currentPCA[c];

        // 3. Construir tensor de entrada: [1, seqLen, 1, optimalN]
        //    (1 "vértice virtual" con optimalN features, igual que en Python)
        inputTensor = new Tensor<float>(new TensorShape(1, seqLen, 1, optimalN));
        for (int t = 0; t < seqLen; t++)
            for (int c = 0; c < optimalN; c++)
                inputTensor[0, t, 0, c] = historyBuffer[t, c];

        // 4. Ejecutar modelo
        worker.Schedule(inputTensor);
        using var output = worker.PeekOutput() as Tensor<float>;
        var result = output.ReadbackAndClone();

        // 5. Aplicar predicciones (delta XYZ denormalizado) a cada vértice
        Vector3[] newVertices = new Vector3[vertexCount];

        for (int i = 0; i < vertexCount; i++)
        {
            // Comprobamos si el vértice está anclado ---
            if (maxDistance[i] == 0f)
            {
                newVertices[i] = vertices[i];
                continue; // Pasamos al siguiente vértice
            }

            // Denormalizar delta XYZ: delta_real = pred * target_std + target_mean
            float dx = result[0, i, 0] * normData.target_std[0] + normData.target_mean[0];
            float dy = result[0, i, 1] * normData.target_std[1] + normData.target_mean[1];
            float dz = result[0, i, 2] * normData.target_std[2] + normData.target_mean[2];

            newVertices[i] = vertices[i] + new Vector3(dx, dy, dz);
        }

        mesh.SetVertices(newVertices);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        // Al final de FixedUpdate, antes de los Dispose:
        for (int v = 0; v < vertexCount; v++)
            lastVertexPositions[v] = newVertices[v];

        inputTensor.Dispose();
        result.Dispose();
    }

    private float[] ComputePCAFrame(Vector3[] verts)
    {
        var mesh = clothMeshFilter.mesh;
        var normals = mesh.normals;
        var uvs = mesh.uv;

        // ── Paso 1: vector plano raw [78] = 6 vértices × 13 features ──
        float[] raw = new float[rawFeatureSize]; // rawFeatureSize = 78
        for (int v = 0; v < normData.num_vertices; v++)
        {
            Vector3 pos = verts[v];
            Vector3 vel = (pos - lastVertexPositions[v]) / Time.fixedDeltaTime;
            float sdf = Vector3.Distance(pos,
                                 transform.InverseTransformPoint(ball.transform.position))
                             - ballCollider.radius;
            Vector3 normal = normals[v];

            // Mismo orden que el CSV
            int f = v * 13;
            raw[f++] = pos.x;
            raw[f++] = pos.y;
            raw[f++] = pos.z;
            raw[f++] = vel.x;
            raw[f++] = vel.y;
            raw[f++] = vel.z;
            raw[f++] = sdf;
            raw[f++] = normal.x;
            raw[f++] = normal.y;
            raw[f++] = normal.z;
            raw[f++] = maxDistance[v];
            raw[f++] = uvs[v].x;
            raw[f++] = uvs[v].y;
        }

        // ── Paso 2: StandardScaler ────────────────────────────────────
        float[] scaled = new float[rawFeatureSize];
        for (int f = 0; f < rawFeatureSize; f++)
            scaled[f] = (raw[f] - normData.input_scaler_mean[f]) / normData.input_scaler_std[f];

        // ── Paso 3: centrar con pca_mean ─────────────────────────────
        float[] centered = new float[rawFeatureSize];
        for (int f = 0; f < rawFeatureSize; f++)
            centered[f] = scaled[f] - normData.pca_mean[f];

        // ── Paso 4: proyección PCA ────────────────────────────────────
        float[] pca = new float[optimalN];
        for (int c = 0; c < optimalN; c++)
        {
            float dot = 0f;
            for (int f = 0; f < rawFeatureSize; f++)
                dot += normData.pca_components[c * rawFeatureSize + f] * centered[f];
            pca[c] = dot;
        }

        return pca;
    }

    /*
    /// <summary>
    /// Dado el array de vértices actual, calcula el vector PCA del frame:
    ///   1. Construir vector plano [x0,y0,z0,sdf0, x1,y1,z1,sdf1, ...]
    ///   2. Aplicar StandardScaler: z = (x - scaler_mean) / scaler_std
    ///   3. Aplicar PCA:            p = components × (z - pca_mean)
    /// Devuelve float[optimal_n]
    /// </summary>
    private float[] ComputePCAFrame(Vector3[] verts)
    {
        // ── Paso 1: vector plano raw ──────────────────────────────
        float[] raw = new float[rawFeatureSize];
        for (int v = 0; v < normData.num_vertices; v++)
        {
            Vector3 pos = verts[v];
            float sdf = Vector3.Distance(pos,
                              transform.InverseTransformPoint(ball.transform.position))
                          - ballCollider.radius;

            raw[v * 4 + 0] = pos.x;
            raw[v * 4 + 1] = pos.y;
            raw[v * 4 + 2] = pos.z;
            raw[v * 4 + 3] = sdf;
        }

        // ── Paso 2: StandardScaler ────────────────────────────────
        float[] scaled = new float[rawFeatureSize];
        for (int f = 0; f < rawFeatureSize; f++)
            scaled[f] = (raw[f] - normData.input_scaler_mean[f]) / normData.input_scaler_std[f];

        // ── Paso 3: centrar con pca_mean ──────────────────────────
        float[] centered = new float[rawFeatureSize];
        for (int f = 0; f < rawFeatureSize; f++)
            centered[f] = scaled[f] - normData.pca_mean[f];

        // ── Paso 4: proyección PCA ────────────────────────────────
        // pca_components está aplanado: [optimal_n × rawFeatureSize]
        // resultado[c] = dot(components[c], centered)
        float[] pca = new float[optimalN];
        for (int c = 0; c < optimalN; c++)
        {
            float dot = 0f;
            for (int f = 0; f < rawFeatureSize; f++)
                dot += normData.pca_components[c * rawFeatureSize + f] * centered[f];
            pca[c] = dot;
        }

        return pca;
    }
    */


    void OnDestroy()
    {
        worker?.Dispose();
    }
}
