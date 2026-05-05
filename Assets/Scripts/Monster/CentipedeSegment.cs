using UnityEngine;

public class CentipedeSegment : MonoBehaviour
{
    [SerializeField] Transform followTarget;
    [SerializeField] float spacing = 1.2f;
    [SerializeField] float followSpeed = 8f;
    [SerializeField] LayerMask surfaceMask = ~0;
    [SerializeField] float surfaceRayDistance = 10f;
    [SerializeField] float surfaceOffset = 0.55f;
    [SerializeField] float surfaceStickSpeed = 12f;
    [SerializeField] float turnSpeed = 10f;
    [SerializeField] SurfaceWalkerMotor motor;
    [SerializeField] CentipedeLegCoordinator legCoordinator;

    Rigidbody rb;
    Vector3 surfaceNormal = Vector3.up;
    Vector3 pathTarget;
    Vector3 pathForward = Vector3.forward;
    Vector3 pathNormal = Vector3.up;
    bool pathAirborne;
    bool hasPathTarget;
    Transform stretchAnchor;
    float maxStretchDistance = 2f;
    float stretchCorrectionStrength = 20f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }
        if (motor == null) motor = GetComponent<SurfaceWalkerMotor>();
        if (legCoordinator == null) legCoordinator = GetComponent<CentipedeLegCoordinator>();
    }

    public void Configure(Transform target, float segmentSpacing, float speed)
    {
        followTarget = target;
        spacing = segmentSpacing;
        followSpeed = speed;
    }

    public void ConfigureSurface(LayerMask mask, float rayDistance, float offset)
    {
        surfaceMask = mask;
        surfaceRayDistance = rayDistance;
        surfaceOffset = offset;
        if (motor != null) motor.Configure(mask, transform.root, offset);
    }

    public void SetPathTarget(Vector3 target, Vector3 forward)
    {
        pathTarget = target;
        if (forward.sqrMagnitude > 0.001f) pathForward = forward.normalized;
        pathNormal = surfaceNormal;
        pathAirborne = false;
        hasPathTarget = true;
    }

    public void SetTrailTarget(Vector3 target, Vector3 forward, Vector3 normal, bool airborne)
    {
        pathTarget = target;
        if (forward.sqrMagnitude > 0.001f) pathForward = forward.normalized;
        if (normal.sqrMagnitude > 0.001f) pathNormal = normal.normalized;
        pathAirborne = airborne;
        hasPathTarget = true;
    }

    public void SetStretchLimit(Transform anchor, float targetSpacing, float maxStretch, float correctionStrength)
    {
        stretchAnchor = anchor;
        spacing = targetSpacing;
        maxStretchDistance = targetSpacing * Mathf.Max(1f, maxStretch);
        stretchCorrectionStrength = correctionStrength;
    }

    public void SetLegCoordinator(CentipedeLegCoordinator coordinator)
    {
        legCoordinator = coordinator;
    }

    void FixedUpdate()
    {
        if (followTarget == null && !hasPathTarget) return;

        Vector3 desired;
        Vector3 lookSource;
        if (hasPathTarget)
        {
            desired = pathTarget;
            lookSource = pathForward;
        }
        else
        {
            Vector3 toTarget = followTarget.position - transform.position;
            Vector3 followDirection = toTarget.sqrMagnitude > 0.001f ? toTarget.normalized : followTarget.forward;
            desired = followTarget.position - followDirection * spacing;
            lookSource = followTarget.position - transform.position;
        }

        if (motor != null)
        {
            motor.StepToTrailTarget(desired, lookSource, pathNormal, pathAirborne, followSpeed, Time.fixedDeltaTime);
            surfaceNormal = motor.SurfaceNormal;
        }
        else
        {
            RaycastHit surfaceHit;
            if (TryFindSurface(desired, out surfaceHit))
            {
                surfaceNormal = Vector3.Slerp(surfaceNormal, surfaceHit.normal, surfaceStickSpeed * Time.fixedDeltaTime).normalized;
                desired = surfaceHit.point + surfaceNormal * surfaceOffset;
            }

            Vector3 nextPosition = Vector3.Lerp(transform.position, desired, followSpeed * Time.fixedDeltaTime);
            if (rb != null) rb.MovePosition(nextPosition);
            else transform.position = nextPosition;

            Vector3 lookDirection = Vector3.ProjectOnPlane(lookSource, surfaceNormal);
            if (lookDirection.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(lookDirection.normalized, surfaceNormal);
                Quaternion nextRotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.fixedDeltaTime);
                if (rb != null) rb.MoveRotation(nextRotation);
                else transform.rotation = nextRotation;
            }
        }

        EnforceStretchLimit();
        if (legCoordinator != null) legCoordinator.Tick(pathAirborne || (motor != null && motor.IsAirborne));
    }

    bool TryFindSurface(Vector3 desiredPosition, out RaycastHit bestHit)
    {
        bestHit = default(RaycastHit);
        float bestDistance = float.MaxValue;
        Vector3 radial = new Vector3(desiredPosition.x, 0f, desiredPosition.z);
        if (radial.sqrMagnitude < 0.01f) radial = transform.right;
        radial.Normalize();

        Vector3[] directions = new Vector3[] { -surfaceNormal, Vector3.down, radial, -radial, transform.forward, -transform.forward };
        for (int i = 0; i < directions.Length; i++)
        {
            RaycastHit[] hits = Physics.RaycastAll(desiredPosition - directions[i] * 0.35f, directions[i], surfaceRayDistance, surfaceMask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int j = 0; j < hits.Length; j++)
            {
                if (hits[j].collider == null) continue;
                if (hits[j].collider.transform.IsChildOf(transform.root)) continue;
                if (hits[j].distance >= bestDistance) continue;
                bestDistance = hits[j].distance;
                bestHit = hits[j];
                break;
            }
        }

        return bestHit.collider != null;
    }

    void EnforceStretchLimit()
    {
        if (stretchAnchor == null) return;
        Vector3 delta = transform.position - stretchAnchor.position;
        float distance = delta.magnitude;
        if (distance <= maxStretchDistance || distance < 0.001f) return;

        Vector3 target = stretchAnchor.position + delta.normalized * maxStretchDistance;
        Vector3 corrected = Vector3.Lerp(transform.position, target, Mathf.Clamp01(stretchCorrectionStrength * Time.fixedDeltaTime));
        if (rb != null) rb.MovePosition(corrected);
        else transform.position = corrected;
    }
}
