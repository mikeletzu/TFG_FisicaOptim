using System;
using System.IO;
using Unity.InferenceEngine; // O Unity.Sentis dependiendo de tu versi�n exacta
using UnityEngine;

public class ClothMLRndFrst : MonoBehaviour
{
    [SerializeField]
    public GameObject ball;
    public SphereCollider ballCollider;
	[SerializeField]
	public SphereCollider[] sphereColliders;
	[SerializeField]
	public CapsuleCollider[] capsuleColliders;
    public ModelAsset modelAsset;
    public MeshFilter clothMeshFilter;
    public float collidersUnionSmoothness = 0.0f;

    private float[] maxDistance;
    private Vector3[] lastVertexPositions;

    Worker worker;
    Tensor<float> inputTensor;

    public int contador = 0;
    int vertexCount;

    // --- NUEVO: Par�metros de la Secuencia ---
    private int seqLen = 5;
    private int numFeatures = 13;  // todas
    // Buffer para guardar el estado normalizado de los �ltimos frames
    // [tiempo, vertice, feature]
    private float[,,] historyBuffer;

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
        worker = new Worker(model, BackendType.GPUCompute);

        clothMeshFilter.mesh.MarkDynamic();
        vertexCount = clothMeshFilter.mesh.vertexCount;
        var normals = clothMeshFilter.mesh.normals;
        var uvs = clothMeshFilter.mesh.uv;

        maxDistance = new float[vertexCount];
        historyBuffer = new float[seqLen, vertexCount, numFeatures];

        lastVertexPositions = clothMeshFilter.mesh.vertices;

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

        // --- NUEVO: Llenar el buffer inicial ---
        // Para que los primeros 5 frames no sean nulos, llenamos la historia
        // asumiendo que la tela est� quieta en su posici�n inicial.

