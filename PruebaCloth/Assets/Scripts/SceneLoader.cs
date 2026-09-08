using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UIElements;
using System.Security.Cryptography.X509Certificates;

public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set; }

    [Header("UI References")]
    public TMP_Dropdown sceneDropdown;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void LoadSelectedScene()
    {
        if (sceneDropdown == null)
        {
            Debug.LogError("Scene Dropdown is not assigned to the SceneLoader script!");
            return;
        }

        int selectedIndex = sceneDropdown.value;
        string sceneToLoad = sceneDropdown.options[selectedIndex].text;

        SceneManager.LoadScene(sceneToLoad);
    }

    public void BackToMenu()
    {
        SceneManager.LoadScene("MenuScene");
    }

    public void doExitGame()
    {
        Application.Quit();
    }
}