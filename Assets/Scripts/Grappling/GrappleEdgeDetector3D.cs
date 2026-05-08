using UnityEngine;

public enum GrappleEdgeType
{
    Unsupported,
    TopSide,
    BottomSide,
    VerticalSide
}

public struct GrappleEdgeHit
{
    public bool IsValid;
    public Collider Collider;
    public Vector3 Start;
    public Vector3 End;
    public Vector3 Point;
    public Vector3 NormalA;
    public Vector3 NormalB;
    public GrappleEdgeType Type;
    public float Distance;
}

public static class GrappleEdgeDetector3D
{
    private enum FaceKind
    {
        Unsupported,
        Top,
        Bottom,
        Side
    }

    public static bool TryFindBestLatchEdge(
        Collider targetCollider,
        Vector3 hookPosition,
        float latchRadius,
        float edgeNormalThreshold,
        float minEdgeAngle,
        float maxLatchSnapDistance,
        bool allowBottomEdgeLatch,
        bool allowTopSideLatch,
        bool allowBottomSideLatch,
        bool allowVerticalSide,
        out GrappleEdgeHit hit)
    {
        hit = default;

        if (targetCollider is not BoxCollider boxCollider || latchRadius <= 0f)
        {
            return false;
        }

        Vector3[] corners = GetBoxCorners(boxCollider);
        Vector3 right = boxCollider.transform.TransformDirection(Vector3.right).normalized;
        Vector3 up = boxCollider.transform.TransformDirection(Vector3.up).normalized;
        Vector3 forward = boxCollider.transform.TransformDirection(Vector3.forward).normalized;

        GrappleEdgeHit bestHit = default;
        float bestDistance = float.PositiveInfinity;

        TryEdge(boxCollider, hookPosition, latchRadius, edgeNormalThreshold, minEdgeAngle, maxLatchSnapDistance, allowBottomEdgeLatch, allowTopSideLatch, allowBottomSideLatch, allowVerticalSide, corners[4], corners[5], up, -forward, ref bestHit, ref bestDistance);
        TryEdge(boxCollider, hookPosition, latchRadius, edgeNormalThreshold, minEdgeAngle, maxLatchSnapDistance, allowBottomEdgeLatch, allowTopSideLatch, allowBottomSideLatch, allowVerticalSide, corners[5], corners[6], up, right, ref bestHit, ref bestDistance);
        TryEdge(boxCollider, hookPosition, latchRadius, edgeNormalThreshold, minEdgeAngle, maxLatchSnapDistance, allowBottomEdgeLatch, allowTopSideLatch, allowBottomSideLatch, allowVerticalSide, corners[6], corners[7], up, forward, ref bestHit, ref bestDistance);
        TryEdge(boxCollider, hookPosition, latchRadius, edgeNormalThreshold, minEdgeAngle, maxLatchSnapDistance, allowBottomEdgeLatch, allowTopSideLatch, allowBottomSideLatch, allowVerticalSide, corners[7], corners[4], up, -right, ref bestHit, ref bestDistance);

        TryEdge(boxCollider, hookPosition, latchRadius, edgeNormalThreshold, minEdgeAngle, maxLatchSnapDistance, allowBottomEdgeLatch, allowTopSideLatch, allowBottomSideLatch, allowVerticalSide, corners[0], corners[1], -up, -forward, ref bestHit, ref bestDistance);
        TryEdge(boxCollider, hookPosition, latchRadius, edgeNormalThreshold, minEdgeAngle, maxLatchSnapDistance, allowBottomEdgeLatch, allowTopSideLatch, allowBottomSideLatch, allowVerticalSide, corners[1], corners[2], -up, right, ref bestHit, ref bestDistance);
        TryEdge(boxCollider, hookPosition, latchRadius, edgeNormalThreshold, minEdgeAngle, maxLatchSnapDistance, allowBottomEdgeLatch, allowTopSideLatch, allowBottomSideLatch, allowVerticalSide, corners[2], corners[3], -up, forward, ref bestHit, ref bestDistance);
        TryEdge(boxCollider, hookPosition, latchRadius, edgeNormalThreshold, minEdgeAngle, maxLatchSnapDistance, allowBottomEdgeLatch, allowTopSideLatch, allowBottomSideLatch, allowVerticalSide, corners[3], corners[0], -up, -right, ref bestHit, ref bestDistance);

        TryEdge(boxCollider, hookPosition, latchRadius, edgeNormalThreshold, minEdgeAngle, maxLatchSnapDistance, allowBottomEdgeLatch, allowTopSideLatch, allowBottomSideLatch, allowVerticalSide, corners[0], corners[4], -right, -forward, ref bestHit, ref bestDistance);
        TryEdge(boxCollider, hookPosition, latchRadius, edgeNormalThreshold, minEdgeAngle, maxLatchSnapDistance, allowBottomEdgeLatch, allowTopSideLatch, allowBottomSideLatch, allowVerticalSide, corners[1], corners[5], right, -forward, ref bestHit, ref bestDistance);
        TryEdge(boxCollider, hookPosition, latchRadius, edgeNormalThreshold, minEdgeAngle, maxLatchSnapDistance, allowBottomEdgeLatch, allowTopSideLatch, allowBottomSideLatch, allowVerticalSide, corners[2], corners[6], right, forward, ref bestHit, ref bestDistance);
        TryEdge(boxCollider, hookPosition, latchRadius, edgeNormalThreshold, minEdgeAngle, maxLatchSnapDistance, allowBottomEdgeLatch, allowTopSideLatch, allowBottomSideLatch, allowVerticalSide, corners[3], corners[7], -right, forward, ref bestHit, ref bestDistance);

        hit = bestHit;
        return hit.IsValid;
    }

