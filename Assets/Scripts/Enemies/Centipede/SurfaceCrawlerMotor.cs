using UnityEngine;

public sealed class SurfaceCrawlerMotor : MonoBehaviour
{
    [Header("Surface")]
    [SerializeField] private LayerMask crawlableSurfaceMask = ~0;
    [SerializeField] private Transform ignoredRoot;
    [SerializeField, Min(0.05f)] private float bodyRadius = 0.45f;
    [SerializeField, Min(0.05f)] private float surfaceOffset = 0.48f;
    [SerializeField, Min(0.1f)] private float surfaceProbeDistance = 3.5f;
    [SerializeField, Min(0.01f)] private float surfaceProbeRadius = 0.25f;
    [SerializeField, Min(1f)] private float reattachSearchRadius = 5f;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float moveSpeed = 1.7f;
    [SerializeField, Min(0f)] private float rotationSmoothing = 8f;
    [SerializeField, Min(0f)] private float surfaceNormalSmoothing = 10f;

    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = true;

    private Vector3 surfaceNormal = Vector3.up;
    private Vector3 moveForward = Vector3.forward;
    private Vector3 lastSurfacePoint;
    private bool hasSurface;

    public Vector3 SurfaceNormal => surfaceNormal;
    public Vector3 SurfacePoint => lastSurfacePoint;
    public bool HasSurface => hasSurface;
    public float MoveSpeed
    {
        get => moveSpeed;
        set => moveSpeed = Mathf.Max(0f, value);
    }

    public LayerMask CrawlableSurfaceMask
    {
        get => crawlableSurfaceMask;
        set => crawlableSurfaceMask = value;
    }

    public Transform IgnoredRoot
    {
        get => ignoredRoot;
        set => ignoredRoot = value;
    }

    public bool TryAttachToSurface(Vector3 surfacePoint, Vector3 normal, Vector3 preferredForward)
    {
        if (normal.sqrMagnitude < 0.0001f)
        {
            return false;
        }

        surfaceNormal = normal.normalized;
        moveForward = Vector3.ProjectOnPlane(preferredForward, surfaceNormal);
        if (moveForward.sqrMagnitude < 0.0001f)
        {
            moveForward = Vector3.ProjectOnPlane(transform.forward, surfaceNormal);
        }
        if (moveForward.sqrMagnitude < 0.0001f)
        {
            moveForward = Vector3.Cross(surfaceNormal, Vector3.up);
        }
        if (moveForward.sqrMagnitude < 0.0001f)
        {
            moveForward = Vector3.forward;
        }

        moveForward.Normalize();
        lastSurfacePoint = surfacePoint;
        hasSurface = true;
        transform.position = surfacePoint + surfaceNormal * Mathf.Max(surfaceOffset, bodyRadius);
        ApplyOrientation(1f);
        return true;
    }

    public void TickToward(Vector3 targetPosition, float deltaTime)
    {
        if (deltaTime <= 0f)
        {
            return;
        }

        if (!TryRefreshSurface(deltaTime))
        {
            TryReattachNear(transform.position);
        }

        Vector3 desired = targetPosition - transform.position;
        Vector3 tangentDesired = Vector3.ProjectOnPlane(desired, surfaceNormal);
        if (tangentDesired.sqrMagnitude > 0.0001f)
        {
            moveForward = Vector3.Slerp(moveForward, tangentDesired.normalized, 1f - Mathf.Exp(-rotationSmoothing * deltaTime));
        }
        else
        {
            moveForward = Vector3.ProjectOnPlane(moveForward, surfaceNormal).normalized;
        }

        Vector3 nextPosition = transform.position + moveForward * (moveSpeed * deltaTime);
        if (TryFindSurfaceNear(nextPosition, surfaceNormal, out RaycastHit hit) ||
            TryFindSurfaceNear(nextPosition, -surfaceNormal, out hit) ||
            TryFindSurfaceNear(nextPosition, -transform.up, out hit))
        {
            surfaceNormal = Vector3.Slerp(surfaceNormal, hit.normal.normalized, 1f - Mathf.Exp(-surfaceNormalSmoothing * deltaTime));
            lastSurfacePoint = hit.point;
            hasSurface = true;
            transform.position = hit.point + surfaceNormal * Mathf.Max(surfaceOffset, bodyRadius);
        }
        else
        {
            transform.position = nextPosition;
        }

        ApplyOrientation(deltaTime);
    }

