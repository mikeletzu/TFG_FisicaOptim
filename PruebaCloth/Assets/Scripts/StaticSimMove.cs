using UnityEngine;
using UnityEngine.Splines;
using static Unity.Burst.Intrinsics.X86;

public class StaticSimMove : MonoBehaviour
{
    //public Transform huesoControl; // Asigna aquí el hueso en el Inspector
    //private Vector3 offset;
    //private float zCoord;
    public int actId = 0;
    public double actTime = 0.0;
    public double actTimer = 5.0;
    public GameObject[] bones;
    SplineAnimate splineAnimate = null;
    public PathGenerator[] pathGens;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

        SetPath(bones[actId].transform.position);
    }

    // Update is called once per frame
    void Update()
    {
        actTime += Time.deltaTime;
        if (actTime >= actTimer)
        {
            actTime = 0;
            splineAnimate.Pause();
            actId++;
            if (actId >= bones.Length)
            {
                actId = 0;
            }
            SetPath(bones[actId].transform.position);
        }
        if (splineAnimate != null && splineAnimate.Container != null && splineAnimate.ElapsedTime >= splineAnimate.Duration)
        {
            SetPath(splineAnimate.Container.Spline.ToArray()[splineAnimate.Container.Spline.ToArray().Length - 1].Position);
        }

    }

    private void SetPath(Vector3 initPos)
    {
        splineAnimate = bones[actId].GetComponent<SplineAnimate>();
        SplineContainer sp = pathGens[actId].GeneratePath(initPos);
        SplineContainer aux = splineAnimate.Container;
        splineAnimate.Container = sp;
        if (aux != null)
        {
            Destroy(aux.gameObject);
        }
        splineAnimate.ElapsedTime = 0;
        splineAnimate.Play();
    }

    //void OnMouseDown()
    //{
    //    Debug.Log("Mouse Down on " + huesoControl.name);
    //    zCoord = Camera.main.WorldToScreenPoint(huesoControl.position).z;
    //    offset = huesoControl.position - GetMouseWorldPos();
    //}

    //void OnMouseDrag()
    //{
    //    // Mueve el hueso y la tela fija lo seguirá inmediatamente
    //    huesoControl.position = GetMouseWorldPos() + offset;
    //}

    //private Vector3 GetMouseWorldPos()
    //{
    //    Vector3 mousePoint = Input.mousePosition;
    //    mousePoint.z = zCoord;
    //    return Camera.main.ScreenToWorldPoint(mousePoint);
    //}
}