    public static bool TryFindBestLatchEdge(
        Collider targetCollider,
        Vector3 hookPosition,
        float latchRadius,
        out GrappleEdgeHit hit)
    {
        return TryFindBestLatchEdge(
            targetCollider,
            hookPosition,
            latchRadius,
            0.65f,
            20f,
            latchRadius,
            true,
            true,
            true,
            false,
            out hit);
    }

    private static Vector3[] GetBoxCorners(BoxCollider boxCollider)
    {
        Vector3 center = boxCollider.center;
        Vector3 extents = boxCollider.size * 0.5f;
        Transform transform = boxCollider.transform;

        return new[]
        {
            transform.TransformPoint(center + new Vector3(-extents.x, -extents.y, -extents.z)),
            transform.TransformPoint(center + new Vector3(extents.x, -extents.y, -extents.z)),
            transform.TransformPoint(center + new Vector3(extents.x, -extents.y, extents.z)),
            transform.TransformPoint(center + new Vector3(-extents.x, -extents.y, extents.z)),
            transform.TransformPoint(center + new Vector3(-extents.x, extents.y, -extents.z)),
            transform.TransformPoint(center + new Vector3(extents.x, extents.y, -extents.z)),
            transform.TransformPoint(center + new Vector3(extents.x, extents.y, extents.z)),
            transform.TransformPoint(center + new Vector3(-extents.x, extents.y, extents.z))
        };
    }

