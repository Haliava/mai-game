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
    [SerializeField] float attachSlack = 0.45f;
    [SerializeField] float attachInputGraceTime = 0.18f;
    [SerializeField] int attachConstraintGraceFrames = 2;
    [SerializeField] float ropeClimbSpeed = 6f;
    [SerializeField] float swingForce = 34f;
    [SerializeField] float ropeConstraintStrength = 80f;
    [SerializeField] float ropeDamping = 4f;
    [SerializeField] bool hardConstraint = true;
    [SerializeField] float maxSwingEffort = 5.5f;
    [SerializeField] float swingEffortDrain = 0.75f;
    [SerializeField] float swingEffortRecharge = 1.6f;
    [SerializeField] float exhaustedSwingMultiplier = 0.4f;
    [SerializeField] float tangentialAirResistance = 0.012f;
    [SerializeField] float inwardVelocityPreservation = 0.85f;
    [SerializeField] float initialSwingKickForce = 4.5f;
    [SerializeField] float steeringBrakeForce = 5f;
    [SerializeField] float swingInputResponse = 5f;
    [SerializeField] float maxPoweredAngleFromDown = 68f;
    [SerializeField] float upwardSwingForceMultiplier = 0.05f;
    [SerializeField] float hardSwingAngleFromDown = 76f;
    [SerializeField] float highAngleBrakeForce = 42f;
    [SerializeField] float highAngleVelocityDamping = 0.8f;
    [SerializeField] float maxTangentialSpeedAtAngleLimit = 8f;
    [SerializeField] float ropeGravityMultiplier = 1.8f;
    [SerializeField] LayerMask ropeCollisionMask = ~0;
    [SerializeField] float wrapPointOffset = 0.2f;
    [SerializeField] float minFiringWrapDistance = 2.5f;
    [SerializeField] float minHookDistancePastWrap = 0.75f;
    [SerializeField] bool allowFiringWrapWhileGrounded = false;
    [SerializeField] float groundedWrapIgnoreDistance = 2.2f;
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
    [SerializeField] float ledgeAssistEdgeSearchDistance = 4f;
    [SerializeField] float ledgeAssistSideClearance = 0.55f;
    [SerializeField] float ledgeAssistStageTolerance = 0.18f;

    readonly List<Vector3> wrapPoints = new List<Vector3>();
    Transform flyingHook;
    Rigidbody flyingHookRb;
    Transform dynamicAnchor;
    Vector3 anchorPoint;
    Vector2 smoothedSwingInput;
    float currentRopeLength;
    float currentSwingEffort;
    float attachTime;
    int constraintGraceFramesRemaining;
    bool isMantlingLedge;
    int mantleStage;
    Vector3 mantleSideLow;
    Vector3 mantleSideHigh;
    Vector3 mantleTop;
    bool isAttached;
    bool isFiring;
    Collider playerCollider;
    bool playerColliderWasEnabled;
    bool mantleCollisionSuppressed;

    public bool IsAttached { get { return isAttached; } }
    public float CurrentRopeLength { get { return currentRopeLength; } }
    public float CurrentSwingEffort { get { return currentSwingEffort; } }

    void Awake()
    {
        if (ropeLine == null) ropeLine = GetComponent<LineRenderer>();
        if (playerRb == null && player != null) playerRb = player.GetComponent<Rigidbody>();
        if (player != null) playerCollider = player.GetComponent<Collider>();
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
        if (constraintGraceFramesRemaining > 0)
        {
            constraintGraceFramesRemaining--;
            return;
        }
        if (ApplyLedgeAssist()) return;
        ApplyRopeGravity();
        ApplySwingForces();
        ApplyHighAngleSwingLimit();
        ApplyConstraint();
        ClampSwingAngle();
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
        currentRopeLength = Mathf.Clamp(Vector3.Distance(GetOrigin(), anchorPoint) + attachSlack, minRopeLength, maxRopeLength);
        currentSwingEffort = maxSwingEffort;
        attachTime = Time.time;
        constraintGraceFramesRemaining = attachConstraintGraceFrames;
        isAttached = true;
        isFiring = false;
        flyingHook = null;
        flyingHookRb = null;
        smoothedSwingInput = Vector2.zero;
        isMantlingLedge = false;
        SetMantleCollision(true);
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
        isMantlingLedge = false;
        SetMantleCollision(true);
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
        if (!allowFiringWrapWhileGrounded && IsPlayerGroundedOnRopeCollision()) return false;

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
            if (IsPlayerStandingOnCollider(hit.collider)) continue;

            RopeCollisionWrapper wrapper = hit.collider.GetComponentInParent<RopeCollisionWrapper>();
            float offset = wrapper != null ? wrapper.WrapOffset : wrapPointOffset;
            Vector3 wrapPoint = hit.point + hit.normal * offset;

            dynamicAnchor = flyingHook;
            anchorPoint = flyingHook.position;
            currentRopeLength = Mathf.Clamp(Vector3.Distance(origin, wrapPoint) + attachSlack, minRopeLength, maxRopeLength);
            currentSwingEffort = maxSwingEffort;
            attachTime = Time.time;
            constraintGraceFramesRemaining = attachConstraintGraceFrames;
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

    bool IsPlayerGroundedOnRopeCollision()
    {
        if (player == null) return false;
        RaycastHit hit;
        return Physics.Raycast(player.position + Vector3.up * 0.15f, Vector3.down, out hit, groundedWrapIgnoreDistance, ropeCollisionMask, QueryTriggerInteraction.Ignore)
            && !IsPlayerOrHookCollider(hit.collider);
    }

    bool IsPlayerStandingOnCollider(Collider collider)
    {
        if (player == null || collider == null) return false;
        RaycastHit hit;
        if (!Physics.Raycast(player.position + Vector3.up * 0.15f, Vector3.down, out hit, groundedWrapIgnoreDistance, ropeCollisionMask, QueryTriggerInteraction.Ignore)) return false;
        if (hit.collider == collider) return true;
        return hit.collider != null && hit.collider.transform.root == collider.transform.root;
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
        if (Time.time - attachTime < attachInputGraceTime) return;
        float delta = 0f;
        if (PrototypeInput.ShiftHeld) delta -= ropeClimbSpeed * Time.deltaTime;
        if (PrototypeInput.ControlHeld) delta += ropeClimbSpeed * Time.deltaTime;
        currentRopeLength = Mathf.Clamp(currentRopeLength + delta, minRopeLength, maxRopeLength);
    }

    void ApplyConstraint()
    {
        Vector3 origin = GetOrigin();
        Vector3 activeAnchor = GetActiveAnchor();
        Vector3 fromAnchor = origin - activeAnchor;
        float distance = fromAnchor.magnitude;
        if (distance <= 0.001f) return;

        Vector3 ropeDirection = fromAnchor / distance;
        if (distance >= currentRopeLength)
        {
            if (hardConstraint)
            {
                Vector3 constrainedOrigin = activeAnchor + ropeDirection * currentRopeLength;
                Vector3 playerDelta = constrainedOrigin - origin;
                playerRb.MovePosition(playerRb.position + playerDelta);

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

    bool ApplyLedgeAssist()
    {
        if (!ledgeAssistEnabled || playerRb == null || player == null) return false;
        if (wrapPoints.Count > 0 || dynamicAnchor != null) return false;
        if (Time.time - attachTime < attachInputGraceTime) return false;
        if (IsPlayerGroundedOnRopeCollision()) return false;
        if (!PrototypeInput.ShiftHeld)
        {
            isMantlingLedge = false;
            SetMantleCollision(true);
            return false;
        }
        if (currentRopeLength > ledgeAssistRopeLength) return false;

        if (isMantlingLedge)
        {
            UpdateMantle();
            return true;
        }

        Vector3 anchor = GetFinalAnchor();
        float heightAbovePlayer = anchor.y - player.position.y;
        if (heightAbovePlayer < ledgeAssistMinAnchorHeight || heightAbovePlayer > ledgeAssistMaxAnchorHeight) return false;

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
            if (!Physics.Raycast(surfaceProbeStart, Vector3.down, out surfaceHit, ledgeAssistSurfaceProbeDistance, ropeCollisionMask, QueryTriggerInteraction.Ignore)) return false;
        }
        if (Vector3.Dot(surfaceHit.normal, Vector3.up) < 0.65f) return false;

        StartMantle(inward, surfaceHit.point);
        UpdateMantle();
        return true;
    }

    void StartMantle(Vector3 inward, Vector3 topPoint)
    {
        float standHeight = GetPlayerStandHeight();
        float sideClearance = GetPlayerRadius() + ledgeAssistSideClearance;
        Vector3 edgePoint = FindLedgeEdge(topPoint, inward);

        mantleSideLow = edgePoint - inward * sideClearance;
        mantleSideLow.y = Mathf.Min(playerRb.position.y, topPoint.y + standHeight);
        mantleSideHigh = new Vector3(mantleSideLow.x, topPoint.y + standHeight, mantleSideLow.z);
        mantleTop = topPoint + inward * sideClearance + Vector3.up * standHeight;
        mantleStage = 0;
        isMantlingLedge = true;
        SetMantleCollision(false);
    }

    Vector3 FindLedgeEdge(Vector3 topPoint, Vector3 inward)
    {
        Vector3 lastGround = topPoint;
        int steps = 8;
        for (int i = 1; i <= steps; i++)
        {
            float distance = ledgeAssistEdgeSearchDistance * (i / (float)steps);
            Vector3 candidate = topPoint - inward * distance;
            Vector3 probeStart = candidate + Vector3.up * ledgeAssistSurfaceProbeHeight;
            RaycastHit hit;
            if (Physics.Raycast(probeStart, Vector3.down, out hit, ledgeAssistSurfaceProbeDistance, ropeCollisionMask, QueryTriggerInteraction.Ignore)
                && Vector3.Dot(hit.normal, Vector3.up) >= 0.65f)
            {
                lastGround = hit.point;
            }
            else
            {
                return lastGround;
            }
        }

        return lastGround;
    }

    void UpdateMantle()
    {
        Vector3 target = mantleStage == 0 ? mantleSideLow : mantleStage == 1 ? mantleSideHigh : mantleTop;
        Vector3 nextPosition = Vector3.MoveTowards(playerRb.position, target, ledgeAssistMoveSpeed * Time.fixedDeltaTime);
        if (Vector3.Distance(nextPosition, target) <= ledgeAssistSnapDistance) nextPosition = target;

        playerRb.MovePosition(nextPosition);

        Vector3 desiredVelocity = target - playerRb.position;
        if (desiredVelocity.sqrMagnitude > 0.01f)
        {
            Vector3 mantleVelocity = desiredVelocity.normalized * Mathf.Min(ledgeAssistMoveSpeed, desiredVelocity.magnitude / Time.fixedDeltaTime);
            playerRb.linearVelocity = Vector3.Lerp(playerRb.linearVelocity, mantleVelocity, 0.55f);
        }

        if (Vector3.Distance(nextPosition, target) <= ledgeAssistStageTolerance)
        {
            mantleStage++;
            if (mantleStage > 2)
            {
                isMantlingLedge = false;
                SetMantleCollision(true);
                playerRb.linearVelocity = Vector3.ProjectOnPlane(playerRb.linearVelocity, Vector3.up);
                Detach();
                return;
            }
        }

        currentRopeLength = Mathf.Clamp(Mathf.Min(currentRopeLength, Vector3.Distance(GetOrigin(), GetFinalAnchor())), minRopeLength, maxRopeLength);
    }

    float GetPlayerStandHeight()
    {
        CapsuleCollider capsule = player != null ? player.GetComponent<CapsuleCollider>() : null;
        if (capsule == null) return 1f;
        return Mathf.Max(0.5f, capsule.height * 0.5f + 0.05f);
    }

    float GetPlayerRadius()
    {
        CapsuleCollider capsule = player != null ? player.GetComponent<CapsuleCollider>() : null;
        if (capsule == null) return 0.5f;
        return Mathf.Max(0.25f, capsule.radius);
    }

    void SetMantleCollision(bool enabled)
    {
        if (playerCollider == null && player != null) playerCollider = player.GetComponent<Collider>();
        if (playerCollider == null) return;

        if (!enabled)
        {
            if (mantleCollisionSuppressed) return;
            playerColliderWasEnabled = playerCollider.enabled;
            playerCollider.enabled = false;
            mantleCollisionSuppressed = true;
            return;
        }

        if (!mantleCollisionSuppressed) return;
        playerCollider.enabled = playerColliderWasEnabled;
        mantleCollisionSuppressed = false;
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

        bool tryingToPowerUp = force.y > 0f || Vector3.Dot(desiredDirection, Vector3.up) > 0.15f;
        if (poweredAngle > maxPoweredAngleFromDown && tryingToPowerUp)
        {
            force *= upwardSwingForceMultiplier;
        }

        if (force.y > 0f)
        {
            force.y *= Mathf.Lerp(upwardSwingForceMultiplier, 1f, Mathf.Clamp01(1f - poweredAngle / maxPoweredAngleFromDown));
        }

        playerRb.AddForce(force, ForceMode.Acceleration);
        currentSwingEffort = Mathf.Max(0f, currentSwingEffort - swingEffortDrain * Time.fixedDeltaTime);
    }

    void ApplyHighAngleSwingLimit()
    {
        Vector3 ropeDirection = ((player != null ? player.position : GetOrigin()) - GetActiveAnchor()).normalized;
        float angleFromDown = Vector3.Angle(-Vector3.up, ropeDirection);
        if (angleFromDown <= hardSwingAngleFromDown) return;

        Vector3 downAlongArc = Vector3.ProjectOnPlane(Vector3.down, ropeDirection);
        if (downAlongArc.sqrMagnitude > 0.001f)
        {
            playerRb.AddForce(downAlongArc.normalized * highAngleBrakeForce, ForceMode.Acceleration);
        }

        Vector3 tangentialVelocity = Vector3.ProjectOnPlane(playerRb.linearVelocity, ropeDirection);
        if (tangentialVelocity.y > 0f)
        {
            Vector3 upwardVelocity = Vector3.Project(tangentialVelocity, Vector3.up);
            playerRb.linearVelocity -= upwardVelocity * Mathf.Clamp01(highAngleVelocityDamping);
        }
    }

    void ClampSwingAngle()
    {
        Vector3 origin = GetOrigin();
        Vector3 activeAnchor = GetActiveAnchor();
        Vector3 fromAnchor = origin - activeAnchor;
        float distance = fromAnchor.magnitude;
        if (distance <= 0.001f) return;

        Vector3 ropeDirection = fromAnchor / distance;
        float angleFromDown = Vector3.Angle(Vector3.down, ropeDirection);
        if (angleFromDown <= hardSwingAngleFromDown) return;

        Vector3 horizontal = Vector3.ProjectOnPlane(ropeDirection, Vector3.up);
        if (horizontal.sqrMagnitude < 0.001f) horizontal = Vector3.ProjectOnPlane(playerRb.linearVelocity, Vector3.up);
        if (horizontal.sqrMagnitude < 0.001f) horizontal = Vector3.forward;
        horizontal.Normalize();

        float limitedRadians = hardSwingAngleFromDown * Mathf.Deg2Rad;
        Vector3 limitedDirection = horizontal * Mathf.Sin(limitedRadians) + Vector3.down * Mathf.Cos(limitedRadians);
        Vector3 limitedOrigin = activeAnchor + limitedDirection.normalized * Mathf.Min(distance, currentRopeLength);
        Vector3 playerDelta = limitedOrigin - origin;
        playerRb.MovePosition(playerRb.position + playerDelta);

        Vector3 limitedRopeDirection = (limitedOrigin - activeAnchor).normalized;
        Vector3 tangentialVelocity = Vector3.ProjectOnPlane(playerRb.linearVelocity, limitedRopeDirection);
        Vector3 downAlongArc = Vector3.ProjectOnPlane(Vector3.down, limitedRopeDirection);
        if (downAlongArc.sqrMagnitude > 0.001f)
        {
            downAlongArc.Normalize();
            float downSpeed = Vector3.Dot(tangentialVelocity, downAlongArc);
            if (downSpeed < 0f)
            {
                tangentialVelocity -= downAlongArc * downSpeed;
            }
        }
        tangentialVelocity = Vector3.ClampMagnitude(tangentialVelocity, maxTangentialSpeedAtAngleLimit);

        Vector3 radialVelocity = Vector3.Project(playerRb.linearVelocity, limitedRopeDirection);
        if (Vector3.Dot(radialVelocity, limitedRopeDirection) > 0f) radialVelocity = Vector3.zero;
        playerRb.linearVelocity = tangentialVelocity + radialVelocity;
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
