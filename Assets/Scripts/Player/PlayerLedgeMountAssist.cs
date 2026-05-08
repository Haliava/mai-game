using UnityEngine;

public class PlayerLedgeMountAssist : MonoBehaviour
{
    [SerializeField] bool requireTopSideEdge = true;
    [SerializeField] bool allowGrapplePointAssist = false;

    public bool CanAssist(GrappleEdgeCandidate attachedEdge, bool isPullingRope)
    {
        if (!isPullingRope) return false;
        if (!attachedEdge.IsValid) return allowGrapplePointAssist && !requireTopSideEdge;
        return !requireTopSideEdge || attachedEdge.AttachSide == GrappleAttachSide.TopSide;
    }
}
