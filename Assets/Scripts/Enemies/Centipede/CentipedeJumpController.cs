using UnityEngine;

[RequireComponent(typeof(SurfaceCrawlerMotor))]
public sealed class CentipedeJumpController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LayerMask crawlableSurfaceMask = ~0;
    [SerializeField] private Transform ignoredRoot;

    [Header("Jump")]
    [SerializeField, Min(0.05f)] private float jumpArcHeight = 3.0f;
    [SerializeField, Min(0.05f)] private float jumpDuration = 0.85f;
    [SerializeField, Min(0f)] private float jumpCooldown = 0.8f;
    [SerializeField, Range(0f, 1f)] private float jumpChanceBias = 1f;
    [SerializeField, Min(0.25f)] private float maxJumpDistance = 30f;
    [SerializeField] private bool limitJumpDistanceByPlayArea = true;
    [SerializeField, Range(0.05f, 1f)] private float maxJumpDistanceAsPlayAreaFraction = 0.333f;
    [SerializeField, Min(1f)] private float fallbackPlayAreaDiameter = 90f;
    [SerializeField, Min(0f)] private float introJumpLockDuration = 5f;
    [SerializeField, Min(0f)] private float maxJumpHeightDifference = 20f;
    [SerializeField, Min(0.05f)] private float landingSnapDistance = 5f;
    [SerializeField, Range(-1f, 1f)] private float landingNormalThreshold = -0.25f;
    [SerializeField, Min(0f)] private float surfaceOffset = 0.48f;
    [SerializeField, Min(0f)] private float rotationSmoothing = 12f;

    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = true;
    [SerializeField] private bool logJumpEvents;

    private SurfaceCrawlerMotor motor;
    private Vector3 startPosition;
    private Vector3 landingPoint;
    private Vector3 landingNormal;
    private Vector3 landingForward;
    private float jumpTime;
    private float nextAllowedJumpTime;
    private float nextRejectLogTime;
    private bool isJumping;

    public bool IsJumping => isJumping;

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

    private void Awake()
    {
        CacheReferences();
    }

    private void OnValidate()
    {
        jumpDuration = Mathf.Max(0.05f, jumpDuration);
        maxJumpDistance = Mathf.Max(0.25f, maxJumpDistance);
        fallbackPlayAreaDiameter = Mathf.Max(1f, fallbackPlayAreaDiameter);
        landingSnapDistance = Mathf.Max(0.05f, landingSnapDistance);
    }

    public bool TryStartJump(Vector3 requestedTarget)
    {
        CacheReferences();
        if (isJumping || Time.time < nextAllowedJumpTime)
        {
            return false;
        }

        if (Time.time < introJumpLockDuration)
        {
            RejectJump("intro jump lock", 0f, GetMaxAllowedJumpDistance());
            return false;
        }

        Vector3 current = transform.position;
        float distance = Vector3.Distance(current, requestedTarget);
        float maxAllowedDistance = GetMaxAllowedJumpDistance();
        if (distance > maxAllowedDistance)
        {
            RejectJump("jump too far", distance, maxAllowedDistance);
            return false;
        }

        if (Mathf.Abs(requestedTarget.y - current.y) > maxJumpHeightDifference)
        {
            return false;
        }

        if (jumpChanceBias < 1f && Random.value > jumpChanceBias)
        {
            return false;
        }

        if (!TryFindLanding(requestedTarget, out landingPoint, out landingNormal))
        {
            RejectJump("no landing surface", distance, maxAllowedDistance);
            return false;
        }

        startPosition = current;
        Vector3 planarDirection = Vector3.ProjectOnPlane(landingPoint - startPosition, landingNormal);
        if (planarDirection.sqrMagnitude < 0.0001f)
        {
            planarDirection = Vector3.ProjectOnPlane(transform.forward, landingNormal);
        }
        if (planarDirection.sqrMagnitude < 0.0001f)
        {
            planarDirection = transform.forward;
        }

        landingForward = planarDirection.normalized;
        jumpTime = 0f;
        isJumping = true;
        if (logJumpEvents)
        {
            Debug.Log($"CentipedeJumpController: jump accepted target={landingPoint:F2}, distance={distance:F1}, maxAllowed={maxAllowedDistance:F1}.", this);
        }

        return true;
    }

    public bool IsJumpDistanceAllowed(Vector3 from, Vector3 to)
    {
        return Vector3.Distance(from, to) <= GetMaxAllowedJumpDistance();
    }

    public float GetMaxAllowedJumpDistance()
    {
        if (!limitJumpDistanceByPlayArea)
        {
            return maxJumpDistance;
        }

        return Mathf.Min(maxJumpDistance, ResolvePlayAreaDiameter() * maxJumpDistanceAsPlayAreaFraction);
    }

    private void RejectJump(string reason, float distance, float maxAllowedDistance)
    {
        if (!logJumpEvents || Time.time < nextRejectLogTime)
        {
            return;
        }

        nextRejectLogTime = Time.time + 1f;
        Debug.Log($"CentipedeJumpController: rejected jump, reason={reason}, distance={distance:F1}, maxAllowed={maxAllowedDistance:F1}.", this);
    }

    private float ResolvePlayAreaDiameter()
    {
        GameObject generatedLevel = GameObject.Find("GeneratedLevel");
        if (generatedLevel != null)
        {
            Collider[] colliders = generatedLevel.GetComponentsInChildren<Collider>();
            if (colliders.Length > 0)
            {
                Bounds bounds = colliders[0].bounds;
                for (int i = 1; i < colliders.Length; i++)
                {
                    bounds.Encapsulate(colliders[i].bounds);
                }

                float diameter = Mathf.Min(bounds.size.x, bounds.size.z);
                if (diameter > 1f)
                {
                    return diameter;
                }
            }
        }

        ProceduralMegastructureGenerator generator = FindAnyObjectByType<ProceduralMegastructureGenerator>();
        if (generator != null)
        {
            System.Reflection.FieldInfo field = typeof(ProceduralMegastructureGenerator).GetField("shaftRadius", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (field != null && field.GetValue(generator) is float radius && radius > 0f)
            {
                return radius * 2f;
            }
        }

        return fallbackPlayAreaDiameter;
    }

    public void Tick(float deltaTime)
    {
        if (!isJumping)
        {
            return;
        }

        jumpTime += deltaTime;
        float t = Mathf.Clamp01(jumpTime / jumpDuration);
        float smoothT = t * t * (3f - 2f * t);
        Vector3 basePosition = Vector3.Lerp(startPosition, landingPoint + landingNormal * surfaceOffset, smoothT);
        Vector3 arcUp = Vector3.up;
        Vector3 arcPosition = basePosition + arcUp * (Mathf.Sin(Mathf.PI * smoothT) * jumpArcHeight);

        transform.position = arcPosition;
        Vector3 travel = landingPoint - startPosition;
        Vector3 forward = travel.sqrMagnitude > 0.0001f ? travel.normalized : transform.forward;
        Vector3 up = Vector3.Slerp(transform.up, landingNormal, smoothT);
        if (Vector3.ProjectOnPlane(forward, up).sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(forward, up).normalized, up.normalized);
            float weight = 1f - Mathf.Exp(-rotationSmoothing * deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, weight);
        }

        if (t >= 1f)
        {
            FinishJump();
        }
    }

    private void FinishJump()
    {
        isJumping = false;
        nextAllowedJumpTime = Time.time + jumpCooldown;
        if (motor != null)
        {
            motor.TryAttachToSurface(landingPoint, landingNormal, landingForward);
        }
        else
        {
            transform.position = landingPoint + landingNormal * surfaceOffset;
        }

        if (logJumpEvents)
        {
            Debug.Log("CentipedeJumpController: jump finished.", this);
        }
    }

    private void CacheReferences()
    {
        if (motor == null)
        {
            motor = GetComponent<SurfaceCrawlerMotor>();
        }

        if (ignoredRoot == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                ignoredRoot = player.transform;
            }
        }
    }

    private bool TryFindLanding(Vector3 requestedTarget, out Vector3 point, out Vector3 normal)
    {
        Vector3[] directions =
        {
            -transform.up,
            Vector3.down,
            Vector3.up,
            -transform.forward,
            transform.forward,
            -transform.right,
            transform.right
        };

        float bestDistance = float.PositiveInfinity;
        point = requestedTarget;
        normal = transform.up;
        bool found = false;

        for (int i = 0; i < directions.Length; i++)
        {
            Vector3 direction = directions[i].sqrMagnitude > 0.0001f ? directions[i].normalized : Vector3.down;
            Vector3 origin = requestedTarget - direction * landingSnapDistance * 0.5f;
            RaycastHit[] hits = Physics.SphereCastAll(origin, 0.18f, direction, landingSnapDistance, crawlableSurfaceMask, QueryTriggerInteraction.Ignore);
            for (int h = 0; h < hits.Length; h++)
            {
                Collider collider = hits[h].collider;
                if (collider == null || collider.isTrigger || IsIgnored(collider))
                {
                    continue;
                }

                Vector3 candidateNormal = hits[h].normal.normalized;
                if (Vector3.Dot(candidateNormal, direction * -1f) < landingNormalThreshold)
                {
                    continue;
                }

                float distance = Vector3.Distance(requestedTarget, hits[h].point);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    point = hits[h].point;
                    normal = candidateNormal;
                    found = true;
                }
            }
        }

        if (found)
        {
            return true;
        }

        Collider[] colliders = Physics.OverlapSphere(requestedTarget, landingSnapDistance, crawlableSurfaceMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || collider.isTrigger || IsIgnored(collider))
            {
                continue;
            }

            if (TryGetSurfacePoint(collider, requestedTarget, out Vector3 candidatePoint, out Vector3 candidateNormal))
            {
                float distance = Vector3.Distance(requestedTarget, candidatePoint);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    point = candidatePoint;
                    normal = candidateNormal;
                    found = true;
                }
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

        Gizmos.color = isJumping ? Color.red : Color.magenta;
        Gizmos.DrawWireSphere(landingPoint, 0.35f);
        Gizmos.DrawLine(landingPoint, landingPoint + landingNormal * 1.2f);
        Gizmos.DrawLine(startPosition, landingPoint);
    }
}
