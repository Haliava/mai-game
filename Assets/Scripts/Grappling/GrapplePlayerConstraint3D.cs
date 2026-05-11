using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class GrapplePlayerConstraint3D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GrapplingHookController3D grappleController;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Rigidbody playerRigidbody;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private FPSCharacterController3D fpsController;

    [Header("Rope Movement")]
    [SerializeField, Min(0f)] private float minRopeLength = 1.5f;
    [SerializeField, Min(0f)] private float reelInSpeed = 12f;
    [SerializeField, Min(0f)] private float reelOutSpeed = 10f;
    [SerializeField, Min(0f)] private float pullStrength = 18f;
    [SerializeField, Min(0f)] private float swingForce = 8f;
    [SerializeField, Min(0f)] private float damping = 1.5f;
    [SerializeField, Min(0f)] private float ropeStretchTolerance = 0.05f;

    [Header("Force Mantle")]
    [SerializeField] private bool forceMantleOnReelIn = true;
    [SerializeField, Min(0f)] private float forceMantleTriggerDistance = 2.0f;
    [SerializeField, Min(0f)] private float forceMantleRopeLengthThreshold = 2.5f;
    [SerializeField, Min(0.01f)] private float forceMantleDuration = 0.25f;
    [SerializeField, Min(0f)] private float forceMantleUpOffset = 1.4f;
    [SerializeField, Min(0f)] private float forceMantleForwardOffset = 0.8f;
    [SerializeField] private bool autoReleaseAfterForceMantle = true;

    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = true;

    [Header("Grapple Tuning")]
    [SerializeField, Min(0f)] private float maxConstraintCorrectionSpeed = 18f;
    [SerializeField, Min(0f)] private float constraintCorrectionStrength = 12f;
    [SerializeField, Min(0f)] private float maxGrappleSpeed = 22f;
    [SerializeField, Min(0f)] private float maxReleaseSpeed = 16f;
    [SerializeField, Min(0f)] private float maxDownwardReleaseSpeed = 8f;
    [SerializeField] private float grappleGravity = -18f;

    private bool isMantling;
    private Vector3 characterExternalVelocity;

    // Grapple movement lifecycle
    private bool grappleControlActive;
    private Vector3 grappleVelocity;
    private bool wasLatchedLastFrame;

    public bool IsMantling => isMantling;

    private void Reset()
    {
        CacheReferences();
    }

    private void Awake()
    {
        CacheReferences();
    }

    private void FixedUpdate()
    {
        if (isMantling)
        {
            return;
        }

        if (grappleController == null || grappleController.ActiveHook == null || grappleController.Rope == null)
        {
            if (grappleControlActive)
            {
                EndGrappleMovementControl();
            }
            return;
        }

        GrappleHookProjectile3D hook = grappleController.ActiveHook;
        GrappleRope3D rope = grappleController.Rope;
        Transform anchor = grappleController.PlayerGrappleAnchor;

        if (hook == null || rope == null || anchor == null || !rope.IsActive)
        {
            if (grappleControlActive)
            {
                EndGrappleMovementControl();
            }
            return;
        }

        bool isLatched = hook.IsLatched;

        // lifecycle transition detection
        if (isLatched && !wasLatchedLastFrame)
        {
            HandleGrappleLatched();
        }

        if (!isLatched && wasLatchedLastFrame)
        {
            HandleGrappleReleased();
        }

        wasLatchedLastFrame = isLatched;

        if (!isLatched)
        {
            return;
        }

        if (!grappleControlActive)
        {
            BeginGrappleMovementControl();
        }

        // Process inputs and physics in a single place and call Move once at the end
        HandleReelInput();

        float dt = Time.fixedDeltaTime;
        Vector3 latchPoint = hook.LatchPoint;
        Vector3 anchorPosition = anchor.position;
        Vector3 fromLatch = anchorPosition - latchPoint;
        float distance = fromLatch.magnitude;
        if (distance < 0.0001f) distance = 0.0001f;
        Vector3 ropeDirectionFromLatch = fromLatch / distance;

        // gravity
        grappleVelocity += Vector3.up * grappleGravity * dt;

        // swing input -> add tangential impulse
        Vector3 moveInput = GetMoveInputWorld();
        if (moveInput.sqrMagnitude > 0.0001f)
        {
            Vector3 tangent = Vector3.ProjectOnPlane(moveInput, ropeDirectionFromLatch);
            if (tangent.sqrMagnitude > 0.0001f)
            {
                grappleVelocity += tangent.normalized * swingForce * dt;
            }
        }

        // reel-in pull contributes to grapple velocity
        if (IsReelInHeld())
        {
            Vector3 pullDirection = (latchPoint - anchorPosition).normalized;
            grappleVelocity += pullDirection * pullStrength * dt;
        }

        // damping
        if (damping > 0f)
        {
            grappleVelocity = Vector3.Lerp(grappleVelocity, Vector3.zero, damping * dt);
        }

        // soft rope constraint correction (no snap)
        float allowedLength = Mathf.Max(minRopeLength, rope.CurrentRopeLength);
        Vector3 correction = Vector3.zero;
        if (distance > allowedLength + ropeStretchTolerance)
        {
            float excess = distance - allowedLength;
            Vector3 desiredCorrection = -ropeDirectionFromLatch * excess;
            float maxCorrection = maxConstraintCorrectionSpeed * dt;
            correction = Vector3.ClampMagnitude(desiredCorrection, maxCorrection);

            // remove outward component from grappleVelocity
            float outwardComponent = Vector3.Dot(grappleVelocity, ropeDirectionFromLatch);
            if (outwardComponent > 0f)
            {
                grappleVelocity -= ropeDirectionFromLatch * outwardComponent;
            }
        }

        // clamp grapple velocity
        if (grappleVelocity.magnitude > maxGrappleSpeed)
        {
            grappleVelocity = grappleVelocity.normalized * maxGrappleSpeed;
        }

        Vector3 delta = grappleVelocity * dt + correction;

        // single Move call per FixedUpdate
        ApplyFinalMove(delta);

        TryStartForceMantle();
    }

    private void CacheReferences()
    {
        if (grappleController == null)
        {
            grappleController = GetComponent<GrapplingHookController3D>();
        }

        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
        }

        if (playerRigidbody == null)
        {
            playerRigidbody = GetComponent<Rigidbody>();
        }

        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>();
        }
        if (fpsController == null)
        {
            fpsController = GetComponent<FPSCharacterController3D>() ?? UnityEngine.Object.FindAnyObjectByType<FPSCharacterController3D>();
        }
    }

    private void HandleReelInput()
    {
        GrappleRope3D rope = grappleController.Rope;
        if (rope == null || !rope.IsActive)
        {
            return;
        }

        float delta = 0f;

        if (IsReelInHeld())
        {
            delta -= reelInSpeed * Time.fixedDeltaTime;
        }

        if (IsReelOutHeld())
        {
            delta += reelOutSpeed * Time.fixedDeltaTime;
        }

        if (Mathf.Abs(delta) > 0.0001f)
        {
            rope.AdjustCurrentLength(delta, minRopeLength);
        }
    }

    private void ApplySimpleRopeConstraint()
    {
        GrappleHookProjectile3D hook = grappleController.ActiveHook;
        GrappleRope3D rope = grappleController.Rope;
        Transform anchor = grappleController.PlayerGrappleAnchor;

        if (hook == null || rope == null || anchor == null)
        {
            return;
        }

        Vector3 latchPoint = hook.LatchPoint;
        Vector3 anchorPosition = anchor.position;
        Vector3 fromLatch = anchorPosition - latchPoint;

        float distance = fromLatch.magnitude;
        float allowedLength = Mathf.Max(minRopeLength, rope.CurrentRopeLength);

        if (distance < 0.0001f)
        {
            return;
        }

        Vector3 ropeDirectionFromLatch = fromLatch / distance;

        if (distance > allowedLength + ropeStretchTolerance)
        {
            float excess = distance - allowedLength;
            Vector3 correction = -ropeDirectionFromLatch * excess;

            MovePlayer(correction);

            RemoveOutwardVelocity(ropeDirectionFromLatch);
        }

        if (IsReelInHeld())
        {
            Vector3 pullDirection = (latchPoint - anchorPosition).normalized;
            ApplyPlayerVelocity(pullDirection * pullStrength * Time.fixedDeltaTime);
        }

        ApplySwingInput(latchPoint, anchorPosition, ropeDirectionFromLatch);
        ApplyDamping();
    }

    private void ApplySwingInput(Vector3 latchPoint, Vector3 anchorPosition, Vector3 ropeDirectionFromLatch)
    {
        Vector3 input = GetMoveInputWorld();

        if (input.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Vector3 tangent = Vector3.ProjectOnPlane(input, ropeDirectionFromLatch);

        if (tangent.sqrMagnitude < 0.0001f)
        {
            return;
        }

        ApplyPlayerVelocity(tangent.normalized * swingForce * Time.fixedDeltaTime);
    }

    private Vector3 GetMoveInputWorld()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
        {
            return Vector3.zero;
        }

        Vector2 input = Vector2.zero;

        if (keyboard.wKey.isPressed) input.y += 1f;
        if (keyboard.sKey.isPressed) input.y -= 1f;
        if (keyboard.dKey.isPressed) input.x += 1f;
        if (keyboard.aKey.isPressed) input.x -= 1f;

        if (input.sqrMagnitude > 1f)
        {
            input.Normalize();
        }

        Transform basis = playerCamera != null ? playerCamera.transform : transform;

        Vector3 forward = Vector3.ProjectOnPlane(basis.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(basis.right, Vector3.up).normalized;

        return forward * input.y + right * input.x;
    }

    private void TryStartForceMantle()
    {
        if (!forceMantleOnReelIn || !IsReelInHeld())
        {
            return;
        }

        GrappleHookProjectile3D hook = grappleController.ActiveHook;
        GrappleRope3D rope = grappleController.Rope;
        Transform anchor = grappleController.PlayerGrappleAnchor;

        if (hook == null || rope == null || anchor == null || !hook.IsLatched)
        {
            return;
        }

        float directDistance = Vector3.Distance(anchor.position, hook.LatchPoint);
        bool shortEnough = rope.CurrentRopeLength <= forceMantleRopeLengthThreshold;
        bool closeEnough = directDistance <= forceMantleTriggerDistance;

        if (!shortEnough && !closeEnough)
        {
            return;
        }

        BeginForceMantle(hook.LatchPoint);
    }

    private void BeginForceMantle(Vector3 latchPoint)
    {
        if (isMantling)
        {
            return;
        }

        Vector3 playerPosition = transform.position;

        Vector3 horizontalToLatch = latchPoint - playerPosition;
        horizontalToLatch.y = 0f;

        Vector3 forward;

        if (horizontalToLatch.sqrMagnitude > 0.0001f)
        {
            forward = horizontalToLatch.normalized;
        }
        else if (playerCamera != null)
        {
            forward = Vector3.ProjectOnPlane(playerCamera.transform.forward, Vector3.up).normalized;
        }
        else
        {
            forward = transform.forward;
            forward.y = 0f;
            forward.Normalize();
        }

        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.forward;
        }

        Vector3 target = latchPoint
            + forward * forceMantleForwardOffset
            + Vector3.up * forceMantleUpOffset;

        StartCoroutine(ForceMantleRoutine(target));
    }

    private IEnumerator ForceMantleRoutine(Vector3 target)
    {
        isMantling = true;

        Vector3 start = transform.position;
        float elapsed = 0f;

        if (characterController != null)
        {
            characterController.enabled = false;
        }

        if (playerRigidbody != null)
        {
            playerRigidbody.linearVelocity = Vector3.zero;
            playerRigidbody.angularVelocity = Vector3.zero;
            playerRigidbody.isKinematic = true;
        }

        while (elapsed < forceMantleDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / forceMantleDuration);
            t = t * t * (3f - 2f * t);

            transform.position = Vector3.Lerp(start, target, t);
            yield return null;
        }

        transform.position = target;

        if (playerRigidbody != null)
        {
            playerRigidbody.isKinematic = false;
            playerRigidbody.linearVelocity = Vector3.zero;
        }

        if (characterController != null)
        {
            characterController.enabled = true;
        }

        characterExternalVelocity = Vector3.zero;

        isMantling = false;

        if (autoReleaseAfterForceMantle && grappleController != null)
        {
            grappleController.ReleaseGrapple();
        }
    }

    private void MovePlayer(Vector3 worldDelta)
    {
        if (worldDelta.sqrMagnitude < 0.0000001f)
        {
            return;
        }

        if (characterController != null && characterController.enabled)
        {
            characterController.Move(worldDelta);
            return;
        }

        if (playerRigidbody != null && !playerRigidbody.isKinematic)
        {
            playerRigidbody.MovePosition(playerRigidbody.position + worldDelta);
            return;
        }

        transform.position += worldDelta;
    }

    private void ApplyPlayerVelocity(Vector3 velocityDelta)
    {
        if (velocityDelta.sqrMagnitude < 0.0000001f)
        {
            return;
        }

        if (grappleControlActive)
        {
            grappleVelocity += velocityDelta;
            return;
        }

        if (playerRigidbody != null && !playerRigidbody.isKinematic)
        {
            playerRigidbody.linearVelocity += velocityDelta;
            return;
        }

        if (characterController != null && characterController.enabled)
        {
            characterExternalVelocity += velocityDelta;
            characterController.Move(characterExternalVelocity * Time.fixedDeltaTime);
            return;
        }

        transform.position += velocityDelta * Time.fixedDeltaTime;
    }

    private void RemoveOutwardVelocity(Vector3 ropeDirectionFromLatch)
    {
        if (grappleControlActive)
        {
            float outward = Vector3.Dot(grappleVelocity, ropeDirectionFromLatch);
            if (outward > 0f)
            {
                grappleVelocity -= ropeDirectionFromLatch * outward;
            }

            return;
        }
        if (playerRigidbody != null && !playerRigidbody.isKinematic)
        {
            Vector3 velocity = playerRigidbody.linearVelocity;
            float outwardSpeed = Vector3.Dot(velocity, ropeDirectionFromLatch);

            if (outwardSpeed > 0f)
            {
                playerRigidbody.linearVelocity = velocity - ropeDirectionFromLatch * outwardSpeed;
            }

            return;
        }

        float externalOutwardSpeed = Vector3.Dot(characterExternalVelocity, ropeDirectionFromLatch);

        if (externalOutwardSpeed > 0f)
        {
            characterExternalVelocity -= ropeDirectionFromLatch * externalOutwardSpeed;
        }
    }

    private void ApplyDamping()
    {
        if (damping <= 0f)
        {
            return;
        }

        if (playerRigidbody != null && !playerRigidbody.isKinematic)
        {
            playerRigidbody.linearVelocity = Vector3.Lerp(
                playerRigidbody.linearVelocity,
                Vector3.zero,
                damping * Time.fixedDeltaTime * 0.05f);

            return;
        }

        if (grappleControlActive)
        {
            grappleVelocity = Vector3.Lerp(grappleVelocity, Vector3.zero, damping * Time.fixedDeltaTime);
            return;
        }

        characterExternalVelocity = Vector3.Lerp(
            characterExternalVelocity,
            Vector3.zero,
            damping * Time.fixedDeltaTime);
    }

    private void BeginGrappleMovementControl()
    {
        grappleControlActive = true;
        grappleVelocity = fpsController != null ? fpsController.CurrentVelocity : Vector3.zero;
        characterExternalVelocity = Vector3.zero;

        if (fpsController != null)
        {
            fpsController.SetGrappleMovementPaused(true);
            fpsController.ClearExternalVelocity();
            fpsController.SetCurrentVelocity(Vector3.zero);
            fpsController.SetGrappleMovementControlActive(true);
            Debug.Log("[GrappleMovement] Begin control");
        }
    }

    private void EndGrappleMovementControl()
    {
        Vector3 rawVelocity = grappleVelocity;
        Vector3 releaseVelocity = GetSafeReleaseVelocity();

        if (fpsController != null)
        {
            fpsController.SetGrappleMovementPaused(false);
            fpsController.SetGrappleMovementControlActive(false);
            if (rawVelocity != releaseVelocity)
            {
                Debug.Log($"[GrappleMovement] Clamped release velocity from {rawVelocity} to {releaseVelocity}");
            }

            fpsController.SetCurrentVelocity(releaseVelocity);
            fpsController.ClearExternalVelocity();
            Debug.Log($"[GrappleMovement] Release. rawVelocity={rawVelocity}, safeVelocity={releaseVelocity}");
        }

        grappleControlActive = false;
        grappleVelocity = Vector3.zero;
        characterExternalVelocity = Vector3.zero;
    }

    public void HandleGrappleLatched()
    {
        if (!grappleControlActive)
        {
            BeginGrappleMovementControl();
        }
    }

    public void HandleGrappleReleased()
    {
        if (grappleControlActive)
        {
            EndGrappleMovementControl();
        }
    }

    private Vector3 GetSafeReleaseVelocity()
    {
        Vector3 v = grappleVelocity;
        if (v.magnitude > maxReleaseSpeed)
        {
            v = v.normalized * maxReleaseSpeed;
        }

        if (v.y < -maxDownwardReleaseSpeed)
        {
            v.y = -maxDownwardReleaseSpeed;
        }

        return v;
    }

    private void ApplyFinalMove(Vector3 delta)
    {
        if (delta.sqrMagnitude <= 0f)
        {
            return;
        }

        if (characterController != null && characterController.enabled)
        {
            characterController.Move(delta);
            Physics.SyncTransforms();
            return;
        }

        if (playerRigidbody != null && !playerRigidbody.isKinematic)
        {
            playerRigidbody.MovePosition(playerRigidbody.position + delta);
            Physics.SyncTransforms();
            return;
        }

        transform.position += delta;
        Physics.SyncTransforms();
    }

    private bool IsReelInHeld()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
    }

    private bool IsReelOutHeld()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed);
    }

    private void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos || grappleController == null || grappleController.ActiveHook == null)
        {
            return;
        }

        if (!grappleController.ActiveHook.IsLatched)
        {
            return;
        }

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(grappleController.ActiveHook.LatchPoint, 0.2f);

        if (grappleController.PlayerGrappleAnchor != null)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawLine(grappleController.PlayerGrappleAnchor.position, grappleController.ActiveHook.LatchPoint);
        }
    }
}
