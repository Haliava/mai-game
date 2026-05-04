using System.Collections.Generic;
using UnityEngine;

public class NavigationNode : MonoBehaviour
{
    public readonly List<NavigationNode> Neighbours = new List<NavigationNode>();
    public GameObject Platform { get; set; }
    public int ChunkIndex { get; set; }

    public Vector3 Position { get { return transform.position; } }

    public void Connect(NavigationNode other)
    {
        if (other == null || other == this) return;
        if (!Neighbours.Contains(other)) Neighbours.Add(other);
        if (!other.Neighbours.Contains(this)) other.Neighbours.Add(this);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.35f);
        Gizmos.color = new Color(0f, 1f, 0f, 0.35f);
        for (int i = 0; i < Neighbours.Count; i++)
        {
            if (Neighbours[i] != null) Gizmos.DrawLine(transform.position, Neighbours[i].transform.position);
        }
    }
}
