using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(LineRenderer))]
public sealed class GrappleRope3D : MonoBehaviour
{
    private struct WrapPoint
    {
        public Vector3 Position;
        public Vector3 EdgeStart;
        public Vector3 EdgeEnd;
        public Vector3 NormalA;
        public Vector3 NormalB;
        public Vector3 SurfaceNormal;
        public Collider Collider;
        public GrappleEdgeType EdgeType;
    }

    public readonly struct WrapPointInfo
    {
        public WrapPointInfo(
            Vector3 position,
            Vector3 edgeStart,
            Vector3 edgeEnd,
            Vector3 normalA,
            Vector3 normalB,
            Vector3 surfaceNormal,
            Collider collider,
            GrappleEdgeType edgeType)
        {
            Position = position;
            EdgeStart = edgeStart;
            EdgeEnd = edgeEnd;
            NormalA = normalA;
            NormalB = normalB;
            SurfaceNormal = surfaceNormal;
            Collider = collider;
            EdgeType = edgeType;
        }

        public Vector3 Position { get; }
        public Vector3 EdgeStart { get; }
        public Vector3 EdgeEnd { get; }
        public Vector3 NormalA { get; }
        public Vector3 NormalB { get; }
        public Vector3 SurfaceNormal { get; }
        public Collider Collider { get; }
        public GrappleEdgeType EdgeType { get; }
        public bool IsMantleEligible => Collider != null && EdgeType == GrappleEdgeType.TopSide;
    }

    private enum WrapInsertFailReason
    {
        None,
        NoEdgeFound,
        DuplicateWrapPoint,
        TooClose,
        MaxWrapPointsReached
    }

    private struct RopeCollisionHit
    {
        public Collider Collider;
        public Vector3 Point;
        public float Distance;
    }

    [Header("References")]
    [SerializeField] private LineRenderer lineRenderer;

    [Header("Stage 2 Runtime")]
    [SerializeField] private Transform startPoint;
    [SerializeField] private Transform endPoint;
    [SerializeField, Min(0f)] private float maxRopeLength = 15f;
    [SerializeField, Min(0f)] private float ropeExtendSpeed = 36f;
    [SerializeField, Min(0f)] private float currentRopeLength;

    [Header("Stage 4 Wrap")]
    [SerializeField] private LayerMask ropeCollisionMask = ~0;
    [SerializeField, Min(0.001f)] private float ropeRadius = 0.035f;
    [SerializeField, Min(0f)] private float wrapDetectionRadius = 0.85f;
    [SerializeField, Min(0f)] private float unwrapAngleThreshold = 8f;
    [SerializeField, Min(0f)] private float wrapPointSurfaceOffset = 0.03f;
    [SerializeField, Min(0f)] private float minWrapPointDistance = 0.15f;
    [SerializeField, Min(0)] private int maxWrapPoints = 8;
    [SerializeField, Min(1)] private int maxWrapResolveIterations = 4;

    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = true;
    [SerializeField] private bool debugWrapLogs;

    private readonly List<WrapPoint> wrapPoints = new();
    private bool ropeActive;
    private bool lengthLocked;
    private bool ropeExtensionBlocked;
    private Vector3 lastBlockedSegmentStart;
    private Vector3 lastBlockedSegmentEnd;
    private Vector3 lastBlockedHitPoint;
    private Vector3 lastSelectedWrapPoint;
    private Collider lastBlockedCollider;
    private int lastBlockedSegmentIndex = -1;
    private int lastResolveIterations;
    private WrapInsertFailReason lastWrapFailReason;
    private float nextDebugLogTime;

    public bool IsActive => ropeActive;
    public bool IsLengthLocked => lengthLocked;
    public Transform StartPoint => startPoint;
    public Transform EndPoint => endPoint;
    public float MaxRopeLength => maxRopeLength;
    public float RopeExtendSpeed => ropeExtendSpeed;
    public float CurrentRopeLength => currentRopeLength;
    public float RopeRadius => ropeRadius;
    public int WrapPointCount => wrapPoints.Count;
    public bool IsExtensionBlocked => ropeExtensionBlocked;
    public bool IsFullyExtended => ropeActive && currentRopeLength >= maxRopeLength - 0.001f;
    public Vector3 PlayerConstraintTarget
    {
        get
        {
            if (wrapPoints.Count > 0)
            {
                return wrapPoints[0].Position;
            }

            return endPoint != null ? endPoint.position : transform.position;
        }
    }

    public float RemainingLengthForStartSegment
    {
        get
        {
            return Mathf.Max(0f, currentRopeLength - MeasureLengthAfterStartSegment());
        }
    }

    public Vector3 ConstraintOrigin
    {
        get
        {
            if (wrapPoints.Count > 0)
            {
                return wrapPoints[wrapPoints.Count - 1].Position;
            }

            return startPoint != null ? startPoint.position : transform.position;
        }
    }

