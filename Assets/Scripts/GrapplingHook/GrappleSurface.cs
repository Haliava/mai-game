using UnityEngine;

public class GrappleSurface : MonoBehaviour
{
    [SerializeField] bool allowTopSideEdges = true;
    [SerializeField] bool allowBottomSideEdges = true;
    [SerializeField] bool allowSideSideEdges = true;
    [SerializeField] float edgeSnapDistanceOverride = 0f;

    public float EdgeSnapDistanceOverride { get { return edgeSnapDistanceOverride; } }

    public bool IsSideAllowed(GrappleAttachSide side)
    {
        switch (side)
        {
            case GrappleAttachSide.TopSide:
                return allowTopSideEdges;
            case GrappleAttachSide.BottomSide:
                return allowBottomSideEdges;
            case GrappleAttachSide.SideSide:
                return allowSideSideEdges;
            default:
                return false;
        }
    }
}
