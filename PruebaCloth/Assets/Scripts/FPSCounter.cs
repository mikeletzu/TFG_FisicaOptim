using UnityEngine;
using TMPro;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine.UI;


public class FPSCounter : MonoBehaviour
{
    string[] modelNames = new string[] { "MLlow", "MLmid", "MLhigh", "Cloth" };
    private float metricTimer;
    [SerializeField]
    private float metricTime;
    [SerializeField]
    private TextMeshProUGUI timerTxt;
    private int countdown = 5;

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
        totalTime += Time.deltaTime;
        if (totalTime >= updateInterval)
        {
            fpsText.text = $"{Mathf.RoundToInt(1 / Time.deltaTime)} FPS";
            totalTime = 0;
        }

        for (int i = 0; i < models.Length; i++)
        {
            if (!models[i].activeSelf) continue;

            metricTimer -= Time.deltaTime;

            if (metricTimer > metricTime*0.9) continue;

            framesCount++;
            accumTime += Time.unscaledDeltaTime;
            if (1 / Time.deltaTime < minFps) minFps = 1 / Time.deltaTime;
            if (1 / Time.deltaTime > maxFps) maxFps = 1 / Time.deltaTime;

            if (metricTimer <= 0)
            {
                reload(i);
                metricTimer = metricTime;
            }

            if (metricTimer <= metricTime - countdown)
            {
                timerTxt.text = "Measuring " + modelNames[i] + ".\nWait " + Mathf.RoundToInt(metricTime - countdown) + "s.";
                countdown += 5;
            }
        }
    }

    public void reload(int i)
    {
        models[i].SetActive(false);

        timerTxt.text = "Finished measuring " + modelNames[i] + ".\nChoose another model.";
     
        metricsText[i].text = Mathf.RoundToInt(framesCount / accumTime) + "\n" +
                            Mathf.RoundToInt(minFps) + "\n" + Mathf.RoundToInt(maxFps);

        GameObject s = GameObject.Find("Sphere");
        s.GetComponent<BallMovement>().enabled = false;
        s.transform.position = new Vector3(1.79999995f, -0.485000014f, 0);

        interactableToggles(true);
    }

    public void resetMean()
    {
        for (int i = 0; i < models.Length; i++)
        {
            if (!models[i].activeSelf) continue;
            fpsHistory = new List<float>();
            minFps = float.MaxValue;
            maxFps = float.MinValue;
            metricTimer = metricTime;
            countdown = 5;
            timerTxt.text = "Measuring " + modelNames[i] + ".\nWait " + Mathf.RoundToInt(metricTime) + "s.";
            metricsText[i].text = "00\n00\n00";
        }
    }

    public void modelChange()
    {
        for (int i = 0; i < models.Length; i++)
        {
            if (!models[i].activeSelf) continue;
            fpsHistory.Clear();
            minFps = float.MaxValue;
            maxFps = float.MinValue;
            metricsText[i].text = "00\n00\n00";
            metricTimer = metricTime;
            countdown = 5;
            timerTxt.text = "Measuring " + modelNames[i] + ".\nWait " + Mathf.RoundToInt(metricTime) + "s.";
            interactableToggles(false);
            GameObject.Find("Sphere").GetComponent<BallMovement>().enabled = true;
        }
    }

    private void interactableToggles(bool isActive)
    {
        GameObject toggles = GameObject.Find("Toggles");
        foreach (Transform toggle in toggles.transform)
        {
            toggle.gameObject.GetComponent<Toggle>().interactable = isActive;
        }
        GameObject.Find("MeasureTime").GetComponent<TMP_InputField>().interactable = isActive;
    }

    public void changeTime(string input)
    {
        if (float.TryParse(input, out float newTime))
        {
            metricTime = newTime;
        }
    }
}