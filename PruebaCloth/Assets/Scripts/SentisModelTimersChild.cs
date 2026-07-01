using System;
using System.Collections.Generic;
using System.IO;
using Unity.InferenceEngine; // O Unity.Sentis dependiendo de tu versi�n exacta
using UnityEngine;

public class ClothMLTimersChild : ClothML
{
    [SerializeField] private int seqLen = 8;

    // La base llama a estas propiedades en Start(), garantizando los valores correctos
    protected override int SeqLen => seqLen;
    protected override int FeatureCount => 6; // x, y, z, sdf, ct, ec

    private float[] collisionTimer_t;     // Negativo si colisiona, positivo si no
    private float[] hasEverCollided_t;

    protected override void FillInitialBuffer()
    {
        collisionTimer_t = new float[VertexCount];
        hasEverCollided_t = new float[VertexCount];

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

        for (int v = 0; v < VertexCount; v++)
        {
            Vector3 pos = vertices[v];
            float sdf = SDFUtil.getSDFOfSet(pos, capsuleColliders, sphereColliders, collidersUnionSmoothness, transform);

            if (sdf <= 0.01f) // COLISIONA
            {
                // Si estaba en positivo (sin colisionar), lo reseteamos a 0 y empezamos a restar
                if (collisionTimer_t[v] > 0f) collisionTimer_t[v] = 0f;

                collisionTimer_t[v] -= Time.fixedDeltaTime;
                hasEverCollided_t[v] = 1.0f; // Ha colisionado alguna vez
            }
            else // NO COLISIONA
            {
                // Si estaba en negativo (colisionando), lo reseteamos a 0 y empezamos a sumar
                if (collisionTimer_t[v] < 0f) collisionTimer_t[v] = 0f;

                collisionTimer_t[v] += Time.fixedDeltaTime;
            }
            // ----------------

            historyBuffer[offset + v * FeatureCount + 0] = (pos.x - normData.mean[0]) / normData.std[0];
            historyBuffer[offset + v * FeatureCount + 1] = (pos.y - normData.mean[1]) / normData.std[1];
            historyBuffer[offset + v * FeatureCount + 2] = (pos.z - normData.mean[2]) / normData.std[2];
            historyBuffer[offset + v * FeatureCount + 3] = (sdf - normData.mean[3]) / normData.std[3];
            historyBuffer[offset + v * FeatureCount + 4] = (collisionTimer_t[v] - normData.mean[4]) / normData.std[4];
            historyBuffer[offset + v * FeatureCount + 5] = (hasEverCollided_t[v] - normData.mean[5]) / normData.std[5];
        }
    }
}