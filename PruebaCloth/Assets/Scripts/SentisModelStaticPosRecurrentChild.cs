using System;
using System.Collections.Generic;
using System.IO;
using Unity.InferenceEngine; // O Unity.Sentis dependiendo de tu versi�n exacta
using UnityEngine;

public class ClothMLStaticPosRecChild : ClothML
{
    [SerializeField] private int seqLen = 8;

    // La base llama a estas propiedades en Start(), garantizando los valores correctos
    protected override int SeqLen => seqLen;
    protected override int FeatureCount => 3; // x, y, z

    public Transform[] anchors;
    private int[] anchorIds;

    public override void Start()
    {        
        var mesh = clothMeshFilter.mesh;
        var vertices = mesh.vertices;
        VertexCount = clothMeshFilter.mesh.vertexCount;

        for (int v = 0; v < VertexCount; v++)
        {
            Vector3 pos = vertices[v];
            Debug.Log($"Vertex {v}: {pos}");
        }

        anchorIds = new int[4] { 0, 0, 0, 0 }; // Bottom-Left, Bottom-Right, Top-Left, Top-Right

        // Una sola pasada para encontrar los 4 vértices de las esquinas
        for (int i = 0; i < vertices.Length; i++)
        {
            if (vertices[anchorIds[0]].x >= vertices[i].x && vertices[anchorIds[0]].y <= vertices[i].y) anchorIds[0] = i; // Top-Left
            if (vertices[anchorIds[1]].x <= vertices[i].x && vertices[anchorIds[1]].y <= vertices[i].y) anchorIds[1] = i; // Top-Right
            if (vertices[anchorIds[2]].x >= vertices[i].x && vertices[anchorIds[2]].y >= vertices[i].y) anchorIds[2] = i; // Bottom-Left
            if (vertices[anchorIds[3]].x <= vertices[i].x && vertices[anchorIds[3]].y >= vertices[i].y) anchorIds[3] = i; // Bottom-Right
        }

        Debug.Log("Anchors in vertex:" + anchorIds[0] + ", " + anchorIds[1] + ", " + anchorIds[2] + ", " + anchorIds[3]);

        base.Start();
    }

    protected override void FillInitialBuffer()
    {
        // 1. Calcula el frame inicial UNA vez en el slot 0
        saveStateAt(0);

        // 2. Duplicación exponencial: rellena todos los slots restantes
        int filled = 1;
        int frameStride = VertexCount * FeatureCount;
        while (filled < seqLen)
        {
            int toCopy = Mathf.Min(filled, seqLen - filled); // no te pases del límite
            Array.Copy(historyBuffer, 0, historyBuffer, filled * frameStride, toCopy * frameStride);
            filled += toCopy;
        }
    }

    protected override void UpdateBuffer()
    {
        int frameStride = VertexCount * FeatureCount;

        // 1. Shift: mueve [1..seqLen-1] → [0..seqLen-2]
        Array.Copy(historyBuffer, frameStride, historyBuffer, 0, (seqLen - 1) * frameStride);

        // 2. Escribe el frame actual al final
        int offset = (seqLen - 1) * frameStride;
        saveStateAt(offset);
    }

    protected override void saveStateAt(int offset)
    {
        var mesh = clothMeshFilter.mesh;
        var vertices = mesh.vertices;

        bool ancla = false;

        for (int v = 0; v < VertexCount; v++)
        {
            Vector3 pos = Vector3.zero;
            for (int j = 0; j < 4; j++) {
                if (v == anchorIds[j])
                {
                    ancla = true;
                    pos = anchors[j].position;
                }
            }
            if (!ancla)
            {
                pos = vertices[v];
            }

            historyBuffer[offset + v * FeatureCount + 0] = (pos.x - normData.mean[0]) / normData.std[0];
            historyBuffer[offset + v * FeatureCount + 1] = (pos.y - normData.mean[1]) / normData.std[1];
            historyBuffer[offset + v * FeatureCount + 2] = (pos.z - normData.mean[2]) / normData.std[2];
        }
    }
}