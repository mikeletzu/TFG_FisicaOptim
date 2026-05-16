using System;
using System.IO;
using Unity.InferenceEngine; // O Unity.Sentis dependiendo de tu versión exacta
using UnityEngine;
using UnityEngine.UIElements;
using static UnityEditor.PlayerSettings;

public class ClothMLPosVelRec : MonoBehaviour
{
    [SerializeField]
    public GameObject ball;
    public SphereCollider ballCollider;
    public ModelAsset modelAsset;
    public MeshFilter clothMeshFilter;

    private Vector3[] lastVertexPositions;

    private float[] maxDistance;

    Worker worker;
    Tensor<float> inputTensor;

    int vertexCount;

    // --- NUEVO: Parámetros de la Secuencia ---
    [SerializeField]
    public int seqLen = 5;
    // Buffer para guardar el estado normalizado de los últimos 5 frames
    // [tiempo, vertice, feature]
    private float[,,] historyBuffer;

    public TextAsset jsonFile;

    [SerializeField]
    public int numFeatures = 7;

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
    private BackendType procActive;

    void Awake()
    {
        if (jsonFile != null)
        {
            normData = JsonUtility.FromJson<NormalizationData>(jsonFile.text);
        }
    }

    void Start()
    {
        var model = ModelLoader.Load(modelAsset);
        worker = new Worker(model, procActive);

        clothMeshFilter.mesh.MarkDynamic();
        vertexCount = clothMeshFilter.mesh.vertexCount;

        lastVertexPositions = clothMeshFilter.mesh.vertices;

        maxDistance = new float[vertexCount];
        historyBuffer = new float[seqLen, vertexCount, numFeatures];

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
        ////MAX
        maxDistance[11] = 0f;
        maxDistance[12] = 0f;
        maxDistance[18] = 0f;
        maxDistance[22] = 0f;
        maxDistance[24] = 0f;

		// --- NUEVO: Llenar el buffer inicial ---
		// Para que los primeros frames no sean nulos, llenamos la historia
		// asumiendo que la tela está quieta en su posición inicial.

		
		for (int t = 0; t < seqLen; t++)
        {
            for (int v = 0; v < vertexCount; v++)
            {
                Vector3 pos = lastVertexPositions[v];
                Vector3 vel = Vector3.zero;

                float sdf = Vector3.Distance(transform.TransformPoint(pos), transform.InverseTransformPoint(ball.transform.position)) - ballCollider.radius;

                historyBuffer[t, v, 0] = (pos.x - normData.mean[0]) / normData.std[0];
                historyBuffer[t, v, 1] = (pos.y - normData.mean[1]) / normData.std[1];
                historyBuffer[t, v, 2] = (pos.z - normData.mean[2]) / normData.std[2];

                historyBuffer[t, v, 3] = (vel.x - normData.mean[3]) / normData.std[3];
                historyBuffer[t, v, 4] = (vel.y - normData.mean[4]) / normData.std[4];
                historyBuffer[t, v, 5] = (vel.z - normData.mean[5]) / normData.std[5];
                
                historyBuffer[t, v, 6] = (sdf - normData.mean[6]) / normData.std[6];
            }
        }
    }

    void FixedUpdate()
    {
        var mesh = clothMeshFilter.mesh;
        var vertices = mesh.vertices;

        // 1. Desplazar la historia hacia atrás (t=0 desaparece, todo se mueve a la izquierda)
        for (int t = 0; t < seqLen - 1; t++)
        {
            for (int v = 0; v < vertexCount; v++)
            {
                for (int f = 0; f < numFeatures; f++)
                {
                    historyBuffer[t, v, f] = historyBuffer[t + 1, v, f];
                }
            }
        }

		string vertex = "";

		// 2. Calcular los features del frame actual y ponerlos al final de la historia (t = seqLen - 1)
		for (int i = 0; i < vertexCount; i++)
        {
            Vector3 pos = vertices[i];
            //Vector3 vel = Vector3.zero;
            Vector3 vel = (pos - lastVertexPositions[i]) / Time.fixedDeltaTime;

            float sdf = Vector3.Distance(pos, transform.InverseTransformPoint(ball.transform.position)) - ballCollider.radius;


            historyBuffer[seqLen - 1, i, 0] = (pos.x - normData.mean[0]) / normData.std[0];
            historyBuffer[seqLen - 1, i, 1] = (pos.y - normData.mean[1]) / normData.std[1];
            historyBuffer[seqLen - 1, i, 2] = (pos.z - normData.mean[2]) / normData.std[2];

            historyBuffer[seqLen - 1, i, 3] = (vel.x - normData.mean[3]) / normData.std[3];
            historyBuffer[seqLen - 1, i, 4] = (vel.y - normData.mean[4]) / normData.std[4];
            historyBuffer[seqLen - 1, i, 5] = (vel.z - normData.mean[5]) / normData.std[5];

			vertex += vel + "/n";
			historyBuffer[seqLen - 1, i, 6] = (sdf - normData.mean[6]) / normData.std[6];
        }
		SaveData("vel", vertex);

		// 3. Crear el tensor con las 4 dimensiones que espera el modelo ONNX: [1, SeqLen, Vertices, Features]
		inputTensor = new Tensor<float>(new TensorShape(1, seqLen, vertexCount, numFeatures));

        for (int t = 0; t < seqLen; t++)
        {
            for (int i = 0; i < vertexCount; i++)
            {
                inputTensor[0, t, i, 0] = historyBuffer[t, i, 0];
                inputTensor[0, t, i, 1] = historyBuffer[t, i, 1];
                inputTensor[0, t, i, 2] = historyBuffer[t, i, 2];
                inputTensor[0, t, i, 3] = historyBuffer[t, i, 3];
                inputTensor[0, t, i, 4] = historyBuffer[t, i, 4];
                inputTensor[0, t, i, 5] = historyBuffer[t, i, 5];
                inputTensor[0, t, i, 6] = historyBuffer[t, i, 6];
            }
        }

         // lastVertexPositions = vertices;

        // 4. Ejecutar modelo
        worker.Schedule(inputTensor);
        using var output = worker.PeekOutput() as Tensor<float>;
        var result = output.ReadbackAndClone();

        Vector3[] newVertices = new Vector3[vertexCount];

        for (int i = 0; i < vertexCount; i++)
        {
            // --- NUEVO: Comprobamos si el vértice está anclado ---
            // Si maxDistance es 0, el vértice no debe moverse bajo ninguna circunstancia
            if (maxDistance[i] == 0f)
            {
                newVertices[i] = vertices[i];
                continue; // Pasamos al siguiente vértice
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

            //displacement = Vector3.ClampMagnitude(displacement, 0.05f);

            // Aplicamos el desplazamiento a la posición actual (en local)
            newVertices[i] = vertices[i] + displacement;
        }
		lastVertexPositions = vertices;
		mesh.SetVertices(newVertices);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        inputTensor.Dispose();
        result.Dispose();
    }
	public void SaveData(string moment, string data)
	{
		string filePath = Application.persistentDataPath + "/velML.txt";
		File.AppendAllText(filePath, moment + " NEW FRAME" + "\n");
		File.AppendAllText(filePath, data + "\n");
	}
	void OnDestroy()
    {
        worker?.Dispose();
    }
}