        for (int t = 0; t < seqLen; t++)
        {
            for (int v = 0; v < vertexCount; v++)
            {
                Vector3 pos = lastVertexPositions[v];
                Vector3 vel = Vector3.zero;

                float sdf;
                if (ball != null)
                    sdf = Vector3.Distance(pos, transform.InverseTransformPoint(ball.transform.position)) - ballCollider.radius;
                else
                    sdf = SDFUtil.getSDFOfSet(pos, capsuleColliders, sphereColliders, collidersUnionSmoothness, transform);

                historyBuffer[t, v, 0] = (pos.x - normData.mean[0]) / normData.std[0];
                historyBuffer[t, v, 1] = (pos.y - normData.mean[1]) / normData.std[1];
                historyBuffer[t, v, 2] = (pos.z - normData.mean[2]) / normData.std[2];

                historyBuffer[t, v, 3] = (vel.x - normData.mean[3]) / normData.std[3];
                historyBuffer[t, v, 4] = (vel.y - normData.mean[4]) / normData.std[4];
                historyBuffer[t, v, 5] = (vel.z - normData.mean[5]) / normData.std[5];

                historyBuffer[t, v, 6] = (sdf - normData.mean[6]) / normData.std[6];

                historyBuffer[t, v, 7] = (normals[v].x - normData.mean[7]) / normData.std[7];
                historyBuffer[t, v, 8] = (normals[v].y - normData.mean[8]) / normData.std[8];
                historyBuffer[t, v, 9] = (normals[v].z - normData.mean[9]) / normData.std[9];

                historyBuffer[t, v, 10] = (maxDistance[v] - normData.mean[10]) / normData.std[10];
                historyBuffer[t, v, 11] = (uvs[v].x - normData.mean[11]) / normData.std[11];
                historyBuffer[t, v, 12] = (uvs[v].y - normData.mean[12]) / normData.std[12];
            }
        }
    }

    void FixedUpdate()
    {
        var mesh = clothMeshFilter.mesh;
        var vertices = mesh.vertices;
        var normals = mesh.normals;
        var uvs = mesh.uv;

        // 1. Desplazar la historia hacia atr�s (t=0 desaparece, todo se mueve a la izquierda)
        for (int t = 0; t < seqLen - 1; t++)
            for (int v = 0; v < vertexCount; v++)
                for (int f = 0; f < numFeatures; f++)
                    historyBuffer[t, v, f] = historyBuffer[t + 1, v, f];

        // 2. Calcular los features del frame actual y ponerlos al final de la historia (t = seqLen - 1)
        for (int v = 0; v < vertexCount; v++)
        {
            Vector3 pos = vertices[v];
            Vector3 vel = (pos - lastVertexPositions[v]) / Time.fixedDeltaTime;

            float sdf;
            if(ball!=null)
               sdf = Vector3.Distance(pos, transform.InverseTransformPoint(ball.transform.position)) - ballCollider.radius;
            else
				sdf = SDFUtil.getSDFOfSet(pos, capsuleColliders, sphereColliders, collidersUnionSmoothness, transform);

            historyBuffer[seqLen - 1, v, 0] = (pos.x - normData.mean[0]) / normData.std[0];
            historyBuffer[seqLen - 1, v, 1] = (pos.y - normData.mean[1]) / normData.std[1];
            historyBuffer[seqLen - 1, v, 2] = (pos.z - normData.mean[2]) / normData.std[2];

            historyBuffer[seqLen - 1, v, 3] = (vel.x - normData.mean[3]) / normData.std[3];
            historyBuffer[seqLen - 1, v, 4] = (vel.y - normData.mean[4]) / normData.std[4];
            historyBuffer[seqLen - 1, v, 5] = (vel.z - normData.mean[5]) / normData.std[5];

            historyBuffer[seqLen - 1, v, 6] = (sdf - normData.mean[6]) / normData.std[6];

            historyBuffer[seqLen - 1, v, 7] = (normals[v].x - normData.mean[7]) / normData.std[7];
            historyBuffer[seqLen - 1, v, 8] = (normals[v].y - normData.mean[8]) / normData.std[8];
            historyBuffer[seqLen - 1, v, 9] = (normals[v].z - normData.mean[9]) / normData.std[9];

            historyBuffer[seqLen - 1, v, 10] = (maxDistance[v] - normData.mean[10]) / normData.std[10];
            historyBuffer[seqLen - 1, v, 11] = (uvs[v].x - normData.mean[11]) / normData.std[11];
            historyBuffer[seqLen - 1, v, 12] = (uvs[v].y - normData.mean[12]) / normData.std[12];

            lastVertexPositions[v] = pos;
        }

        // 3. Crear el tensor con las dimensiones que espera el modelo ONNX: [1, SeqLen, Vertices, Features]
        // RF  espera:   [1, seqLen * vertexCount * numFeatures] (vector plano 1D)
        int inputDim = seqLen * vertexCount * numFeatures;
        inputTensor = new Tensor<float>(new TensorShape(1, inputDim));

        int idx = 0;
        for (int t = 0; t < seqLen; t++)
            for (int v = 0; v < vertexCount; v++)
                for (int f = 0; f < numFeatures; f++)
                    inputTensor[0, idx++] = historyBuffer[t, v, f];

        // 4. Ejecutar modelo
        worker.Schedule(inputTensor);
        using var output = worker.PeekOutput() as Tensor<float>;
        var result = output.ReadbackAndClone();

        Vector3[] newVertices = new Vector3[vertexCount];

        for (int i = 0; i < vertexCount; i++)
        {
            // --- NUEVO: Comprobamos si el v�rtice est� anclado ---
            // Si maxDistance es 0, el v�rtice no debe moverse bajo ninguna circunstancia
            if (maxDistance[i] == 0f)
            {
                newVertices[i] = vertices[i];
                continue; // Pasamos al siguiente v�rtice
            }

            // Denormalizamos el desplazamiento predicho
            int base_idx = i * 3;
            float dx = result[0, base_idx + 0];
            float dy = result[0, base_idx + 1];
            float dz = result[0, base_idx + 2];

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

        inputTensor.Dispose();
        result.Dispose();
    }

    void OnDestroy()
    {
        worker?.Dispose();
    }
}