    private static void TryEdge(
        Collider collider,
        Vector3 hookPosition,
        float latchRadius,
        float edgeNormalThreshold,
        float minEdgeAngle,
        float maxLatchSnapDistance,
        bool allowBottomEdgeLatch,
        bool allowTopSideLatch,
        bool allowBottomSideLatch,
        bool allowVerticalSide,
        Vector3 start,
        Vector3 end,
        Vector3 normalA,
        Vector3 normalB,
        ref GrappleEdgeHit bestHit,
        ref float bestDistance)
    {
        float edgeAngle = Vector3.Angle(normalA, normalB);
        if (edgeAngle < minEdgeAngle)
        {
            return;
        }

        GrappleEdgeType edgeType = ClassifyEdge(normalA, normalB, edgeNormalThreshold);
        if (!IsAllowed(edgeType, allowBottomEdgeLatch, allowTopSideLatch, allowBottomSideLatch, allowVerticalSide))
        {
            return;
        }

        Vector3 point = ClosestPointOnSegment(start, end, hookPosition);
        float distance = Vector3.Distance(point, hookPosition);
        if (distance > latchRadius || distance > maxLatchSnapDistance || distance >= bestDistance)
        {
            return;
        }

        if (!HasClearSnapPath(hookPosition, point, collider))
        {
            return;
        }

        bestDistance = distance;
        bestHit = new GrappleEdgeHit
        {
            IsValid = true,
            Collider = collider,
            Start = start,
            End = end,
            Point = point,
            NormalA = normalA,
            NormalB = normalB,
            Type = edgeType,
            Distance = distance
        };
    }

    private static GrappleEdgeType ClassifyEdge(Vector3 normalA, Vector3 normalB, float edgeNormalThreshold)
    {
        FaceKind faceA = ClassifyFace(normalA, edgeNormalThreshold);
        FaceKind faceB = ClassifyFace(normalB, edgeNormalThreshold);

        if ((faceA == FaceKind.Top && faceB == FaceKind.Side) || (faceA == FaceKind.Side && faceB == FaceKind.Top))
        {
            return GrappleEdgeType.TopSide;
        }

        if ((faceA == FaceKind.Bottom && faceB == FaceKind.Side) || (faceA == FaceKind.Side && faceB == FaceKind.Bottom))
        {
            return GrappleEdgeType.BottomSide;
        }

        if (faceA == FaceKind.Side && faceB == FaceKind.Side)
        {
            return GrappleEdgeType.VerticalSide;
        }

        return GrappleEdgeType.Unsupported;
    }

    private static FaceKind ClassifyFace(Vector3 normal, float threshold)
    {
        float upDot = Vector3.Dot(normal.normalized, Vector3.up);
        if (upDot >= threshold)
        {
            return FaceKind.Top;
        }

        if (upDot <= -threshold)
        {
            return FaceKind.Bottom;
        }

        if (Mathf.Abs(upDot) < threshold)
        {
            return FaceKind.Side;
        }

        return FaceKind.Unsupported;
    }

    private static bool IsAllowed(GrappleEdgeType type, bool allowBottomEdgeLatch, bool allowTopSideLatch, bool allowBottomSideLatch, bool allowVerticalSide)
    {
        switch (type)
        {
            case GrappleEdgeType.TopSide:
                return allowTopSideLatch;
            case GrappleEdgeType.BottomSide:
                return allowBottomSideLatch || allowBottomEdgeLatch;
            case GrappleEdgeType.VerticalSide:
                return allowVerticalSide;
            default:
                return false;
        }
    }

    private static Vector3 ClosestPointOnSegment(Vector3 start, Vector3 end, Vector3 point)
    {
        Vector3 segment = end - start;
        float lengthSquared = segment.sqrMagnitude;
        if (lengthSquared < 0.0001f)
        {
            return start;
        }

        float t = Vector3.Dot(point - start, segment) / lengthSquared;
        return start + segment * Mathf.Clamp01(t);
    }

    private static bool HasClearSnapPath(Vector3 from, Vector3 to, Collider targetCollider)
    {
        Vector3 direction = to - from;
        float distance = direction.magnitude;
        if (distance <= 0.0001f)
        {
            return true;
        }

        RaycastHit[] hits = Physics.RaycastAll(from, direction / distance, distance, ~0, QueryTriggerInteraction.Ignore);
        const float endpointTolerance = 0.08f;
        const float startTolerance = 0.02f;

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider == null)
            {
                continue;
            }

            if (hit.collider == targetCollider && (hit.distance <= startTolerance || hit.distance >= distance - endpointTolerance))
            {
                continue;
            }

            return false;
        }

        return true;
    }
}
