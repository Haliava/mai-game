using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public sealed class FPSCharacterController3D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Camera playerCamera;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float walkSpeed = 5f;
    [SerializeField, Min(0f)] private float sprintSpeed = 8f;
    [SerializeField, Min(0f)] private float acceleration = 24f;
    [SerializeField, Min(0f)] private float airAcceleration = 8f;
    [SerializeField, Min(0f)] private float jumpHeight = 1.35f;
    [SerializeField] private float gravity = -18f;
    [SerializeField] private float groundedStickForce = -2f;
    [SerializeField, Min(0f)] private float externalVelocityDamping = 6f;

    [Header("Look")]
    [SerializeField, Min(0.01f)] private float mouseSensitivity = 0.12f;
    [SerializeField] private float minPitch = -85f;
    [SerializeField] private float maxPitch = 85f;
    [SerializeField] private bool lockCursorOnStart = true;

    private Vector3 horizontalVelocity;
    private Vector3 externalVelocity;
    private float verticalVelocity;
    private float pitch;
    private bool grappleMovementControlActive;
    private bool grappleMovementPaused;
    private bool lookPaused;

    public CharacterController CharacterController => characterController;
    public Camera PlayerCamera => playerCamera;
    public Vector3 HorizontalVelocity => horizontalVelocity;
    public Vector3 ExternalVelocity => externalVelocity;
    public Vector3 CurrentVelocity => horizontalVelocity + Vector3.up * verticalVelocity + externalVelocity;
    public bool IsGrounded => characterController != null && characterController.isGrounded;

    public Vector2 GetMoveInput()
    {
        return ReadMoveInput();
    }

    public void AddExternalVelocity(Vector3 velocity)
    {
        externalVelocity += velocity;
    }

    public void AddVelocity(Vector3 velocity)
    {
        horizontalVelocity += Vector3.ProjectOnPlane(velocity, Vector3.up);
        verticalVelocity += velocity.y;
    }

    public void SetExternalVelocity(Vector3 velocity)
    {
        externalVelocity = velocity;
    }

    public void SetCurrentVelocity(Vector3 velocity)
    {
        horizontalVelocity = Vector3.ProjectOnPlane(velocity, Vector3.up);
        verticalVelocity = velocity.y;
        externalVelocity = Vector3.zero;
    }

    public void ClearExternalVelocity()
    {
        externalVelocity = Vector3.zero;
    }

    public void SetGrappleMovementControlActive(bool active)
    {
        grappleMovementControlActive = active;
    }

    public void SetGrappleMovementPaused(bool paused)
    {
        grappleMovementPaused = paused;
    }

    public void SetLookPaused(bool paused)
    {
        lookPaused = paused;
        if (!paused && playerCamera != null)
        {
            pitch = NormalizePitch(playerCamera.transform.localEulerAngles.x);
        }
    }

    public void ForceLookAt(Vector3 worldPoint)
    {
        if (playerCamera == null)
        {
            return;
        }

        Vector3 cameraPosition = playerCamera.transform.position;
        Vector3 toTarget = worldPoint - cameraPosition;
        if (toTarget.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Vector3 horizontalDirection = Vector3.ProjectOnPlane(toTarget, Vector3.up);
        if (horizontalDirection.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(horizontalDirection.normalized, Vector3.up);
        }

        Vector3 localDirection = transform.InverseTransformDirection(toTarget.normalized);
        pitch = Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(localDirection.y, -1f, 1f)) * Mathf.Rad2Deg, minPitch, maxPitch);
        playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    public void ForceViewRotation(Quaternion bodyRotation, Quaternion cameraLocalRotation)
    {
        transform.rotation = bodyRotation;
        if (playerCamera != null)
        {
            playerCamera.transform.localRotation = cameraLocalRotation;
            pitch = NormalizePitch(cameraLocalRotation.eulerAngles.x);
        }
    }

    public void ClampCurrentVelocity(float maxSpeed)
    {
        if (maxSpeed <= 0f)
        {
            return;
        }

        Vector3 currentVelocity = CurrentVelocity;
        float speed = currentVelocity.magnitude;
        if (speed <= maxSpeed)
        {
            return;
        }

        SetCurrentVelocity(currentVelocity / speed * maxSpeed);
    }

    public void DampVelocityOnPlane(Vector3 planeNormal, float damping, float deltaTime)
    {
        if (damping <= 0f || deltaTime <= 0f || planeNormal.sqrMagnitude < 0.0001f)
        {
            return;
        }

        planeNormal.Normalize();
        Vector3 currentVelocity = CurrentVelocity;
        Vector3 normalVelocity = Vector3.Project(currentVelocity, planeNormal);
        Vector3 tangentVelocity = currentVelocity - normalVelocity;
        float dampingFactor = 1f - Mathf.Exp(-damping * deltaTime);
        SetCurrentVelocity(normalVelocity + Vector3.Lerp(tangentVelocity, Vector3.zero, dampingFactor));
    }

    public void RemoveExternalVelocityAlong(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        direction.Normalize();
        float speedAlongDirection = Vector3.Dot(externalVelocity, direction);
        if (speedAlongDirection > 0f)
        {
            externalVelocity -= direction * speedAlongDirection;
        }
    }

    public void RemoveVelocityAlong(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        direction.Normalize();
        Vector3 currentVelocity = CurrentVelocity;
        float speedAlongDirection = Vector3.Dot(currentVelocity, direction);
        if (speedAlongDirection <= 0f)
        {
            return;
        }

        Vector3 adjustedVelocity = currentVelocity - direction * speedAlongDirection;
        horizontalVelocity = Vector3.ProjectOnPlane(adjustedVelocity, Vector3.up);
        verticalVelocity = adjustedVelocity.y;
        externalVelocity = Vector3.zero;
    }

    private void Reset()
    {
        CacheReferences();
    }

    private void OnValidate()
    {
        if (sprintSpeed < walkSpeed)
        {
            sprintSpeed = walkSpeed;
        }

        if (maxPitch < minPitch)
        {
            (minPitch, maxPitch) = (maxPitch, minPitch);
        }

        CacheReferences();
    }

    private void Awake()
    {
        CacheReferences();
        pitch = NormalizePitch(playerCamera != null ? playerCamera.transform.localEulerAngles.x : 0f);
    }

    private void Start()
    {
        if (lockCursorOnStart)
        {
            LockCursor();
        }
    }

    private void Update()
    {
        HandleCursorLock();
        HandleLook();
        HandleMovement();
    }

    private void CacheReferences()
    {
        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
        }

        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>();
        }
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit == null || hit.collider == null) return;
        var trigger = hit.collider.GetComponent<DescentSphereTrigger>() ?? hit.collider.GetComponentInParent<DescentSphereTrigger>();
        if (trigger != null)
        {
            Debug.Log($"FPSCharacterController3D: OnControllerColliderHit with {hit.collider.name}, invoking EndlessDescent.");
            if (EndlessDescentGameManager.Instance != null)
            {
                EndlessDescentGameManager.Instance.CompleteCurrentLevel(trigger);
            }
        }
    }

    private void HandleCursorLock()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }

        if (!lockCursorOnStart || Mouse.current == null || Cursor.lockState == CursorLockMode.Locked)
        {
            return;
        }

        if (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame)
        {
            LockCursor();
        }
    }

    private void HandleLook()
    {
        if (lookPaused || playerCamera == null || Mouse.current == null || Cursor.lockState != CursorLockMode.Locked)
        {
            return;
        }

        Vector2 lookDelta = Mouse.current.delta.ReadValue() * mouseSensitivity;
        pitch = Mathf.Clamp(pitch - lookDelta.y, minPitch, maxPitch);

        transform.Rotate(Vector3.up * lookDelta.x, Space.Self);
        playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void HandleMovement()
    {
        if (characterController == null)
        {
            return;
        }

        if (grappleMovementPaused)
        {
            return;
        }

        float deltaTime = Time.deltaTime;
        bool grounded = characterController.isGrounded;
        if (!grappleMovementControlActive)
        {
            Vector2 moveInput = ReadMoveInput();
            Vector3 desiredDirection = transform.right * moveInput.x + transform.forward * moveInput.y;
            desiredDirection = Vector3.ClampMagnitude(desiredDirection, 1f);

            float targetSpeed = IsSprinting() ? sprintSpeed : walkSpeed;
            Vector3 targetHorizontalVelocity = desiredDirection * targetSpeed;
            float velocityAcceleration = grounded ? acceleration : airAcceleration;
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetHorizontalVelocity, velocityAcceleration * deltaTime);
        }

        if (grounded && verticalVelocity < 0f)
        {
            verticalVelocity = groundedStickForce;
        }

        if (!grappleMovementControlActive && grounded && WasJumpPressed())
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        verticalVelocity += gravity * deltaTime;
        externalVelocity = Vector3.Lerp(externalVelocity, Vector3.zero, 1f - Mathf.Exp(-externalVelocityDamping * deltaTime));

        Vector3 frameVelocity = horizontalVelocity + Vector3.up * verticalVelocity + externalVelocity;
        CollisionFlags collisionFlags = characterController.Move(frameVelocity * deltaTime);

        if ((collisionFlags & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
        {
            verticalVelocity = 0f;
        }
    }

    private Vector2 ReadMoveInput()
    {
        if (Keyboard.current == null)
        {
            return Vector2.zero;
        }

        Vector2 input = Vector2.zero;

        if (Keyboard.current.aKey.isPressed)
        {
            input.x -= 1f;
        }

        if (Keyboard.current.dKey.isPressed)
        {
            input.x += 1f;
        }

        if (Keyboard.current.sKey.isPressed)
        {
            input.y -= 1f;
        }

        if (Keyboard.current.wKey.isPressed)
        {
            input.y += 1f;
        }

        return Vector2.ClampMagnitude(input, 1f);
    }

    private bool IsSprinting()
    {
        return Keyboard.current != null &&
               (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
    }

    private bool WasJumpPressed()
    {
        return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
    }

    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private static float NormalizePitch(float angle)
    {
        return angle > 180f ? angle - 360f : angle;
    }
}