    private bool TryRefreshSurface(float deltaTime)
    {
        Vector3 origin = transform.position + surfaceNormal * surfaceProbeRadius;
        if (!TrySphereCastFiltered(origin, surfaceProbeRadius, -surfaceNormal, surfaceProbeDistance, out RaycastHit hit))
        {
            hasSurface = false;
            return false;
        }

        surfaceNormal = Vector3.Slerp(surfaceNormal, hit.normal.normalized, 1f - Mathf.Exp(-surfaceNormalSmoothing * deltaTime));
        lastSurfacePoint = hit.point;
        hasSurface = true;
        transform.position = hit.point + surfaceNormal * Mathf.Max(surfaceOffset, bodyRadius);
        return true;
    }

    private bool TryReattachNear(Vector3 origin)
    {
        Collider[] colliders = Physics.OverlapSphere(origin, reattachSearchRadius, crawlableSurfaceMask, QueryTriggerInteraction.Ignore);
        float bestDistance = float.PositiveInfinity;
        Vector3 bestPoint = Vector3.zero;
        Vector3 bestNormal = Vector3.up;
        bool found = false;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || IsIgnored(collider))
            {
                continue;
            }

            Vector3 closest;
            Vector3 normal;
            if (!TryGetSurfacePoint(collider, origin, out closest, out normal))
            {
                continue;
            }

            float distance = Vector3.Distance(origin, closest);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestPoint = closest;
                bestNormal = normal;
                found = true;
            }
        }

        return found && TryAttachToSurface(bestPoint, bestNormal, moveForward);
    }

    private bool TryFindSurfaceNear(Vector3 origin, Vector3 normalHint, out RaycastHit hit)
    {
        Vector3 direction = normalHint.sqrMagnitude > 0.0001f ? -normalHint.normalized : -surfaceNormal;
        Vector3 castOrigin = origin - direction * surfaceProbeDistance * 0.45f;
        return TrySphereCastFiltered(castOrigin, surfaceProbeRadius, direction, surfaceProbeDistance * 1.5f, out hit);
    }

    private bool TrySphereCastFiltered(Vector3 origin, float radius, Vector3 direction, float distance, out RaycastHit hit)
    {
        RaycastHit[] hits = Physics.SphereCastAll(origin, radius, direction, distance, crawlableSurfaceMask, QueryTriggerInteraction.Ignore);
        int bestIndex = -1;
        float bestDistance = float.PositiveInfinity;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider collider = hits[i].collider;
            if (collider == null || IsIgnored(collider))
            {
                continue;
            }

            if (hits[i].distance < bestDistance)
            {
                bestDistance = hits[i].distance;
                bestIndex = i;
            }
        }

        if (bestIndex >= 0)
        {
            hit = hits[bestIndex];
            return true;
        }

        hit = default;
        return false;
    }

    private bool IsIgnored(Collider collider)
    {
        return ignoredRoot != null && collider.transform.IsChildOf(ignoredRoot);
    }

    private static bool TryGetSurfacePoint(Collider collider, Vector3 position, out Vector3 point, out Vector3 normal)
    {
        point = Vector3.zero;
        normal = Vector3.up;
        if (collider == null)
        {
            return false;
        }

        MeshCollider meshCollider = collider as MeshCollider;
        if (meshCollider != null && !meshCollider.convex)
        {
            Vector3 toCenter = collider.bounds.center - position;
            if (toCenter.sqrMagnitude > 0.0001f && collider.Raycast(new Ray(position, toCenter.normalized), out RaycastHit hit, toCenter.magnitude + collider.bounds.extents.magnitude + 1f))
            {
                point = hit.point;
                normal = hit.normal.normalized;
                return true;
            }

            point = collider.bounds.ClosestPoint(position);
            normal = position - point;
        }
        else
        {
            point = collider.ClosestPoint(position);
            normal = position - point;
        }

        if (normal.sqrMagnitude < 0.0001f)
        {
            normal = position - collider.bounds.center;
        }
        if (normal.sqrMagnitude < 0.0001f)
        {
            normal = Vector3.up;
        }
        normal.Normalize();
        return true;
    }

    private void ApplyOrientation(float deltaTime)
    {
        Vector3 forward = Vector3.ProjectOnPlane(moveForward, surfaceNormal);
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.ProjectOnPlane(transform.forward, surfaceNormal);
        }
        if (forward.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(forward.normalized, surfaceNormal);
        float t = deltaTime >= 1f ? 1f : 1f - Mathf.Exp(-rotationSmoothing * deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, t);
    }

    private void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos)
        {
            return;
        }

        Gizmos.color = hasSurface ? Color.green : Color.red;
        Gizmos.DrawWireSphere(lastSurfacePoint, 0.18f);
        Gizmos.DrawLine(lastSurfacePoint, lastSurfacePoint + surfaceNormal * 1.2f);
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, transform.position + moveForward * 1.5f);
    }
}
