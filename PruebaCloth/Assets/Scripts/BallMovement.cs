using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

public class BallMovement : MonoBehaviour
{
    [SerializeField]
    private bool isLineal = false;
    SplineAnimate splineAnimate = null;
    PathGenerator pathGen = null;
    [SerializeField]
    private float movementSpeed = 10.0f;
    [SerializeField]
    private bool isAuto = false;
    private int dir = -1;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (isAuto && !isLineal)
        {
            splineAnimate = GetComponent<SplineAnimate>();
            pathGen = GameObject.Find("PathGen").GetComponent<PathGenerator>();
            SetPath(transform.position);
            splineAnimate.MaxSpeed = movementSpeed;
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (isAuto)
        {
            if (isLineal)
                AutoLinealUpdate();
            else AutoBezierUpdate();
        }
        else KeyUpdate();
    }

    void KeyUpdate()
    {
        // Movemos a la bola según el input en tres ejes
        // X
        if (Input.GetKey(KeyCode.D)) // Establecer límites para el movimiento?
        {
            transform.position += Vector3.right * Time.deltaTime * movementSpeed;
        }
        else if (Input.GetKey(KeyCode.A))
        {
            transform.position += Vector3.left * Time.deltaTime * movementSpeed;
        }
        // Y
        if (Input.GetKey(KeyCode.S))
        {
            transform.position += Vector3.back * Time.deltaTime * movementSpeed;
        }
        else if (Input.GetKey(KeyCode.W))
        {
            transform.position += Vector3.forward * Time.deltaTime * movementSpeed;
        }
        // Z
        if (Input.GetKey(KeyCode.R))
        {
            transform.position += Vector3.up * Time.deltaTime * movementSpeed;
        }
        else if (Input.GetKey(KeyCode.F))
        {
            transform.position += Vector3.down * Time.deltaTime * movementSpeed;
        }
    }

    void AutoLinealUpdate()
    {
        if (transform.position.x > 2) dir = -1;
        else if (transform.position.x < -2) dir = 1;
        transform.position += dir * Vector3.right * Time.deltaTime * movementSpeed;
    }

    void AutoBezierUpdate()
    {
        if (splineAnimate != null && splineAnimate.Container != null && splineAnimate.ElapsedTime >= splineAnimate.Duration)
        {
            SetPath(splineAnimate.Container.Spline.ToArray()[splineAnimate.Container.Spline.ToArray().Length - 1].Position);
        }
    }
    private void SetPath(Vector3 initPos)
    {
        SplineContainer sp = pathGen.GeneratePath(initPos);
        SplineContainer aux = splineAnimate.Container;
        splineAnimate.Container = sp;
        if (aux != null)
        {
            Destroy(aux.gameObject);
        }
        splineAnimate.ElapsedTime = 0;
        splineAnimate.Play();
    }
}