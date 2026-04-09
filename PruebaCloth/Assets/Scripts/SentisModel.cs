using System.Drawing;
using Unity.AppUI.UI;
using Unity.InferenceEngine;
using UnityEditor.Build.Content;
using UnityEngine;

public class ClothML : MonoBehaviour
{
    [SerializeField]
    public GameObject ball;
    public SphereCollider ballCollider; // collider o mesh 
	public ModelAsset modelAsset;
    public MeshFilter clothMeshFilter;

    private Vector3[] lastVertexPositions;
    private Vector3[] maxDistance;

    Worker worker;
    Tensor<float> inputTensor;

    int vertexCount;

    void Start()
    {
        var model = ModelLoader.Load(modelAsset);
        worker = new Worker(model, BackendType.GPUCompute);

        vertexCount = clothMeshFilter.mesh.vertexCount;

        lastVertexPositions = new Vector3[vertexCount];
        ballCollider = ball.GetComponent<SphereCollider>();

        maxDistance = new Vector3[vertexCount];

        // Necesitaremos mantener cloth para algo? Entiendo que no.
        // Max distance se almacena y accede originalmente en cloth, aquí habría que añadirlo a mano en un vector
    }

    void Update()
    {
        var mesh = clothMeshFilter.mesh;
        var vertices = mesh.vertices;
        var normals = mesh.normals;

        inputTensor = new Tensor<float>(new TensorShape(1, vertexCount, 13));

        for (int i = 0; i < vertexCount; i++)
        {
			Vector3 pos = clothMeshFilter.transform.TransformPoint(vertices[i]);
            Vector3 vel = lastVertexPositions[i] - pos;        
            float sdf = Vector3.Distance(pos, ballCollider.transform.position) - ballCollider.radius;             // distancia a la bola
			Vector3 normal = normals[i];
            float maxDist = GetMaxDistance(i);     // constraint de la tela
            Vector2 uv = GetUV(i, mesh);

            int f = 0;

            // posición
            inputTensor[0, i, f++] = pos.x;
            inputTensor[0, i, f++] = pos.y;
            inputTensor[0, i, f++] = pos.z;

            // velocidad
            inputTensor[0, i, f++] = vel.x;
            inputTensor[0, i, f++] = vel.y;
            inputTensor[0, i, f++] = vel.z;

            // sdf
            inputTensor[0, i, f++] = sdf;

            // normal
            inputTensor[0, i, f++] = normal.x;
            inputTensor[0, i, f++] = normal.y;
            inputTensor[0, i, f++] = normal.z;

            // max distance
            inputTensor[0, i, f++] = maxDist;

            // uv
            inputTensor[0, i, f++] = uv.x;
            inputTensor[0, i, f++] = uv.y;
        }

        worker.Schedule(inputTensor);

        var output = worker.PeekOutput() as Tensor<float>;
        var result = output.ReadbackAndClone();

        Vector3[] newVertices = new Vector3[vertexCount];

        for (int i = 0; i < vertexCount; i++)
        {
            newVertices[i] = new Vector3(
                result[0, i, 0],
                result[0, i, 1],
                result[0, i, 2]
            );
        }

        mesh.vertices = newVertices;
        mesh.RecalculateNormals();

        inputTensor.Dispose();
        output.Dispose();
        result.Dispose();
    }

    Vector3 GetVelocity(int i)
    {
        return Vector3.zero;
    }

    float GetSDF(Vector3 pos)
    {
        return 0f;
    }

    float GetMaxDistance(int i)
    {
        return 1f;
    }

    Vector2 GetUV(int i, Mesh mesh)
    {
        return mesh.uv[i];
    }

    void OnDestroy()
    {
        worker?.Dispose();
    }
}