using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class GrapplingHookController3D : MonoBehaviour
{
    public enum GrappleState
    {
        Idle,
        Firing,
        Flying,
        Latched,
        MaxLength,
        Released
    }

    [Header("References")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform playerGrappleAnchor;
    [SerializeField] private GameObject hookPrefab;
    [SerializeField] private GrappleRope3D rope;
    [SerializeField] private Material ropeMaterial;

    [Header("Aim")]
    [SerializeField] private LayerMask aimMask = ~0;
    [SerializeField, Min(1f)] private float aimDistance = 100f;

    [Header("Latch: Top Surfaces Only")]
    [SerializeField] private LayerMask grappleSurfaceMask = ~0;

    [Tooltip("Hook can latch only if contact normal is close enough to Vector3.up. 0.65 = forgiving top surfaces.")]
    [SerializeField, Range(0f, 1f)] private float topSurfaceNormalThreshold = 0.65f;

    [Header("Hook")]
    [SerializeField, Min(0f)] private float hookLaunchSpeed = 32f;
    [SerializeField, Min(0.001f)] private float hookMass = 0.35f;
    [SerializeField, Min(0f)] private float hookDrag = 0.02f;
    [SerializeField, Min(0f)] private float hookGravityMultiplier = 0.65f;
    [SerializeField, Min(0.01f)] private float hookColliderRadius = 0.12f;
    [SerializeField, Min(0f)] private float maxHookLifetime = 8f;

    [Header("Rope")]
    [SerializeField, Min(0.1f)] private float maxRopeLength = 15f;
    [SerializeField, Min(0f)] private float ropeExtendSpeed = 36f;
    [SerializeField, Min(0.001f)] private float ropeVisualWidth = 0.035f;

    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = true;

    [Header("Runtime")]
    [SerializeField] private GrappleState state = GrappleState.Idle;

    private GameObject activeHookObject;
    private GrappleHookProjectile3D activeHook;
    private Collider[] playerColliders;
    private bool latchedRopeLengthLocked;
    [SerializeField] private GrapplePlayerConstraint3D playerConstraint;

    public GrappleState State => state;
    public GrappleHookProjectile3D ActiveHook => activeHook;
    public GrappleRope3D Rope => rope;
    public Transform PlayerGrappleAnchor => playerGrappleAnchor;
    public float MaxRopeLength => maxRopeLength;
    public float CurrentRopeLength => rope != null && rope.IsActive ? rope.CurrentRopeLength : 0f;
    public bool IsLatched => IsHookActive() && activeHook.IsLatched;

    private void Reset()
    {
        CacheReferences();
    }

    private void Awake()
    {
        CacheReferences();
    }

    private void Update()
    {
        bool fireHeld = IsFireHeld();

        if ((state == GrappleState.Idle || state == GrappleState.Released) && WasFirePressed())
        {
            StartGrapple();
            return;
        }

        if (!IsHookActive())
        {
            if (state == GrappleState.Released)
            {
                state = GrappleState.Idle;
            }

            return;
        }

        if (!fireHeld || activeHook.HasExpired)
        {
            ReleaseGrapple();
            return;
        }

        if (activeHook.IsLatched)
        {
            LockLatchedRopeLengthIfNeeded();
            state = GrappleState.Latched;
            return;
        }

        state = activeHook.State == GrappleHookProjectile3D.HookState.MaxLengthReached
            && rope != null
            && rope.IsFullyExtended
                ? GrappleState.MaxLength
                : GrappleState.Flying;
    }

    private void FixedUpdate()
    {
        if (!IsHookActive() || playerGrappleAnchor == null)
        {
            return;
        }

        float allowedLength = maxRopeLength;

        if (rope != null && rope.IsActive)
        {
            allowedLength = activeHook.IsLatched
                ? rope.CurrentRopeLength
                : rope.AdvanceLength(Time.fixedDeltaTime);

            if (activeHook.IsLatched && !latchedRopeLengthLocked)
            {
                LockLatchedRopeLengthIfNeeded();
                allowedLength = rope.CurrentRopeLength;
            }
        }

        if (activeHook.IsLatched)
        {
            return;
        }

        activeHook.ApplyMaxDistanceConstraint(playerGrappleAnchor.position, allowedLength);
    }

    public void StartGrapple()
    {
        if (playerGrappleAnchor == null || playerCamera == null)
        {
            CacheReferences();
        }

        if (playerGrappleAnchor == null || playerCamera == null)
        {
            Debug.LogWarning("GrapplingHookController3D needs a player camera and grapple anchor before firing.", this);
            return;
        }

        ReleaseGrappleImmediate();

        Vector3 fireDirection = GetFireDirection();

        activeHookObject = CreateHookObject(fireDirection);
        activeHook = activeHookObject.GetComponent<GrappleHookProjectile3D>();

        activeHook.Launch(
            fireDirection,
            hookLaunchSpeed,
            hookMass,
            hookDrag,
            hookGravityMultiplier,
            maxHookLifetime,
            hookColliderRadius);

        activeHook.ConfigureTopSurfaceLatch(
            grappleSurfaceMask,
            topSurfaceNormalThreshold);

        IgnorePlayerCollision(activeHook.HookCollider);

        if (rope != null)
        {
            float initialRopeLength = Mathf.Max(
                hookColliderRadius * 2f,
                Vector3.Distance(playerGrappleAnchor.position, activeHookObject.transform.position));

            rope.Begin(
                playerGrappleAnchor,
                activeHookObject.transform,
                maxRopeLength,
                ropeExtendSpeed,
                initialRopeLength,
                ropeVisualWidth,
                ropeMaterial);
        }

        state = GrappleState.Firing;
    }

    public void ReleaseGrapple()
    {
        ReleaseGrappleImmediate();
        state = GrappleState.Released;
    }

    public void ReleaseGrappleImmediate()
    {
        if (activeHook != null)
        {
            activeHook.MarkReleased();
        }

        
        if (playerConstraint != null)
        {
            playerConstraint.HandleGrappleReleased();
        }

        if (activeHookObject != null)
        {
            Destroy(activeHookObject);
        }

        activeHookObject = null;
        activeHook = null;
        latchedRopeLengthLocked = false;

        if (rope != null)
        {
            rope.Clear();
        }
    }

    private void CacheReferences()
    {
        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>();
        }

        if (playerGrappleAnchor == null)
        {
            Transform foundAnchor = transform.Find("Grapple Fire Point");
            if (foundAnchor != null)
            {
                playerGrappleAnchor = foundAnchor;
            }
        }

        if (rope == null)
        {
            rope = GetComponentInChildren<GrappleRope3D>();
        }

        playerColliders = GetComponentsInChildren<Collider>();
        if (playerConstraint == null)
        {
            playerConstraint = GetComponent<GrapplePlayerConstraint3D>();
        }
    }

    private Vector3 GetFireDirection()
    {
        Ray aimRay = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        Vector3 aimPoint = aimRay.origin + aimRay.direction * aimDistance;

        if (Physics.Raycast(aimRay, out RaycastHit hit, aimDistance, aimMask, QueryTriggerInteraction.Ignore))
        {
            aimPoint = hit.point;
        }

        Vector3 direction = aimPoint - playerGrappleAnchor.position;

        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = playerCamera.transform.forward;
        }

        return direction.normalized;
    }

    private GameObject CreateHookObject(Vector3 fireDirection)
    {
        Quaternion rotation = Quaternion.LookRotation(fireDirection, Vector3.up);
        GameObject hookObject;

        if (hookPrefab != null)
        {
            hookObject = Instantiate(hookPrefab, playerGrappleAnchor.position, rotation);
        }
        else
        {
            hookObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            hookObject.name = "Runtime Grapple Hook";
            hookObject.transform.SetPositionAndRotation(playerGrappleAnchor.position, rotation);
            hookObject.transform.localScale = Vector3.one * hookColliderRadius * 2f;
        }

        GrappleHookProjectile3D projectile = hookObject.GetComponent<GrappleHookProjectile3D>();
        if (projectile == null)
        {
            projectile = hookObject.AddComponent<GrappleHookProjectile3D>();
        }

        Rigidbody hookRigidbody = hookObject.GetComponent<Rigidbody>();
        if (hookRigidbody == null)
        {
            hookRigidbody = hookObject.AddComponent<Rigidbody>();
        }

        Collider hookCollider = hookObject.GetComponent<Collider>();
        if (hookCollider == null)
        {
            hookObject.AddComponent<SphereCollider>();
        }

        return hookObject;
    }

    private bool IsHookActive()
    {
        return activeHookObject != null && activeHook != null;
    }

    private void LockLatchedRopeLengthIfNeeded()
    {
        if (latchedRopeLengthLocked || activeHook == null || !activeHook.IsLatched || rope == null || !rope.IsActive)
        {
            return;
        }

        rope.LockLengthToCurrentDirectDistance();
        latchedRopeLengthLocked = true;
    }

    private bool IsFireHeld()
    {
        return Mouse.current != null && Mouse.current.leftButton.isPressed;
    }

    private bool WasFirePressed()
    {
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
    }

    private void IgnorePlayerCollision(Collider hookCollider)
    {
        if (hookCollider == null)
        {
            return;
        }

        if (playerColliders == null || playerColliders.Length == 0)
        {
            playerColliders = GetComponentsInChildren<Collider>();
        }

        for (int i = 0; i < playerColliders.Length; i++)
        {
            Collider playerCollider = playerColliders[i];

            if (playerCollider != null && playerCollider != hookCollider)
            {
                Physics.IgnoreCollision(hookCollider, playerCollider, true);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos)
        {
            return;
        }

        CacheReferences();

        if (playerGrappleAnchor != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(playerGrappleAnchor.position, 0.08f);
            Gizmos.DrawWireSphere(playerGrappleAnchor.position, maxRopeLength);

            if (rope != null && rope.IsActive)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(playerGrappleAnchor.position, rope.CurrentRopeLength);
            }
        }

        if (activeHookObject != null && playerGrappleAnchor != null)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawLine(playerGrappleAnchor.position, activeHookObject.transform.position);

            Gizmos.color = state == GrappleState.Latched
                ? Color.red
                : state == GrappleState.MaxLength ? Color.yellow : Color.green;

            Gizmos.DrawWireSphere(activeHookObject.transform.position, hookColliderRadius);

            if (activeHook != null && activeHook.IsLatched)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(activeHook.LatchPoint, hookColliderRadius * 2f);
            }
        }
    }
}
