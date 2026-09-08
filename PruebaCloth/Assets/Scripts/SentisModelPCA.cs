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

    public int seqLen = 8; //8
    private int numFeatures = 4;
    // Buffer en espacio PCA: [seqLen, optimal_n]
    private float[,] historyBuffer;
    private int optimalN;

    // FIX: guardamos posiciones predichas (no reales del mesh externo)
    // para calcular la velocidad de la misma forma que hizo el entrenamiento.
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
        public float[] input_scaler_mean;   // longitud = num_vertices * 13
        public float[] input_scaler_std;    // longitud = num_vertices * 13
        public float[] pca_components;      // aplanado: optimal_n × (num_vertices*13)
        public float[] pca_mean;            // longitud = num_vertices*13
        public float[] target_pca_mean;
        public float[] target_pca_std;
        public int optimal_n;
        public int num_vertices;
		public float[] pca_min;
		public float[] pca_max;
	}
 //   [System.Serializable]

 //   public class NormalizationData
	//{
	//	public float[] input_scaler_mean;   // longitud = num_vertices * 13
	//	public float[] input_scaler_std;    // longitud = num_vertices * 13
	//	public float[] pca_components;      // aplanado: optimal_n × (num_vertices*13)
	//	public float[] pca_mean;            // longitud = num_vertices*13
	//	public float[] target_mean;
	//	public float[] target_std;
	//	public int optimal_n;
	//	public int num_vertices;
	//}
	public NormalizationData normData;

    // num_vertices * 13 features: x,y,z,vx,vy,vz,sdf,nx,ny,nz,maxDist,u,v
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
        rawFeatureSize = normData.num_vertices * numFeatures; // x,y,z, sdf /o/ ,vx,vy,vz,sdf,nx,ny,nz,md,u,v

        maxDistance = new float[vertexCount];
        historyBuffer = new float[seqLen, optimalN];

        // FIX: inicializamos con las posiciones reales del mesh,
        // igual que hace el entrenamiento en el primer frame.
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

        // Definir puntos anclados (maxDistance = 0 → no se mueven)
        // Definir puntos anclados (0 = se mueve)
        int j = 0;
        //MINI
        //for (; j < 4; j++)
        //{
        //    maxDistance[j] = 1.0f;
        //}
        while (j < vertexCount)
        {
            maxDistance[j] = 1.0f;
            j++;
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
        //    FIX: pasamos lastPredictedPositions (no mesh.vertices del frame anterior)
        //    para que la velocidad sea consistente con el entrenamiento.
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

        //// 5. Aplicar predicciones

        //for (int i = 0; i < vertexCount; i++)
        //{
        //    if (maxDistance[i] == 0f)
        //    {
        //        newVertices[i] = vertices[i];
        //        continue;
        //    }


        //    float dx = result[0, i, 0] * normData.target_pca_std[0] + normData.target_pca_mean[0];
        //    float dy = result[0, i, 1] * normData.target_pca_std[1] + normData.target_pca_mean[1];
        //    float dz = result[0, i, 2] * normData.target_pca_std[2] + normData.target_pca_mean[2];

        //    newVertices[i] = vertices[i] + new Vector3(dx, dy, dz);
        //}

        mesh.SetVertices(newVertices);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        // FIX: guardamos las posiciones PREDICHAS (newVertices), no las originales del mesh.
        // Así la velocidad del siguiente frame es (pred_t - pred_{t-1}) / dt,
        // igual que ocurre en el training loop tras aplicar el rollout.
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

        // ── Paso 1: vector plano raw ──────────────────────────────────
        float[] raw = new float[rawFeatureSize];

        // FIX: el SDF se calcula en espacio local del transform de la tela.
        // ball.transform.position está en world space → transformamos a local.
        Vector3 ballPosLocal = transform.InverseTransformPoint(ball.transform.position);

        for (int v = 0; v < normData.num_vertices; v++)
        {
            Vector3 pos = verts[v];  // ya en espacio local (mesh.vertices)

            // FIX: velocidad desde posiciones predichas del frame anterior
            Vector3 vel = (pos - prevPositions[v]) / Time.fixedDeltaTime;

            // FIX: SDF coherente — pos y ballPosLocal ambos en espacio local
            float sdf = Vector3.Distance(pos, ballPosLocal) - ballCollider.radius;

            //Vector3 normal = (v < normals.Length) ? normals[v] : Vector3.up;

            int f = v * numFeatures;
            raw[f++] = pos.x;
            raw[f++] = pos.y;
            raw[f++] = pos.z;
            //raw[f++] = vel.x;
            //raw[f++] = vel.y;
            //raw[f++] = vel.z;
            raw[f++] = sdf;
            //raw[f++] = normal.x;
            //raw[f++] = normal.y;
            //raw[f++] = normal.z;
            //raw[f++] = (v < maxDistance.Length) ? maxDistance[v] : 0f;
            //raw[f++] = (v < uvs.Length) ? uvs[v].x : 0f;
            //raw[f] = (v < uvs.Length) ? uvs[v].y : 0f;
        }

        // ── Paso 2: StandardScaler ────────────────────────────────────
        float[] scaled = new float[rawFeatureSize];
        for (int f = 0; f < rawFeatureSize; f++)
            scaled[f] = (raw[f] - scalerMean[f]) / scalerStd[f];

        // ── Paso 3: centrar con pca_mean ─────────────────────────────
        float[] centered = new float[rawFeatureSize];
        for (int f = 0; f < rawFeatureSize; f++)
            centered[f] = scaled[f] - pcaMean[f];

        // ── Paso 4: proyección PCA ────────────────────────────────────
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
