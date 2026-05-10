using System;
using System.Drawing;
using System.IO;
using Unity.AppUI.UI;
using Unity.InferenceEngine;
using UnityEditor.Build.Content;
using UnityEngine;
using UnityEngine.UIElements;


public class ClothMLRecAnchored : MonoBehaviour
{
    [SerializeField]
    public GameObject ball;
    public SphereCollider ballCollider; // collider o mesh 
	public ModelAsset modelAsset;
    public MeshFilter clothMeshFilter;

    private float[] maxDistance;

    Worker worker;
    Tensor<float> inputTensor;

    public int contador = 0;
    int vertexCount;

    [SerializeField]
    public int seqLen = 5;
    private int numFeatures = 7;
    private float[,,] historyBuffer;

    // Datos de normalización
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

    public Vector3[] originalPositions;

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

        originalPositions = clothMeshFilter.mesh.vertices;

        ballCollider = ball.GetComponent<SphereCollider>();

        maxDistance = new float[vertexCount];
        historyBuffer = new float[seqLen, vertexCount, 7];

        // set max distance
        int i = 0;
		for (;  i < 4; i++)
        {
            maxDistance[i] = 0.2f;
        }
        while(i<vertexCount)
        {
            maxDistance[i] = 0f;
            i++;
        }

        for (int t = 0; t < seqLen; t++)
        {
            for (int v = 0; v < vertexCount; v++)
            {
                Vector3 pos = originalPositions[v];

                float sdf = Vector3.Distance(pos, transform.InverseTransformPoint(ball.transform.position)) - ballCollider.radius;

                historyBuffer[t, v, 0] = pos.x;
                historyBuffer[t, v, 1] = pos.y;
                historyBuffer[t, v, 2] = pos.z;
                historyBuffer[t, v, 3] = (pos.x - normData.mean[3]) / normData.std[3];
                historyBuffer[t, v, 4] = (pos.y - normData.mean[4]) / normData.std[4];
                historyBuffer[t, v, 5] = (pos.z - normData.mean[5]) / normData.std[5];
                historyBuffer[t, v, 6] = (sdf - normData.mean[6]) / normData.std[6];
            }
        }
    }

	void FixedUpdate()
	{
		var mesh = clothMeshFilter.mesh;
		var vertices = mesh.vertices;
		var normals = mesh.normals;
		var uvs = mesh.uv; // Cachear las uvs fuera del bucle

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

        // 2. Calcular los features del frame actual y ponerlos al final de la historia (t = seqLen - 1)
        for (int i = 0; i < vertexCount; i++)
        {
            Vector3 pos = vertices[i];
            float sdf = Vector3.Distance(pos, transform.InverseTransformPoint(ball.transform.position)) - ballCollider.radius;

            historyBuffer[seqLen - 1, i, 0] = originalPositions[i].x;
            historyBuffer[seqLen - 1, i, 1] = originalPositions[i].y;
            historyBuffer[seqLen - 1, i, 2] = originalPositions[i].z;
            historyBuffer[seqLen - 1, i, 3] = (pos.x - normData.mean[3]) / normData.std[3];
            historyBuffer[seqLen - 1, i, 4] = (pos.y - normData.mean[4]) / normData.std[4];
            historyBuffer[seqLen - 1, i, 5] = (pos.z - normData.mean[5]) / normData.std[5];
            historyBuffer[seqLen - 1, i, 6] = (sdf - normData.mean[6]) / normData.std[6];
        }


        inputTensor = new Tensor<float>(new TensorShape(1, seqLen, vertexCount, numFeatures));

        for (int t = 0; t < seqLen; t++)
        {
            for (int i = 0; i < vertexCount; i++)
            {
                //Vector3 pos = clothMeshFilter.transform.TransformPoint(vertices[i]);
                Vector3 pos = vertices[i];
                
                float sdf = Vector3.Distance(pos, transform.InverseTransformPoint(ball.transform.position)) - ballCollider.radius;

                int f = 0;
                inputTensor[0, i, f++] = originalPositions[i].x;
                inputTensor[0, i, f++] = originalPositions[i].y;
                inputTensor[0, i, f++] = originalPositions[i].z;

                inputTensor[0, i, f++] = (pos.x - normData.mean[3]) / normData.std[3];
                inputTensor[0, i, f++] = (pos.y - normData.mean[4]) / normData.std[4];
                inputTensor[0, i, f++] = (pos.z - normData.mean[5]) / normData.std[5];

                inputTensor[0, i, f++] = (sdf - normData.mean[6]) / normData.std[6];
            }
        }

		worker.Schedule(inputTensor);

		using var output = worker.PeekOutput() as Tensor<float>;
		var result = output.ReadbackAndClone();

		Vector3[] newVertices = new Vector3[vertexCount];
		
		for (int i = 0; i < vertexCount; i++)
		{
            if (maxDistance[i] == 0f)
            {
                newVertices[i] = vertices[i];
                continue; // Pasamos al siguiente vértice
            }

            // Denormalizamos (world 
            float dx = result[0, i, 0];
            float dy = result[0, i, 1];
            float dz = result[0, i, 2];

            Vector3 displacement;
      
            displacement = new Vector3(
                ((normData.target_std[0] * dx) + normData.target_mean[0]),
                ((normData.target_std[1] * dy) + normData.target_mean[1]),
                ((normData.target_std[2] * dz) + normData.target_mean[2])
            );            

            // Espacio local para vértices
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