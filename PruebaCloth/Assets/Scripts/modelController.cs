using Unity.InferenceEngine;
using UnityEngine;

public class modelController : MonoBehaviour
{
    public GameObject modelML;
    public GameObject modelCloth;

    public void MLtoggle(bool isActive)
    {
        if (modelML != null)
        {
            modelML.SetActive(isActive);
        }
    }

    public void CLOTHtoggle(bool isActive)
    {
        if (modelCloth != null)
        {
            modelCloth.SetActive(isActive);
        }
    }
}