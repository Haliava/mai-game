using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public sealed class GrappleHookProjectile3D : MonoBehaviour
{
    public enum HookState
    {
        Inactive,
        Flying,
        Latched,
        MaxLengthReached,
        Released
    }

    [Header("References")]
    [SerializeField] private Rigidbody hookRigidbody;
    [SerializeField] private Collider hookCollider;

    [Header("Runtime")]
    [SerializeField] private HookState state = HookState.Inactive;
    [SerializeField] private Vector3 latchPoint;
    [SerializeField] private GrappleEdgeType latchedEdgeType = GrappleEdgeType.Unsupported;

    private float gravityMultiplier = 1f;
    private float maxLifetime = 8f;
    private float launchTime;
    private bool launched;
    private bool edgeLatchEnabled = true;
    private Transform ropeStart;
    private LayerMask grappleSurfaceMask = ~0;
    private float latchRadius = 0.45f;
    private float edgeNormalThreshold = 0.65f;
    private float minEdgeAngle = 20f;
    private float maxLatchSnapDistance = 0.75f;
    private bool allowBottomEdgeLatch = true;
    private bool allowTopSideLatch = true;
    private bool allowBottomSideLatch = true;
    private Collider latchedCollider;

    public Rigidbody Rigidbody => hookRigidbody;
    public Collider HookCollider => hookCollider;
    public HookState State => state;
    public Vector3 Velocity => hookRigidbody != null ? hookRigidbody.linearVelocity : Vector3.zero;
    public bool HasExpired => launched && state != HookState.Latched && maxLifetime > 0f && Time.time - launchTime >= maxLifetime;
    public bool IsLatched => state == HookState.Latched;
    public Vector3 LatchPoint => latchPoint;
    public Collider LatchedCollider => latchedCollider;
    public GrappleEdgeType LatchedEdgeType => latchedEdgeType;

    public void Launch(Vector3 direction, float speed, float mass, float linearDamping, float gravityMultiplierValue, float lifetime, float colliderRadius)
    {
        CacheReferences();

        if (hookRigidbody == null)
        {
            return;
        }

        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = transform.forward;
        }

        direction.Normalize();
        transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

        gravityMultiplier = Mathf.Max(0f, gravityMultiplierValue);
        maxLifetime = Mathf.Max(0f, lifetime);
        launchTime = Time.time;
        launched = true;
        state = HookState.Flying;

        hookRigidbody.isKinematic = false;
        hookRigidbody.useGravity = true;
        hookRigidbody.mass = Mathf.Max(0.001f, mass);
        hookRigidbody.linearDamping = Mathf.Max(0f, linearDamping);
        hookRigidbody.angularDamping = 0.05f;
        hookRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        hookRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        hookRigidbody.linearVelocity = direction * Mathf.Max(0f, speed);
        hookRigidbody.angularVelocity = Vector3.zero;

        ConfigureCollider(colliderRadius);
    }

    public void ConfigureEdgeLatch(
        Transform ropeStartTransform,
        LayerMask surfaceMask,
        float radius,
        float normalThreshold,
        float minimumEdgeAngle,
        float maximumSnapDistance,
        bool allowBottomEdge,
        bool allowTopSide,
        bool allowBottomSide)
    {
        ropeStart = ropeStartTransform;
        grappleSurfaceMask = surfaceMask;
        latchRadius = Mathf.Max(0f, radius);
        edgeNormalThreshold = Mathf.Clamp01(normalThreshold);
        minEdgeAngle = Mathf.Max(0f, minimumEdgeAngle);
        maxLatchSnapDistance = Mathf.Max(0f, maximumSnapDistance);
        allowBottomEdgeLatch = allowBottomEdge;
        allowTopSideLatch = allowTopSide;
        allowBottomSideLatch = allowBottomSide;
        edgeLatchEnabled = latchRadius > 0f && maxLatchSnapDistance > 0f;
    }

    public void ApplyMaxDistanceConstraint(Vector3 anchorPosition, float maxDistance)
    {
        if (!launched || hookRigidbody == null || maxDistance < 0f || state == HookState.Latched)
        {
            return;
        }

        Vector3 offset = hookRigidbody.position - anchorPosition;
        float distance = offset.magnitude;

        if (distance <= maxDistance)
        {
            LimitOutwardVelocity(anchorPosition, maxDistance);

            if (state == HookState.MaxLengthReached && distance < maxDistance - 0.001f)
            {
                state = HookState.Flying;
            }

            return;
        }

        if (distance < 0.0001f)
        {
            return;
        }

        Vector3 direction = offset / distance;
        Vector3 limitedPosition = anchorPosition + direction * maxDistance;
        hookRigidbody.position = limitedPosition;
        transform.position = limitedPosition;
        Physics.SyncTransforms();

        Vector3 velocity = hookRigidbody.linearVelocity;
        float outwardSpeed = Vector3.Dot(velocity, direction);
        if (outwardSpeed > 0f)
        {
            hookRigidbody.linearVelocity = velocity - direction * outwardSpeed;
        }

        state = HookState.MaxLengthReached;
    }

    private void LimitOutwardVelocity(Vector3 anchorPosition, float maxDistance)
    {
        Vector3 offset = hookRigidbody.position - anchorPosition;
        float distance = offset.magnitude;
        Vector3 direction;
        if (distance < 0.0001f)
        {
            Vector3 velocityDirection = hookRigidbody.linearVelocity;
            if (velocityDirection.sqrMagnitude < 0.0001f)
            {
                return;
            }

            direction = velocityDirection.normalized;
        }
        else
        {
            direction = offset / distance;
        }

        Vector3 velocity = hookRigidbody.linearVelocity;
        float outwardSpeed = Vector3.Dot(velocity, direction);
        if (outwardSpeed <= 0f)
        {
            return;
        }

        float remainingDistance = Mathf.Max(0f, maxDistance - distance);
        float step = Mathf.Max(Time.fixedDeltaTime, 0.0001f);
        float allowedOutwardSpeed = remainingDistance / step;
        if (outwardSpeed <= allowedOutwardSpeed)
        {
            return;
        }

        hookRigidbody.linearVelocity = velocity - direction * (outwardSpeed - allowedOutwardSpeed);
        state = HookState.MaxLengthReached;
    }

    public void MarkReleased()
    {
        launched = false;
        state = HookState.Released;
        latchedCollider = null;
        latchedEdgeType = GrappleEdgeType.Unsupported;
    }

    public bool TryLatchAgainstCollider(Collider targetCollider)
    {
        if (!CanTryLatch(targetCollider))
        {
            return false;
        }

        if (!GrappleEdgeDetector3D.TryFindBestLatchEdge(
                targetCollider,
                hookRigidbody.position,
                latchRadius,
                edgeNormalThreshold,
                minEdgeAngle,
                maxLatchSnapDistance,
                allowBottomEdgeLatch,
                allowTopSideLatch,
                allowBottomSideLatch,
                false,
                out GrappleEdgeHit edgeHit))
        {
            return false;
        }

        LatchToEdge(edgeHit);
        return true;
    }

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
        if (!launched || hookRigidbody == null || state == HookState.Released || state == HookState.Latched)
        {
            return;
        }

        if (!Mathf.Approximately(gravityMultiplier, 1f))
        {
            hookRigidbody.AddForce(Physics.gravity * (gravityMultiplier - 1f), ForceMode.Acceleration);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryLatchFromCollision(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        TryLatchFromCollision(collision);
    }

    private void CacheReferences()
    {
        if (hookRigidbody == null)
        {
            hookRigidbody = GetComponent<Rigidbody>();
        }

        if (hookCollider == null)
        {
            hookCollider = GetComponent<Collider>();
        }
    }

    private void ConfigureCollider(float radius)
    {
        if (hookCollider == null)
        {
            return;
        }

        hookCollider.isTrigger = false;

        float safeRadius = Mathf.Max(0.01f, radius);
        if (hookCollider is SphereCollider sphereCollider)
        {
            sphereCollider.radius = safeRadius;
        }
        else if (hookCollider is CapsuleCollider capsuleCollider)
        {
            capsuleCollider.radius = safeRadius;
            capsuleCollider.height = Mathf.Max(safeRadius * 2f, capsuleCollider.height);
        }
    }

    private bool CanTryLatch(Collider targetCollider)
    {
        if (!launched || !edgeLatchEnabled || hookRigidbody == null || state == HookState.Latched || state == HookState.Released || targetCollider == null)
        {
            return false;
        }

        int targetLayer = targetCollider.gameObject.layer;
        return (grappleSurfaceMask.value & (1 << targetLayer)) != 0;
    }

    private void TryLatchFromCollision(Collision collision)
    {
        if (collision == null)
        {
            return;
        }

        TryLatchAgainstCollider(collision.collider);
    }

    private void LatchToEdge(GrappleEdgeHit edgeHit)
    {
        latchPoint = edgeHit.Point;
        latchedCollider = edgeHit.Collider;
        latchedEdgeType = edgeHit.Type;
        state = HookState.Latched;

        hookRigidbody.linearVelocity = Vector3.zero;
        hookRigidbody.angularVelocity = Vector3.zero;
        hookRigidbody.useGravity = false;
        hookRigidbody.isKinematic = true;
        hookRigidbody.position = latchPoint;
        transform.position = latchPoint;
        Physics.SyncTransforms();
    }
}
