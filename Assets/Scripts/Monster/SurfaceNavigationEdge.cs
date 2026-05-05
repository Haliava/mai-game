using UnityEngine;

public class SurfaceNavigationEdge
{
    public SurfaceNavigationNode From { get; private set; }
    public SurfaceNavigationNode To { get; private set; }
    public EdgeTraversalType Type { get; private set; }
    public float Distance { get; private set; }
    public float JumpDistance { get; private set; }
    public float Cost { get; private set; }

    public SurfaceNavigationEdge(
        SurfaceNavigationNode from,
        SurfaceNavigationNode to,
        EdgeTraversalType type,
        float distance,
        float jumpDistance,
        float cost)
    {
        From = from;
        To = to;
        Type = type;
        Distance = distance;
        JumpDistance = jumpDistance;
        Cost = cost;
    }
}
