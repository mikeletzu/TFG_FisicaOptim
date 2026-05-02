using System.IO;
using System;
using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter))]
public class DataVerifier : MonoBehaviour
{
    [Header("Configuración de Archivo")]
    public string fileName = "clothDataset_31_.csv";

    [Header("Control de Reproducción")]
    public float snapShotTime = 0.6f;
    private float snapTimeLeft = 0.0f;
    public bool loop = true;
    public bool playOnStart = true;

    private Mesh targetMesh;
    private Vector3[][] animationFrames;
    private int totalFrames = 0;
    private int currentFrameIndex = 0;
    private bool isPlaying = false;

    void Start()
    {
        targetMesh = GetComponent<MeshFilter>().mesh;
        // Optimizamos el mesh para actualizaciones frecuentes
        targetMesh.MarkDynamic();

        LoadCSV();

        if (playOnStart) isPlaying = true;
    }

    void LoadCSV()
    {
        string content = "";

        string path = Path.Combine(Application.dataPath + "/Datasets/", fileName);
        if (File.Exists(path)) content = File.ReadAllText(path);

        string[] lines = content.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        // La primera línea es el header, la saltamos
        totalFrames = lines.Length - 1;
        animationFrames = new Vector3[totalFrames][];

        for (int i = 0; i < totalFrames; i++)
        {
            string[] values = lines[i + 1].Split(',');

            // Según tu CSV: col 0 es 'frame', luego cada vértice tiene 13 parámetros:
            // x, y, z, vx, vy, vz, sdf, nx, ny, nz, md, u, v
            int vertexCount = (values.Length - 1) / 13;
            animationFrames[i] = new Vector3[vertexCount];

            for (int v = 0; v < vertexCount; v++)
            {
                int startIndex = 1 + (v * 13);
                float x = float.Parse(values[startIndex], System.Globalization.CultureInfo.InvariantCulture);
                float y = float.Parse(values[startIndex + 1], System.Globalization.CultureInfo.InvariantCulture);
                float z = float.Parse(values[startIndex + 2], System.Globalization.CultureInfo.InvariantCulture);

                animationFrames[i][v] = new Vector3(x, y, z);
            }
        }

        Debug.Log($"CSV Cargado: {totalFrames} frames para {animationFrames[0].Length} vértices.");
    }

    void FixedUpdate()
    {
        if (!isPlaying || animationFrames == null || totalFrames == 0) return;


        snapTimeLeft -= Time.deltaTime;
        if (snapTimeLeft < 0)
        {
            snapTimeLeft = snapShotTime;
            ApplyFrame(currentFrameIndex);

            currentFrameIndex++;
            if (currentFrameIndex >= totalFrames)
            {
                if (loop) currentFrameIndex = 0;
                else isPlaying = false;
            }
        }
    }

    void ApplyFrame(int frameIdx)
    {
        targetMesh.vertices = animationFrames[frameIdx];

        targetMesh.RecalculateNormals();
        targetMesh.RecalculateBounds();
    }
}
