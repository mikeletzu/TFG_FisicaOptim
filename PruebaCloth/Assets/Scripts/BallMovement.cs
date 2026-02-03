using UnityEngine;

public class BallMovement : MonoBehaviour
{
    [SerializeField]
    private float movementSpeed = 10.0f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
       
            
    }

    // Update is called once per frame
    void Update()
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
}
