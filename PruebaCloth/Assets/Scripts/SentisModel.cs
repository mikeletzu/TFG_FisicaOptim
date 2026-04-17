using System;
using System.Drawing;
using System.IO;
using Unity.AppUI.UI;
using Unity.InferenceEngine;
using UnityEditor.Build.Content;
using UnityEngine;
using UnityEngine.UIElements;


public class ClothML : MonoBehaviour
{
    [SerializeField]
    public GameObject ball;
    public SphereCollider ballCollider; // collider o mesh 
	public ModelAsset modelAsset;
    public MeshFilter clothMeshFilter;

    private Vector3[] lastVertexPositions;
    private float[] maxDistance;

    Worker worker;
    Tensor<float> inputTensor;

    int vertexCount;

	// Datos de normalización
	public TextAsset jsonFile;

	[System.Serializable]
	public class NormalizationData
	{
		public float[] mean;
		public float[] std;
		public string[] feature_prefixes;
	}
	public NormalizationData normData;

	void Awake()
	{
		if (jsonFile != null)
		{
			normData = JsonUtility.FromJson<NormalizationData>(jsonFile.text);
			Debug.Log("Media de "+ normData.feature_prefixes[0].ToString() + ": "+ normData.mean[0].ToString());
		}
	}

	void Start()
    {
        var model = ModelLoader.Load(modelAsset);
        worker = new Worker(model, BackendType.GPUCompute);

		clothMeshFilter.mesh.MarkDynamic();

		vertexCount = clothMeshFilter.mesh.vertexCount;

        lastVertexPositions = new Vector3[vertexCount];

		for (int im = 0; im < vertexCount; im++)
		{
			lastVertexPositions[im] = new Vector3(0,0,0);
		}

        ballCollider = ball.GetComponent<SphereCollider>();

        maxDistance = new float[vertexCount];

        // Necesitaremos mantener cloth para algo? Entiendo que no.
        // Max distance se almacena y accede originalmente en cloth, aquí habría que añadirlo a mano en un vector


        // set max distance
        int i = 0;
		for (;  i < 10; i++)
        {
            maxDistance[i] = 0f;
        }
        while(i<vertexCount)
        {
            maxDistance[i] = 0.2f;
            i++;
        }

	}

	public void SaveData(string data)
	{
		string filePath = Application.persistentDataPath + "/debugOutput.txt";
        File.AppendAllText(filePath, "NEW FRAME" + "\n");
		File.AppendAllText(filePath, data + "\n");
	}

	// Versión de antes de correciones
	//void Update()
	//   {
	//       var mesh = clothMeshFilter.mesh;
	//       var vertices = mesh.vertices;
	//       var normals = mesh.normals;

	//       inputTensor = new Tensor<float>(new TensorShape(1, vertexCount, 13));

	//       for (int i = 0; i < vertexCount; i++)
	//       {
	//		Vector3 pos = clothMeshFilter.transform.TransformPoint(vertices[i]);
	//           Vector3 vel = lastVertexPositions[i] - pos;        
	//           float sdf = Vector3.Distance(pos, ballCollider.transform.position) - ballCollider.radius;             // distancia a la bola
	//		Vector3 normal = normals[i];
	//           float maxDist = GetMaxDistance(i);     // constraint de la tela
	//           Vector2 uv = GetUV(i, mesh);

	//           int f = 0;

	//           // posición
	//           inputTensor[0, i, f++] = pos.x;
	//           inputTensor[0, i, f++] = pos.y;
	//           inputTensor[0, i, f++] = pos.z;

	//           // velocidad
	//           inputTensor[0, i, f++] = vel.x;
	//           inputTensor[0, i, f++] = vel.y;
	//           inputTensor[0, i, f++] = vel.z;

	//           // sdf
	//           inputTensor[0, i, f++] = sdf;

	//           // normal
	//           inputTensor[0, i, f++] = normal.x;
	//           inputTensor[0, i, f++] = normal.y;
	//           inputTensor[0, i, f++] = normal.z;

	//           // max distance
	//           inputTensor[0, i, f++] = maxDist;

	//           // uv
	//           inputTensor[0, i, f++] = uv.x;
	//           inputTensor[0, i, f++] = uv.y;
	//       }

	//       worker.Schedule(inputTensor);

	//       var output = worker.PeekOutput() as Tensor<float>;
	//       var result = output.ReadbackAndClone();

	//       Vector3[] newVertices = new Vector3[vertexCount];

