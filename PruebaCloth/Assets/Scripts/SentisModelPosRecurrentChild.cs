using System;
using System.Collections.Generic;
using System.IO;
using Unity.InferenceEngine; // O Unity.Sentis dependiendo de tu versi�n exacta
using UnityEngine;

public class ClothMLPosRecChild : ClothML
{
    [SerializeField] private int seqLen = 8;

    // La base llama a estas propiedades en Start(), garantizando los valores correctos
    protected override int SeqLen => seqLen;
    protected override int FeatureCount => 4; // x, y, z, sdf

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

        //float[] sdf = new float [vertices.Length];
        //SDFUtil.getSDFOfSet(vertices, capsuleColliders, sphereColliders, collidersUnionSmoothness, transform, sdf);
        Vector3 spherePos = transform.InverseTransformPoint(sphereColliders[0].transform.position); 
		float sphereRad = sphereColliders[0].radius/4;
		for (int v = 0; v < VertexCount; v++)
        {
            Vector3 pos = vertices[v];
            float sdf = Vector3.Distance(pos, spherePos) - sphereRad; 
            //float sdf = SDFUtil.getSDFOfSet(pos, capsuleColliders, sphereColliders, collidersUnionSmoothness, transform);

            historyBuffer[offset + v * FeatureCount + 0] = (pos.x - normData.mean[0]) / normData.std[0];
            historyBuffer[offset + v * FeatureCount + 1] = (pos.y - normData.mean[1]) / normData.std[1];
            historyBuffer[offset + v * FeatureCount + 2] = (pos.z - normData.mean[2]) / normData.std[2];
           // historyBuffer[offset + v * FeatureCount + 3] = (sdf[v] - normData.mean[3]) / normData.std[3];
            historyBuffer[offset + v * FeatureCount + 3] = (sdf - normData.mean[3]) / normData.std[3];
        }
    }
}