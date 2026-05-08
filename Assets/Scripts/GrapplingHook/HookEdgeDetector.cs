using UnityEngine;

public class HookEdgeDetector : MonoBehaviour
{
    [SerializeField] float maxEdgeSnapDistance = 1.05f;
    [SerializeField] bool requireGrappleSurface = false;
    [SerializeField] bool useBoundsFallback = true;
    [SerializeField, Range(0.1f, 0.95f)] float verticalFaceDot = 0.65f;

    public float MaxEdgeSnapDistance { get { return maxEdgeSnapDistance; } }

    public bool TryFindEdge(Collision collision, out GrappleEdgeCandidate candidate)
    {
        candidate = default(GrappleEdgeCandidate);
        if (collision == null || collision.collider == null || collision.contactCount == 0) return false;

        ContactPoint contact = collision.GetContact(0);
        return TryFindNearestEdge(collision.collider, contact.point, contact.normal, out candidate);
    }

    public bool TryFindEdge(Collision collision, Vector3 probePoint, out GrappleEdgeCandidate candidate)
    {
        candidate = default(GrappleEdgeCandidate);
        if (collision == null || collision.collider == null || collision.contactCount == 0) return false;

        float bestDistance = float.MaxValue;
        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint contact = collision.GetContact(i);
            TryKeepBestEdge(collision.collider, contact.point, contact.normal, ref candidate, ref bestDistance);
        }

        Vector3 averageNormal = Vector3.zero;
        for (int i = 0; i < collision.contactCount; i++)
        {
            averageNormal += collision.GetContact(i).normal;
        }

