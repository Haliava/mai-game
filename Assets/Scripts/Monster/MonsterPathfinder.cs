using System.Collections.Generic;
using UnityEngine;

public class MonsterPathfinder : MonoBehaviour
{
    [SerializeField] ProceduralLevelGenerator generator;
    [SerializeField] float nodeSearchRadius = 80f;
    [SerializeField] float walkableStepDistance = 10f;
    [SerializeField] float jumpPenalty = 100f;
    [SerializeField] float jumpDistanceCost = 0.05f;
    [SerializeField] float maxJumpEdgeDistance = 30f;
    [SerializeField] float maxJumpUpHeight = 18f;
    [SerializeField] float maxJumpDownHeight = 24f;

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

        List<NavigationNode> open = new List<NavigationNode>();
        Dictionary<NavigationNode, NavigationNode> cameFrom = new Dictionary<NavigationNode, NavigationNode>();
        Dictionary<NavigationNode, float> costSoFar = new Dictionary<NavigationNode, float>();
        open.Add(start);
        cameFrom[start] = null;
        costSoFar[start] = 0f;

        while (open.Count > 0)
        {
            NavigationNode current = PopCheapest(open, costSoFar);
            if (current == goal) break;

            for (int i = 0; i < generator.AllNodes.Count; i++)
            {
                NavigationNode next = generator.AllNodes[i];
                if (next == null || next == current || !next.gameObject.activeInHierarchy) continue;

                float distance = Vector3.Distance(current.Position, next.Position);
                bool walkable = current.Neighbours.Contains(next) && distance <= walkableStepDistance;
                if (!walkable && !CanUseJumpEdge(current.Position, next.Position)) continue;

                float edgeCost = walkable ? distance : jumpPenalty + distance * jumpDistanceCost;
                float newCost = costSoFar[current] + edgeCost;
                float oldCost;
                if (costSoFar.TryGetValue(next, out oldCost) && newCost >= oldCost) continue;

                costSoFar[next] = newCost;
                cameFrom[next] = current;
                if (!open.Contains(next)) open.Add(next);
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

    bool CanUseJumpEdge(Vector3 from, Vector3 to)
    {
        Vector3 delta = to - from;
        if (delta.magnitude > maxJumpEdgeDistance) return false;
        if (delta.y > maxJumpUpHeight) return false;
        if (-delta.y > maxJumpDownHeight) return false;
        return true;
    }

    NavigationNode PopCheapest(List<NavigationNode> open, Dictionary<NavigationNode, float> costs)
    {
        int bestIndex = 0;
        float bestCost = float.MaxValue;
        for (int i = 0; i < open.Count; i++)
        {
            float cost;
            if (!costs.TryGetValue(open[i], out cost)) cost = float.MaxValue;
            if (cost < bestCost)
            {
                bestCost = cost;
                bestIndex = i;
            }
        }

        NavigationNode node = open[bestIndex];
        open.RemoveAt(bestIndex);
        return node;
    }

    NavigationNode FindNearestNode(Vector3 position)
    {
        NavigationNode best = null;
        NavigationNode fallback = null;
        float bestDistance = nodeSearchRadius * nodeSearchRadius;
        float fallbackDistance = float.MaxValue;
        for (int i = 0; i < generator.AllNodes.Count; i++)
        {
            NavigationNode node = generator.AllNodes[i];
            if (node == null || !node.gameObject.activeInHierarchy) continue;
            float distance = (node.Position - position).sqrMagnitude;
            if (distance < fallbackDistance)
            {
                fallbackDistance = distance;
                fallback = node;
            }
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = node;
            }
        }
        return best != null ? best : fallback;
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
