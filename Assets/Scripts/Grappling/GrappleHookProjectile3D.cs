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
    [SerializeField] private Vector3 latchNormal = Vector3.up;

    private float gravityMultiplier = 1f;
    private float maxLifetime = 8f;
    private float launchTime;
    private bool launched;

    private LayerMask grappleSurfaceMask = ~0;
    private float topSurfaceNormalThreshold = 0.65f;
    private Collider latchedCollider;

    public Rigidbody Rigidbody => hookRigidbody;
    public Collider HookCollider => hookCollider;
    public HookState State => state;
    public Vector3 Velocity => hookRigidbody != null ? hookRigidbody.linearVelocity : Vector3.zero;
    public bool HasExpired => launched && state != HookState.Latched && maxLifetime > 0f && Time.time - launchTime >= maxLifetime;
    public bool IsLatched => state == HookState.Latched;
    public Vector3 LatchPoint => latchPoint;
    public Vector3 LatchNormal => latchNormal;
    public Collider LatchedCollider => latchedCollider;

    
    public GrappleEdgeType LatchedEdgeType => GrappleEdgeType.Unsupported;

    public void Launch(
        Vector3 direction,
        float speed,
        float mass,
        float linearDamping,
        float gravityMultiplierValue,
        float lifetime,
        float colliderRadius)
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

    public void ConfigureTopSurfaceLatch(LayerMask surfaceMask, float normalThreshold)
    {
        grappleSurfaceMask = surfaceMask;
        topSurfaceNormalThreshold = Mathf.Clamp01(normalThreshold);
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
        ConfigureTopSurfaceLatch(surfaceMask, normalThreshold);
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

    public void MarkReleased()
    {
        launched = false;
        state = HookState.Released;
        latchedCollider = null;
    }

    
    public bool TryLatchAgainstCollider(Collider targetCollider)
    {
        return false;
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

    private bool IsInGrappleSurfaceMask(Collider targetCollider)
    {
        if (targetCollider == null)
        {
            return false;
        }

        int targetLayer = targetCollider.gameObject.layer;
        return (grappleSurfaceMask.value & (1 << targetLayer)) != 0;
    }

    private bool IsTopSurface(Vector3 normal)
    {
        if (normal.sqrMagnitude < 0.0001f)
        {
            return false;
        }

        return Vector3.Dot(normal.normalized, Vector3.up) >= topSurfaceNormalThreshold;
    }

    private void TryLatchFromCollision(Collision collision)
    {
        if (collision == null || !launched || state == HookState.Latched || state == HookState.Released)
        {
            return;
        }

        if (!IsInGrappleSurfaceMask(collision.collider))
        {
            return;
        }

        ContactPoint bestContact = default;
        float bestDot = -1f;
        bool found = false;

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint contact = collision.GetContact(i);
            float dot = Vector3.Dot(contact.normal.normalized, Vector3.up);

            if (dot >= topSurfaceNormalThreshold && dot > bestDot)
            {
                bestDot = dot;
                bestContact = contact;
                found = true;
            }
        }

        if (!found)
        {
            return;
        }

        LatchToTopSurface(bestContact.point, bestContact.normal, collision.collider);
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

    private void LatchToTopSurface(Vector3 point, Vector3 normal, Collider targetCollider)
    {
        latchPoint = point;
        latchNormal = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;
        latchedCollider = targetCollider;

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
