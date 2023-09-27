using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Freecam : MonoBehaviour
{
    public float sensitivity = 2.0f;
    public float moveSpeedNormal = 5.0f, moveSpeedFast = 10.0f;

    private float rotationX = 0.0f;
    private float rotationY = 0.0f;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        // Rotation
        rotationX -= Input.GetAxis("Mouse Y") * sensitivity;
        rotationY += Input.GetAxis("Mouse X") * sensitivity;

        rotationX = Mathf.Clamp(rotationX, -90, 90);

        transform.rotation = Quaternion.Euler(rotationX, rotationY, 0);

        // Translation
        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");

        Vector3 moveDirection = new Vector3(horizontalInput, 0, verticalInput);

        float currentMoveSpeed = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? moveSpeedFast : moveSpeedNormal;


        moveDirection = transform.TransformDirection(moveDirection);
        moveDirection *= currentMoveSpeed * Time.deltaTime;

        transform.position += moveDirection;

        // Unlock cursor on Escape key press
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
        }
    }
}