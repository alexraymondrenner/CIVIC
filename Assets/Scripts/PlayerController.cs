/*
using UnityEngine;

public class First_Person_Movement : MonoBehaviour
{
    private Vector3 velocity;
    private Vector3 playerMovementInput;
    private Vector2 playerMouseInput;
    private bool isSneaking = false;
    private float xRotation;

    [Header("Components Needed")]
    [SerializeField] private Transform playerCamera;
    [SerializeField] private CharacterController controller;
    [SerializeField] private Transform player;

    [Header("Movement")]
    [SerializeField] private float speed = 5f;
    [SerializeField] private float jumpForce = 5f;
    [SerializeField] private float sensitivity = 2f;
    [SerializeField] private float gravity = 9.81f;

    [Header("Sneaking")]
    [SerializeField] private bool canSneak = true;
    [SerializeField] private float sneakSpeed = 2.5f;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        GetInputs();
        HandleSneaking();
        MovePlayer();
        MoveCamera();
    }

    private void GetInputs()
    {
        playerMovementInput = new Vector3(Input.GetAxis("Horizontal"), 0f, Input.GetAxis("Vertical"));
        playerMouseInput = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
    }

    private void HandleSneaking()
    {
        if (Input.GetKey(KeyCode.LeftControl) && canSneak)
        {
            player.localScale = new Vector3(1f, 0.5f, 1f);
            isSneaking = true;
        }
        else
        {
            player.localScale = new Vector3(1f, 1f, 1f);
            isSneaking = false;
        }
    }

    private void MovePlayer()
    {
        Vector3 moveDirection = transform.TransformDirection(playerMovementInput.normalized);
        float currentSpeed = isSneaking ? sneakSpeed : speed;

        if (controller.isGrounded)
        {
            velocity.y = -1f;

            if (Input.GetKeyDown(KeyCode.Space) && !isSneaking)
            {
                velocity.y = jumpForce;
            }
        }
        else
        {
            velocity.y += gravity * Time.deltaTime;
        }

        Vector3 finalMovement = (moveDirection * currentSpeed) + new Vector3(0, velocity.y, 0);
        controller.Move(finalMovement * Time.deltaTime);
    }

    private void MoveCamera()
    {
        xRotation -= playerMouseInput.y * sensitivity;
    }
}
*/

using UnityEngine;

public class First_Person_Movement : MonoBehaviour
{
    private Vector3 velocity;
    private Vector3 playerMovementInput;
    private Vector2 playerMouseInput;
    private bool isSneaking = false;
    private float xRotation;

    [Header("Components Needed")]
    [SerializeField] private Transform playerCamera;
    [SerializeField] private CharacterController controller;
    [SerializeField] private Transform player;

    [Header("Movement")]
    [SerializeField] private float speed = 5f;
    [SerializeField] private float jumpForce = 5f;
    [SerializeField] private float sensitivity = 2f;
    [SerializeField] private float gravity = 9.81f;

    [Header("Sneaking")]
    [SerializeField] private bool canSneak = true;
    [SerializeField] private float sneakSpeed = 2.5f;

    [Header("Head Bobbing")]
    [SerializeField] private float bobFrequency = 8f;
    [SerializeField] private float bobAmplitude = 0.05f;
    private float bobTimer = 0f;
    private Vector3 defaultCamPos;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        defaultCamPos = playerCamera.localPosition;
    }

    void Update()
    {
        GetInputs();
        HandleSneaking();
        MovePlayer();
        MoveCamera();
        HeadBob();
    }

    private void GetInputs()
    {
        playerMovementInput = new Vector3(Input.GetAxis("Horizontal"), 0f, Input.GetAxis("Vertical"));
        playerMouseInput = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
    }

    private void HandleSneaking()
    {
        if (Input.GetKey(KeyCode.LeftControl) && canSneak)
        {
            player.localScale = new Vector3(1f, 0.5f, 1f);
            isSneaking = true;
        }
        else
        {
            player.localScale = new Vector3(1f, 1f, 1f);
            isSneaking = false;
        }
    }

    private void MovePlayer()
    {
        Vector3 moveDirection = transform.TransformDirection(playerMovementInput.normalized);
        float currentSpeed = isSneaking ? sneakSpeed : speed;

        if (controller.isGrounded)
        {
            velocity.y = -1f;

            if (Input.GetKeyDown(KeyCode.Space) && !isSneaking)
            {
                velocity.y = jumpForce;
            }
        }
        else
        {
            velocity.y += gravity * Time.deltaTime;
        }

        Vector3 finalMovement = (moveDirection * currentSpeed) + new Vector3(0, velocity.y, 0);
        controller.Move(finalMovement * Time.deltaTime);
    }

    private void MoveCamera()
    {
        // Vertical look
        xRotation -= playerMouseInput.y * sensitivity;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);
        playerCamera.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        // Horizontal rotation
        transform.Rotate(Vector3.up * playerMouseInput.x * sensitivity);
    }

    private void HeadBob()
    {
        if (controller.isGrounded && playerMovementInput.magnitude > 0.1f)
        {
            bobTimer += Time.deltaTime * bobFrequency;
            float bobOffset = Mathf.Sin(bobTimer) * bobAmplitude;
            Vector3 targetPosition = defaultCamPos + new Vector3(0f, bobOffset, 0f);
            playerCamera.localPosition = targetPosition;
        }
        else
        {
            bobTimer = 0f;
            playerCamera.localPosition = Vector3.Lerp(playerCamera.localPosition, defaultCamPos, Time.deltaTime * 5f);
        }
    }
}