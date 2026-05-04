using System.Collections.Generic;
using UnityEngine;

public class MonsterPathfinder : MonoBehaviour
{
    [SerializeField] ProceduralLevelGenerator generator;
    [SerializeField] float nodeSearchRadius = 80f;

    readonly List<NavigationNode> path = new List<NavigationNode>();
    public IReadOnlyList<NavigationNode> CurrentPath { get { return path; } }

    public List<NavigationNode> FindPath(Vector3 from, Vector3 to)
    {
        path.Clear();
        if (generator == null) generator = FindAnyObjectByType<ProceduralLevelGenerator>();
        if (generator == null || generator.AllNodes.Count == 0) return path;

        NavigationNode start = FindNearestNode(from);
        NavigationNode goal = FindNearestNode(to);
        if (start == null || goal == null) return path;

        Queue<NavigationNode> queue = new Queue<NavigationNode>();
        Dictionary<NavigationNode, NavigationNode> cameFrom = new Dictionary<NavigationNode, NavigationNode>();
        queue.Enqueue(start);
        cameFrom[start] = null;

        while (queue.Count > 0)
        {
            NavigationNode current = queue.Dequeue();
            if (current == goal) break;
            for (int i = 0; i < current.Neighbours.Count; i++)
            {
                NavigationNode next = current.Neighbours[i];
                if (next == null || cameFrom.ContainsKey(next)) continue;
                cameFrom[next] = current;
                queue.Enqueue(next);
            }
        }

        if (!cameFrom.ContainsKey(goal)) return path;

        NavigationNode step = goal;
        while (step != null)
        {
            path.Insert(0, step);
            step = cameFrom[step];
        }
        return path;
    }

    NavigationNode FindNearestNode(Vector3 position)
    {
        NavigationNode best = null;
        float bestDistance = nodeSearchRadius * nodeSearchRadius;
        for (int i = 0; i < generator.AllNodes.Count; i++)
        {
            NavigationNode node = generator.AllNodes[i];
            if (node == null || !node.gameObject.activeInHierarchy) continue;
            float distance = (node.Position - position).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = node;
            }
        }
        return best;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        for (int i = 1; i < path.Count; i++)
        {
            if (path[i - 1] != null && path[i] != null) Gizmos.DrawLine(path[i - 1].Position, path[i].Position);
        }
    }
}