        if (averageNormal.sqrMagnitude < 0.001f) averageNormal = collision.GetContact(0).normal;
        TryKeepBestEdge(collision.collider, probePoint, averageNormal.normalized, ref candidate, ref bestDistance);
        return candidate.IsValid;
    }

    void TryKeepBestEdge(Collider targetCollider, Vector3 worldPoint, Vector3 contactNormal, ref GrappleEdgeCandidate best, ref float bestDistance)
    {
        GrappleEdgeCandidate candidate;
        if (!TryFindNearestEdge(targetCollider, worldPoint, contactNormal, out candidate)) return;
        if (candidate.DistanceToContact >= bestDistance) return;

        best = candidate;
        bestDistance = candidate.DistanceToContact;
    }

    public bool TryFindNearestEdge(Collider targetCollider, Vector3 worldPoint, Vector3 contactNormal, out GrappleEdgeCandidate candidate)
    {
        candidate = default(GrappleEdgeCandidate);
        if (targetCollider == null) return false;

        GrappleSurface surface = targetCollider.GetComponentInParent<GrappleSurface>();
        if (surface == null && requireGrappleSurface) return false;

        BoxCollider box = targetCollider as BoxCollider;
        bool found = box != null
            ? TryFindBoxEdge(box, surface, worldPoint, contactNormal, out candidate)
            : useBoundsFallback && TryFindBoundsEdge(targetCollider, surface, worldPoint, contactNormal, out candidate);

        if (!found) return false;

        float allowedDistance = surface != null && surface.EdgeSnapDistanceOverride > 0f
            ? surface.EdgeSnapDistanceOverride
            : maxEdgeSnapDistance;

        if (candidate.DistanceToContact > allowedDistance)
        {
            candidate = default(GrappleEdgeCandidate);
            return false;
        }

        if (surface != null && !surface.IsSideAllowed(candidate.AttachSide))
        {
            candidate = default(GrappleEdgeCandidate);
            return false;
        }

        return true;
    }

    bool TryFindBoxEdge(BoxCollider box, GrappleSurface surface, Vector3 worldPoint, Vector3 contactNormal, out GrappleEdgeCandidate best)
    {
        best = default(GrappleEdgeCandidate);

        Transform t = box.transform;
        Vector3 localPoint = t.InverseTransformPoint(worldPoint) - box.center;
        Vector3 extents = box.size * 0.5f;
        if (extents.x <= 0f || extents.y <= 0f || extents.z <= 0f) return false;

        float bestDistance = float.MaxValue;
        for (int lockedA = 0; lockedA < 3; lockedA++)
        {
            for (int lockedB = lockedA + 1; lockedB < 3; lockedB++)
            {
                int freeAxis = 3 - lockedA - lockedB;
                for (int signA = -1; signA <= 1; signA += 2)
                {
                    for (int signB = -1; signB <= 1; signB += 2)
                    {
                        Vector3 localEdge = localPoint;
                        SetAxis(ref localEdge, lockedA, GetAxis(extents, lockedA) * signA);
                        SetAxis(ref localEdge, lockedB, GetAxis(extents, lockedB) * signB);
                        SetAxis(ref localEdge, freeAxis, Mathf.Clamp(GetAxis(localPoint, freeAxis), -GetAxis(extents, freeAxis), GetAxis(extents, freeAxis)));

                        Vector3 worldEdge = t.TransformPoint(localEdge + box.center);
                        float distance = Vector3.Distance(worldPoint, worldEdge);
                        if (distance >= bestDistance) continue;

                        Vector3 normalA = t.TransformDirection(GetAxisVector(lockedA) * signA).normalized;
                        Vector3 normalB = t.TransformDirection(GetAxisVector(lockedB) * signB).normalized;
                        GrappleAttachSide side = ClassifySide(normalA, normalB);
                        Vector3 edgeDirection = t.TransformDirection(GetAxisVector(freeAxis)).normalized;
                        Vector3 surfaceNormal = ChooseSurfaceNormal(contactNormal, normalA, normalB);

                        bestDistance = distance;
                        best = new GrappleEdgeCandidate(
                            box,
                            surface,
                            worldEdge,
                            edgeDirection,
                            surfaceNormal,
                            side,
                            distance,
                            side.ToString());
                    }
                }
            }
        }

        return best.IsValid;
    }

    bool TryFindBoundsEdge(Collider targetCollider, GrappleSurface surface, Vector3 worldPoint, Vector3 contactNormal, out GrappleEdgeCandidate best)
    {
        best = default(GrappleEdgeCandidate);
        Bounds bounds = targetCollider.bounds;
        Vector3 localPoint = worldPoint - bounds.center;
        Vector3 extents = bounds.extents;
        if (extents.x <= 0f || extents.y <= 0f || extents.z <= 0f) return false;

        float bestDistance = float.MaxValue;
        for (int lockedA = 0; lockedA < 3; lockedA++)
        {
            for (int lockedB = lockedA + 1; lockedB < 3; lockedB++)
            {
                int freeAxis = 3 - lockedA - lockedB;
                for (int signA = -1; signA <= 1; signA += 2)
                {
                    for (int signB = -1; signB <= 1; signB += 2)
                    {
                        Vector3 edge = localPoint;
                        SetAxis(ref edge, lockedA, GetAxis(extents, lockedA) * signA);
                        SetAxis(ref edge, lockedB, GetAxis(extents, lockedB) * signB);
                        SetAxis(ref edge, freeAxis, Mathf.Clamp(GetAxis(localPoint, freeAxis), -GetAxis(extents, freeAxis), GetAxis(extents, freeAxis)));

                        Vector3 worldEdge = bounds.center + edge;
                        float distance = Vector3.Distance(worldPoint, worldEdge);
                        if (distance >= bestDistance) continue;

                        Vector3 normalA = GetAxisVector(lockedA) * signA;
                        Vector3 normalB = GetAxisVector(lockedB) * signB;
                        GrappleAttachSide side = ClassifySide(normalA, normalB);
                        Vector3 edgeDirection = GetAxisVector(freeAxis);
                        Vector3 surfaceNormal = ChooseSurfaceNormal(contactNormal, normalA, normalB);

                        bestDistance = distance;
                        best = new GrappleEdgeCandidate(
                            targetCollider,
                            surface,
                            worldEdge,
                            edgeDirection,
                            surfaceNormal,
                            side,
                            distance,
                            side.ToString());
                    }
                }
            }
        }

        return best.IsValid;
    }

    GrappleAttachSide ClassifySide(Vector3 normalA, Vector3 normalB)
    {
        bool top = Vector3.Dot(normalA, Vector3.up) > verticalFaceDot || Vector3.Dot(normalB, Vector3.up) > verticalFaceDot;
        bool bottom = Vector3.Dot(normalA, Vector3.up) < -verticalFaceDot || Vector3.Dot(normalB, Vector3.up) < -verticalFaceDot;

        if (top) return GrappleAttachSide.TopSide;
        if (bottom) return GrappleAttachSide.BottomSide;
        return GrappleAttachSide.SideSide;
    }

    Vector3 ChooseSurfaceNormal(Vector3 contactNormal, Vector3 normalA, Vector3 normalB)
    {
        if (contactNormal.sqrMagnitude < 0.001f) return (normalA + normalB).normalized;
        return Vector3.Dot(contactNormal.normalized, normalA) > Vector3.Dot(contactNormal.normalized, normalB) ? normalA : normalB;
    }

    static Vector3 GetAxisVector(int axis)
    {
        switch (axis)
        {
            case 0: return Vector3.right;
            case 1: return Vector3.up;
            default: return Vector3.forward;
        }
    }

    static float GetAxis(Vector3 value, int axis)
    {
        switch (axis)
        {
            case 0: return value.x;
            case 1: return value.y;
            default: return value.z;
        }
    }

    static void SetAxis(ref Vector3 value, int axis, float component)
    {
        switch (axis)
        {
            case 0:
                value.x = component;
                break;
            case 1:
                value.y = component;
                break;
            default:
                value.z = component;
                break;
        }
    }
}