	//       string vertex = "";
	//       for (int i = 0; i < vertexCount; i++)
	//       {
	//           newVertices[i] = new Vector3(
	//               result[0, i, 0],
	//               result[0, i, 1],
	//               result[0, i, 2]
	//           );
	//           vertex = vertex + newVertices[i].ToString() + "\n";

	//       }
	//       SaveData(vertex);
	//	// print(mesh.vertices);

	//	mesh.SetVertices(newVertices);
	//	//mesh.vertices = newVertices;
	//	mesh.RecalculateNormals();
	//       mesh.RecalculateBounds();

	//       inputTensor.Dispose();
	//       output.Dispose();
	//       result.Dispose();
	//   }
	void FixedUpdate()
	{
		var mesh = clothMeshFilter.mesh;
		var vertices = mesh.vertices;
		var normals = mesh.normals;
		var uvs = mesh.uv; // Cachear las uvs fuera del bucle

		inputTensor = new Tensor<float>(new TensorShape(1, vertexCount, 13));

		for (int i = 0; i < vertexCount; i++)
		{
			Vector3 worldPos = clothMeshFilter.transform.TransformPoint(vertices[i]);
			Debug.DrawRay(worldPos, Vector3.up * 0.1f, UnityEngine.Color.red);

			Vector3 vel = (worldPos - lastVertexPositions[i]) / Time.fixedDeltaTime;

			float sdf = Vector3.Distance(worldPos, ball.transform.position) - ballCollider.radius;
			Vector3 normal = normals[i];

			int f = 0;
			// Posicion
			inputTensor[0, i, f++] = (worldPos.x - normData.mean[0]) / normData.std[0];
			inputTensor[0, i, f++] = (worldPos.y - normData.mean[1]) / normData.std[1];
			inputTensor[0, i, f++] = (worldPos.z - normData.mean[2]) / normData.std[2];

			// Velocidad
			inputTensor[0, i, f++] = vel.x;
			inputTensor[0, i, f++] = vel.y;
			inputTensor[0, i, f++] = vel.z;

			inputTensor[0, i, f++] = sdf;
			inputTensor[0, i, f++] = normal.x;
			inputTensor[0, i, f++] = normal.y;
			inputTensor[0, i, f++] = normal.z;
			inputTensor[0, i, f++] = maxDistance[i];
			inputTensor[0, i, f++] = uvs[i].x;
			inputTensor[0, i, f++] = uvs[i].y;

			// Para la velocidad del proximo frame
			lastVertexPositions[i] = worldPos;
		}

		worker.Schedule(inputTensor);

		using var output = worker.PeekOutput() as Tensor<float>;
		var result = output.ReadbackAndClone();

		Vector3[] newVertices = new Vector3[vertexCount];
		string vertex = "";
		for (int i = 0; i < vertexCount; i++)
		{
			// World space!
			float normalizedX = result[0, i, 0]; 
			float normalizedY = result[0, i, 1]; 
			float normalizedZ = result[0, i, 2];

			// El que tendria que ser
			//Vector3 modelOutput = new Vector3(
			//	((normData.std[0] * normalizedX) + normData.mean[0]),
			//	((normData.std[1] * normalizedY) + normData.mean[1]),
			//	((normData.std[2] * normalizedZ) + normData.mean[2]));

			// Pequeñito
			//Vector3 modelOutput = new Vector3(
			//	((normData.std[0] * normalizedX) + normData.mean[0])*0.000001f,
			//	((normData.std[1] * normalizedY) + normData.mean[1])*0.00001f,
			//	((normData.std[2] * normalizedZ) + normData.mean[2]) * 0.000001f);

			// Clamp
			Vector3 modelOutput = new Vector3(
				Mathf.Clamp((normData.std[0] * normalizedX) + normData.mean[0], -1f, 1f),
				Mathf.Clamp((normData.std[1] * normalizedY) + normData.mean[1], -1f, 1f),
				Mathf.Clamp((normData.std[2] * normalizedZ) + normData.mean[2], -1f, 1f));

			// Vector3 modelOutput = new Vector3(normalizedX, normalizedY, normalizedZ);

			// En principio debería ser world space, pero el nuevo vértice en local space.
			newVertices[i] = transform.InverseTransformPoint(modelOutput);
			// newVertices[i] = modelOutput;
			vertex = vertex + newVertices[i].ToString() + "\n";
		}
		SaveData(vertex);
		mesh.SetVertices(newVertices);
		mesh.RecalculateNormals();
		mesh.RecalculateBounds();
		mesh.vertices = newVertices;

		inputTensor.Dispose();
		result.Dispose();
	}

	void OnDestroy()
    {
        worker?.Dispose();
    }
}