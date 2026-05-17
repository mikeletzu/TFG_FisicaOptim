using System;
using System.IO;
using Unity.InferenceEngine; // O Unity.Sentis dependiendo de tu versi�n exacta
using UnityEngine;

public class ClothMLPosRec : MonoBehaviour
{
	[SerializeField]
	public SphereCollider[] sphereColliders;
	[SerializeField]
	public CapsuleCollider[] capsuleColliders;
    public ModelAsset modelAsset;
    public MeshFilter clothMeshFilter;
    public float collidersUnionSmoothness = 0.0f;

    private float[] maxDistance;

    Worker worker;
    Tensor<float> inputTensor;

    public int contador = 0;
    int vertexCount;

    // --- NUEVO: Par�metros de la Secuencia ---
    private int seqLen = 5;
    // Buffer para guardar el estado normalizado de los �ltimos 5 frames
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

        maxDistance = new float[vertexCount];
        historyBuffer = new float[seqLen, vertexCount, 4];

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

        var initialVertices = clothMeshFilter.mesh.vertices;
        for (int t = 0; t < seqLen; t++)
        {
            Debug.Log("SEQUENCIA");
            for (int v = 0; v < vertexCount; v++)
            {
                Vector3 pos = initialVertices[v];

                Debug.Log("vertex: " + v + ", pos: " + pos);

                float sdf;
                //if (ball != null)
                //    sdf = Vector3.Distance(pos, transform.InverseTransformPoint(ball.transform.position)) - ballCollider.radius;
                //else
                sdf = SDFUtil.getSDFOfSet(pos, capsuleColliders, sphereColliders, collidersUnionSmoothness, transform);
    
                historyBuffer[t, v, 0] = (pos.x - normData.mean[0]) / normData.std[0];
                historyBuffer[t, v, 1] = (pos.y - normData.mean[1]) / normData.std[1];
                historyBuffer[t, v, 2] = (pos.z - normData.mean[2]) / normData.std[2];
                historyBuffer[t, v, 3] = (sdf - normData.mean[3]) / normData.std[3];
            }
        }
    }

    void FixedUpdate()
    {
        var mesh = clothMeshFilter.mesh;
        var vertices = mesh.vertices;

        // 1. Desplazar la historia hacia atr�s (t=0 desaparece, todo se mueve a la izquierda)
        for (int t = 0; t < seqLen - 1; t++)
        {
            for (int v = 0; v < vertexCount; v++)
            {
                for (int f = 0; f < 4; f++)
                {
                    historyBuffer[t, v, f] = historyBuffer[t + 1, v, f];
                }
            }
        }

        // 2. Calcular los features del frame actual y ponerlos al final de la historia (t = seqLen - 1)
        for (int i = 0; i < vertexCount; i++)
        {
            Vector3 pos = vertices[i];
            // pos = transform.TransformPoint(pos); //CONFIRMAR QUE ESTO HACE FALTA LOL

            float sdf;
           
		    sdf = SDFUtil.getSDFOfSet(pos, capsuleColliders, sphereColliders, collidersUnionSmoothness, transform);

			historyBuffer[seqLen - 1, i, 0] = (pos.x - normData.mean[0]) / normData.std[0];
            historyBuffer[seqLen - 1, i, 1] = (pos.y - normData.mean[1]) / normData.std[1];
            historyBuffer[seqLen - 1, i, 2] = (pos.z - normData.mean[2]) / normData.std[2];
            historyBuffer[seqLen - 1, i, 3] = (sdf - normData.mean[3]) / normData.std[3];
        }

        // 3. Crear el tensor con las 4 dimensiones que espera el modelo ONNX: [1, SeqLen, Vertices, Features]
        inputTensor = new Tensor<float>(new TensorShape(1, seqLen, vertexCount, 4));

        for (int t = 0; t < seqLen; t++)
        {
            for (int i = 0; i < vertexCount; i++)
            {
                inputTensor[0, t, i, 0] = historyBuffer[t, i, 0];
                inputTensor[0, t, i, 1] = historyBuffer[t, i, 1];
                inputTensor[0, t, i, 2] = historyBuffer[t, i, 2];
                inputTensor[0, t, i, 3] = historyBuffer[t, i, 3];
            }
        }

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
            float dx = result[0, i, 0];
            float dy = result[0, i, 1];
            float dz = result[0, i, 2];

            Vector3 displacement = new Vector3(
                (normData.target_std[0] * dx) + normData.target_mean[0],
                (normData.target_std[1] * dy) + normData.target_mean[1],
                (normData.target_std[2] * dz) + normData.target_mean[2]
            );

            //displacement = Vector3.ClampMagnitude(displacement, 0.05f);

            // Aplicamos el desplazamiento a la posici�n actual (en local)
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