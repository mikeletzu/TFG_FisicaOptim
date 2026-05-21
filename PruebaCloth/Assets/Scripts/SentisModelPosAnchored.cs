
using System.IO;
using Unity.InferenceEngine;
using UnityEngine;


public class ClothMLAnchored : MonoBehaviour
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

    public int contador = 0;
    int vertexCount;

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

		vertexCount = clothMeshFilter.mesh.vertexCount;

		originalPositions = new Vector3[vertexCount];
		for (int i = 0; i < vertexCount; i++)
		{
			originalPositions[i] = clothMeshFilter.mesh.vertices[i];
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
    

			Debug.Log("V: " + im + " Pos: " + clothMeshFilter.mesh.vertices[im]);
		}

        ballCollider = ball.GetComponent<SphereCollider>();

        maxDistance = new float[vertexCount];

        // Necesitaremos mantener cloth para algo? Entiendo que no.
        // Max distance se almacena y accede originalmente en cloth, aquí habría que añadirlo a mano en un vector


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

	}

	public void SaveData(string data)
	{
		string filePath = Application.persistentDataPath + "/debugOutput.txt";
        File.AppendAllText(filePath, "NEW FRAME" + "\n");
		File.AppendAllText(filePath, data + "\n");
	}

	void FixedUpdate()
	{
		var mesh = clothMeshFilter.mesh;
		var vertices = mesh.vertices;
		var normals = mesh.normals;
		var uvs = mesh.uv; // Cachear las uvs fuera del bucle

		inputTensor = new Tensor<float>(new TensorShape(1, vertexCount, 16));
		Vector3[] prevPositions = lastVertexPositions;

		for (int i = 0; i < vertexCount; i++)
		{
			//Vector3 pos = clothMeshFilter.transform.TransformPoint(vertices[i]);
			Vector3 pos = vertices[i];
			Debug.DrawRay(clothMeshFilter.transform.TransformPoint(pos), Vector3.up * 0.1f, UnityEngine.Color.red);
			Vector3 vel = (pos - lastVertexPositions[i]) / Time.fixedDeltaTime;

		    lastVertexPositions[i] = clothMeshFilter.mesh.vertices[i]; 
			


			float sdf = Vector3.Distance(pos, transform.InverseTransformPoint(ball.transform.position)) - ballCollider.radius;
            Vector3 normal = normals[i];

            int f = 0;
            inputTensor[0, i, f++] = (originalPositions[i].x- normData.mean[0]) / normData.std[0];
            inputTensor[0, i, f++] = (originalPositions[i].y- normData.mean[1]) / normData.std[1];
            inputTensor[0, i, f++] = (originalPositions[i].z - normData.mean[2]) / normData.std[2];

            inputTensor[0, i, f++] = (pos.x - normData.mean[3]) / normData.std[3];
            inputTensor[0, i, f++] = (pos.y - normData.mean[4]) / normData.std[4];
            inputTensor[0, i, f++] = (pos.z - normData.mean[5]) / normData.std[5];

            inputTensor[0, i, f++] = (vel.x - normData.mean[6]) / normData.std[6];
            inputTensor[0, i, f++] = (vel.y - normData.mean[7]) / normData.std[7];
            inputTensor[0, i, f++] = (vel.z - normData.mean[8]) / normData.std[8];

            inputTensor[0, i, f++] = (sdf - normData.mean[9]) / normData.std[9];

            inputTensor[0, i, f++] = (normal.x - normData.mean[10]) / normData.std[10];
            inputTensor[0, i, f++] = (normal.y - normData.mean[11]) / normData.std[11];
            inputTensor[0, i, f++] = (normal.z - normData.mean[12]) / normData.std[12];

            inputTensor[0, i, f++] = (maxDistance[i] - normData.mean[13]) / normData.std[13];

            inputTensor[0, i, f++] = (uvs[i].x - normData.mean[14]) / normData.std[14];
            inputTensor[0, i, f++] = (uvs[i].y - normData.mean[15]) / normData.std[15];

            

			if (contador < 5 && i==0)
			{
				Debug.Log($"Frame {contador} V0 — " +
						  $"anc:({originalPositions[0].x:F4},{originalPositions[0].y:F4},{originalPositions[0].z:F4}) " +
						  $"pos:({vertices[0].x:F4},{vertices[0].y:F4},{vertices[0].z:F4}) " +
						  $"vel:({vel.x:F4},{vel.y:F4},{vel.z:F4}) " +
						  $"sdf:{sdf:F4} " +
						  $"md:{maxDistance[0]:F4}");
				contador++;
			}
			for (int im = 0; im< vertexCount; im++)
				lastVertexPositions[im] = vertices[im];
		}

		worker.Schedule(inputTensor);

		using var output = worker.PeekOutput() as Tensor<float>;
		var result = output.ReadbackAndClone();

		Vector3[] newVertices = new Vector3[vertexCount];
		
		string vertex = "";
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

            //Vector3 displacement = new Vector3(
            //    ((normData.target_std[0] * dx) + normData.target_mean[0]),
            //    ((normData.target_std[1] * dy) + normData.target_mean[1]),
            //    ((normData.target_std[2] * dz) + normData.target_mean[2]));
            Vector3 displacement;
      
            displacement = new Vector3(
                ((normData.target_std[0] * dx) + normData.target_mean[0]),
                ((normData.target_std[1] * dy) + normData.target_mean[1]),
                ((normData.target_std[2] * dz) + normData.target_mean[2]));


			//        Vector3 displacement = new Vector3(
			//            0,
			//            ((normData.target_std[1] * dy) + normData.target_mean[1]),
			//(normData.target_std[2] * dz) + normData.target_mean[2]);
			//Vector3 displacement = new Vector3(
			//    0,
			//    0,
			//    ((normData.target_std[2] * dz) + normData.target_mean[2]));

			// Añadimos el desplazamiento (Se ve que sustitución como que no)
			//Vector3 currentWorldPos = clothMeshFilter.transform.TransformPoint(vertices[i]);
			Vector3 newWorldPos = vertices[i] + displacement;

            // Espacio local para vértices
            // newVertices[i] = transform.InverseTransformPoint(newWorldPos);
            newVertices[i] = newWorldPos;

            vertex += transform.TransformPoint(newWorldPos) + "\n";
        }
		SaveData(vertex);
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