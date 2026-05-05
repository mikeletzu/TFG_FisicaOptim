using System.Numerics;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;
using Vector3 = UnityEngine.Vector3;

public class LegMovement : MonoBehaviour
{
    [SerializeField]
    private bool isLeft = false;
    [SerializeField]
    private float movementSpeed = 30.0f;
    [SerializeField]
    private float maxRot = 30.0f;
    [SerializeField]
    private bool isAuto = true;
    private Vector3 dir = Vector3.forward;

    private void Start()
    {
        if (isLeft) dir = Vector3.forward;
        else dir = Vector3.back;
    }

    void Update()
    {
        if (transform.eulerAngles.z > maxRot && transform.eulerAngles.z < 180)
		    dir = Vector3.back; 
		else if(transform.eulerAngles.z < 330 && transform.eulerAngles.z >= 180)
            dir = Vector3.forward; 

        transform.Rotate(dir * Time.deltaTime * movementSpeed);
    }
}