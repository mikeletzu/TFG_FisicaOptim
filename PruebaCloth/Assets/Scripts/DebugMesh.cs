using System.IO;
using Unity.InferenceEngine;
using UnityEngine;

public class DebugMesh : MonoBehaviour
{
	[SerializeField]
	public GameObject ball;
	public SphereCollider ballCollider;
	public ModelAsset modelAsset;
	public MeshFilter clothMeshFilter;
	public SkinnedMeshRenderer smr;
	int vertexCount;
	public Mesh bakedMesh;
	void Start()
    {
		var model = ModelLoader.Load(modelAsset);

		clothMeshFilter.mesh.MarkDynamic();

		vertexCount = clothMeshFilter.mesh.vertexCount;

		smr = GetComponent<SkinnedMeshRenderer>();

		bakedMesh = new Mesh();
	}

    // Update is called once per frame
    void Update()
    {
		// Capture the current poses
		smr.BakeMesh(bakedMesh);

		var mesh = bakedMesh;
		var vertices = mesh.vertices;
		var normals = mesh.normals;
		var uvs = mesh.uv; // Cachear las uvs fuera del bucle

		Vector3[] newVertices = new Vector3[vertexCount];
		Vector3 ayuda = new Vector3(0f, -0.001f, 0f);

		string vertex = "";
		for (int i = 0; i < vertexCount; i++)
		{
			newVertices[i] = vertices[i] + ayuda;
			vertex = vertex + newVertices[i].ToString() + "\n";
		}

		SaveData(vertex);
		mesh.Clear(); 
		mesh.SetVertices(newVertices);
		mesh.RecalculateNormals();
		mesh.RecalculateBounds();
		smr.localBounds = mesh.bounds;
		smr.sharedMesh = mesh;

		// Example: Apply to a MeshFilter for static rendering
		GetComponent<MeshFilter>().mesh = bakedMesh;

	}

	public void SaveData(string data)
	{
		string filePath = Application.persistentDataPath + "/debugOutput.txt";
		File.AppendAllText(filePath, "NEW FRAME" + "\n");
		File.AppendAllText(filePath, data + "\n");
	}
}
