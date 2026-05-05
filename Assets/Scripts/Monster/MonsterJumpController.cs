using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class MonsterJumpController : MonoBehaviour
{
    [SerializeField] float jumpCooldown = 4f;
    [SerializeField] float maxJumpDistance = 30f;
    [SerializeField] float maxJumpUpHeight = 18f;
    [SerializeField] float maxJumpDownHeight = 24f;
    [SerializeField] float jumpAssistDuration = 1.2f;
    [SerializeField] float jumpMoveSpeedMultiplier = 2.2f;
    [SerializeField] float jumpProbeRadius = 0.55f;
    [SerializeField] float landingProbeDistance = 8f;

    Rigidbody rb;
    float nextJumpTime;
    float jumpAssistEndTime;
    Vector3 activeJumpTarget;
    LayerMask surfaceMask = ~0;
    float landingSurfaceOffset = 0.85f;

    public bool HasJumpAssist { get { return Time.time < jumpAssistEndTime; } }
    public Vector3 ActiveJumpTarget { get { return activeJumpTarget; } }
    public float MoveSpeedMultiplier { get { return HasJumpAssist ? jumpMoveSpeedMultiplier : 1f; } }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public bool TryJumpTo(Vector3 target)
    {
        return TryJumpTo(target, surfaceMask, landingSurfaceOffset);
    }

    public bool TryJumpTo(Vector3 target, LayerMask mask, float surfaceOffset)
    {
        if (Time.time < nextJumpTime) return false;
        Vector3 jumpTarget = ClampTargetToJumpRange(target);
        if (!CanJumpTo(jumpTarget)) return false;
        surfaceMask = mask;
        landingSurfaceOffset = surfaceOffset;

        Vector3 landing;
        if (!TryResolveLandingPosition(jumpTarget, mask, surfaceOffset, out landing)) return false;
        activeJumpTarget = landing;
        jumpAssistEndTime = Time.time + jumpAssistDuration;
        nextJumpTime = Time.time + jumpCooldown;

        rb.useGravity = false;
        return true;
    }

    public bool CanJumpTo(Vector3 target)
    {
        Vector3 delta = target - transform.position;
        if (delta.magnitude > maxJumpDistance) return false;
        if (delta.y > maxJumpUpHeight) return false;
        if (-delta.y > maxJumpDownHeight) return false;
        return true;
    }

    Vector3 ClampTargetToJumpRange(Vector3 target)
    {
        Vector3 from = transform.position;
        Vector3 delta = target - from;
        if (delta.magnitude > maxJumpDistance)
        {
            delta = delta.normalized * maxJumpDistance;
        }

        delta.y = Mathf.Clamp(delta.y, -maxJumpDownHeight, maxJumpUpHeight);
        return from + delta;
    }

    bool TryResolveLandingPosition(Vector3 target, LayerMask mask, float surfaceOffset, out Vector3 landing)
    {
        RaycastHit bestHit;
        if (!TryFindSurfaceNear(target, mask, out bestHit))
        {
            landing = target;
            return false;
        }

        landing = bestHit.point + bestHit.normal * surfaceOffset;
        return true;
    }

    bool TryFindSurfaceNear(Vector3 target, LayerMask mask, out RaycastHit bestHit)
    {
        bestHit = default(RaycastHit);
        float bestDistance = float.MaxValue;
        Vector3 radial = new Vector3(target.x, 0f, target.z);
        if (radial.sqrMagnitude < 0.01f) radial = transform.forward;
        radial.Normalize();

        Vector3[] directions = new Vector3[] { Vector3.down, Vector3.up, radial, -radial, transform.forward, -transform.forward };
        for (int i = 0; i < directions.Length; i++)
        {
            RaycastHit[] hits = Physics.SphereCastAll(target - directions[i] * landingProbeDistance * 0.5f, jumpProbeRadius, directions[i], landingProbeDistance, mask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int j = 0; j < hits.Length; j++)
            {
                if (hits[j].collider == null) continue;
                if (hits[j].collider.transform.IsChildOf(transform)) continue;
                if (hits[j].distance >= bestDistance) continue;
                bestDistance = hits[j].distance;
                bestHit = hits[j];
                break;
            }
        }

        return bestHit.collider != null;
    }
}
