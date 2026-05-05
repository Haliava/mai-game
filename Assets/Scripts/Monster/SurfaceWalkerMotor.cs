using UnityEngine;

public class SurfaceWalkerMotor : MonoBehaviour
{
    [SerializeField] LayerMask walkableMask = ~0;
    [SerializeField] float probeRadius = 0.28f;
    [SerializeField] float probeDistance = 4f;
    [SerializeField] float surfaceOffset = 0.75f;
    [SerializeField] float stickToSurfaceSpeed = 18f;
    [SerializeField] float surfaceAlignSpeed = 12f;
    [SerializeField] float minMoveDirectionSqrMagnitude = 0.0004f;
    [SerializeField] bool useKinematicRigidbody = true;

    Rigidbody rb;
    Transform ignoredRoot;
    Vector3 surfaceNormal = Vector3.up;
    Vector3 lastMoveDirection = Vector3.forward;
    bool hasSurface;
    bool airborne;

    public Vector3 SurfaceNormal { get { return surfaceNormal; } }
    public bool HasSurface { get { return hasSurface; } }
    public bool IsAirborne { get { return airborne || !hasSurface; } }
    public float SurfaceOffset { get { return surfaceOffset; } }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null && useKinematicRigidbody) rb = gameObject.AddComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }
        surfaceNormal = transform.up.sqrMagnitude > 0.001f ? transform.up.normalized : Vector3.up;
        lastMoveDirection = transform.forward.sqrMagnitude > 0.001f ? transform.forward.normalized : Vector3.forward;
    }

    public void Configure(LayerMask mask, Transform rootToIgnore, float offset)
    {
        walkableMask = mask;
        ignoredRoot = rootToIgnore;
        surfaceOffset = offset;
    }

    public void SetIgnoredRoot(Transform rootToIgnore)
    {
        ignoredRoot = rootToIgnore;
    }

    public Vector3 StepTowards(Vector3 target, float speed, float deltaTime)
    {
        airborne = false;
        Vector3 current = transform.position;
        Vector3 rawToTarget = target - current;
        Vector3 moveDirection = Vector3.ProjectOnPlane(rawToTarget, surfaceNormal);

        if (moveDirection.sqrMagnitude < minMoveDirectionSqrMagnitude)
        {
            moveDirection = ResolveFallbackDirection(rawToTarget);
        }

        if (moveDirection.sqrMagnitude < minMoveDirectionSqrMagnitude)
        {
            moveDirection = lastMoveDirection;
        }

        moveDirection = Vector3.ProjectOnPlane(moveDirection, surfaceNormal);
        if (moveDirection.sqrMagnitude < minMoveDirectionSqrMagnitude) moveDirection = Vector3.ProjectOnPlane(transform.forward, surfaceNormal);
        if (moveDirection.sqrMagnitude < minMoveDirectionSqrMagnitude) moveDirection = Vector3.forward;
        moveDirection.Normalize();
        lastMoveDirection = moveDirection;

        Vector3 desired = current + moveDirection * Mathf.Max(0f, speed) * deltaTime;
        RaycastHit hit;
        if (TryFindSurface(desired, moveDirection, out hit))
        {
            hasSurface = true;
            surfaceNormal = Vector3.Slerp(surfaceNormal, hit.normal, surfaceAlignSpeed * deltaTime).normalized;
            desired = Vector3.Lerp(desired, hit.point + surfaceNormal * surfaceOffset, stickToSurfaceSpeed * deltaTime);
        }
        else
        {
            hasSurface = false;
            desired += moveDirection * Mathf.Max(0.1f, speed) * deltaTime;
        }

        MoveTo(desired, moveDirection, surfaceNormal, deltaTime);
        return moveDirection;
    }

    public void StepToTrailTarget(Vector3 target, Vector3 forward, Vector3 preferredNormal, bool targetAirborne, float followSpeed, float deltaTime)
    {
        Vector3 desired = target;
        Vector3 look = forward.sqrMagnitude > 0.001f ? forward.normalized : lastMoveDirection;
        Vector3 normal = preferredNormal.sqrMagnitude > 0.001f ? preferredNormal.normalized : surfaceNormal;
        airborne = targetAirborne;

        if (!targetAirborne)
        {
            RaycastHit hit;
            if (TryFindSurface(target, look, out hit))
            {
                hasSurface = true;
                normal = Vector3.Slerp(surfaceNormal, hit.normal, surfaceAlignSpeed * deltaTime).normalized;
                desired = hit.point + normal * surfaceOffset;
            }
            else
            {
                hasSurface = false;
            }
        }
        else
        {
            hasSurface = false;
        }

        surfaceNormal = normal;
        Vector3 next = Vector3.Lerp(transform.position, desired, Mathf.Clamp01(followSpeed * deltaTime));
        MoveTo(next, look, normal, deltaTime);
    }

    public bool SnapToNearestSurface(Vector3 aroundPosition, out RaycastHit hit)
    {
        if (!SurfaceProbe.TryFindSurfaceAround(aroundPosition, ignoredRoot, walkableMask, probeRadius, probeDistance * 2f, out hit))
        {
            return false;
        }

        hasSurface = true;
        airborne = false;
        surfaceNormal = hit.normal;
        Vector3 projectedForward = Vector3.ProjectOnPlane(lastMoveDirection, surfaceNormal);
        if (projectedForward.sqrMagnitude < 0.001f) projectedForward = Vector3.ProjectOnPlane(transform.forward, surfaceNormal);
        if (projectedForward.sqrMagnitude < 0.001f) projectedForward = Vector3.Cross(surfaceNormal, Vector3.up);
        if (projectedForward.sqrMagnitude < 0.001f) projectedForward = Vector3.forward;
        MoveTo(hit.point + surfaceNormal * surfaceOffset, projectedForward.normalized, surfaceNormal, Time.fixedDeltaTime);
        return true;
    }

    public void SetPose(Vector3 position, Vector3 forward, Vector3 normal, bool isAirborne)
    {
        airborne = isAirborne;
        hasSurface = !isAirborne;
        surfaceNormal = normal.sqrMagnitude > 0.001f ? normal.normalized : surfaceNormal;
        Vector3 projectedForward = Vector3.ProjectOnPlane(forward, surfaceNormal);
        if (projectedForward.sqrMagnitude < 0.001f) projectedForward = lastMoveDirection;
        if (projectedForward.sqrMagnitude < 0.001f) projectedForward = Vector3.forward;
        lastMoveDirection = projectedForward.normalized;
        MoveTo(position, lastMoveDirection, surfaceNormal, Time.fixedDeltaTime);
    }

    bool TryFindSurface(Vector3 origin, Vector3 preferredDirection, out RaycastHit hit)
    {
        return SurfaceProbe.TryFindNearestSurface(
            origin,
            preferredDirection,
            surfaceNormal,
            ignoredRoot,
            walkableMask,
            probeRadius,
            probeDistance,
            out hit);
    }

    Vector3 ResolveFallbackDirection(Vector3 rawToTarget)
    {
        Vector3 targetDirection = rawToTarget.sqrMagnitude > 0.001f ? rawToTarget.normalized : lastMoveDirection;
        Vector3 best = Vector3.zero;
        float bestScore = -999f;

        Vector3[] candidates = new Vector3[]
        {
            Vector3.ProjectOnPlane(Vector3.up, surfaceNormal),
            Vector3.ProjectOnPlane(Vector3.down, surfaceNormal),
            Vector3.ProjectOnPlane(Vector3.Cross(surfaceNormal, Vector3.up), surfaceNormal),
            Vector3.ProjectOnPlane(-Vector3.Cross(surfaceNormal, Vector3.up), surfaceNormal),
            Vector3.ProjectOnPlane(transform.forward, surfaceNormal),
            Vector3.ProjectOnPlane(lastMoveDirection, surfaceNormal)
        };

        for (int i = 0; i < candidates.Length; i++)
        {
            if (candidates[i].sqrMagnitude < minMoveDirectionSqrMagnitude) continue;
            Vector3 candidate = candidates[i].normalized;
            float score = Vector3.Dot(candidate, targetDirection);
            if (Mathf.Abs(rawToTarget.y) > 0.25f)
            {
                score += Mathf.Sign(rawToTarget.y) * candidate.y * 0.65f;
            }
            if (score <= bestScore) continue;
            bestScore = score;
            best = candidate;
        }

        return best;
    }

    void MoveTo(Vector3 position, Vector3 forward, Vector3 up, float deltaTime)
    {
        Vector3 projectedForward = Vector3.ProjectOnPlane(forward, up);
        if (projectedForward.sqrMagnitude < 0.001f) projectedForward = Vector3.ProjectOnPlane(lastMoveDirection, up);
        if (projectedForward.sqrMagnitude < 0.001f) projectedForward = Vector3.forward;
        Quaternion rotation = Quaternion.LookRotation(projectedForward.normalized, up.normalized);

        if (rb != null)
        {
            rb.MovePosition(position);
            rb.MoveRotation(Quaternion.Slerp(transform.rotation, rotation, surfaceAlignSpeed * deltaTime));
        }
        else
        {
            transform.position = position;
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, surfaceAlignSpeed * deltaTime);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = hasSurface ? Color.cyan : Color.magenta;
        Gizmos.DrawLine(transform.position, transform.position + surfaceNormal * 1.5f);
    }
}
