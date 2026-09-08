using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json;
using Unity.InferenceEngine;
using UnityEngine;
using Debug = UnityEngine.Debug;

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
    public int vertexCount;

    public int seqLen = 8;
    private int numFeatures = 4;
    // Buffer en espacio PCA: [seqLen, optimal_n]
    private float[,] historyBuffer;
    private int optimalN;

    // se guardan las posiciones predichas
    private Vector3[] lastPredictedPositions;

    // Cache de matrices PCA en formato optimizado para evitar recalcular cada frame
    private float[] pcaMean;           // [rawFeatureSize]
    private float[] scalerMean;        // [rawFeatureSize]
    private float[] scalerStd;         // [rawFeatureSize]
    private float[,] pcaComponents;    // [optimal_n, rawFeatureSize]
    float[] pcaMin, pcaMax;

	public TextAsset jsonFile;

    [System.Serializable]
    public class NormalizationData
    {
        public float[] input_scaler_mean;   // longitud = num_vertices
        public float[] input_scaler_std;    // longitud = num_vertices
        public float[] pca_components;      // aplanado: optimal_n × (num_vertices*13)
        public float[] pca_mean;            // longitud = num_vertices
        public float[] target_pca_mean;
        public float[] target_pca_std;
        public int optimal_n;
        public int num_vertices;
		public float[] pca_min;
		public float[] pca_max;
	}

	public NormalizationData normData;

    private int rawFeatureSize;

	// Midiendo tiempos de tratado de datos y modelo 
	protected Stopwatch stopwatch;

	private List<double> inferenceArchive;
	private double inferenceTime;

	private int inferenceFrames = 0;

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
        rawFeatureSize = normData.num_vertices * numFeatures;

        maxDistance = new float[vertexCount];
        historyBuffer = new float[seqLen, optimalN];

        lastPredictedPositions = new Vector3[vertexCount];
        Vector3[] initialVerts = clothMeshFilter.mesh.vertices;
        for (int i = 0; i < vertexCount; i++)
            lastPredictedPositions[i] = initialVerts[i];

        // Cache de matrices para evitar reconstruirlas en cada FixedUpdate
        scalerMean = normData.input_scaler_mean;
        scalerStd = normData.input_scaler_std;
        pcaMean = normData.pca_mean;

		pcaMin = normData.pca_min;
		pcaMax = normData.pca_max;

		pcaComponents = new float[optimalN, rawFeatureSize];
        for (int c = 0; c < optimalN; c++)
            for (int f = 0; f < rawFeatureSize; f++)
                pcaComponents[c, f] = normData.pca_components[c * rawFeatureSize + f];


        maxDistance = Utils.setMaxDistance(vertexCount);

        int expectedSize = optimalN * rawFeatureSize;
        int actualSize = normData.pca_components.Length;
        Debug.Log($"PCA components — esperado: {expectedSize}, cargado: {actualSize}");
        if (expectedSize != actualSize)
            Debug.LogError("¡El tamaño de pca_components no coincide! Regenera el JSON.");

        // Rellenar el buffer histórico con el frame inicial (velocidad = 0)
        float[] initialPCA = ComputePCAFrame(initialVerts, lastPredictedPositions);
        for (int t = 0; t < seqLen; t++)
            for (int c = 0; c < optimalN; c++)
                historyBuffer[t, c] = initialPCA[c];

		stopwatch = new Stopwatch();

		inferenceTime = 0; inferenceFrames = 0;
		inferenceArchive = new List<double>();
	}

    void FixedUpdate()
    {
        var mesh = clothMeshFilter.mesh;
        var vertices = mesh.vertices;  // posiciones actuales del mesh (local)

        // 1. Desplazar historia hacia atrás
        for (int t = 0; t < seqLen - 1; t++)
            for (int c = 0; c < optimalN; c++)
                historyBuffer[t, c] = historyBuffer[t + 1, c];

        // 2. Calcular PCA del frame actual
        float[] currentPCA = ComputePCAFrame(vertices, lastPredictedPositions);
        for (int c = 0; c < optimalN; c++)
            historyBuffer[seqLen - 1, c] = currentPCA[c];

        // 3. Construir tensor de entrada: [1, seqLen, 1, optimalN]
        inputTensor = new Tensor<float>(new TensorShape(1, seqLen, 1, optimalN));
        for (int t = 0; t < seqLen; t++)
            for (int c = 0; c < optimalN; c++)
                inputTensor[0, t, 0, c] = historyBuffer[t, c];

		// 4. Ejecutar modelo
		// Medimos tiempo de inferencia
		stopwatch.Restart();
		worker.Schedule(inputTensor);
        using var output = worker.PeekOutput() as Tensor<float>;
        var result = output.ReadbackAndClone();
		// Debug inference time
		stopwatch.Stop();
		UnityEngine.Debug.Log($"Elapsed: {stopwatch.Elapsed}");
		inferenceTime += stopwatch.Elapsed.TotalMilliseconds; inferenceFrames++;
		if (inferenceFrames >= 60)
		{
			UnityEngine.Debug.Log($"Media de frames: {stopwatch.Elapsed}");
			inferenceArchive.Add(inferenceTime / inferenceFrames);
			inferenceFrames = 0;
			inferenceTime = 0;
		}

		// 5. Reconstruir pose deshaciendo PCA
		Vector3[] newVertices = new Vector3[vertexCount];

        // Desnormalizacion delta e integración
        float[] predPCANext = new float[optimalN];
        for (int c = 0; c < optimalN; c++)
        {
            float delta = result[0, c] * normData.target_pca_std[c]
                                       + normData.target_pca_mean[c];
            predPCANext[c] = currentPCA[c] + delta;
        }

        // Reconstrucción PCA (inverso espacio componentes)
        float[] scaledRaw = new float[rawFeatureSize];
        for (int f = 0; f < rawFeatureSize; f++)
        {
            float val = pcaMean[f];
            for (int c = 0; c < optimalN; c++)
                val += predPCANext[c] * pcaComponents[c, f];
            scaledRaw[f] = val;
        }

        // Desescalar
        float[] raw = new float[rawFeatureSize];
        for (int f = 0; f < rawFeatureSize; f++)
            raw[f] = scaledRaw[f] * scalerStd[f] + scalerMean[f];

        // Coordenadas 3D, aplicar predicciones
        for (int v = 0; v < vertexCount; v++)
        {
            if (maxDistance[v] == 0f)
            {
                newVertices[v] = vertices[v];
                continue;
            }

            int b = v * numFeatures;  // numFeatures = 4 (x,y,z,sdf)
            newVertices[v] = new Vector3(raw[b], raw[b + 1], raw[b + 2]);
        }

        mesh.SetVertices(newVertices);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        for (int v = 0; v < vertexCount; v++)
            lastPredictedPositions[v] = newVertices[v];

        inputTensor.Dispose();
        result.Dispose();
    }

    /// <summary>
    /// Dado el array de vértices actual y las posiciones del frame anterior,
    /// calcula el vector PCA del frame:
    ///   1. Vector plano raw [num_vertices * 13]
    ///   2. StandardScaler: z = (x - mean) / std
    ///   3. Centrar con pca_mean
    ///   4. Proyectar con pca_components
    ///
    /// Se pasa prevPositions explícitamente para que la velocidad pueda calcularse
    /// desde posiciones predichas (inferencia) o reales (frame inicial).
    /// </summary>
    private float[] ComputePCAFrame(Vector3[] verts, Vector3[] prevPositions)
    {
        var normals = clothMeshFilter.mesh.normals;
        var uvs = clothMeshFilter.mesh.uv;

        // 1. vector plano raw
        float[] raw = new float[rawFeatureSize];

        Vector3 ballPosLocal = transform.InverseTransformPoint(ball.transform.position);

        for (int v = 0; v < normData.num_vertices; v++)
        {
            Vector3 pos = verts[v];  // ya en espacio local (mesh.vertices)

            Vector3 vel = (pos - prevPositions[v]) / Time.fixedDeltaTime;

            float sdf = Vector3.Distance(pos, ballPosLocal) - ballCollider.radius;

            int f = v * numFeatures;
            raw[f++] = pos.x;
            raw[f++] = pos.y;
            raw[f++] = pos.z;
            raw[f++] = sdf;
        }

        // 2. StandardScaler 
        float[] scaled = new float[rawFeatureSize];
        for (int f = 0; f < rawFeatureSize; f++)
            scaled[f] = (raw[f] - scalerMean[f]) / scalerStd[f];

        // 3. centrar con pca_mean 
        float[] centered = new float[rawFeatureSize];
        for (int f = 0; f < rawFeatureSize; f++)
            centered[f] = scaled[f] - pcaMean[f];

        // 4. proyección PCA 
        float[] pca = new float[optimalN];
        for (int c = 0; c < optimalN; c++)
        {
            float dot = 0f;
            for (int f = 0; f < rawFeatureSize; f++)
                dot += pcaComponents[c, f] * centered[f];
            pca[c] = dot;
        }

        return pca;
    }

	public void VolcarTiempos()
	{
		string filePath = Application.persistentDataPath + "/debugInferencePCA.txt";

		foreach (double arc in inferenceArchive)
		{
			File.WriteAllText(filePath, arc + "\n");
		}
	}

	void OnDestroy()
    {
		VolcarTiempos();
		worker?.Dispose();
    }
}