using UnityEngine;

public sealed class CentipedeRecoveryController : MonoBehaviour
{
    [Header("Recovery")]
    [SerializeField, Min(0.05f)] private float stuckCheckInterval = 0.75f;
    [SerializeField, Min(0f)] private float stuckDistanceThreshold = 0.18f;
    [SerializeField, Min(0.1f)] private float stuckTimeThreshold = 2.0f;
    [SerializeField, Min(0.05f)] private float safePositionSaveInterval = 0.6f;
    [SerializeField, Min(0.1f)] private float maxRecoverSnapDistance = 8f;
    [SerializeField] private bool allowEmergencyReattach = true;
    [SerializeField] private bool allowEmergencyTeleportIfCompletelyBroken = true;
    [SerializeField, Min(0f)] private float targetFarDistance = 5f;

    [Header("Surface")]
    [SerializeField] private LayerMask crawlableSurfaceMask = ~0;
    [SerializeField] private Transform ignoredRoot;

    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = true;
    [SerializeField] private bool logRecoveryEvents;

    private Vector3 lastSafePosition;
    private Vector3 lastSafeSurfacePoint;
    private Vector3 lastSafeNormal = Vector3.up;
    private Vector3 lastCheckedPosition;
    private float nextSafeSaveTime;
    private float nextStuckCheckTime;
    private float stuckTimer;
    private bool hasSafePosition;
    private bool isStuck;

    public bool IsStuck => isStuck;
    public Vector3 LastSafePosition => lastSafePosition;

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

    private void OnValidate()
    {
        stuckCheckInterval = Mathf.Max(0.05f, stuckCheckInterval);
        stuckTimeThreshold = Mathf.Max(0.1f, stuckTimeThreshold);
        safePositionSaveInterval = Mathf.Max(0.05f, safePositionSaveInterval);
        maxRecoverSnapDistance = Mathf.Max(0.1f, maxRecoverSnapDistance);
    }

    public bool Tick(Transform target, SurfaceCrawlerMotor motor, bool skipStuckCheck)
    {
        if (motor != null && motor.HasSurface && Time.time >= nextSafeSaveTime)
        {
            SaveSafePosition(motor);
        }

        if (skipStuckCheck)
        {
            return false;
        }

        if (motor == null || !motor.HasSurface)
        {
            return allowEmergencyReattach && TryRecover(motor, "lost surface");
        }

        if (Time.time < nextStuckCheckTime)
        {
            return false;
        }

        nextStuckCheckTime = Time.time + stuckCheckInterval;
        float moved = Vector3.Distance(transform.position, lastCheckedPosition);
        lastCheckedPosition = transform.position;
        float targetDistance = target != null ? Vector3.Distance(transform.position, target.position) : targetFarDistance + 1f;

        if (moved <= stuckDistanceThreshold && targetDistance >= targetFarDistance)
        {
            stuckTimer += stuckCheckInterval;
        }
        else
        {
            stuckTimer = 0f;
            isStuck = false;
        }

        if (stuckTimer >= stuckTimeThreshold)
        {
            isStuck = true;
            stuckTimer = 0f;
            return TryRecover(motor, "stuck");
        }

        return false;
    }

    private void SaveSafePosition(SurfaceCrawlerMotor motor)
    {
        lastSafePosition = transform.position;
        lastSafeSurfacePoint = motor.SurfacePoint;
        lastSafeNormal = motor.SurfaceNormal;
        hasSafePosition = true;
        nextSafeSaveTime = Time.time + safePositionSaveInterval;
    }

    private bool TryRecover(SurfaceCrawlerMotor motor, string reason)
    {
        if (motor == null)
        {
            return false;
        }

        if (TryFindSurfaceNear(transform.position, maxRecoverSnapDistance, out Vector3 point, out Vector3 normal))
        {
            motor.TryAttachToSurface(point, normal, transform.forward);
            isStuck = false;
            if (logRecoveryEvents)
            {
                Debug.Log($"CentipedeRecoveryController: recovered by reattach, reason={reason}.", this);
            }
            return true;
        }

        if (allowEmergencyTeleportIfCompletelyBroken && hasSafePosition)
        {
            transform.position = lastSafePosition;
            motor.TryAttachToSurface(lastSafeSurfacePoint, lastSafeNormal, transform.forward);
            isStuck = false;
            if (logRecoveryEvents)
            {
                Debug.LogWarning($"CentipedeRecoveryController: emergency teleported to last safe position, reason={reason}.", this);
            }
            return true;
        }

        return false;
    }

    private bool TryFindSurfaceNear(Vector3 origin, float radius, out Vector3 point, out Vector3 normal)
    {
        Collider[] colliders = Physics.OverlapSphere(origin, radius, crawlableSurfaceMask, QueryTriggerInteraction.Ignore);
        float bestDistance = float.PositiveInfinity;
        point = Vector3.zero;
        normal = Vector3.up;
        bool found = false;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || collider.isTrigger || IsIgnored(collider))
            {
                continue;
            }

            if (!TryGetSurfacePoint(collider, origin, out Vector3 candidatePoint, out Vector3 candidateNormal))
            {
                continue;
            }

            float distance = Vector3.Distance(origin, candidatePoint);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                point = candidatePoint;
                normal = candidateNormal;
                found = true;
            }
        }

        return found;
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

    private void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos)
        {
            return;
        }

        Gizmos.color = isStuck ? Color.red : Color.green;
        Gizmos.DrawWireSphere(transform.position, maxRecoverSnapDistance);
        if (hasSafePosition)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(lastSafePosition, 0.35f);
            Gizmos.DrawLine(lastSafeSurfacePoint, lastSafeSurfacePoint + lastSafeNormal * 1.2f);
        }
    }
}
