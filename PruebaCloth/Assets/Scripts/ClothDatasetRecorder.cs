using UnityEngine;
using System.IO;
using System.Text;

[RequireComponent(typeof(Cloth))]
public class ClothDatasetRecorder : MonoBehaviour
{
    [Header("Recording")]
    public bool record = true;
    public int[] vertexIndices;

    [Header("Output")]
    public string fileName = "cloth_dataset.csv";

    private Cloth cloth;

    // Estado en t
    private Vector3[] pos_t;
    private Vector3[] vel_t;

    // Para calcular velocidades
    private Vector3[] pos_t_minus_1;
    private bool hasPrevious = false;

    private StringBuilder sb;
    private string filePath;

    void Start()
    {
        cloth = GetComponent<Cloth>();

        AutoSelectVertices(10);

        if (vertexIndices == null || vertexIndices.Length == 0)
        {
            Debug.LogError("No vertex indices assigned.");
            enabled = false;
            return;
        }

        filePath = Path.Combine(Application.dataPath, fileName);
        sb = new StringBuilder();

        Debug.Log(filePath);

        // CSV Header
        sb.Append("frame");
        foreach (int i in vertexIndices)
            sb.Append($",x{i},y{i},z{i},vx{i},vy{i},vz{i}");
        foreach (int i in vertexIndices)
            sb.Append($",dx{i},dy{i},dz{i}");
        sb.AppendLine();
    }

    void AutoSelectVertices(int count)
    {
        vertexIndices = new int[count];
        for (int i = 0; i < count; i++)
            vertexIndices[i] = i;
    }

    void FixedUpdate()
    {
        if (!record) return;

        Vector3[] particles = cloth.vertices;

        pos_t = new Vector3[vertexIndices.Length];
        vel_t = new Vector3[vertexIndices.Length];

        for (int j = 0; j < vertexIndices.Length; j++)
        {
            int idx = vertexIndices[j];
            pos_t[j] = particles[idx];

            if (hasPrevious)
            {
                vel_t[j] = (particles[idx] - pos_t_minus_1[j]) / Time.fixedDeltaTime;
            }
            else
            {
                vel_t[j] = Vector3.zero;
            }
        }
    }

    void LateUpdate()
    {
        if (!record || pos_t == null) return;

        Vector3[] particles = cloth.vertices;

        // ?x = x(t+1) - x(t)
        Vector3[] delta = new Vector3[vertexIndices.Length];
        for (int j = 0; j < vertexIndices.Length; j++)
        {
            delta[j] = particles[vertexIndices[j]] - pos_t[j];
        }

        SaveSample(pos_t, vel_t, delta);

        // Guardar para el siguiente frame
        pos_t_minus_1 = (Vector3[])pos_t.Clone();
        hasPrevious = true;
    }

    void SaveSample(Vector3[] pos, Vector3[] vel, Vector3[] delta)
    {
        sb.Append(Time.frameCount);

        for (int j = 0; j < pos.Length; j++)
        {
            sb.Append($",{pos[j].x},{pos[j].y},{pos[j].z}");
            sb.Append($",{vel[j].x},{vel[j].y},{vel[j].z}");
        }

        for (int j = 0; j < delta.Length; j++)
        {
            sb.Append($",{delta[j].x},{delta[j].y},{delta[j].z}");
        }

        sb.AppendLine();

        File.AppendAllText(filePath, sb.ToString());
        sb.Length = 0;
    }
}
