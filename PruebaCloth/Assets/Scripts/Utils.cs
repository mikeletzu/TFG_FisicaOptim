using System;
using UnityEngine;

public class Utils
{
    static public float[] setMaxDistance(int vertexCount)
    {
        float[] maxDistance = new float[vertexCount];

        Array.Fill(maxDistance, 0.2f); // Valor por defecto para todos los vértices

        switch (vertexCount)
        {
            case 6:
                // Plano 6 v
                maxDistance[4] = 0f;
                maxDistance[5] = 0f;
                break;
            case 25:
                // Plano 25 v
                maxDistance[11] = 0f;
                maxDistance[12] = 0f;
                maxDistance[18] = 0f;
                maxDistance[22] = 0f;
                maxDistance[24] = 0f;
                break;
            case 32:
                // Falda 32 v
                maxDistance[2] = 0f;
                maxDistance[3] = 0f;
                maxDistance[4] = 0f;
                maxDistance[6] = 0f;
                maxDistance[8] = 0f;
                maxDistance[10] = 0f;
                maxDistance[12] = 0f;
                maxDistance[14] = 0f;
                break;
            default:
                break;
        }

        return maxDistance;
    }

}
