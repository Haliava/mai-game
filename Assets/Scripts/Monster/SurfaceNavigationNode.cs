using System.Collections.Generic;
using UnityEngine;

public class SurfaceNavigationNode
{
    public int Id { get; private set; }
    public Vector3 Position { get; private set; }
    public Vector3 Normal { get; private set; }
    public Collider SourceCollider { get; private set; }
    public readonly List<SurfaceNavigationEdge> Edges = new List<SurfaceNavigationEdge>();

    public SurfaceNavigationNode(int id, Vector3 position, Vector3 normal, Collider sourceCollider)
    {
        Id = id;
        Position = position;
        Normal = normal.sqrMagnitude > 0.001f ? normal.normalized : Vector3.up;
        SourceCollider = sourceCollider;
    }

    public void AddEdge(SurfaceNavigationEdge edge)
    {
        if (edge == null || edge.To == null || edge.To == this) return;
        for (int i = 0; i < Edges.Count; i++)
        {
            if (Edges[i].To == edge.To && Edges[i].Type == edge.Type) return;
        }
        Edges.Add(edge);
    }
}
