using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

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

    // Update is called once per frame
    void Update()
    {
        if (isAuto)
            AutoLinealUpdate();
    }

    void AutoLinealUpdate()
    {
        if (transform.eulerAngles.z > maxRot) dir = Vector3.forward;
        else if (transform.eulerAngles.x < -maxRot) dir = Vector3.back;
        transform.Rotate(dir * Time.deltaTime * movementSpeed);
    }
}