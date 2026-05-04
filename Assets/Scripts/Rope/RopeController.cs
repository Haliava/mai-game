using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class RopeController : MonoBehaviour
{
    [SerializeField] Transform player;
    [SerializeField] Rigidbody playerRb;
    [SerializeField] Transform ropeOrigin;
    [SerializeField] Transform cameraTransform;
    [SerializeField] LineRenderer ropeLine;
    [SerializeField] float maxRopeLength = 35f;
    [SerializeField] float minRopeLength = 2f;
    [SerializeField] float ropeClimbSpeed = 6f;
    [SerializeField] float swingForce = 25f;
    [SerializeField] float ropeConstraintStrength = 80f;
    [SerializeField] float ropeDamping = 4f;
    [SerializeField] bool hardConstraint = true;
    [SerializeField] float maxSwingEffort = 3.5f;
    [SerializeField] float swingEffortDrain = 1.4f;
    [SerializeField] float swingEffortRecharge = 0.8f;
    [SerializeField] float exhaustedSwingMultiplier = 0.25f;
    [SerializeField] float tangentialAirResistance = 0.012f;
    [SerializeField] float inwardVelocityPreservation = 0.85f;
    [SerializeField] float initialSwingKickForce = 4f;
    [SerializeField] float steeringBrakeForce = 6f;
    [SerializeField] float maxPoweredAngleFromDown = 65f;
    [SerializeField] float upwardSwingForceMultiplier = 0.08f;
    [SerializeField] float ropeGravityMultiplier = 1.8f;
    [SerializeField] LayerMask ropeCollisionMask = ~0;
    [SerializeField] float wrapPointOffset = 0.2f;

    readonly List<Vector3> wrapPoints = new List<Vector3>();
    Transform flyingHook;
    Vector3 anchorPoint;
    float currentRopeLength;
    float currentSwingEffort;
    bool isAttached;
    bool isFiring;

    public bool IsAttached { get { return isAttached; } }
    public float CurrentRopeLength { get { return currentRopeLength; } }
    public float CurrentSwingEffort { get { return currentSwingEffort; } }

    void Awake()
    {
        if (ropeLine == null) ropeLine = GetComponent<LineRenderer>();
        if (playerRb == null && player != null) playerRb = player.GetComponent<Rigidbody>();
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        ropeLine.positionCount = 0;
        ropeLine.widthMultiplier = 0.04f;
        currentSwingEffort = maxSwingEffort;
    }

    void Update()
    {
        if (isAttached) UpdateClimbInput();
        UpdateLine();
    }

    void FixedUpdate()
    {
        if (!isAttached || playerRb == null) return;

        UpdateWrapping();
        ApplyRopeGravity();
        ApplySwingForces();
        ApplyConstraint();
    }

    public void BeginFiring(Transform hook, float ropeLength)
    {
        flyingHook = hook;
        maxRopeLength = ropeLength;
        isFiring = true;
        isAttached = false;
        wrapPoints.Clear();
    }

    public void Attach(Vector3 point, float ropeLength, LayerMask collisionMask)
    {
        anchorPoint = point;
        ropeCollisionMask = collisionMask;
        maxRopeLength = ropeLength;
        currentRopeLength = Mathf.Clamp(Vector3.Distance(GetOrigin(), anchorPoint), minRopeLength, maxRopeLength);
        currentSwingEffort = maxSwingEffort;
        isAttached = true;
        isFiring = false;
        flyingHook = null;
        wrapPoints.Clear();
    }

    public void Detach()
    {
        isAttached = false;
        isFiring = false;
        flyingHook = null;
        wrapPoints.Clear();
        if (ropeLine != null) ropeLine.positionCount = 0;
    }

    Vector3 GetOrigin()
    {
        if (ropeOrigin != null) return ropeOrigin.position;
        return player != null ? player.position : transform.position;
    }

    Vector3 GetActiveAnchor()
    {
        return wrapPoints.Count > 0 ? wrapPoints[wrapPoints.Count - 1] : anchorPoint;
    }

    void UpdateClimbInput()
    {
        float delta = 0f;
        if (PrototypeInput.ShiftHeld) delta -= ropeClimbSpeed * Time.deltaTime;
        if (PrototypeInput.ControlHeld) delta += ropeClimbSpeed * Time.deltaTime;
        currentRopeLength = Mathf.Clamp(currentRopeLength + delta, minRopeLength, maxRopeLength);
    }

    void ApplyConstraint()
    {
        Vector3 origin = player != null ? player.position : GetOrigin();
        Vector3 activeAnchor = GetActiveAnchor();
        Vector3 fromAnchor = origin - activeAnchor;
        float distance = fromAnchor.magnitude;
        if (distance <= 0.001f) return;

        Vector3 ropeDirection = fromAnchor / distance;
        if (distance >= currentRopeLength)
        {
            if (hardConstraint)
            {
                Vector3 constrainedPosition = activeAnchor + ropeDirection * currentRopeLength;
                playerRb.MovePosition(constrainedPosition);

                Vector3 radialVelocity = Vector3.Project(playerRb.linearVelocity, ropeDirection);
                Vector3 tangentialVelocity = playerRb.linearVelocity - radialVelocity;
                float radialSpeed = Vector3.Dot(radialVelocity, ropeDirection);
                if (radialSpeed > 0f)
                {
                    playerRb.linearVelocity = tangentialVelocity;
                }
                else
                {
                    playerRb.linearVelocity = tangentialVelocity + radialVelocity * inwardVelocityPreservation;
                }
                return;
            }

            float excess = distance - currentRopeLength;
            playerRb.AddForce(-ropeDirection * excess * ropeConstraintStrength, ForceMode.Acceleration);

            Vector3 awayVelocity = Vector3.Project(playerRb.linearVelocity, ropeDirection);
            if (Vector3.Dot(awayVelocity, ropeDirection) > 0f)
            {
                playerRb.linearVelocity -= awayVelocity * Mathf.Clamp01(ropeDamping * Time.fixedDeltaTime);
            }
        }
    }

    void ApplySwingForces()
    {
        if (cameraTransform == null) return;

        Vector2 move = PrototypeInput.Move;
        float x = move.x;
        float z = move.y;
        Vector3 input = cameraTransform.forward * z + cameraTransform.right * x;
        RechargeSwingEffort(input.sqrMagnitude < 0.01f);

        Vector3 ropeDirection = ((player != null ? player.position : GetOrigin()) - GetActiveAnchor()).normalized;
        Vector3 tangentialVelocity = Vector3.ProjectOnPlane(playerRb.linearVelocity, ropeDirection);
        if (tangentialVelocity.sqrMagnitude > 0.01f)
        {
            playerRb.AddForce(-tangentialVelocity * tangentialAirResistance, ForceMode.Acceleration);
        }

        if (input.sqrMagnitude < 0.01f || currentSwingEffort <= 0.001f) return;

        Vector3 swingDirection = Vector3.ProjectOnPlane(input, ropeDirection);
        if (swingDirection.sqrMagnitude < 0.001f) return;

        float effort01 = Mathf.Clamp01(currentSwingEffort / maxSwingEffort);
        float effortMultiplier = Mathf.Lerp(exhaustedSwingMultiplier, 1f, effort01);
        Vector3 desiredDirection = swingDirection.normalized;
        Vector3 movingDirection = tangentialVelocity.sqrMagnitude > 0.04f ? tangentialVelocity.normalized : Vector3.zero;
        float alignment = movingDirection == Vector3.zero ? 0f : Vector3.Dot(desiredDirection, movingDirection);
        float poweredAngle = Vector3.Angle(-Vector3.up, ropeDirection);

        Vector3 force = Vector3.zero;
        if (movingDirection == Vector3.zero)
        {
            force = desiredDirection * initialSwingKickForce;
        }
        else if (alignment > 0f)
        {
            force = desiredDirection * swingForce * effortMultiplier * alignment;
        }
        else
        {
            force = desiredDirection * steeringBrakeForce * effortMultiplier;
        }

        if (poweredAngle > maxPoweredAngleFromDown && force.y > 0f)
        {
            force.y *= upwardSwingForceMultiplier;
        }

        if (force.y > 0f)
        {
            force.y *= Mathf.Lerp(upwardSwingForceMultiplier, 1f, Mathf.Clamp01(1f - poweredAngle / maxPoweredAngleFromDown));
        }

        playerRb.AddForce(force, ForceMode.Acceleration);
        currentSwingEffort = Mathf.Max(0f, currentSwingEffort - swingEffortDrain * Time.fixedDeltaTime);
    }

    void ApplyRopeGravity()
    {
        Vector3 ropeDirection = ((player != null ? player.position : GetOrigin()) - GetActiveAnchor()).normalized;
        Vector3 tangentialGravity = Vector3.ProjectOnPlane(Physics.gravity, ropeDirection);
        playerRb.AddForce(tangentialGravity * Mathf.Max(0f, ropeGravityMultiplier - 1f), ForceMode.Acceleration);
    }

    void RechargeSwingEffort(bool hasNoInput)
    {
        float rechargeMultiplier = hasNoInput ? 1f : 0.35f;
        currentSwingEffort = Mathf.Min(maxSwingEffort, currentSwingEffort + swingEffortRecharge * rechargeMultiplier * Time.fixedDeltaTime);
    }

    void UpdateWrapping()
    {
        Vector3 origin = GetOrigin();
        Vector3 activeAnchor = GetActiveAnchor();
        Vector3 toAnchor = activeAnchor - origin;
        float distance = toAnchor.magnitude;
        if (distance > 0.01f)
        {
            RaycastHit hit;
            if (Physics.Raycast(origin, toAnchor.normalized, out hit, distance - 0.05f, ropeCollisionMask, QueryTriggerInteraction.Ignore))
            {
                if (Vector3.Distance(hit.point, activeAnchor) > 0.5f && (wrapPoints.Count == 0 || Vector3.Distance(wrapPoints[wrapPoints.Count - 1], hit.point) > 0.5f))
                {
                    wrapPoints.Add(hit.point + hit.normal * wrapPointOffset);
                }
            }
        }

        if (wrapPoints.Count > 0)
        {
            Vector3 previous = wrapPoints.Count > 1 ? wrapPoints[wrapPoints.Count - 2] : anchorPoint;
            Vector3 toPrevious = previous - origin;
            if (!Physics.Raycast(origin, toPrevious.normalized, toPrevious.magnitude - 0.05f, ropeCollisionMask, QueryTriggerInteraction.Ignore))
            {
                wrapPoints.RemoveAt(wrapPoints.Count - 1);
            }
        }
    }

    void UpdateLine()
    {
        if (ropeLine == null) return;

        if (isFiring && flyingHook != null)
        {
            ropeLine.positionCount = 2;
            ropeLine.SetPosition(0, GetOrigin());
            ropeLine.SetPosition(1, flyingHook.position);
            return;
        }

        if (!isAttached)
        {
            ropeLine.positionCount = 0;
            return;
        }

        ropeLine.positionCount = wrapPoints.Count + 2;
        ropeLine.SetPosition(0, GetOrigin());
        for (int i = 0; i < wrapPoints.Count; i++)
        {
            ropeLine.SetPosition(i + 1, wrapPoints[wrapPoints.Count - 1 - i]);
        }
        ropeLine.SetPosition(wrapPoints.Count + 1, anchorPoint);
    }

    void OnDrawGizmos()
    {
        if (!isAttached) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(anchorPoint, 0.35f);
        Gizmos.color = Color.magenta;
        for (int i = 0; i < wrapPoints.Count; i++) Gizmos.DrawWireSphere(wrapPoints[i], 0.25f);
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(anchorPoint, currentRopeLength);
    }
}
