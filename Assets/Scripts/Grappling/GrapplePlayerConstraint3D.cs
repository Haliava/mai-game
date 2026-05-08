using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-50)]
public sealed class GrapplePlayerConstraint3D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GrapplingHookController3D grappleController;
    [SerializeField] private FPSCharacterController3D fpsController;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Rigidbody playerRigidbody;

    [Header("Tension")]
    [SerializeField, Min(0f)] private float pullStrength = 0f;
    [SerializeField, Min(0f)] private float swingForceMultiplier = 1f;
    [SerializeField, Min(0f)] private float damping = 10f;
    [SerializeField, Min(0f)] private float maxCorrectionSpeed = 80f;
    [SerializeField, Min(0f)] private float slackTolerance = 0.03f;
    [SerializeField, Min(0f)] private float minPullDistance = 0.25f;
    [SerializeField, Min(1)] private int constraintSolverIterations = 3;

    [Header("Swing")]
    [SerializeField, Min(0f)] private float swingForce = 12f;
    [SerializeField, Min(0f)] private float maxSwingAcceleration = 12f;
    [SerializeField, Min(0f)] private float swingDamping = 0.15f;
    [SerializeField, Range(0f, 1f)] private float upwardSwingLimit = 0.05f;
    [SerializeField, Range(0f, 1f)] private float horizontalRopeUpwardPenalty = 1f;
    [SerializeField, Min(0f)] private float maxPlayerSwingSpeed = 16f;

    [Header("Reel")]
    [SerializeField, Min(0f)] private float reelInSpeed = 8f;
    [SerializeField, Min(0f)] private float reelOutSpeed = 8f;
    [SerializeField, Min(0f)] private float minRopeLength = 2f;

    [Header("Mantle")]
    [SerializeField] private bool enableMantleAssist = true;
    [SerializeField] private bool autoReleaseAfterMantle = true;
    [SerializeField, Min(0f)] private float mantleTriggerDistance = 1.5f;
    [SerializeField, Min(0f)] private float mantleMinReelInAmount = 1f;
    [SerializeField, Min(0.05f)] private float mantleDuration = 0.25f;
    [SerializeField, Min(0f)] private float mantleUpOffset = 0.6f;
    [SerializeField, Min(0f)] private float mantleForwardOffset = 0.8f;
    [SerializeField, Min(0f)] private float mantleUpwardImpulse = 3f;
    [SerializeField, Min(0f)] private float mantleInwardImpulse = 2f;
    [SerializeField, Min(0.01f)] private float mantleClearanceRadius = 0.4f;
    [SerializeField, Min(0.1f)] private float mantleClearanceHeight = 1.8f;
    [FormerlySerializedAs("mantleCollisionMask")]
    [SerializeField] private LayerMask environmentMask = ~0;

    [Header("Mantle From Wrap Points")]
    [SerializeField] private bool enableMantleFromWrapPoints = true;
    [SerializeField, Min(0f)] private float wrapMantleTriggerDistance = 1.6f;
    [SerializeField, Min(0f)] private float wrapMantleForwardOffset = 0.8f;
    [SerializeField, Min(0f)] private float wrapMantleUpOffset = 0.6f;
    [SerializeField, Min(0.05f)] private float wrapMantleDuration = 0.25f;
    [SerializeField, Min(0f)] private float wrapMantleUpwardImpulse = 3f;
    [SerializeField, Min(0f)] private float wrapMantleInwardImpulse = 2f;
    [SerializeField, Range(0f, 1f)] private float mantleTopNormalThreshold = 0.65f;
    [SerializeField] private bool preferNearestWrapPointForMantle = true;
    [SerializeField] private bool autoReleaseAfterWrapMantle = true;

    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = true;

    private Vector3 lastConstraintTarget;
    private float lastAllowedDistance;
    private bool hasActiveConstraint;
    private GrappleHookProjectile3D trackedLatchedHook;
    private float latchedInitialRopeLength;
    private bool mantleActive;
    private float mantleElapsed;
    private Vector3 mantleStartPosition;
    private Vector3 mantleLiftPosition;
    private Vector3 mantleOverEdgePosition;
    private Vector3 mantleTargetPosition;
    private Vector3 mantleInwardDirection;
    private Collider mantleSupportCollider;
    private bool mantleStartedFromWrapPoint;
    private bool mantleAutoReleaseOnComplete;
    private float activeMantleDuration;
    private float activeMantleUpwardImpulse;
    private float activeMantleInwardImpulse;
    private Vector3 lastMantleLatchPoint;
    private Vector3 lastHookMantleCandidatePoint;
    private Vector3 lastSelectedMantleCandidatePoint;
    private Vector3 lastMantleTopNormal;
    private readonly List<Vector3> lastWrapMantleCandidatePoints = new();
    private readonly List<Vector3> lastRejectedWrapMantleCandidatePoints = new();
    private Vector3 lastMantleTargetPosition;
    private Vector3 lastMantleLiftPosition;
    private Vector3 lastMantleOverEdgePosition;
    private Vector3 lastMantleInwardDirection;
    private Vector3 lastMantleCapsuleBottom;
    private Vector3 lastMantleCapsuleTop;
    private float lastMantleCapsuleRadius;
    private float lastMantleDistanceToLatch;
    private float lastMantleCurrentRopeLength;
    private bool lastMantleIsReelingIn;
    private bool lastMantleIsNearTopEdge;
    private bool lastMantleHasValidTarget;
    private bool lastMantleBlockedByGeometry;
    private bool lastMantleStarted;
    private string lastMantleReason = "None";
    private float nextMantleDebugLogTime;

    private enum MantleCandidateSource
    {
        HookLatch,
        WrapPoint
    }

    private struct MantleCandidate
    {
        public MantleCandidateSource Source;
        public Vector3 Point;
        public Collider SupportCollider;
        public GrappleEdgeType EdgeType;
        public Vector3 PreferredInwardDirection;
        public int RopePointIndex;
        public float TriggerDistance;
        public float ForwardOffset;
        public float UpOffset;
        public float Duration;
        public float UpwardImpulse;
        public float InwardImpulse;
        public bool AutoReleaseOnComplete;
    }

    private struct MantleTargetInfo
    {
        public Vector3 TargetPosition;
        public Vector3 LiftPosition;
        public Vector3 OverEdgePosition;
        public Vector3 InwardDirection;
        public Vector3 CandidatePoint;
        public Vector3 TopNormal;
        public Vector3 SurfacePoint;
        public Collider SupportCollider;
        public MantleCandidateSource Source;
        public int RopePointIndex;
        public float UpOffset;
        public float Duration;
        public float UpwardImpulse;
        public float InwardImpulse;
        public bool AutoReleaseOnComplete;
    }

    public float PullStrength => pullStrength;
    public float SwingForceMultiplier => swingForceMultiplier;
    public float Damping => damping;
    public float MaxCorrectionSpeed => maxCorrectionSpeed;
    public float SlackTolerance => slackTolerance;
    public bool HasActiveConstraint => hasActiveConstraint;
    public bool IsMantling => mantleActive;

    private void Reset()
    {
        CacheReferences();
    }

    private void OnValidate()
    {
        pullStrength = Mathf.Max(0f, pullStrength);
        swingForceMultiplier = Mathf.Max(0f, swingForceMultiplier);
        damping = Mathf.Max(0f, damping);
        maxCorrectionSpeed = Mathf.Max(0f, maxCorrectionSpeed);
        slackTolerance = Mathf.Max(0f, slackTolerance);
        minPullDistance = Mathf.Max(0f, minPullDistance);
        constraintSolverIterations = Mathf.Max(1, constraintSolverIterations);
        swingForce = Mathf.Max(0f, swingForce);
        maxSwingAcceleration = Mathf.Max(0f, maxSwingAcceleration);
        swingDamping = Mathf.Max(0f, swingDamping);
        maxPlayerSwingSpeed = Mathf.Max(0f, maxPlayerSwingSpeed);
        reelInSpeed = Mathf.Max(0f, reelInSpeed);
        reelOutSpeed = Mathf.Max(0f, reelOutSpeed);
        minRopeLength = Mathf.Max(0f, minRopeLength);
        mantleTriggerDistance = Mathf.Max(0f, mantleTriggerDistance);
        mantleMinReelInAmount = Mathf.Max(0f, mantleMinReelInAmount);
        mantleDuration = Mathf.Max(0.05f, mantleDuration);
        mantleUpOffset = Mathf.Max(0f, mantleUpOffset);
        mantleForwardOffset = Mathf.Max(0f, mantleForwardOffset);
        mantleUpwardImpulse = Mathf.Max(0f, mantleUpwardImpulse);
        mantleInwardImpulse = Mathf.Max(0f, mantleInwardImpulse);
        mantleClearanceRadius = Mathf.Max(0.01f, mantleClearanceRadius);
        mantleClearanceHeight = Mathf.Max(0.1f, mantleClearanceHeight);
        wrapMantleTriggerDistance = Mathf.Max(0f, wrapMantleTriggerDistance);
        wrapMantleForwardOffset = Mathf.Max(0f, wrapMantleForwardOffset);
        wrapMantleUpOffset = Mathf.Max(0f, wrapMantleUpOffset);
        wrapMantleDuration = Mathf.Max(0.05f, wrapMantleDuration);
        wrapMantleUpwardImpulse = Mathf.Max(0f, wrapMantleUpwardImpulse);
        wrapMantleInwardImpulse = Mathf.Max(0f, wrapMantleInwardImpulse);
        mantleTopNormalThreshold = Mathf.Clamp01(mantleTopNormalThreshold);
        CacheReferences();
    }

    private void Awake()
    {
        CacheReferences();
    }

    private void Update()
    {
        bool characterControlled = playerRigidbody == null || playerRigidbody.isKinematic;
        bool hasConstraint = TryGetConstraint(out Transform anchor, out Vector3 targetPosition, out float allowedDistance);
        UpdateLatchedLengthTracking(hasConstraint);

        if (fpsController != null)
        {
            fpsController.SetGrappleMovementControlActive(characterControlled && (hasConstraint || mantleActive));
        }

        if (mantleActive)
        {
            if (!hasConstraint)
            {
                EndMantle(false);
                return;
            }

            UpdateMantle(Time.deltaTime);
            return;
        }

        if (characterControlled && hasConstraint)
        {
            ApplyRopeReel(Time.deltaTime);
            if (TryBeginMantle(anchor.position))
            {
                return;
            }

            ApplyCharacterSwing(anchor.position, targetPosition, Time.deltaTime);
        }
    }

    private void LateUpdate()
    {
        if (mantleActive || (playerRigidbody != null && !playerRigidbody.isKinematic))
        {
            return;
        }

        ApplyCharacterControllerConstraint(Time.deltaTime);
    }

    private void FixedUpdate()
    {
        if (playerRigidbody == null || playerRigidbody.isKinematic)
        {
            return;
        }

        if (TryGetConstraint(out Transform anchor, out Vector3 targetPosition, out float allowedDistance))
        {
            ApplyRopeReel(Time.fixedDeltaTime);
            ApplyRigidbodySwing(anchor.position, targetPosition);
        }

        ApplyRigidbodyConstraint(Time.fixedDeltaTime);
    }

    private void OnDisable()
    {
        if (fpsController != null)
        {
            fpsController.SetGrappleMovementControlActive(false);
            fpsController.SetGrappleMovementPaused(false);
        }
    }

    public Vector3 CalculateCharacterExternalVelocity(
        Vector3 anchorPosition,
        Vector3 targetPosition,
        float allowedDistance,
        Vector3 currentVelocity,
        float deltaTime)
    {
        Vector3 correction = CalculateNonElasticCorrection(anchorPosition, targetPosition, allowedDistance, deltaTime);
        if (deltaTime <= 0f || correction.sqrMagnitude <= 0.000001f)
        {
            return Vector3.zero;
        }

        return correction / deltaTime;
    }

    public Vector3 CalculateNonElasticCorrection(
        Vector3 anchorPosition,
        Vector3 targetPosition,
        float allowedDistance,
        float deltaTime)
    {
        if (deltaTime <= 0f)
        {
            return Vector3.zero;
        }

        Vector3 toTarget = targetPosition - anchorPosition;
        float distance = toTarget.magnitude;
        if (distance <= 0.0001f)
        {
            return Vector3.zero;
        }

        Vector3 directionToTarget = toTarget / distance;
        float safeAllowedDistance = Mathf.Max(0f, allowedDistance);
        float slack = distance - safeAllowedDistance;
        if (slack <= slackTolerance)
        {
            return Vector3.zero;
        }

        return directionToTarget * slack;
    }

    public Vector3 CalculateSwingAcceleration(
        Vector3 anchorPosition,
        Vector3 targetPosition,
        Transform cameraTransform,
        Vector2 moveInput)
    {
        if (moveInput.sqrMagnitude <= 0.0001f || swingForce <= 0f)
        {
            return Vector3.zero;
        }

        Vector3 ropeVector = anchorPosition - targetPosition;
        if (ropeVector.sqrMagnitude <= 0.0001f)
        {
            return Vector3.zero;
        }

        Vector3 ropeDirectionAwayFromTarget = ropeVector.normalized;
        Vector3 cameraForward = cameraTransform != null ? cameraTransform.forward : transform.forward;
        Vector3 cameraRight = cameraTransform != null ? cameraTransform.right : transform.right;
        Vector3 wishDirection = cameraRight * moveInput.x + cameraForward * moveInput.y;
        if (wishDirection.sqrMagnitude <= 0.0001f)
        {
            return Vector3.zero;
        }

        Vector3 tangentDirection = Vector3.ProjectOnPlane(wishDirection, ropeDirectionAwayFromTarget);
        if (tangentDirection.sqrMagnitude <= 0.0001f)
        {
            return Vector3.zero;
        }

        tangentDirection.Normalize();
        tangentDirection = LimitUpwardSwingDirection(tangentDirection, ropeDirectionAwayFromTarget);
        if (tangentDirection.sqrMagnitude <= 0.0001f)
        {
            return Vector3.zero;
        }

        float acceleration = maxSwingAcceleration > 0f
            ? Mathf.Min(swingForce, maxSwingAcceleration)
            : swingForce;

        return Vector3.ClampMagnitude(tangentDirection, 1f) * acceleration;
    }

    private void CacheReferences()
    {
        if (grappleController == null)
        {
            grappleController = GetComponent<GrapplingHookController3D>();
        }

        if (fpsController == null)
        {
            fpsController = GetComponent<FPSCharacterController3D>();
        }

        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
        }

        if (playerRigidbody == null)
        {
            playerRigidbody = GetComponent<Rigidbody>();
        }
    }

    private void ApplyCharacterSwing(Vector3 anchorPosition, Vector3 targetPosition, float deltaTime)
    {
        if (fpsController == null || deltaTime <= 0f)
        {
            return;
        }

        Transform cameraTransform = fpsController.PlayerCamera != null ? fpsController.PlayerCamera.transform : transform;
        Vector3 acceleration = CalculateSwingAcceleration(anchorPosition, targetPosition, cameraTransform, fpsController.GetMoveInput());
        if (acceleration.sqrMagnitude > 0.000001f)
        {
            fpsController.AddVelocity(acceleration * deltaTime);
        }

        Vector3 ropeDirection = anchorPosition - targetPosition;
        if (ropeDirection.sqrMagnitude > 0.0001f && swingDamping > 0f)
        {
            fpsController.DampVelocityOnPlane(ropeDirection.normalized, swingDamping, deltaTime);
        }

        fpsController.ClampCurrentVelocity(maxPlayerSwingSpeed);
    }

    private void ApplyRigidbodySwing(Vector3 anchorPosition, Vector3 targetPosition)
    {
        if (playerRigidbody == null)
        {
            return;
        }

        Vector2 moveInput = fpsController != null ? fpsController.GetMoveInput() : Vector2.zero;
        Transform cameraTransform = fpsController != null && fpsController.PlayerCamera != null ? fpsController.PlayerCamera.transform : transform;
        Vector3 acceleration = CalculateSwingAcceleration(anchorPosition, targetPosition, cameraTransform, moveInput);
        if (acceleration.sqrMagnitude > 0.000001f)
        {
            playerRigidbody.AddForce(acceleration, ForceMode.Acceleration);
        }

        if (maxPlayerSwingSpeed > 0f && playerRigidbody.linearVelocity.magnitude > maxPlayerSwingSpeed)
        {
            playerRigidbody.linearVelocity = playerRigidbody.linearVelocity.normalized * maxPlayerSwingSpeed;
        }
    }

    private void ApplyRopeReel(float deltaTime)
    {
        if (deltaTime <= 0f || grappleController == null || !grappleController.IsLatched)
        {
            return;
        }

        GrappleRope3D rope = grappleController.Rope;
        if (rope == null || !rope.IsActive)
        {
            return;
        }

        bool reelInHeld = IsReelInHeld();
        bool reelOutHeld = IsReelOutHeld();
        if (reelInHeld == reelOutHeld)
        {
            return;
        }

        if (reelInHeld)
        {
            rope.AdjustCurrentLength(-reelInSpeed * deltaTime, minRopeLength);
            return;
        }

        rope.AdjustCurrentLength(reelOutSpeed * deltaTime, minRopeLength);
    }

    private bool TryBeginMantle(Vector3 anchorPosition)
    {
        bool isReelingIn = IsReelInHeld();
        if (!enableMantleAssist || mantleActive || !isReelingIn || characterController == null || grappleController == null)
        {
            RecordMantleDebug("DisabledOrNotReeling", 0f, 0f, isReelingIn, false, false, false, false);
            return false;
        }

        GrappleHookProjectile3D hook = grappleController.ActiveHook;
        GrappleRope3D rope = grappleController.Rope;
        if (hook == null || !hook.IsLatched || rope == null || !rope.IsActive)
        {
            RecordMantleDebug("NoLatchedRopeForMantle", 0f, rope != null ? rope.CurrentRopeLength : 0f, isReelingIn, false, false, false, false);
            return false;
        }

        if (!TryFindMantleCandidate(anchorPosition, hook, rope, out MantleTargetInfo targetInfo, out string failReason, out float distanceToCandidate, out bool closeEnough, out bool blockedByGeometry))
        {
            RecordMantleDebug(failReason, distanceToCandidate, rope.CurrentRopeLength, true, closeEnough, false, blockedByGeometry, false);
            return false;
        }

        string startedReason = targetInfo.Source == MantleCandidateSource.WrapPoint
            ? "StartedMantleAssistFromWrapPoint"
            : "StartedMantleAssistFromHookLatch";
        mantleStartedFromWrapPoint = targetInfo.Source == MantleCandidateSource.WrapPoint;
        lastSelectedMantleCandidatePoint = targetInfo.CandidatePoint;
        lastMantleTopNormal = targetInfo.TopNormal;
        RecordMantleDebug(startedReason, distanceToCandidate, rope.CurrentRopeLength, true, closeEnough, true, false, true);
        BeginMantle(targetInfo);
        return true;
    }

    private bool TryFindMantleCandidate(
        Vector3 anchorPosition,
        GrappleHookProjectile3D hook,
        GrappleRope3D rope,
        out MantleTargetInfo targetInfo,
        out string failReason,
        out float distanceToCandidate,
        out bool closeEnough,
        out bool blockedByGeometry)
    {
        targetInfo = default;
        failReason = "NoMantleCandidate";
        distanceToCandidate = 0f;
        closeEnough = false;
        blockedByGeometry = false;

        lastWrapMantleCandidatePoints.Clear();
        lastRejectedWrapMantleCandidatePoints.Clear();
        lastHookMantleCandidatePoint = hook != null ? hook.LatchPoint : Vector3.zero;

        bool foundValidWrapCandidate = false;
        float bestWrapScore = float.PositiveInfinity;
        MantleTargetInfo bestWrapTarget = default;
        string lastWrapFailReason = enableMantleFromWrapPoints ? "NoWrapPoints" : "WrapMantleDisabled";
        float lastWrapDistance = 0f;
        bool lastWrapCloseEnough = false;
        bool lastWrapBlocked = false;

        if (enableMantleFromWrapPoints && rope != null && rope.IsActive)
        {
            for (int i = 0; i < rope.WrapPointCount; i++)
            {
                if (!rope.TryGetWrapPointInfo(i, out GrappleRope3D.WrapPointInfo wrapPoint))
                {
                    lastWrapFailReason = "NoColliderMetadata";
                    LogMantleCandidateDebug($"Checking mantle from wrap point: index={i}, reason=no collider metadata");
                    continue;
                }

                lastWrapMantleCandidatePoints.Add(wrapPoint.Position);

                if (preferNearestWrapPointForMantle && i > 0)
                {
                    lastWrapFailReason = "CandidateBehindNearestWrapPoint";
                    lastRejectedWrapMantleCandidatePoints.Add(wrapPoint.Position);
                    LogMantleCandidateDebug($"Checking mantle from wrap point: index={i}, edgeType={wrapPoint.EdgeType}, reason=candidate behind/invalid");
                    continue;
                }

                MantleCandidate wrapCandidate = new()
                {
                    Source = MantleCandidateSource.WrapPoint,
                    Point = wrapPoint.Position,
                    SupportCollider = wrapPoint.Collider,
                    EdgeType = wrapPoint.EdgeType,
                    PreferredInwardDirection = CalculatePreferredWrapInwardDirection(wrapPoint),
                    RopePointIndex = i,
                    TriggerDistance = wrapMantleTriggerDistance,
                    ForwardOffset = wrapMantleForwardOffset,
                    UpOffset = wrapMantleUpOffset,
                    Duration = wrapMantleDuration,
                    UpwardImpulse = wrapMantleUpwardImpulse,
                    InwardImpulse = wrapMantleInwardImpulse,
                    AutoReleaseOnComplete = autoReleaseAfterWrapMantle
                };

                if (TryEvaluateMantleCandidate(
                        wrapCandidate,
                        anchorPosition,
                        rope,
                        out MantleTargetInfo wrapTarget,
                        out string wrapFailReason,
                        out bool wrapBlocked,
                        out float wrapDistance,
                        out bool wrapCloseEnough,
                        out float wrapScore))
                {
                    if (wrapScore < bestWrapScore)
                    {
                        bestWrapScore = wrapScore;
                        bestWrapTarget = wrapTarget;
                        foundValidWrapCandidate = true;
                    }
                }
                else
                {
                    lastRejectedWrapMantleCandidatePoints.Add(wrapPoint.Position);
                    lastWrapFailReason = wrapFailReason;
                    lastWrapDistance = wrapDistance;
                    lastWrapCloseEnough = wrapCloseEnough;
                    lastWrapBlocked = wrapBlocked;
                }
            }
        }

        if (foundValidWrapCandidate)
        {
            targetInfo = bestWrapTarget;
            distanceToCandidate = Vector3.Distance(anchorPosition, targetInfo.CandidatePoint);
            closeEnough = true;
            blockedByGeometry = false;
            LogMantleCandidateDebug($"selected mantle candidate: source=wrap point, index={targetInfo.RopePointIndex}, point={targetInfo.CandidatePoint:F3}");
            return true;
        }

        MantleCandidate hookCandidate = new()
        {
            Source = MantleCandidateSource.HookLatch,
            Point = hook.LatchPoint,
            SupportCollider = hook.LatchedCollider,
            EdgeType = hook.LatchedEdgeType,
            PreferredInwardDirection = Vector3.zero,
            RopePointIndex = -1,
            TriggerDistance = mantleTriggerDistance,
            ForwardOffset = mantleForwardOffset,
            UpOffset = mantleUpOffset,
            Duration = mantleDuration,
            UpwardImpulse = mantleUpwardImpulse,
            InwardImpulse = mantleInwardImpulse,
            AutoReleaseOnComplete = autoReleaseAfterMantle
        };

        if (TryEvaluateMantleCandidate(
                hookCandidate,
                anchorPosition,
                rope,
                out targetInfo,
                out failReason,
                out blockedByGeometry,
                out distanceToCandidate,
                out closeEnough,
                out _))
        {
            LogMantleCandidateDebug($"selected mantle candidate: source=hook latch, point={targetInfo.CandidatePoint:F3}");
            return true;
        }

        if (lastWrapFailReason != "NoWrapPoints" && lastWrapFailReason != "WrapMantleDisabled")
        {
            failReason = lastWrapFailReason;
            distanceToCandidate = lastWrapDistance;
            closeEnough = lastWrapCloseEnough;
            blockedByGeometry = lastWrapBlocked;
        }

        return false;
    }

    private bool TryEvaluateMantleCandidate(
        MantleCandidate candidate,
        Vector3 anchorPosition,
        GrappleRope3D rope,
        out MantleTargetInfo targetInfo,
        out string failReason,
        out bool blockedByGeometry,
        out float distanceToCandidate,
        out bool closeEnough,
        out float score)
    {
        targetInfo = default;
        failReason = "InvalidMantleCandidate";
        blockedByGeometry = false;
        distanceToCandidate = Vector3.Distance(anchorPosition, candidate.Point);
        closeEnough = false;
        score = float.PositiveInfinity;

        string sourceName = candidate.Source == MantleCandidateSource.WrapPoint ? "wrap point" : "hook latch";
        LogMantleCandidateDebug(
            $"Checking mantle from {sourceName}: index={candidate.RopePointIndex}, edgeType={candidate.EdgeType}, distance={distanceToCandidate:F2}, isReelingIn={IsReelInHeld()}");

        if (candidate.SupportCollider == null)
        {
            failReason = "NoColliderMetadata";
            return false;
        }

        if (candidate.EdgeType != GrappleEdgeType.TopSide)
        {
            failReason = "EdgeTypeNotMantleEligible";
            return false;
        }

        bool ropeNearMinimum = candidate.Source == MantleCandidateSource.HookLatch &&
                               rope.CurrentRopeLength <= minRopeLength + slackTolerance + 0.35f;
        bool reeledEnough = latchedInitialRopeLength - rope.CurrentRopeLength >= mantleMinReelInAmount;
        if (candidate.Source == MantleCandidateSource.WrapPoint)
        {
            float remainingLengthToWrap = rope.RemainingLengthForStartSegment;
            closeEnough = distanceToCandidate <= candidate.TriggerDistance || remainingLengthToWrap <= candidate.TriggerDistance;
            bool ropeTenseTowardWrap = distanceToCandidate >= Mathf.Max(0f, remainingLengthToWrap - slackTolerance - 0.05f);
            if (!ropeTenseTowardWrap)
            {
                failReason = "RopeNotTense";
                return false;
            }
        }
        else
        {
            closeEnough = distanceToCandidate <= candidate.TriggerDistance || ropeNearMinimum;
        }

        bool playerBelowOrBeside = anchorPosition.y <= candidate.Point.y + candidate.UpOffset;
        if (!closeEnough || (!reeledEnough && !ropeNearMinimum && candidate.Source == MantleCandidateSource.HookLatch) || !playerBelowOrBeside)
        {
            failReason = playerBelowOrBeside ? "TooFarFromMantleCandidate" : "PlayerNotBelowOrSideOfEdge";
            return false;
        }

        if (!TryFindMantleTarget(candidate, anchorPosition, out targetInfo, out failReason, out blockedByGeometry))
        {
            return false;
        }

        score = candidate.Source == MantleCandidateSource.WrapPoint
            ? candidate.RopePointIndex * 100f + distanceToCandidate
            : 10000f + distanceToCandidate;
        return true;
    }

    private bool TryFindMantleTarget(
        MantleCandidate candidate,
        Vector3 anchorPosition,
        out MantleTargetInfo targetInfo,
        out string failReason,
        out bool blockedByGeometry)
    {
        targetInfo = default;
        failReason = "NoTopSurface";
        blockedByGeometry = false;

        if (!TryFindTopMantleSurfacePoint(
                candidate.SupportCollider,
                candidate.Point,
                candidate.PreferredInwardDirection,
                candidate.ForwardOffset,
                candidate.UpOffset,
                mantleTopNormalThreshold,
                out Vector3 surfacePoint,
                out Vector3 inwardDirection,
                out Vector3 topNormal,
                out bool topSurfaceBlocked))
        {
            failReason = topSurfaceBlocked ? "TopSurfaceBlocked" : "NoTopSurface";
            blockedByGeometry = topSurfaceBlocked;
            return false;
        }

        Vector3 targetPosition = GetRootPositionForFootPoint(surfacePoint);
        Vector3 overEdgePosition = targetPosition + Vector3.up * GetMantleLiftHeight(candidate.UpOffset, candidate.UpwardImpulse) + inwardDirection * GetMantleInwardOvershoot(candidate.InwardImpulse);
        Vector3 liftPosition = transform.position;
        liftPosition.y = Mathf.Max(liftPosition.y, overEdgePosition.y);

        if (!HasTopSupport(targetPosition, candidate.SupportCollider))
        {
            failReason = "NoTopSupport";
            return false;
        }

        if (!HasMantleClearanceAt(targetPosition, candidate.SupportCollider))
        {
            failReason = "TargetBlocked";
            blockedByGeometry = true;
            return false;
        }

        if (!HasClearMantleRoute(transform.position, liftPosition, overEdgePosition, targetPosition, candidate.SupportCollider))
        {
            failReason = "PathBlocked";
            blockedByGeometry = true;
            return false;
        }

        targetInfo = new MantleTargetInfo
        {
            TargetPosition = targetPosition,
            LiftPosition = liftPosition,
            OverEdgePosition = overEdgePosition,
            InwardDirection = inwardDirection,
            CandidatePoint = candidate.Point,
            TopNormal = topNormal,
            SurfacePoint = surfacePoint,
            SupportCollider = candidate.SupportCollider,
            Source = candidate.Source,
            RopePointIndex = candidate.RopePointIndex,
            UpOffset = candidate.UpOffset,
            Duration = candidate.Duration,
            UpwardImpulse = candidate.UpwardImpulse,
            InwardImpulse = candidate.InwardImpulse,
            AutoReleaseOnComplete = candidate.AutoReleaseOnComplete
        };
        failReason = "Valid";
        return true;
    }

    private bool TryFindTopMantleSurfacePoint(
        Collider supportCollider,
        Vector3 edgePoint,
        Vector3 preferredInwardDirection,
        float forwardOffset,
        float upOffset,
        float topNormalThreshold,
        out Vector3 surfacePoint,
        out Vector3 inwardDirection,
        out Vector3 topNormal,
        out bool blockedByGeometry)
    {
        surfacePoint = edgePoint;
        inwardDirection = Vector3.zero;
        topNormal = Vector3.zero;
        blockedByGeometry = false;

        Vector3[] candidateDirections = new Vector3[8];
        int candidateDirectionCount = 0;
        AddCandidateDirection(preferredInwardDirection, candidateDirections, ref candidateDirectionCount);

        if (supportCollider is BoxCollider boxCollider)
        {
            Vector3 local = boxCollider.transform.InverseTransformPoint(edgePoint) - boxCollider.center;
            Vector3 extents = boxCollider.size * 0.5f;
            float distanceToXSide = Mathf.Abs(Mathf.Abs(local.x) - extents.x);
            float distanceToZSide = Mathf.Abs(Mathf.Abs(local.z) - extents.z);
            Vector3 inwardLocal = distanceToXSide <= distanceToZSide
                ? new Vector3(-Mathf.Sign(local.x == 0f ? 1f : local.x), 0f, 0f)
                : new Vector3(0f, 0f, -Mathf.Sign(local.z == 0f ? 1f : local.z));

            AddCandidateDirection(Vector3.ProjectOnPlane(boxCollider.transform.TransformDirection(inwardLocal), Vector3.up), candidateDirections, ref candidateDirectionCount);
        }
        else
        {
            Bounds bounds = supportCollider.bounds;
            AddCandidateDirection(Vector3.ProjectOnPlane(bounds.center - edgePoint, Vector3.up), candidateDirections, ref candidateDirectionCount);
            AddCandidateDirection(Vector3.ProjectOnPlane(edgePoint - transform.position, Vector3.up), candidateDirections, ref candidateDirectionCount);
        }

        int initialCount = candidateDirectionCount;
        for (int i = 0; i < initialCount; i++)
        {
            AddCandidateDirection(-candidateDirections[i], candidateDirections, ref candidateDirectionCount);
        }

        for (int i = 0; i < candidateDirectionCount; i++)
        {
            Vector3 candidateInward = candidateDirections[i];
            if (candidateInward.sqrMagnitude <= 0.0001f)
            {
                continue;
            }

            candidateInward.Normalize();
            Vector3 probeBase = edgePoint + candidateInward * forwardOffset;
            Vector3 rayOrigin = probeBase + Vector3.up * (mantleClearanceHeight + upOffset + 0.75f);
            float rayDistance = mantleClearanceHeight + upOffset + 2f;
            if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, rayDistance, environmentMask, QueryTriggerInteraction.Ignore))
            {
                continue;
            }

            Vector3 hitNormal = hit.normal.normalized;
            if (hit.collider == supportCollider && Vector3.Dot(hitNormal, Vector3.up) >= topNormalThreshold)
            {
                surfacePoint = hit.point;
                inwardDirection = candidateInward;
                topNormal = hitNormal;
                return true;
            }

            blockedByGeometry |= hit.collider != null && hit.collider != supportCollider;
        }

        return false;
    }

    private Vector3 CalculatePreferredWrapInwardDirection(GrappleRope3D.WrapPointInfo wrapPoint)
    {
        Vector3 normalA = wrapPoint.NormalA.sqrMagnitude > 0.0001f ? wrapPoint.NormalA.normalized : Vector3.zero;
        Vector3 normalB = wrapPoint.NormalB.sqrMagnitude > 0.0001f ? wrapPoint.NormalB.normalized : Vector3.zero;
        Vector3 sideNormal = Vector3.zero;

        if (Vector3.Dot(normalA, Vector3.up) >= mantleTopNormalThreshold)
        {
            sideNormal = normalB;
        }
        else if (Vector3.Dot(normalB, Vector3.up) >= mantleTopNormalThreshold)
        {
            sideNormal = normalA;
        }

        Vector3 inwardDirection = Vector3.zero;
        if (sideNormal.sqrMagnitude > 0.0001f)
        {
            inwardDirection = Vector3.ProjectOnPlane(-sideNormal, Vector3.up);
        }

        if (inwardDirection.sqrMagnitude <= 0.0001f && wrapPoint.Collider != null)
        {
            inwardDirection = Vector3.ProjectOnPlane(wrapPoint.Collider.bounds.center - wrapPoint.Position, Vector3.up);
        }

        return inwardDirection.sqrMagnitude > 0.0001f ? inwardDirection.normalized : Vector3.zero;
    }

    private static void AddCandidateDirection(Vector3 direction, Vector3[] directions, ref int count)
    {
        if (directions == null || count >= directions.Length || direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        direction = Vector3.ProjectOnPlane(direction, Vector3.up);
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        direction.Normalize();
        for (int i = 0; i < count; i++)
        {
            if (Vector3.Dot(directions[i], direction) > 0.98f)
            {
                return;
            }
        }

        directions[count] = direction;
        count++;
    }

    private bool HasTopSupport(Vector3 targetPosition, Collider supportCollider)
    {
        Vector3 origin = targetPosition + Vector3.up * 0.75f;
        float rayDistance = Mathf.Max(1.5f, mantleClearanceHeight);
        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, rayDistance, environmentMask, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        return hit.collider == supportCollider && Vector3.Dot(hit.normal.normalized, Vector3.up) >= mantleTopNormalThreshold;
    }

    private bool HasMantleClearanceAt(Vector3 rootPosition, Collider supportCollider)
    {
        GetMantleCapsule(rootPosition, out Vector3 bottom, out Vector3 top, out float radius);
        lastMantleCapsuleBottom = bottom;
        lastMantleCapsuleTop = top;
        lastMantleCapsuleRadius = radius;

        Collider[] hits = Physics.OverlapCapsule(bottom, top, radius, environmentMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            if (IsMantleBlockingCollider(hits[i], supportCollider))
            {
                return false;
            }
        }

        return true;
    }

    private bool HasClearMantlePath(Vector3 fromRootPosition, Vector3 toRootPosition, Collider supportCollider)
    {
        Vector3 motion = toRootPosition - fromRootPosition;
        float distance = motion.magnitude;
        if (distance <= 0.0001f)
        {
            return true;
        }

        GetMantleCapsule(fromRootPosition, out Vector3 bottom, out Vector3 top, out float radius);
        RaycastHit[] hits = Physics.CapsuleCastAll(bottom, top, radius, motion / distance, distance, environmentMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            if (IsMantleBlockingCollider(hits[i].collider, supportCollider))
            {
                return false;
            }
        }

        return true;
    }

    private bool HasClearMantleRoute(
        Vector3 startPosition,
        Vector3 liftPosition,
        Vector3 overEdgePosition,
        Vector3 targetPosition,
        Collider supportCollider)
    {
        return HasMantleClearanceAt(liftPosition, supportCollider) &&
               HasMantleClearanceAt(overEdgePosition, supportCollider) &&
               HasMantleClearanceAt(targetPosition, supportCollider) &&
               HasClearMantlePath(startPosition, liftPosition, supportCollider) &&
               HasClearMantlePath(liftPosition, overEdgePosition, supportCollider) &&
               HasClearMantlePath(overEdgePosition, targetPosition, supportCollider);
    }

    private void BeginMantle(MantleTargetInfo targetInfo)
    {
        mantleActive = true;
        mantleElapsed = 0f;
        mantleStartPosition = transform.position;
        mantleLiftPosition = targetInfo.LiftPosition;
        mantleOverEdgePosition = targetInfo.OverEdgePosition;
        mantleTargetPosition = targetInfo.TargetPosition;
        mantleInwardDirection = targetInfo.InwardDirection;
        mantleSupportCollider = targetInfo.SupportCollider;
        mantleStartedFromWrapPoint = targetInfo.Source == MantleCandidateSource.WrapPoint;
        mantleAutoReleaseOnComplete = targetInfo.AutoReleaseOnComplete;
        activeMantleDuration = Mathf.Max(0.05f, targetInfo.Duration);
        activeMantleUpwardImpulse = targetInfo.UpwardImpulse;
        activeMantleInwardImpulse = targetInfo.InwardImpulse;

        lastSelectedMantleCandidatePoint = targetInfo.CandidatePoint;
        lastMantleTargetPosition = mantleTargetPosition;
        lastMantleLiftPosition = mantleLiftPosition;
        lastMantleOverEdgePosition = mantleOverEdgePosition;
        lastMantleInwardDirection = mantleInwardDirection;
        lastMantleTopNormal = targetInfo.TopNormal;
        lastMantleLatchPoint = targetInfo.CandidatePoint;

        if (fpsController != null)
        {
            Vector3 mantleImpulse = Vector3.up * activeMantleUpwardImpulse + mantleInwardDirection * activeMantleInwardImpulse;
            fpsController.SetCurrentVelocity(mantleImpulse);
            fpsController.SetGrappleMovementPaused(true);
            fpsController.SetGrappleMovementControlActive(true);
        }
    }

    private void UpdateMantle(float deltaTime)
    {
        if (!mantleActive)
        {
            return;
        }

        if (deltaTime <= 0f)
        {
            return;
        }

        mantleElapsed += deltaTime;
        float progress = Mathf.Clamp01(mantleElapsed / Mathf.Max(0.05f, activeMantleDuration));
        Vector3 desiredPosition = EvaluateMantlePosition(progress);

        if (!HasMantleClearanceAt(desiredPosition, mantleSupportCollider) || !HasClearMantlePath(transform.position, desiredPosition, mantleSupportCollider))
        {
            RecordMantleDebug("MantleInterruptedByGeometry", lastMantleDistanceToLatch, lastMantleCurrentRopeLength, true, true, true, true, false);
            EndMantle(false);
            return;
        }

        Vector3 delta = desiredPosition - transform.position;
        Vector3 previousPosition = transform.position;
        if (characterController != null)
        {
            characterController.Move(delta);
        }
        else
        {
            transform.position += delta;
        }

        Physics.SyncTransforms();

        if (progress >= 1f)
        {
            float finishTolerance = Mathf.Max(0.12f, characterController != null ? characterController.skinWidth * 3f : 0.12f);
            if (Vector3.Distance(transform.position, mantleTargetPosition) > finishTolerance && Vector3.Distance(previousPosition, transform.position) < delta.magnitude * 0.25f)
            {
                RecordMantleDebug("MantleCouldNotReachTarget", lastMantleDistanceToLatch, lastMantleCurrentRopeLength, true, true, true, true, false);
                EndMantle(false);
                return;
            }

            EndMantle(true);
        }
    }

    private void EndMantle(bool completed)
    {
        mantleActive = false;
        mantleElapsed = 0f;
        mantleSupportCollider = null;
        mantleStartedFromWrapPoint = false;

        if (fpsController != null)
        {
            fpsController.SetGrappleMovementPaused(false);
            fpsController.SetCurrentVelocity(Vector3.zero);
        }

        if (completed && mantleAutoReleaseOnComplete && grappleController != null)
        {
            grappleController.ReleaseGrapple();
        }
    }

    private Vector3 EvaluateMantlePosition(float progress)
    {
        float clampedProgress = Mathf.Clamp01(progress);
        if (clampedProgress < 0.4f)
        {
            float segmentProgress = Smooth01(clampedProgress / 0.4f);
            return Vector3.Lerp(mantleStartPosition, mantleLiftPosition, segmentProgress);
        }

        if (clampedProgress < 0.78f)
        {
            float segmentProgress = Smooth01((clampedProgress - 0.4f) / 0.38f);
            return Vector3.Lerp(mantleLiftPosition, mantleOverEdgePosition, segmentProgress);
        }

        float finalProgress = Smooth01((clampedProgress - 0.78f) / 0.22f);
        return Vector3.Lerp(mantleOverEdgePosition, mantleTargetPosition, finalProgress);
    }

    private Vector3 GetRootPositionForFootPoint(Vector3 footPoint)
    {
        float bottomOffset = characterController != null ? characterController.center.y - characterController.height * 0.5f : 0f;
        float skinOffset = characterController != null ? Mathf.Max(characterController.skinWidth, 0.02f) : 0.03f;
        return footPoint + Vector3.up * (skinOffset - bottomOffset);
    }

    private float GetMantleLiftHeight(float upOffset, float upwardImpulse)
    {
        float stepClearance = characterController != null ? characterController.stepOffset + mantleClearanceRadius : mantleClearanceRadius;
        return Mathf.Max(upOffset, stepClearance) + upwardImpulse * 0.05f;
    }

    private float GetMantleInwardOvershoot(float inwardImpulse)
    {
        return inwardImpulse * 0.03f;
    }

    private static float Smooth01(float value)
    {
        float t = Mathf.Clamp01(value);
        return t * t * (3f - 2f * t);
    }

    private void ApplyCharacterControllerConstraint(float deltaTime)
    {
        if (!TryGetConstraint(out Transform anchor, out Vector3 targetPosition, out float allowedDistance))
        {
            hasActiveConstraint = false;
            return;
        }

        for (int i = 0; i < constraintSolverIterations; i++)
        {
            Vector3 correction = CalculateNonElasticCorrection(anchor.position, targetPosition, allowedDistance, deltaTime);
            if (correction.sqrMagnitude <= 0.000001f)
            {
                break;
            }

            if (characterController != null)
            {
                characterController.Move(correction);
            }
            else
            {
                transform.position += correction;
            }

            Physics.SyncTransforms();
        }

        SuppressCharacterVelocityThatStretchesRope(anchor.position, targetPosition, allowedDistance);

        RecordConstraintDebug(targetPosition, allowedDistance);
    }

    private void ApplyRigidbodyConstraint(float deltaTime)
    {
        if (!TryGetConstraint(out Transform anchor, out Vector3 targetPosition, out float allowedDistance) || deltaTime <= 0f)
        {
            hasActiveConstraint = false;
            return;
        }

        Vector3 toTarget = targetPosition - anchor.position;
        float distance = toTarget.magnitude;
        if (distance <= 0.0001f)
        {
            RecordConstraintDebug(targetPosition, allowedDistance);
            return;
        }

        Vector3 directionToTarget = toTarget / distance;
        for (int i = 0; i < constraintSolverIterations; i++)
        {
            Vector3 correction = CalculateNonElasticCorrection(anchor.position, targetPosition, allowedDistance, deltaTime);
            if (correction.sqrMagnitude <= 0.000001f)
            {
                break;
            }

            playerRigidbody.position += correction;
            Physics.SyncTransforms();

            toTarget = targetPosition - anchor.position;
            distance = toTarget.magnitude;
            directionToTarget = distance > 0.0001f ? toTarget / distance : directionToTarget;
        }

        Vector3 outwardDirection = -directionToTarget;
        Vector3 velocity = playerRigidbody.linearVelocity;
        float outwardSpeed = Vector3.Dot(velocity, outwardDirection);
        if (outwardSpeed > 0f)
        {
            playerRigidbody.linearVelocity = velocity - outwardDirection * outwardSpeed;
        }

        RecordConstraintDebug(targetPosition, allowedDistance);
    }

    private void SuppressCharacterVelocityThatStretchesRope(Vector3 anchorPosition, Vector3 targetPosition, float allowedDistance)
    {
        if (fpsController == null)
        {
            return;
        }

        Vector3 toTarget = targetPosition - anchorPosition;
        float distance = toTarget.magnitude;
        if (distance <= 0.0001f)
        {
            return;
        }

        if (distance < Mathf.Max(0f, allowedDistance - slackTolerance))
        {
            return;
        }

        Vector3 directionAwayFromTarget = -toTarget / distance;
        fpsController.RemoveVelocityAlong(directionAwayFromTarget);
    }

    private Vector3 LimitUpwardSwingDirection(Vector3 tangentDirection, Vector3 ropeDirectionAwayFromTarget)
    {
        Vector3 upwardTangent = Vector3.ProjectOnPlane(Vector3.up, ropeDirectionAwayFromTarget);
        if (upwardTangent.sqrMagnitude <= 0.0001f)
        {
            return tangentDirection;
        }

        upwardTangent.Normalize();
        float upwardComponent = Vector3.Dot(tangentDirection, upwardTangent);
        if (upwardComponent <= 0f)
        {
            return tangentDirection;
        }

        float horizontalRopeFactor = Vector3.ProjectOnPlane(ropeDirectionAwayFromTarget, Vector3.up).magnitude;
        float penalty = Mathf.Clamp01(horizontalRopeFactor * horizontalRopeUpwardPenalty);
        float allowedUpwardComponent = Mathf.Lerp(1f, upwardSwingLimit, penalty);
        if (upwardComponent <= allowedUpwardComponent)
        {
            return tangentDirection;
        }

        return tangentDirection - upwardTangent * (upwardComponent - allowedUpwardComponent);
    }

    private static bool IsReelInHeld()
    {
        return Keyboard.current != null &&
               (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
    }

    private static bool IsReelOutHeld()
    {
        return Keyboard.current != null &&
               (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed);
    }

    private bool TryGetConstraint(out Transform anchor, out Vector3 targetPosition, out float allowedDistance)
    {
        anchor = null;
        targetPosition = transform.position;
        allowedDistance = 0f;

        if (grappleController == null || !grappleController.IsLatched)
        {
            return false;
        }

        GrappleRope3D rope = grappleController.Rope;
        GrappleHookProjectile3D hook = grappleController.ActiveHook;
        anchor = grappleController.PlayerGrappleAnchor;
        if (anchor == null || rope == null || !rope.IsActive || hook == null || !hook.IsLatched)
        {
            return false;
        }

        targetPosition = rope.PlayerConstraintTarget;
        allowedDistance = rope.RemainingLengthForStartSegment;
        return true;
    }

    private void UpdateLatchedLengthTracking(bool hasConstraint)
    {
        GrappleHookProjectile3D hook = hasConstraint && grappleController != null ? grappleController.ActiveHook : null;
        if (hook == null)
        {
            trackedLatchedHook = null;
            latchedInitialRopeLength = 0f;
            if (!mantleActive && fpsController != null)
            {
                fpsController.SetGrappleMovementPaused(false);
            }

            return;
        }

        if (trackedLatchedHook == hook)
        {
            return;
        }

        trackedLatchedHook = hook;
        GrappleRope3D rope = grappleController.Rope;
        latchedInitialRopeLength = rope != null ? rope.CurrentRopeLength : 0f;
    }

    private void GetMantleCapsule(Vector3 rootPosition, out Vector3 bottom, out Vector3 top, out float radius)
    {
        radius = Mathf.Max(0.01f, mantleClearanceRadius);
        float height = Mathf.Max(radius * 2f, mantleClearanceHeight);
        if (characterController != null)
        {
            radius = Mathf.Max(radius, characterController.radius);
            height = Mathf.Max(height, characterController.height);
        }

        Vector3 center = rootPosition + Vector3.up * (height * 0.5f);
        float halfSegment = Mathf.Max(0f, height * 0.5f - radius);
        bottom = center - Vector3.up * halfSegment;
        top = center + Vector3.up * halfSegment;
    }

    private bool IsMantleBlockingCollider(Collider candidate, Collider supportCollider)
    {
        if (candidate == null || candidate.isTrigger || candidate == supportCollider)
        {
            return false;
        }

        Transform candidateTransform = candidate.transform;
        return candidateTransform != transform && !candidateTransform.IsChildOf(transform);
    }

    private Vector3 CalculateOutwardDamping(Vector3 currentVelocity, Vector3 directionToTarget, float deltaTime)
    {
        if (damping <= 0f || deltaTime <= 0f)
        {
            return Vector3.zero;
        }

        Vector3 outwardDirection = -directionToTarget;
        float outwardSpeed = Vector3.Dot(currentVelocity, outwardDirection);
        if (outwardSpeed <= 0f)
        {
            return Vector3.zero;
        }

        float dampingFactor = 1f - Mathf.Exp(-damping * deltaTime);
        return directionToTarget * (outwardSpeed * dampingFactor);
    }

    private Vector3 CalculateSwingAssist(Vector3 currentVelocity, Vector3 directionToTarget, float deltaTime)
    {
        if (swingForceMultiplier <= 1f || deltaTime <= 0f)
        {
            return Vector3.zero;
        }

        Vector3 tangentVelocity = Vector3.ProjectOnPlane(currentVelocity, directionToTarget);
        if (tangentVelocity.sqrMagnitude <= 0.0001f)
        {
            return Vector3.zero;
        }

        return tangentVelocity * ((swingForceMultiplier - 1f) * deltaTime);
    }

    private void RecordConstraintDebug(Vector3 targetPosition, float allowedDistance)
    {
        lastConstraintTarget = targetPosition;
        lastAllowedDistance = Mathf.Max(0f, allowedDistance);
        hasActiveConstraint = true;
    }

    private void RecordMantleDebug(
        string reason,
        float distanceToLatch,
        float currentRopeLength,
        bool isReelingIn,
        bool isNearTopEdge,
        bool hasValidMantleTarget,
        bool blockedByGeometry,
        bool startedMantleAssist)
    {
        lastMantleReason = reason;
        lastMantleDistanceToLatch = distanceToLatch;
        lastMantleCurrentRopeLength = currentRopeLength;
        lastMantleIsReelingIn = isReelingIn;
        lastMantleIsNearTopEdge = isNearTopEdge;
        lastMantleHasValidTarget = hasValidMantleTarget;
        lastMantleBlockedByGeometry = blockedByGeometry;
        lastMantleStarted = startedMantleAssist;

        GrappleHookProjectile3D hook = grappleController != null ? grappleController.ActiveHook : null;
        if (hook != null)
        {
            lastMantleLatchPoint = hook.LatchPoint;
        }

        if (!showDebugGizmos || !Application.isPlaying || Time.unscaledTime < nextMantleDebugLogTime)
        {
            return;
        }

        nextMantleDebugLogTime = Time.unscaledTime + 0.5f;
        Debug.Log(
            $"MantleAssist: reason={reason}, distanceToCandidate={distanceToLatch:F2}, currentRopeLength={currentRopeLength:F2}, minRopeLength={minRopeLength:F2}, isReelingIn={isReelingIn}, isNearTopEdge={isNearTopEdge}, hasValidMantleTarget={hasValidMantleTarget}, blockedByGeometry={blockedByGeometry}, startedMantleAssist={startedMantleAssist}, selectedFromWrapPoint={mantleStartedFromWrapPoint}, topNormal={lastMantleTopNormal:F3}",
            this);
    }

    private void LogMantleCandidateDebug(string message)
    {
        if (!showDebugGizmos || !Application.isPlaying || Time.unscaledTime < nextMantleDebugLogTime)
        {
            return;
        }

        Debug.Log($"MantleAssist: {message}", this);
    }

    private void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos || !hasActiveConstraint)
        {
            return;
        }

        Transform anchor = grappleController != null ? grappleController.PlayerGrappleAnchor : null;
        if (anchor == null)
        {
            return;
        }

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(anchor.position, lastConstraintTarget);
        Gizmos.DrawWireSphere(anchor.position, Mathf.Max(0.05f, lastAllowedDistance));

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(lastConstraintTarget, 0.12f);

        if (lastHookMantleCandidatePoint != Vector3.zero)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(lastHookMantleCandidatePoint, 0.13f);
        }

        Gizmos.color = Color.cyan;
        for (int i = 0; i < lastWrapMantleCandidatePoints.Count; i++)
        {
            Gizmos.DrawWireSphere(lastWrapMantleCandidatePoints[i], 0.11f);
        }

        Gizmos.color = Color.red;
        for (int i = 0; i < lastRejectedWrapMantleCandidatePoints.Count; i++)
        {
            Gizmos.DrawWireCube(lastRejectedWrapMantleCandidatePoints[i], Vector3.one * 0.18f);
        }

        if (lastSelectedMantleCandidatePoint != Vector3.zero)
        {
            Gizmos.color = mantleStartedFromWrapPoint ? Color.green : Color.blue;
            Gizmos.DrawWireSphere(lastSelectedMantleCandidatePoint, 0.2f);
            Gizmos.DrawLine(lastSelectedMantleCandidatePoint, lastSelectedMantleCandidatePoint + lastMantleTopNormal);
        }

        if (lastMantleLatchPoint != Vector3.zero)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(lastMantleLatchPoint, 0.16f);
        }

        if (lastMantleTargetPosition != Vector3.zero)
        {
            Gizmos.color = lastMantleBlockedByGeometry ? Color.red : Color.green;
            Gizmos.DrawWireSphere(lastMantleTargetPosition, 0.18f);
            Gizmos.DrawLine(lastMantleTargetPosition, lastMantleTargetPosition + lastMantleInwardDirection);

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(mantleStartPosition, lastMantleLiftPosition);
            Gizmos.DrawLine(lastMantleLiftPosition, lastMantleOverEdgePosition);
            Gizmos.DrawLine(lastMantleOverEdgePosition, lastMantleTargetPosition);

            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(lastMantleCapsuleBottom, Mathf.Max(0.02f, lastMantleCapsuleRadius));
            Gizmos.DrawWireSphere(lastMantleCapsuleTop, Mathf.Max(0.02f, lastMantleCapsuleRadius));
            Gizmos.DrawLine(lastMantleCapsuleBottom, lastMantleCapsuleTop);
        }
    }
}
