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
    [SerializeField] float swingForce = 16f;
    [SerializeField] float ropeConstraintStrength = 80f;
    [SerializeField] float ropeDamping = 4f;
    [SerializeField] bool hardConstraint = true;
    [SerializeField] float maxSwingEffort = 3.5f;
    [SerializeField] float swingEffortDrain = 1.4f;
    [SerializeField] float swingEffortRecharge = 0.8f;
    [SerializeField] float exhaustedSwingMultiplier = 0.25f;
    [SerializeField] float tangentialAirResistance = 0.012f;
    [SerializeField] float inwardVelocityPreservation = 0.85f;
    [SerializeField] float initialSwingKickForce = 1.25f;
    [SerializeField] float steeringBrakeForce = 2.5f;
    [SerializeField] float swingInputResponse = 5f;
    [SerializeField] float maxPoweredAngleFromDown = 65f;
    [SerializeField] float upwardSwingForceMultiplier = 0.08f;
    [SerializeField] float ropeGravityMultiplier = 1.8f;
    [SerializeField] LayerMask ropeCollisionMask = ~0;
    [SerializeField] float wrapPointOffset = 0.2f;
    [SerializeField] float minFiringWrapDistance = 2.5f;
    [SerializeField] float minHookDistancePastWrap = 0.75f;
    [Header("Flying Rope Physics")]
    [SerializeField] bool applyFlyingRopeTension = true;
    [SerializeField] float flyingHookVelocityDamping = 0.92f;
    [SerializeField] float flyingPlayerPullStrength = 0.08f;
    [Header("Ledge Assist")]
    [SerializeField] bool ledgeAssistEnabled = true;
    [SerializeField] float ledgeAssistRopeLength = 6f;
    [SerializeField] float ledgeAssistMaxAnchorHeight = 6f;
    [SerializeField] float ledgeAssistMinAnchorHeight = 0.45f;
    [SerializeField] float ledgeAssistInwardOffset = 1.7f;
    [SerializeField] float ledgeAssistSurfaceProbeHeight = 2.2f;
    [SerializeField] float ledgeAssistSurfaceProbeDistance = 5f;
    [SerializeField] float ledgeAssistMoveSpeed = 11f;
    [SerializeField] float ledgeAssistSnapDistance = 0.45f;

    readonly List<Vector3> wrapPoints = new List<Vector3>();
    Transform flyingHook;
    Rigidbody flyingHookRb;
    Transform dynamicAnchor;
    Vector3 anchorPoint;
    Vector2 smoothedSwingInput;
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
        if (isFiring)
        {
            ApplyFlyingRopePhysics();
            return;
        }

        if (!isAttached || playerRb == null) return;

        UpdateWrapping();
        ApplyRopeGravity();
        ApplySwingForces();
        ApplyConstraint();
        ApplyLedgeAssist();
    }

    public void BeginFiring(Transform hook, float ropeLength)
    {
        flyingHook = hook;
        flyingHookRb = hook != null ? hook.GetComponent<Rigidbody>() : null;
        dynamicAnchor = null;
        maxRopeLength = ropeLength;
        isFiring = true;
        isAttached = false;
        wrapPoints.Clear();
    }

    public void Attach(Vector3 point, float ropeLength, LayerMask collisionMask)
    {
        anchorPoint = point;
        dynamicAnchor = null;
        ropeCollisionMask = collisionMask;
        maxRopeLength = ropeLength;
        currentRopeLength = Mathf.Clamp(Vector3.Distance(GetOrigin(), anchorPoint), minRopeLength, maxRopeLength);
        currentSwingEffort = maxSwingEffort;
        isAttached = true;
        isFiring = false;
        flyingHook = null;
        flyingHookRb = null;
        smoothedSwingInput = Vector2.zero;
        wrapPoints.Clear();
    }

    public void Detach()
    {
        isAttached = false;
        isFiring = false;
        flyingHook = null;
        flyingHookRb = null;
        dynamicAnchor = null;
        smoothedSwingInput = Vector2.zero;
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
        return wrapPoints.Count > 0 ? wrapPoints[wrapPoints.Count - 1] : GetFinalAnchor();
    }

    Vector3 GetFinalAnchor()
    {
        return dynamicAnchor != null ? dynamicAnchor.position : anchorPoint;
    }

    public bool TryAttachToFiringWrap(LayerMask collisionMask)
    {
        if (!isFiring || isAttached || flyingHook == null) return false;

        ropeCollisionMask = collisionMask;
        Vector3 origin = GetOrigin();
        Vector3 toHook = flyingHook.position - origin;
        float distance = toHook.magnitude;
        if (distance <= minFiringWrapDistance + minHookDistancePastWrap) return false;

        RaycastHit[] hits = Physics.RaycastAll(origin, toHook / distance, distance - 0.05f, ropeCollisionMask, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider == null) continue;
            if (hit.distance < minFiringWrapDistance) continue;
            if (distance - hit.distance < minHookDistancePastWrap) continue;
            if (IsPlayerOrHookCollider(hit.collider)) continue;

            RopeCollisionWrapper wrapper = hit.collider.GetComponentInParent<RopeCollisionWrapper>();
            float offset = wrapper != null ? wrapper.WrapOffset : wrapPointOffset;
            Vector3 wrapPoint = hit.point + hit.normal * offset;

            dynamicAnchor = flyingHook;
            anchorPoint = flyingHook.position;
            currentRopeLength = Mathf.Clamp(Vector3.Distance(origin, wrapPoint), minRopeLength, maxRopeLength);
            currentSwingEffort = maxSwingEffort;
            smoothedSwingInput = Vector2.zero;
            wrapPoints.Clear();
            wrapPoints.Add(wrapPoint);
            isAttached = true;
            isFiring = false;
            return true;
        }

        return false;
    }

    bool IsPlayerOrHookCollider(Collider collider)
    {
        if (player != null && collider.transform.IsChildOf(player)) return true;
        if (flyingHook != null && collider.transform.IsChildOf(flyingHook)) return true;
        if (collider.GetComponentInParent<HookProjectile>() != null) return true;
        return false;
    }

    void ApplyFlyingRopePhysics()
    {
        if (!applyFlyingRopeTension || flyingHook == null || flyingHookRb == null) return;

        Vector3 origin = GetOrigin();
        Vector3 toHook = flyingHook.position - origin;
        float distance = toHook.magnitude;
        if (distance <= maxRopeLength || distance <= 0.001f) return;

        Vector3 ropeDirection = toHook / distance;
        flyingHookRb.MovePosition(origin + ropeDirection * maxRopeLength);

        Vector3 hookVelocity = flyingHookRb.linearVelocity;
        Vector3 radialVelocity = Vector3.Project(hookVelocity, ropeDirection);
        if (Vector3.Dot(radialVelocity, ropeDirection) > 0f)
        {
            flyingHookRb.linearVelocity = (hookVelocity - radialVelocity) * flyingHookVelocityDamping;
        }

        if (playerRb != null)
        {
            playerRb.AddForce(ropeDirection * (distance - maxRopeLength) * flyingPlayerPullStrength, ForceMode.Acceleration);
        }
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

    void ApplyLedgeAssist()
    {
        if (!ledgeAssistEnabled || playerRb == null || player == null) return;
        if (wrapPoints.Count > 0 || dynamicAnchor != null) return;
        if (!PrototypeInput.ShiftHeld) return;
        if (currentRopeLength > ledgeAssistRopeLength) return;

        Vector3 anchor = GetFinalAnchor();
        float heightAbovePlayer = anchor.y - player.position.y;
        if (heightAbovePlayer < ledgeAssistMinAnchorHeight || heightAbovePlayer > ledgeAssistMaxAnchorHeight) return;

        Vector3 planarToAnchor = Vector3.ProjectOnPlane(anchor - player.position, Vector3.up);
        if (planarToAnchor.sqrMagnitude < 0.04f && cameraTransform != null)
        {
            planarToAnchor = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up);
        }
        if (planarToAnchor.sqrMagnitude < 0.04f) planarToAnchor = player.forward;

        Vector3 inward = planarToAnchor.normalized;
        Vector3 surfaceProbeStart = anchor + inward * ledgeAssistInwardOffset + Vector3.up * ledgeAssistSurfaceProbeHeight;
        RaycastHit surfaceHit;
        if (!Physics.Raycast(surfaceProbeStart, Vector3.down, out surfaceHit, ledgeAssistSurfaceProbeDistance, ropeCollisionMask, QueryTriggerInteraction.Ignore))
        {
            surfaceProbeStart = anchor + Vector3.up * ledgeAssistSurfaceProbeHeight;
            if (!Physics.Raycast(surfaceProbeStart, Vector3.down, out surfaceHit, ledgeAssistSurfaceProbeDistance, ropeCollisionMask, QueryTriggerInteraction.Ignore)) return;
        }
        if (Vector3.Dot(surfaceHit.normal, Vector3.up) < 0.65f) return;

        float standHeight = GetPlayerStandHeight();
        Vector3 targetPosition = surfaceHit.point + Vector3.up * standHeight;
        Vector3 nextPosition = Vector3.MoveTowards(playerRb.position, targetPosition, ledgeAssistMoveSpeed * Time.fixedDeltaTime);
        if (Vector3.Distance(playerRb.position, targetPosition) <= ledgeAssistSnapDistance)
        {
            nextPosition = targetPosition;
        }
        playerRb.MovePosition(nextPosition);

        Vector3 desiredVelocity = (targetPosition - playerRb.position);
        if (desiredVelocity.sqrMagnitude > 0.01f)
        {
            Vector3 mantleVelocity = desiredVelocity.normalized * Mathf.Min(ledgeAssistMoveSpeed, desiredVelocity.magnitude / Time.fixedDeltaTime);
            playerRb.linearVelocity = Vector3.Lerp(playerRb.linearVelocity, mantleVelocity, 0.35f);
        }

        currentRopeLength = Mathf.Clamp(Mathf.Min(currentRopeLength, Vector3.Distance(nextPosition, anchor)), minRopeLength, maxRopeLength);
    }

    float GetPlayerStandHeight()
    {
        CapsuleCollider capsule = player != null ? player.GetComponent<CapsuleCollider>() : null;
        if (capsule == null) return 1f;
        return Mathf.Max(0.5f, capsule.height * 0.5f + 0.05f);
    }

    void ApplySwingForces()
    {
        if (cameraTransform == null) return;

        smoothedSwingInput = Vector2.Lerp(smoothedSwingInput, PrototypeInput.Move, Mathf.Clamp01(swingInputResponse * Time.fixedDeltaTime));
        float x = smoothedSwingInput.x;
        float z = smoothedSwingInput.y;
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
        Vector3 movingDirection = tangentialVelocity.sqrMagnitude > 0.16f ? tangentialVelocity.normalized : Vector3.zero;
        float alignment = movingDirection == Vector3.zero ? 0f : Vector3.Dot(desiredDirection, movingDirection);
        float poweredAngle = Vector3.Angle(-Vector3.up, ropeDirection);

        Vector3 force = Vector3.zero;
        if (movingDirection == Vector3.zero)
        {
            force = desiredDirection * initialSwingKickForce;
        }
        else if (alignment > 0f)
        {
            force = desiredDirection * swingForce * effortMultiplier * Mathf.Lerp(0.35f, 1f, alignment);
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
            Vector3 previous = wrapPoints.Count > 1 ? wrapPoints[wrapPoints.Count - 2] : GetFinalAnchor();
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
        ropeLine.SetPosition(wrapPoints.Count + 1, GetFinalAnchor());
    }

    void OnDrawGizmos()
    {
        if (!isAttached) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(GetFinalAnchor(), 0.35f);
        Gizmos.color = Color.magenta;
        for (int i = 0; i < wrapPoints.Count; i++) Gizmos.DrawWireSphere(wrapPoints[i], 0.25f);
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(GetActiveAnchor(), currentRopeLength);
    }
}
