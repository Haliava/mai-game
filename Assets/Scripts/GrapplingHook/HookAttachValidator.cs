using UnityEngine;

public class HookAttachValidator : MonoBehaviour
{
    [SerializeField] bool requireWrapForBottomSide = false;
    [SerializeField] float playerAboveEdgeHeight = 0.35f;
    [SerializeField] float downwardThrowVelocity = -0.5f;
    [SerializeField] float minHookSpeedForDirectAttach = 0f;
    [SerializeField] float minRopeTensionForWrapAttach = 2f;
    [SerializeField, Range(-1f, 1f)] float minApproachDot = -1f;

    public string LastRejectReason { get; private set; }

    public bool Validate(
        GrappleEdgeCandidate candidate,
        Vector3 playerPosition,
        Vector3 hookVelocity,
        RopeCollisionTracker collisionTracker,
        float ropeTension,
        bool forceWrappedAttach,
        out string rejectReason)
    {
        rejectReason = string.Empty;

        if (!candidate.IsValid)
        {
            rejectReason = "Rejected: not near edge";
            LastRejectReason = rejectReason;
            return false;
        }

        if (candidate.Surface != null && !candidate.Surface.IsSideAllowed(candidate.AttachSide))
        {
            rejectReason = "Rejected: edge side disabled on GrappleSurface";
            LastRejectReason = rejectReason;
            return false;
        }

        bool hasWrap = forceWrappedAttach || (collisionTracker != null && collisionTracker.HasConfirmedWrap(candidate.Collider, ropeTension));
        bool playerAbove = playerPosition.y > candidate.EdgePoint.y + playerAboveEdgeHeight;
        bool thrownDown = hookVelocity.y < downwardThrowVelocity;

        if (candidate.AttachSide == GrappleAttachSide.BottomSide && requireWrapForBottomSide)
        {
            if (!hasWrap && playerAbove && thrownDown)
            {
                rejectReason = "Rejected: bottom edge from above without wrap";
                LastRejectReason = rejectReason;
                return false;
            }

            if (!hasWrap)
            {
                rejectReason = ropeTension < minRopeTensionForWrapAttach
                    ? "Rejected: rope not tensioned"
                    : "Rejected: insufficient wrap history";
                LastRejectReason = rejectReason;
                return false;
            }
        }

        if (!hasWrap && hookVelocity.magnitude < minHookSpeedForDirectAttach)
        {
            rejectReason = "Rejected: invalid approach angle";
            LastRejectReason = rejectReason;
            return false;
        }

        if (!hasWrap && candidate.SurfaceNormal.sqrMagnitude > 0.001f && hookVelocity.sqrMagnitude > 0.001f)
        {
            float approachDot = Vector3.Dot(hookVelocity.normalized, -candidate.SurfaceNormal.normalized);
            if (approachDot < minApproachDot)
            {
                rejectReason = "Rejected: invalid approach angle";
                LastRejectReason = rejectReason;
                return false;
            }
        }

        rejectReason = "Allowed: " + candidate.AttachSide;
        LastRejectReason = rejectReason;
        return true;
    }
}
