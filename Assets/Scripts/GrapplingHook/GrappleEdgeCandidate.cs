using UnityEngine;

public struct GrappleEdgeCandidate
{
    public bool IsValid;
    public Collider Collider;
    public GrappleSurface Surface;
    public Vector3 EdgePoint;
    public Vector3 EdgeDirection;
    public Vector3 SurfaceNormal;
    public GrappleAttachSide AttachSide;
    public float DistanceToContact;
    public string Label;

    public GrappleEdgeCandidate(
        Collider collider,
        GrappleSurface surface,
        Vector3 edgePoint,
        Vector3 edgeDirection,
        Vector3 surfaceNormal,
        GrappleAttachSide attachSide,
        float distanceToContact,
        string label)
    {
        IsValid = true;
        Collider = collider;
        Surface = surface;
        EdgePoint = edgePoint;
        EdgeDirection = edgeDirection.sqrMagnitude > 0.001f ? edgeDirection.normalized : Vector3.forward;
        SurfaceNormal = surfaceNormal.sqrMagnitude > 0.001f ? surfaceNormal.normalized : Vector3.up;
        AttachSide = attachSide;
        DistanceToContact = distanceToContact;
        Label = label;
    }
}
