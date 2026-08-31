using System.Diagnostics;
using System.IO;
using Unity.InferenceEngine;
using UnityEngine;
using System.Collections.Generic;


public abstract class ClothML : MonoBehaviour
{
    [SerializeField]
    public SphereCollider[] sphereColliders;
    [SerializeField]
    public CapsuleCollider[] capsuleColliders;
    public ModelAsset modelAsset;
    public MeshFilter clothMeshFilter;
    public float collidersUnionSmoothness = 0.0f;

    protected float[] maxDistance;
    protected int VertexCount;
    private Vector3[] newVertices;

    Worker worker;
    Tensor<float> inputTensor;

    protected abstract int SeqLen { get; }
    protected abstract int FeatureCount { get; }

    // Buffer para guardar el estado normalizado de los �ltimos seqLen frames
    // [tiempo x vertice x feature]
    protected float[] historyBuffer;

    public TextAsset jsonFile;

    [System.Serializable]
    public class NormalizationData
    {
        public float[] mean;
        public float[] std;
        public float[] target_mean;
        public float[] target_std;
    }
    public NormalizationData normData;

    [SerializeField]
    protected BackendType procActive;

	// Midiendo tiempos de tratado de datos y modelo 
	protected Stopwatch stopwatch;

    private  List<double> inferenceArchive;
    private double inferenceTime;

    private int inferenceFrames = 0;

	void Awake()
    {
        // Sacar la normalización de los datos
        if (jsonFile != null)
        {
            normData = JsonUtility.FromJson<NormalizationData>(jsonFile.text);
        }
    }

    public virtual void Start()
    {
        var model = ModelLoader.Load(modelAsset);
        worker = new Worker(model, procActive);

        clothMeshFilter.mesh.MarkDynamic();
        VertexCount = clothMeshFilter.mesh.vertexCount;

        maxDistance = Utils.setMaxDistance(VertexCount);
        historyBuffer = new float[SeqLen * VertexCount * FeatureCount];
        // Se marca la referencia del tensor al array para que se actualice automaticamente
        inputTensor = new Tensor<float>(new TensorShape(1, SeqLen, VertexCount, FeatureCount), historyBuffer);

        FillInitialBuffer();
        inputTensor.Upload(historyBuffer);

		newVertices = new Vector3[VertexCount];

        stopwatch = new Stopwatch();

        inferenceTime = 0; inferenceFrames = 0;
        inferenceArchive = new List<double>();
	}

    public void SaveData(string moment, string data)
    {
        string filePath = Application.persistentDataPath + "/debugOutput.txt";
        File.AppendAllText(filePath, moment + " NEW FRAME" + "\n");
        File.AppendAllText(filePath, data + "\n");
    }

    void FixedUpdate()
    {
        var mesh = clothMeshFilter.mesh;
        var vertices = mesh.vertices;

        // Se actualiza el buffer del historial
        UpdateBuffer();
        inputTensor.Upload(historyBuffer);

        // Medimos tiempo de inferencia
		stopwatch.Restart();
		// Se ejecuta el modelo, el inputTensor ya apunta al array historyBuffer, que se ha actualizado
		worker.Schedule(inputTensor);
        using var output = worker.PeekOutput() as Tensor<float>;
		var result = output.ReadbackAndClone();
        // Debug inference time
		stopwatch.Stop();
		UnityEngine.Debug.Log($"Elapsed: {stopwatch.Elapsed}");
        inferenceTime += stopwatch.Elapsed.TotalMilliseconds; inferenceFrames++;
        if( inferenceFrames >= 60)
        {
            UnityEngine.Debug.Log($"Media de frames: {stopwatch.Elapsed}");
            inferenceArchive.Add(inferenceTime/inferenceFrames);
            inferenceFrames = 0;
            inferenceTime = 0;
        }
			

		for (int i = 0; i < VertexCount; i++)
        {
            // --- NUEVO: Comprobamos si el v�rtice est� anclado ---
            // Si maxDistance es 0, el v�rtice no debe moverse bajo ninguna circunstancia
            if (maxDistance[i] == 0f)
            {
                newVertices[i] = vertices[i];
                continue; // Pasamos al siguiente v�rtice
            }

            // Denormalizamos el desplazamiento predicho
            float dx = result[0, i, 0];
            float dy = result[0, i, 1];
            float dz = result[0, i, 2];

            Vector3 displacement = new Vector3(
                (normData.target_std[0] * dx) + normData.target_mean[0],
                (normData.target_std[1] * dy) + normData.target_mean[1],
                (normData.target_std[2] * dz) + normData.target_mean[2]
            );
            newVertices[i] = vertices[i] + displacement;
        }

        mesh.SetVertices(newVertices);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        result.Dispose();
	}

    protected abstract void FillInitialBuffer();
    protected abstract void UpdateBuffer();
    protected abstract void saveStateAt(int offset);

    void OnDestroy()
    {
        VolcarTiempos();
        inputTensor?.Dispose();
        worker?.Dispose();
    }

	public void VolcarTiempos()
	{
		string filePath = Application.persistentDataPath + "/debugInference.txt";

		foreach (double arc in inferenceArchive)
		{
            File.WriteAllText(filePath, arc + "\n");
		}
	}
}