    public float RemainingLengthForEndSegment
    {
        get
        {
            return Mathf.Max(0f, currentRopeLength - MeasureLengthBeforeEndSegment());
        }
    }

    public float CurrentDistance
    {
        get
        {
            if (startPoint == null || endPoint == null)
            {
                return 0f;
            }

            return MeasureCurrentPathLength();
        }
    }

    public void Begin(Transform start, Transform end, float maxLength, float extendSpeed, float initialLength, float visualWidth, Material material)
    {
        CacheReferences();

        startPoint = start;
        endPoint = end;
        maxRopeLength = Mathf.Max(0f, maxLength);
        ropeExtendSpeed = Mathf.Max(0f, extendSpeed);
        currentRopeLength = Mathf.Clamp(initialLength, 0f, maxRopeLength);
        wrapPoints.Clear();
        lengthLocked = false;
        ropeExtensionBlocked = false;
        ropeActive = startPoint != null && endPoint != null;

        if (lineRenderer == null)
        {
            return;
        }

        lineRenderer.enabled = ropeActive;
        lineRenderer.useWorldSpace = true;
        lineRenderer.startWidth = Mathf.Max(0.001f, visualWidth);
        lineRenderer.endWidth = Mathf.Max(0.001f, visualWidth);
        lineRenderer.numCapVertices = 6;
        lineRenderer.numCornerVertices = 2;

        if (material != null)
        {
            lineRenderer.sharedMaterial = material;
        }

        UpdateRopeVisual();
    }

    public void ConfigureWrap(
        LayerMask collisionMask,
        float radius,
        float detectionRadius,
        float unwrapAngle,
        float minPointDistance,
        int maxPoints)
    {
        ropeCollisionMask = collisionMask;
        ropeRadius = Mathf.Max(0.001f, radius);
        wrapDetectionRadius = Mathf.Max(0f, detectionRadius);
        unwrapAngleThreshold = Mathf.Max(0f, unwrapAngle);
        minWrapPointDistance = Mathf.Max(0f, minPointDistance);
        maxWrapPoints = Mathf.Max(0, maxPoints);
    }

    public float AdvanceLength(float deltaTime)
    {
        if (!ropeActive)
        {
            return 0f;
        }

        if (lengthLocked || ropeExtensionBlocked || ShouldStopAutomaticExtensionForWraps())
        {
            return currentRopeLength;
        }

        if (maxRopeLength <= 0f)
        {
            currentRopeLength = 0f;
            return currentRopeLength;
        }

        currentRopeLength = Mathf.Min(maxRopeLength, currentRopeLength + ropeExtendSpeed * Mathf.Max(0f, deltaTime));
        return currentRopeLength;
    }

    private bool ShouldStopAutomaticExtensionForWraps()
    {
        return wrapPoints.Count >= 2;
    }

    public void LockLengthToCurrentPathWithoutStretch()
    {
        if (!ropeActive)
        {
            lengthLocked = true;
            currentRopeLength = 0f;
            return;
        }

        float currentPathLength = MeasureCurrentPathLength();
        currentRopeLength = Mathf.Clamp(Mathf.Min(currentRopeLength, currentPathLength), 0f, maxRopeLength);
        lengthLocked = true;
        UpdateRopeVisual();
    }

    public float SetCurrentLength(float length)
    {
        currentRopeLength = Mathf.Clamp(length, 0f, maxRopeLength);
        if (ropeActive)
        {
            lengthLocked = true;
        }

        UpdateRopeVisual();
        return currentRopeLength;
    }

    public float AdjustCurrentLength(float deltaLength, float minimumLength)
    {
        float safeMinimum = Mathf.Max(0f, Mathf.Max(minimumLength, MeasureLengthAfterStartSegment()));
        return SetCurrentLength(Mathf.Clamp(currentRopeLength + deltaLength, safeMinimum, maxRopeLength));
    }

    public void UpdateWrapPoints(
        LayerMask collisionMask,
        float radius,
        float detectionRadius,
        float unwrapAngle,
        float minPointDistance,
        int maxPoints,
        float edgeNormalThreshold,
        float minEdgeAngle,
        bool allowBottomEdgeLatch,
        bool allowTopSideLatch,
        bool allowBottomSideLatch)
    {
        if (!ropeActive || startPoint == null || endPoint == null)
        {
            return;
        }

        ConfigureWrap(collisionMask, radius, detectionRadius, unwrapAngle, minPointDistance, maxPoints);
        RefreshWrapPointPositions();
        RemoveClearWrapPoints();
        ResolveRopeCollisions(edgeNormalThreshold, minEdgeAngle, allowBottomEdgeLatch, allowTopSideLatch, allowBottomSideLatch);

        UpdateRopeVisual();
    }

