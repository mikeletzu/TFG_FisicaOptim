using System;
using System.Collections.Generic;
using System.IO;
using Unity.InferenceEngine;
using UnityEngine;

public class ClothMLPosRecChild : ClothML
{
    [SerializeField] private int seqLen = 8;
    protected override int SeqLen => seqLen;
    protected override int FeatureCount => 4; // x, y, z, sdf

    protected override void FillInitialBuffer()
    {

        saveStateAt(0);


        int filled = 1;
        int frameStride = VertexCount * FeatureCount;
        while (filled < seqLen)
        {
            int toCopy = Mathf.Min(filled, seqLen - filled);
            Array.Copy(historyBuffer, 0, historyBuffer, filled * frameStride, toCopy * frameStride);
            filled += toCopy;
        }
    }

    protected override void UpdateBuffer()
    {
        int frameStride = VertexCount * FeatureCount;

        Array.Copy(historyBuffer, frameStride, historyBuffer, 0, (seqLen - 1) * frameStride);

        int offset = (seqLen - 1) * frameStride;
        saveStateAt(offset);
    }

    protected override void saveStateAt(int offset)
    {
        var mesh = clothMeshFilter.mesh;
        var vertices = mesh.vertices;

        Vector3 spherePos = transform.InverseTransformPoint(sphereColliders[0].transform.position); 
		float sphereRad = sphereColliders[0].radius/4;
		for (int v = 0; v < VertexCount; v++)
        {
            Vector3 pos = vertices[v];
            float sdf = Vector3.Distance(pos, spherePos) - sphereRad; 

            historyBuffer[offset + v * FeatureCount + 0] = (pos.x - normData.mean[0]) / normData.std[0];
            historyBuffer[offset + v * FeatureCount + 1] = (pos.y - normData.mean[1]) / normData.std[1];
            historyBuffer[offset + v * FeatureCount + 2] = (pos.z - normData.mean[2]) / normData.std[2];
            historyBuffer[offset + v * FeatureCount + 3] = (sdf - normData.mean[3]) / normData.std[3];
        }
    }
}