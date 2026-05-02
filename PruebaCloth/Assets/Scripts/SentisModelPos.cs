using System;
using System.Drawing;
using System.IO;
using Unity.AppUI.UI;
using Unity.InferenceEngine;
using UnityEditor.Build.Content;
using UnityEngine;
using UnityEngine.UIElements;


public class ClothMLPos : MonoBehaviour
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

	// Datos de normalizaci�n
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

        lastVertexPositions = new Vector3[vertexCount];
        originalPositions = new Vector3[vertexCount];

		for (int im = 0; im < vertexCount; im++)
		{
			lastVertexPositions[im] = new Vector3(0,0,0);
            originalPositions[im] = clothMeshFilter.mesh.vertices[im];

			Debug.Log("V: " + im + " Pos: " + clothMeshFilter.mesh.vertices[im]);
		}

        ballCollider = ball.GetComponent<SphereCollider>();

        maxDistance = new float[vertexCount];

        // Necesitaremos mantener cloth para algo? Entiendo que no.
        // Max distance se almacena y accede originalmente en cloth, aqu� habr�a que a�adirlo a mano en un vector


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
		var normals = mesh.normals;
		var uvs = mesh.uv; // Cachear las uvs fuera del bucle
        string vertex = "";

        inputTensor = new Tensor<float>(new TensorShape(1, vertexCount, 4));

		for (int i = 0; i < vertexCount; i++)
		{
			Vector3 pos = vertices[i];
            vertex += clothMeshFilter.transform.TransformPoint(pos) + "\n";

            Debug.DrawRay(clothMeshFilter.transform.TransformPoint(pos), Vector3.up * 0.1f, UnityEngine.Color.red);
            
			if (lastVertexPositions[i] == Vector3.zero) lastVertexPositions[i] = pos;
            Vector3 vel = (pos - lastVertexPositions[i]) / Time.fixedDeltaTime;

            float sdf = Vector3.Distance(pos, transform.InverseTransformPoint(ball.transform.position)) - ballCollider.radius;
            Vector3 normal = normals[i];

            int f = 0;
            inputTensor[0, i, f++] = (originalPositions[i].x - normData.mean[0]) / normData.std[0];
            inputTensor[0, i, f++] = (originalPositions[i].y - normData.mean[1]) / normData.std[1];
            inputTensor[0, i, f++] = (originalPositions[i].z - normData.mean[2]) / normData.std[2];

            inputTensor[0, i, f++] = (sdf - normData.mean[3]) / normData.std[3];

            lastVertexPositions[i] = pos;
        }

        SaveData("pre", vertex);

        worker.Schedule(inputTensor);

		using var output = worker.PeekOutput() as Tensor<float>;
		var result = output.ReadbackAndClone();

		Vector3[] newVertices = new Vector3[vertexCount];
		
		vertex = "";
		for (int i = 0; i < vertexCount; i++)
		{
            if (maxDistance[i] == 0f)
            {
                newVertices[i] = vertices[i];
                continue; // Pasamos al siguiente v�rtice
            }

            // Denormalizamos (world 
            float dx = result[0, i, 0];
            float dy = result[0, i, 1];
            float dz = result[0, i, 2];

            Vector3 displacement;

            displacement = new Vector3(
                ((normData.target_std[0] * dx) + normData.target_mean[0]),
                ((normData.target_std[1] * dy) + normData.target_mean[1]),
                ((normData.target_std[2] * dz) + normData.target_mean[2]));

            //displacement = new Vector3(
            //    ((normData.target_std[0] * dx) + normData.target_mean[0]),
            //    0,
            //    0);

            // A�adimos el desplazamiento (Se ve que sustituci�n como que no)
            //Vector3 currentWorldPos = clothMeshFilter.transform.TransformPoint(vertices[i]);
            Vector3 newWorldPos = vertices[i] + displacement;

            // Espacio local para v�rtices
            // newVertices[i] = transform.InverseTransformPoint(newWorldPos);
            newVertices[i] = newWorldPos;

            vertex += transform.TransformPoint(newWorldPos) + "\n";
        }
		SaveData("post", vertex);
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