    public void Clear()
    {
        ropeActive = false;
        startPoint = null;
        endPoint = null;
        currentRopeLength = 0f;
        lengthLocked = false;
        ropeExtensionBlocked = false;
        wrapPoints.Clear();

        if (lineRenderer != null)
        {
            lineRenderer.positionCount = 0;
            lineRenderer.enabled = false;
        }
    }

    public Vector3 GetPointPosition(int index)
    {
        if (index == 0)
        {
            return startPoint != null ? startPoint.position : transform.position;
        }

        if (index == wrapPoints.Count + 1)
        {
            return endPoint != null ? endPoint.position : transform.position;
        }

        return wrapPoints[index - 1].Position;
    }

    public bool TryGetWrapPointInfo(int wrapPointIndex, out WrapPointInfo info)
    {
        info = default;
        if (wrapPointIndex < 0 || wrapPointIndex >= wrapPoints.Count)
        {
            return false;
        }

        WrapPoint wrapPoint = wrapPoints[wrapPointIndex];
        info = new WrapPointInfo(
            wrapPoint.Position,
            wrapPoint.EdgeStart,
            wrapPoint.EdgeEnd,
            wrapPoint.NormalA,
            wrapPoint.NormalB,
            wrapPoint.SurfaceNormal,
            wrapPoint.Collider,
            wrapPoint.EdgeType);
        return true;
    }

    private void Reset()
    {
        CacheReferences();
        ConfigureDefaultLineRenderer();
    }

    private void OnValidate()
    {
        maxRopeLength = Mathf.Max(0f, maxRopeLength);
        ropeExtendSpeed = Mathf.Max(0f, ropeExtendSpeed);
        currentRopeLength = Mathf.Clamp(currentRopeLength, 0f, maxRopeLength);
        ropeRadius = Mathf.Max(0.001f, ropeRadius);
        wrapDetectionRadius = Mathf.Max(0f, wrapDetectionRadius);
        unwrapAngleThreshold = Mathf.Max(0f, unwrapAngleThreshold);
        wrapPointSurfaceOffset = Mathf.Max(0f, wrapPointSurfaceOffset);
        minWrapPointDistance = Mathf.Max(0f, minWrapPointDistance);
        maxWrapPoints = Mathf.Max(0, maxWrapPoints);
        maxWrapResolveIterations = Mathf.Max(1, maxWrapResolveIterations);
        CacheReferences();
    }

    private void Awake()
    {
        CacheReferences();
        ConfigureDefaultLineRenderer();
        Clear();
    }

    private void LateUpdate()
    {
        UpdateRopeVisual();
    }

