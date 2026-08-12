using System.Numerics;
using Unity.Mathematics;
using Unity.VisualScripting;
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
    private Vector3 dir = Vector3.forward;

    [SerializeField]
    private float startTime = 0.0f;
    private bool move = false;

    private void Start()
    {
        if (isLeft) dir = Vector3.forward;
        else dir = Vector3.back;
    }

    void Update()
    {
        if (move)
        {
            if (transform.eulerAngles.z > maxRot && transform.eulerAngles.z < 180)
                dir = Vector3.back;
            else if (transform.eulerAngles.z < 330 && transform.eulerAngles.z >= 180)
                dir = Vector3.forward;

            transform.Rotate(dir * Time.deltaTime * movementSpeed);
        }
        else
        {
            startTime -= Time.deltaTime;
            if (startTime - Time.deltaTime <= 0) move = true;
        }
    }
}