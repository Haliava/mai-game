using System.Collections.Generic;
using UnityEngine;

public class CentipedePathfinder : MonoBehaviour
{
    [SerializeField] SurfaceNavigationGraph graph;
    [SerializeField] float heuristicMultiplier = 1f;

    readonly List<SurfaceNavigationNode> currentPath = new List<SurfaceNavigationNode>();
    public IReadOnlyList<SurfaceNavigationNode> CurrentPath { get { return currentPath; } }

    public void SetGraph(SurfaceNavigationGraph navigationGraph)
    {
        graph = navigationGraph;
    }

    public List<SurfaceNavigationNode> FindBestPath(Vector3 from, Vector3 to, out bool usesJump)
    {
        usesJump = false;
        currentPath.Clear();
        if (graph == null || graph.Nodes.Count == 0) return currentPath;

        List<SurfaceNavigationNode> walkPath = FindPath(from, to, false, out usesJump);
        if (walkPath.Count > 0)
        {
            currentPath.AddRange(walkPath);
            usesJump = false;
            return currentPath;
        }

        List<SurfaceNavigationNode> mixedPath = FindPath(from, to, true, out usesJump);
        currentPath.AddRange(mixedPath);
        return currentPath;
    }

    public List<SurfaceNavigationNode> FindPath(Vector3 from, Vector3 to, bool allowJumpEdges, out bool usesJump)
    {
        usesJump = false;
        List<SurfaceNavigationNode> result = new List<SurfaceNavigationNode>();
        if (graph == null || graph.Nodes.Count == 0) return result;

        SurfaceNavigationNode start = graph.FindNearestNode(from);
        SurfaceNavigationNode goal = graph.FindNearestNode(to);
        if (start == null || goal == null) return result;
        if (start == goal)
        {
            result.Add(start);
            return result;
        }

        List<SurfaceNavigationNode> open = new List<SurfaceNavigationNode>();
        HashSet<SurfaceNavigationNode> closed = new HashSet<SurfaceNavigationNode>();
        Dictionary<SurfaceNavigationNode, SurfaceNavigationNode> cameFrom = new Dictionary<SurfaceNavigationNode, SurfaceNavigationNode>();
        Dictionary<SurfaceNavigationNode, float> gScore = new Dictionary<SurfaceNavigationNode, float>();
        Dictionary<SurfaceNavigationNode, float> fScore = new Dictionary<SurfaceNavigationNode, float>();

        open.Add(start);
        cameFrom[start] = null;
        gScore[start] = 0f;
        fScore[start] = Heuristic(start, goal);

        while (open.Count > 0)
        {
            SurfaceNavigationNode current = PopLowest(open, fScore);
            if (current == goal)
            {
                result = Reconstruct(cameFrom, current);
                usesJump = PathUsesJump(result);
                return result;
            }

            closed.Add(current);
            for (int i = 0; i < current.Edges.Count; i++)
            {
                SurfaceNavigationEdge edge = current.Edges[i];
                if (!allowJumpEdges && edge.Type == EdgeTraversalType.Jump) continue;
                SurfaceNavigationNode next = edge.To;
                if (next == null || closed.Contains(next)) continue;

                float tentative = gScore[current] + edge.Cost;
                float oldScore;
                if (gScore.TryGetValue(next, out oldScore) && tentative >= oldScore) continue;

                cameFrom[next] = current;
                gScore[next] = tentative;
                fScore[next] = tentative + Heuristic(next, goal);
                if (!open.Contains(next)) open.Add(next);
            }
        }

        return result;
    }

    public SurfaceNavigationNode FindUsefulFallbackNode(Vector3 from, Vector3 target)
    {
        if (graph == null || graph.Nodes.Count == 0) return null;

        SurfaceNavigationNode direct = graph.FindNearestNodeToward(from, target);
        if (direct != null) return direct;
        return graph.FindNearestNode(target);
    }

    float Heuristic(SurfaceNavigationNode a, SurfaceNavigationNode b)
    {
        return Vector3.Distance(a.Position, b.Position) * heuristicMultiplier;
    }

    SurfaceNavigationNode PopLowest(List<SurfaceNavigationNode> open, Dictionary<SurfaceNavigationNode, float> scores)
    {
        int bestIndex = 0;
        float bestScore = float.MaxValue;
        for (int i = 0; i < open.Count; i++)
        {
            float score;
            if (!scores.TryGetValue(open[i], out score)) score = float.MaxValue;
            if (score >= bestScore) continue;
            bestScore = score;
            bestIndex = i;
        }

        SurfaceNavigationNode node = open[bestIndex];
        open.RemoveAt(bestIndex);
        return node;
    }

    List<SurfaceNavigationNode> Reconstruct(Dictionary<SurfaceNavigationNode, SurfaceNavigationNode> cameFrom, SurfaceNavigationNode current)
    {
        List<SurfaceNavigationNode> result = new List<SurfaceNavigationNode>();
        while (current != null)
        {
            result.Insert(0, current);
            current = cameFrom.ContainsKey(current) ? cameFrom[current] : null;
        }
        return result;
    }

    bool PathUsesJump(List<SurfaceNavigationNode> path)
    {
        for (int i = 1; i < path.Count; i++)
        {
            SurfaceNavigationEdge edge = FindEdge(path[i - 1], path[i]);
            if (edge != null && edge.Type == EdgeTraversalType.Jump) return true;
        }
        return false;
    }

    public SurfaceNavigationEdge FindEdge(SurfaceNavigationNode from, SurfaceNavigationNode to)
    {
        if (from == null || to == null) return null;
        for (int i = 0; i < from.Edges.Count; i++)
        {
            if (from.Edges[i].To == to) return from.Edges[i];
        }
        return null;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        for (int i = 1; i < currentPath.Count; i++)
        {
            if (currentPath[i - 1] != null && currentPath[i] != null)
            {
                Gizmos.DrawLine(currentPath[i - 1].Position, currentPath[i].Position);
            }
        }
    }
}