    private void CacheReferences()
    {
        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
        }
    }

    private void ConfigureDefaultLineRenderer()
    {
        if (lineRenderer == null)
        {
            return;
        }

        lineRenderer.useWorldSpace = true;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        lineRenderer.enabled = false;
    }

    private void UpdateRopeVisual()
    {
        if (!ropeActive || lineRenderer == null || startPoint == null || endPoint == null)
        {
            return;
        }

        int pointCount = wrapPoints.Count + 2;
        if (lineRenderer.positionCount != pointCount)
        {
            lineRenderer.positionCount = pointCount;
        }

        lineRenderer.SetPosition(0, startPoint.position);
        for (int i = 0; i < wrapPoints.Count; i++)
        {
            lineRenderer.SetPosition(i + 1, wrapPoints[i].Position);
        }

        lineRenderer.SetPosition(pointCount - 1, endPoint.position);
    }

    private void ResolveRopeCollisions(
        float edgeNormalThreshold,
        float minEdgeAngle,
        bool allowBottomEdgeLatch,
        bool allowTopSideLatch,
        bool allowBottomSideLatch)
    {
        ropeExtensionBlocked = false;
        lastResolveIterations = 0;
        lastWrapFailReason = WrapInsertFailReason.None;
        lastBlockedSegmentIndex = -1;

        int iterationLimit = Mathf.Max(1, Mathf.Min(maxWrapResolveIterations, maxWrapPoints + 1));
        for (int i = 0; i < iterationLimit; i++)
        {
            lastResolveIterations = i + 1;
            if (!TryAddBlockingWrapPoint(
                    edgeNormalThreshold,
                    minEdgeAngle,
                    allowBottomEdgeLatch,
                    allowTopSideLatch,
                    allowBottomSideLatch,
                    out bool unresolvedBlockedSegment))
            {
                ropeExtensionBlocked = unresolvedBlockedSegment;
                if (ropeExtensionBlocked)
                {
                    ClampLengthToBlockedSegment();
                    LogWrapDebug();
                }

                return;
            }

            RefreshWrapPointPositions();
        }

        if (TryFindBlockedSegment(out int segmentIndex, out RopeCollisionHit hit))
        {
            ropeExtensionBlocked = true;
            lastBlockedSegmentIndex = segmentIndex;
            lastBlockedHitPoint = hit.Point;
            lastBlockedCollider = hit.Collider;
            lastWrapFailReason = WrapInsertFailReason.MaxWrapPointsReached;
            ClampLengthToBlockedSegment();
            LogWrapDebug();
        }
    }

    private bool TryAddBlockingWrapPoint(
        float edgeNormalThreshold,
        float minEdgeAngle,
        bool allowBottomEdgeLatch,
        bool allowTopSideLatch,
        bool allowBottomSideLatch,
        out bool unresolvedBlockedSegment)
    {
        unresolvedBlockedSegment = false;
        if (wrapPoints.Count >= maxWrapPoints)
        {
            unresolvedBlockedSegment = TryFindBlockedSegment(out int blockedSegmentIndex, out RopeCollisionHit blockedHit);
            if (unresolvedBlockedSegment)
            {
                RecordBlockedSegment(blockedSegmentIndex, blockedHit, WrapInsertFailReason.MaxWrapPointsReached);
            }

            return false;
        }

        int segmentCount = wrapPoints.Count + 1;
        for (int segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
        {
            Vector3 from = GetPointPosition(segmentIndex);
            Vector3 to = GetPointPosition(segmentIndex + 1);
            Collider startCollider = segmentIndex == 0 ? null : wrapPoints[segmentIndex - 1].Collider;
            Collider endCollider = segmentIndex == wrapPoints.Count ? null : wrapPoints[segmentIndex].Collider;

            if (!TryGetBlockingHit(from, to, startCollider, endCollider, out RopeCollisionHit hit))
            {
                continue;
            }

            if (!TryCreateWrapPoint(
                    hit,
                    from,
                    to,
                    edgeNormalThreshold,
                    minEdgeAngle,
                    allowBottomEdgeLatch,
                    allowTopSideLatch,
                    allowBottomSideLatch,
                    out WrapPoint wrapPoint,
                    out WrapInsertFailReason failReason))
            {
                unresolvedBlockedSegment = true;
                RecordBlockedSegment(segmentIndex, hit, failReason);
                return false;
            }

            wrapPoints.Insert(segmentIndex, wrapPoint);
            lastSelectedWrapPoint = wrapPoint.Position;
            RecordBlockedSegment(segmentIndex, hit, WrapInsertFailReason.None);
            LogWrapDebug();
            return true;
        }

        return false;
    }

    private bool TryCreateWrapPoint(
        RopeCollisionHit blockingHit,
        Vector3 segmentStart,
        Vector3 segmentEnd,
        float edgeNormalThreshold,
        float minEdgeAngle,
        bool allowBottomEdgeLatch,
        bool allowTopSideLatch,
        bool allowBottomSideLatch,
        out WrapPoint wrapPoint,
        out WrapInsertFailReason failReason)
    {
        wrapPoint = default;
        failReason = WrapInsertFailReason.None;

        if (blockingHit.Collider == null)
        {
            failReason = WrapInsertFailReason.NoEdgeFound;
            return false;
        }

        if (!TryFindWrapEdge(
                blockingHit.Collider,
                blockingHit.Point,
                segmentStart,
                segmentEnd,
                edgeNormalThreshold,
                minEdgeAngle,
                allowBottomEdgeLatch,
                allowTopSideLatch,
                allowBottomSideLatch,
                out GrappleEdgeHit edgeHit,
                out failReason))
        {
            return false;
        }

        Vector3 surfaceNormal = GetWrapSurfaceNormal(edgeHit);
        Vector3 wrapPosition = edgeHit.Point + surfaceNormal * wrapPointSurfaceOffset;
        if (IsDuplicateWrapPoint(wrapPosition))
        {
            failReason = WrapInsertFailReason.DuplicateWrapPoint;
            return false;
        }

        wrapPoint = new WrapPoint
        {
            Position = wrapPosition,
            EdgeStart = edgeHit.Start,
            EdgeEnd = edgeHit.End,
            NormalA = edgeHit.NormalA,
            NormalB = edgeHit.NormalB,
            SurfaceNormal = surfaceNormal,
            Collider = edgeHit.Collider,
            EdgeType = edgeHit.Type
        };

        return true;
    }

    private bool TryFindWrapEdge(
        Collider blockingCollider,
        Vector3 hitPoint,
        Vector3 segmentStart,
        Vector3 segmentEnd,
        float edgeNormalThreshold,
        float minEdgeAngle,
        bool allowBottomEdgeLatch,
        bool allowTopSideLatch,
        bool allowBottomSideLatch,
        out GrappleEdgeHit edgeHit,
        out WrapInsertFailReason failReason)
    {
        edgeHit = default;
        failReason = WrapInsertFailReason.NoEdgeFound;

        float searchRadius = GetWrapEdgeSearchRadius(blockingCollider);
        Vector3 segmentDirection = segmentEnd - segmentStart;
        if (segmentDirection.sqrMagnitude > 0.0001f)
        {
            segmentDirection.Normalize();
        }

        Vector3[] searchPoints =
        {
            hitPoint,
            hitPoint + segmentDirection * wrapDetectionRadius,
            hitPoint - segmentDirection * wrapDetectionRadius,
            blockingCollider.ClosestPoint(segmentEnd),
            blockingCollider.ClosestPoint(segmentStart),
            blockingCollider.bounds.center
        };

        float bestScore = float.PositiveInfinity;
        GrappleEdgeHit bestHit = default;
        bool foundDuplicateOnly = false;

        for (int i = 0; i < searchPoints.Length; i++)
        {
            if (!GrappleEdgeDetector3D.TryFindBestLatchEdge(
                    blockingCollider,
                    searchPoints[i],
                    searchRadius,
                    edgeNormalThreshold,
                    minEdgeAngle,
                    searchRadius,
                    allowBottomEdgeLatch,
                    allowTopSideLatch,
                    allowBottomSideLatch,
                    true,
                    out GrappleEdgeHit candidate))
            {
                continue;
            }

            Vector3 surfaceNormal = GetWrapSurfaceNormal(candidate);
            Vector3 candidatePosition = candidate.Point + surfaceNormal * wrapPointSurfaceOffset;
            if (IsDuplicateWrapPoint(candidatePosition))
            {
                foundDuplicateOnly = true;
                continue;
            }

            float distanceToHit = Vector3.Distance(candidate.Point, hitPoint);
            float distanceToSegment = DistancePointToSegment(candidate.Point, segmentStart, segmentEnd);
            float pathLength = Vector3.Distance(segmentStart, candidatePosition) + Vector3.Distance(candidatePosition, segmentEnd);
            float score = distanceToHit + distanceToSegment * 3f + pathLength * 0.05f;
            if (score >= bestScore)
            {
                continue;
            }

            bestScore = score;
            bestHit = candidate;
        }

        if (!bestHit.IsValid)
        {
            failReason = foundDuplicateOnly ? WrapInsertFailReason.DuplicateWrapPoint : WrapInsertFailReason.NoEdgeFound;
            return false;
        }

        edgeHit = bestHit;
        failReason = WrapInsertFailReason.None;
        return true;
    }

    private static float DistancePointToSegment(Vector3 point, Vector3 segmentStart, Vector3 segmentEnd)
    {
        Vector3 segment = segmentEnd - segmentStart;
        float lengthSquared = segment.sqrMagnitude;
        if (lengthSquared <= 0.0001f)
        {
            return Vector3.Distance(point, segmentStart);
        }

        float t = Vector3.Dot(point - segmentStart, segment) / lengthSquared;
        Vector3 closest = segmentStart + segment * Mathf.Clamp01(t);
        return Vector3.Distance(point, closest);
    }

    private float GetWrapEdgeSearchRadius(Collider targetCollider)
    {
        float searchRadius = Mathf.Max(wrapDetectionRadius, ropeRadius * 4f);
        if (targetCollider != null)
        {
            Bounds bounds = targetCollider.bounds;
            searchRadius = Mathf.Max(searchRadius, Mathf.Min(bounds.extents.magnitude, maxRopeLength));
        }

        return searchRadius;
    }

    private static Vector3 GetWrapSurfaceNormal(GrappleEdgeHit edgeHit)
    {
        Vector3 normal = edgeHit.NormalA + edgeHit.NormalB;
        if (normal.sqrMagnitude <= 0.0001f)
        {
            normal = edgeHit.NormalA.sqrMagnitude > 0.0001f ? edgeHit.NormalA : Vector3.up;
        }

        return normal.normalized;
    }

    private void RefreshWrapPointPositions()
    {
        for (int i = 0; i < wrapPoints.Count; i++)
        {
            WrapPoint wrapPoint = wrapPoints[i];
            if ((wrapPoint.EdgeEnd - wrapPoint.EdgeStart).sqrMagnitude <= 0.0001f)
            {
                continue;
            }

            Vector3 previous = i == 0 ? startPoint.position : wrapPoints[i - 1].Position;
            Vector3 next = i == wrapPoints.Count - 1 ? endPoint.position : wrapPoints[i + 1].Position;
            Vector3 rawEdgePoint = FindShortestPathPointOnEdge(previous, next, wrapPoint.EdgeStart, wrapPoint.EdgeEnd);
            Vector3 surfaceNormal = wrapPoint.SurfaceNormal.sqrMagnitude > 0.0001f ? wrapPoint.SurfaceNormal.normalized : Vector3.up;
            wrapPoint.Position = rawEdgePoint + surfaceNormal * wrapPointSurfaceOffset;
            wrapPoints[i] = wrapPoint;
        }
    }

    private void RemoveClearWrapPoints()
    {
        for (int i = wrapPoints.Count - 1; i >= 0; i--)
        {
            Vector3 previous = i == 0 ? startPoint.position : wrapPoints[i - 1].Position;
            Vector3 current = wrapPoints[i].Position;
            Vector3 next = i == wrapPoints.Count - 1 ? endPoint.position : wrapPoints[i + 1].Position;
            Collider previousCollider = i == 0 ? null : wrapPoints[i - 1].Collider;
            Collider nextCollider = i == wrapPoints.Count - 1 ? null : wrapPoints[i + 1].Collider;

            if (!IsUnwrapAngleOpen(previous, current, next))
            {
                continue;
            }

            if (!IsSegmentClear(previous, next, previousCollider, nextCollider))
            {
                continue;
            }

            wrapPoints.RemoveAt(i);
        }
    }

    private bool IsUnwrapAngleOpen(Vector3 previous, Vector3 current, Vector3 next)
    {
        Vector3 toPrevious = previous - current;
        Vector3 toNext = next - current;
        if (toPrevious.sqrMagnitude < 0.0001f || toNext.sqrMagnitude < 0.0001f)
        {
            return true;
        }

        return Vector3.Angle(toPrevious, toNext) >= unwrapAngleThreshold;
    }

    private bool IsSegmentClear(Vector3 from, Vector3 to, Collider startCollider, Collider endCollider)
    {
        RopeCollisionHit ignoredHit;
        return !TryGetBlockingHit(from, to, startCollider, endCollider, out ignoredHit);
    }

    private bool TryFindBlockedSegment(out int segmentIndex, out RopeCollisionHit hit)
    {
        hit = default;
        int segmentCount = wrapPoints.Count + 1;
        for (segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
        {
            Vector3 from = GetPointPosition(segmentIndex);
            Vector3 to = GetPointPosition(segmentIndex + 1);
            Collider startCollider = segmentIndex == 0 ? null : wrapPoints[segmentIndex - 1].Collider;
            Collider endCollider = segmentIndex == wrapPoints.Count ? null : wrapPoints[segmentIndex].Collider;
            if (TryGetBlockingHit(from, to, startCollider, endCollider, out hit))
            {
                return true;
            }
        }

        segmentIndex = -1;
        return false;
    }

    private void RecordBlockedSegment(int segmentIndex, RopeCollisionHit hit, WrapInsertFailReason failReason)
    {
        lastBlockedSegmentIndex = segmentIndex;
        lastBlockedCollider = hit.Collider;
        lastBlockedHitPoint = hit.Point;
        lastWrapFailReason = failReason;

        if (segmentIndex >= 0)
        {
            lastBlockedSegmentStart = GetPointPosition(segmentIndex);
            lastBlockedSegmentEnd = GetPointPosition(segmentIndex + 1);
        }
    }

    private void ClampLengthToBlockedSegment()
    {
        if (lastBlockedSegmentIndex < 0 || lastBlockedCollider == null)
        {
            return;
        }

        float safePathLength = 0f;
        for (int i = 0; i < lastBlockedSegmentIndex; i++)
        {
            safePathLength += Vector3.Distance(GetPointPosition(i), GetPointPosition(i + 1));
        }

        safePathLength += Vector3.Distance(lastBlockedSegmentStart, lastBlockedHitPoint);
        if (safePathLength > 0f)
        {
            currentRopeLength = Mathf.Min(currentRopeLength, Mathf.Clamp(safePathLength, 0f, maxRopeLength));
        }
    }

    private void LogWrapDebug()
    {
        if (!debugWrapLogs || !Application.isPlaying || Time.unscaledTime < nextDebugLogTime)
        {
            return;
        }

        nextDebugLogTime = Time.unscaledTime + 0.25f;
        string colliderName = lastBlockedCollider != null ? lastBlockedCollider.name : "None";
        Debug.Log(
            $"Rope segment blocked: segment={lastBlockedSegmentIndex}, collider={colliderName}, hit={lastBlockedHitPoint:F3}, selectedWrap={lastSelectedWrapPoint:F3}, failReason={lastWrapFailReason}, currentRopeLength={currentRopeLength:F2}, totalRopePathLength={MeasureCurrentPathLength():F2}, resolveIterations={lastResolveIterations}",
            this);
    }

    private bool TryGetBlockingHit(Vector3 from, Vector3 to, Collider startCollider, Collider endCollider, out RopeCollisionHit blockingHit)
    {
        blockingHit = default;
        Vector3 direction = to - from;
        float distance = direction.magnitude;
        if (distance <= minWrapPointDistance)
        {
            return false;
        }

        Vector3 normalizedDirection = direction / distance;
        float endpointPadding = Mathf.Min(distance * 0.25f, Mathf.Max(ropeRadius + wrapPointSurfaceOffset, 0.02f));
        Vector3 castFrom = from + normalizedDirection * endpointPadding;
        float castDistance = Mathf.Max(0f, distance - endpointPadding * 2f);
        if (castDistance <= minWrapPointDistance)
        {
            castFrom = from;
            castDistance = distance;
        }

        RaycastHit[] hits = Physics.SphereCastAll(
            castFrom,
            ropeRadius,
            normalizedDirection,
            castDistance,
            ropeCollisionMask,
            QueryTriggerInteraction.Ignore);

        if (hits != null && hits.Length > 0)
        {
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == null)
                {
                    continue;
                }

                float distanceFromOriginalStart = hit.distance + (castFrom - from).magnitude;
                float distanceFromOriginalEnd = distance - distanceFromOriginalStart;

                if (distanceFromOriginalStart <= minWrapPointDistance && hit.collider == startCollider)
                {
                    continue;
                }

                if (distanceFromOriginalEnd <= minWrapPointDistance && hit.collider == endCollider)
                {
                    continue;
                }

                if (distanceFromOriginalStart <= Mathf.Max(ropeRadius * 0.5f, 0.01f) && hit.collider == startCollider)
                {
                    continue;
                }

                if (distanceFromOriginalEnd <= Mathf.Max(ropeRadius * 0.5f, 0.01f) && hit.collider == endCollider)
                {
                    continue;
                }

                blockingHit = new RopeCollisionHit
                {
                    Collider = hit.collider,
                    Point = hit.point,
                    Distance = distanceFromOriginalStart
                };
                return true;
            }
        }

        return TryGetOverlappingSegmentHit(from, to, startCollider, endCollider, out blockingHit);
    }

    private bool TryGetOverlappingSegmentHit(Vector3 from, Vector3 to, Collider startCollider, Collider endCollider, out RopeCollisionHit blockingHit)
    {
        blockingHit = default;
        Vector3 direction = to - from;
        float distance = direction.magnitude;
        if (distance <= minWrapPointDistance)
        {
            return false;
        }

        Collider[] colliders = Physics.OverlapCapsule(from, to, ropeRadius, ropeCollisionMask, QueryTriggerInteraction.Ignore);
        if (colliders == null || colliders.Length == 0)
        {
            return false;
        }

        Vector3 normalizedDirection = direction / distance;
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || collider.isTrigger)
            {
                continue;
            }

            if (collider == startCollider && Vector3.Distance(from, collider.ClosestPoint(from)) <= ropeRadius + wrapPointSurfaceOffset)
            {
                // Keep checking: a segment leaving an existing wrap point can immediately enter the same collider.
            }

            if (!TryGetSegmentColliderIntersection(collider, from, to, out Vector3 hitPoint, out float hitDistance))
            {
                continue;
            }

            if (hitDistance <= minWrapPointDistance && collider != startCollider)
            {
                continue;
            }

            if (distance - hitDistance <= minWrapPointDistance && collider == endCollider)
            {
                continue;
            }

            blockingHit = new RopeCollisionHit
            {
                Collider = collider,
                Point = hitPoint,
                Distance = hitDistance
            };
            return true;
        }

        return false;
    }

    private static bool TryGetSegmentColliderIntersection(Collider collider, Vector3 from, Vector3 to, out Vector3 hitPoint, out float hitDistance)
    {
        if (collider is BoxCollider boxCollider)
        {
            return TryGetSegmentBoxIntersection(boxCollider, from, to, out hitPoint, out hitDistance);
        }

        Vector3 closest = collider.ClosestPoint((from + to) * 0.5f);
        hitPoint = closest;
        hitDistance = Vector3.Distance(from, closest);
        return hitDistance > 0.0001f;
    }

    private static bool TryGetSegmentBoxIntersection(BoxCollider boxCollider, Vector3 from, Vector3 to, out Vector3 hitPoint, out float hitDistance)
    {
        Transform boxTransform = boxCollider.transform;
        Vector3 localFrom = boxTransform.InverseTransformPoint(from) - boxCollider.center;
        Vector3 localTo = boxTransform.InverseTransformPoint(to) - boxCollider.center;
        Vector3 localDirection = localTo - localFrom;
        Vector3 extents = boxCollider.size * 0.5f;

        float tMin = 0f;
        float tMax = 1f;
        if (!ClipSegmentToSlab(localFrom.x, localDirection.x, -extents.x, extents.x, ref tMin, ref tMax) ||
            !ClipSegmentToSlab(localFrom.y, localDirection.y, -extents.y, extents.y, ref tMin, ref tMax) ||
            !ClipSegmentToSlab(localFrom.z, localDirection.z, -extents.z, extents.z, ref tMin, ref tMax))
        {
            hitPoint = default;
            hitDistance = 0f;
            return false;
        }

        float t = Mathf.Clamp01(tMin);
        Vector3 localHit = localFrom + localDirection * t + boxCollider.center;
        hitPoint = boxTransform.TransformPoint(localHit);
        hitDistance = Vector3.Distance(from, hitPoint);
        return true;
    }

    private static bool ClipSegmentToSlab(float origin, float direction, float min, float max, ref float tMin, ref float tMax)
    {
        if (Mathf.Abs(direction) < 0.000001f)
        {
            return origin >= min && origin <= max;
        }

        float inverse = 1f / direction;
        float t1 = (min - origin) * inverse;
        float t2 = (max - origin) * inverse;
        if (t1 > t2)
        {
            (t1, t2) = (t2, t1);
        }

        tMin = Mathf.Max(tMin, t1);
        tMax = Mathf.Min(tMax, t2);
        return tMin <= tMax;
    }

    private bool IsDuplicateWrapPoint(Vector3 point)
    {
        float minDistanceSquared = minWrapPointDistance * minWrapPointDistance;

        if (startPoint != null && (startPoint.position - point).sqrMagnitude < minDistanceSquared)
        {
            return true;
        }

        if (endPoint != null && (endPoint.position - point).sqrMagnitude < minDistanceSquared)
        {
            return true;
        }

        for (int i = 0; i < wrapPoints.Count; i++)
        {
            if ((wrapPoints[i].Position - point).sqrMagnitude < minDistanceSquared)
            {
                return true;
            }
        }

        return false;
    }

    private void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos || !ropeActive)
        {
            return;
        }

        Gizmos.color = ropeExtensionBlocked ? Color.red : Color.cyan;
        int pointCount = wrapPoints.Count + 2;
        for (int i = 0; i < pointCount - 1; i++)
        {
            Vector3 from = GetPointPosition(i);
            Vector3 to = GetPointPosition(i + 1);
            Gizmos.DrawLine(from, to);
            DrawWireCapsuleSegment(from, to, Mathf.Max(ropeRadius, 0.01f));
        }

        Gizmos.color = Color.magenta;
        for (int i = 0; i < wrapPoints.Count; i++)
        {
            Gizmos.DrawWireSphere(wrapPoints[i].Position, Mathf.Max(ropeRadius * 2f, 0.06f));
        }

        if (lastBlockedSegmentIndex >= 0)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(lastBlockedSegmentStart, lastBlockedSegmentEnd);
            Gizmos.DrawWireSphere(lastBlockedHitPoint, Mathf.Max(ropeRadius * 2.5f, 0.08f));
        }

        if (lastSelectedWrapPoint != Vector3.zero)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(lastSelectedWrapPoint, Mathf.Max(ropeRadius * 2.5f, 0.08f));
        }
    }

    private static void DrawWireCapsuleSegment(Vector3 from, Vector3 to, float radius)
    {
        Gizmos.DrawWireSphere(from, radius);
        Gizmos.DrawWireSphere(to, radius);
    }

    private static Vector3 FindShortestPathPointOnEdge(Vector3 previous, Vector3 next, Vector3 edgeStart, Vector3 edgeEnd)
    {
        Vector3 edge = edgeEnd - edgeStart;
        if (edge.sqrMagnitude <= 0.0001f)
        {
            return edgeStart;
        }

        float low = 0f;
        float high = 1f;
        for (int i = 0; i < 12; i++)
        {
            float leftT = Mathf.Lerp(low, high, 1f / 3f);
            float rightT = Mathf.Lerp(low, high, 2f / 3f);
            Vector3 leftPoint = edgeStart + edge * leftT;
            Vector3 rightPoint = edgeStart + edge * rightT;
            float leftLength = Vector3.Distance(previous, leftPoint) + Vector3.Distance(leftPoint, next);
            float rightLength = Vector3.Distance(previous, rightPoint) + Vector3.Distance(rightPoint, next);

            if (leftLength < rightLength)
            {
                high = rightT;
            }
            else
            {
                low = leftT;
            }
        }

        return edgeStart + edge * ((low + high) * 0.5f);
    }

    private float MeasureLengthBeforeEndSegment()
    {
        if (startPoint == null || wrapPoints.Count == 0)
        {
            return 0f;
        }

        float length = 0f;
        Vector3 previous = startPoint.position;
        for (int i = 0; i < wrapPoints.Count; i++)
        {
            length += Vector3.Distance(previous, wrapPoints[i].Position);
            previous = wrapPoints[i].Position;
        }

        return length;
    }

    private float MeasureLengthAfterStartSegment()
    {
        if (endPoint == null || wrapPoints.Count == 0)
        {
            return 0f;
        }

        float length = 0f;
        Vector3 previous = wrapPoints[0].Position;
        for (int i = 1; i < wrapPoints.Count; i++)
        {
            length += Vector3.Distance(previous, wrapPoints[i].Position);
            previous = wrapPoints[i].Position;
        }

        length += Vector3.Distance(previous, endPoint.position);
        return length;
    }

    private float MeasureCurrentPathLength()
    {
        if (startPoint == null || endPoint == null)
        {
            return 0f;
        }

        float length = 0f;
        Vector3 previous = startPoint.position;
        for (int i = 0; i < wrapPoints.Count; i++)
        {
            length += Vector3.Distance(previous, wrapPoints[i].Position);
            previous = wrapPoints[i].Position;
        }

        length += Vector3.Distance(previous, endPoint.position);
        return length;
    }
}
