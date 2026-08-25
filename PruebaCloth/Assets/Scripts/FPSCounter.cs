using UnityEngine;
using TMPro;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine.UI;
using UnityEngine.Splines;


public class FPSCounter : MonoBehaviour
{
    private float metricTimer;
    [SerializeField]
    private float metricTime;
    [SerializeField]
    private TextMeshProUGUI timerTxt;
    private int countdown = 5;

    private int activeModel = 0;
    public GameObject[] models;
    [SerializeField]
    private TextMeshProUGUI[] metricsText;

    [Header("Configuración de Rendimiento")]
    [SerializeField]
    private TextMeshProUGUI fpsText;
    [SerializeField] private float updateInterval = 1.0f; // Cada cuántos segundos se actualiza el contador

    List<float> fpsHistory; // Para almacenar los FPS de los últimos frames

    private float totalTime = 0f;

    private float accumTime = 0f;
    private int framesCount = 0;

    private float minFps = float.MinValue;
    private float maxFps = float.MinValue;

    private bool measuring = false;

    [SerializeField]
    GameObject stopButton;

    private void Awake()
    {
        fpsHistory = new List<float>();
        metricTimer = metricTime;
    }

    private void Start()
    {
        // Disable V-Sync to allow uncapped framerate
        QualitySettings.vSyncCount = 0;

        // Ensure no cap.
        Application.targetFrameRate = -1;
    }

    private void Update()
    {
        if (measuring)
        {
            totalTime += Time.deltaTime;
            if (totalTime >= updateInterval)
            {
                fpsText.text = $"{Mathf.RoundToInt(1 / Time.deltaTime)} FPS";
                totalTime = 0;
            }

            metricTimer -= Time.deltaTime;

            framesCount++;
            accumTime += Time.unscaledDeltaTime;
            if (1 / Time.deltaTime < minFps) minFps = 1 / Time.deltaTime;
            if (1 / Time.deltaTime > maxFps) maxFps = 1 / Time.deltaTime;

            if (metricTimer <= 0)
            {
                stop();
            }

            if (metricTimer <= metricTime - countdown)
            {
                timerTxt.text = "Measuring.\nWait " + Mathf.RoundToInt(metricTime - countdown) + "s.";
                countdown += 5;
            }
        }
        
    }

    public void reload(int i)
    {
        models[i].SetActive(false);

        timerTxt.text = "Finished measuring.\nChoose another model.";
     
        metricsText[i].text = Mathf.RoundToInt(minFps).ToString("000") + "   " + Mathf.RoundToInt(maxFps).ToString("000")
              + "   " + Mathf.RoundToInt(framesCount / accumTime).ToString("000");

        interactableToggles(true);
    }

    public void modelChange()
    {
        for (int i = 0; i < models.Length; i++)
        {
            if (!models[i].activeSelf) continue;
            activeModel = i;
            fpsHistory.Clear();
            minFps = float.MaxValue;
            maxFps = float.MinValue;
            metricsText[i].text = "000   000   000";
        }
    }

    public void start()
    {
        metricTimer = metricTime;
        countdown = 5;
        measuring = true;
        interactableToggles(false);
        timerTxt.text = "Measuring.\nWait " + Mathf.RoundToInt(metricTime) + "s.";
        GameObject.Find("Sphere").GetComponent<BallMovement>().enabled = true;
    }

    public void stop()
    {
        measuring = false;
        interactableToggles(true);
        GameObject.Find("Sphere").GetComponent<BallMovement>().enabled = false;
        stopButton.SetActive(false);
        reload(activeModel);
    }

    private void interactableToggles(bool isActive)
    {
        GameObject toggles = GameObject.Find("Toggles");
        foreach (Transform toggle in toggles.transform)
        {
            toggle.gameObject.GetComponent<Toggle>().interactable = isActive;
        }
    }

    public void changeTime(string input)
    {
        if (float.TryParse(input, out float newTime))
        {
            metricTime = newTime;
        }
    }
}