using UnityEngine;
using TMPro;
using System.Collections.Generic;
public class FPSCounter : MonoBehaviour
{
    [SerializeField]
    private TextMeshProUGUI fpsText;
    [SerializeField]
    private TextMeshProUGUI meanText;

    [Header("Configuración de Rendimiento")]
    [SerializeField] private float updateInterval = 1.0f; // Cada cuántos segundos se actualiza el contador

    List<float> fpsHistory; // Para almacenar los FPS de los últimos frames
    int maxHistorial = 1000; // Número máximo de muestras a almacenar para el cálculo de la media

    private float accumTime = 0f;
    private int framesCount = 0;

    private void Start()
    {
        fpsHistory = new List<float>();
        resetMean();
    }

    private void Update()
    {
        framesCount++;
        accumTime += Time.unscaledDeltaTime;

        if (accumTime >= updateInterval)
        {
            float currentFPS = framesCount / accumTime;
            fpsHistory.Add(currentFPS);
            if (fpsHistory.Count > maxHistorial)
            {
                fpsHistory.RemoveAt(0);
            }

            fpsText.text = $"{Mathf.RoundToInt(currentFPS)} FPS";

            framesCount = 0;
            accumTime = 0f;
        }
    }

    public void resetMean()
    {
        float suma = 0f;
        for (int i = 0; i < fpsHistory.Count; i++)
        {
            suma += fpsHistory[i];
        }
        meanText.text = $"Mean: {Mathf.RoundToInt(suma / fpsHistory.Count)}";
        fpsHistory = new List<float>();
    }
}