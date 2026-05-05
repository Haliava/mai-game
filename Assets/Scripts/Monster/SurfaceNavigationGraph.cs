using System.Collections.Generic;
using UnityEngine;

public class SurfaceNavigationGraph : MonoBehaviour
{
    [SerializeField] LayerMask walkableMask = ~0;
    [SerializeField] float graphRadius = 80f;
    [SerializeField] float rebuildInterval = 2.5f;
    [SerializeField] float rebuildMoveDistance = 18f;
    [SerializeField] float nodeSurfaceOffset = 0.75f;
    [SerializeField] float nodeSampleProbeRadius = 0.22f;
    [SerializeField] float nodeSamplePadding = 6f;
    [SerializeField] float minNodeSeparation = 1.3f;
    [SerializeField] float walkConnectionDistance = 18f;
    [SerializeField] float adjacentSurfaceWalkDistance = 4f;
    [SerializeField] float jumpConnectionMaxConsiderDistance = 70f;
    [SerializeField] int maxJumpEdgesPerNode = 6;
    [SerializeField] int maxSampledColliders = 72;
    [SerializeField] int maxNodes = 360;
    [SerializeField] bool sampleColliderCorners = false;
    [SerializeField] float walkCostMultiplier = 1f;
    [SerializeField] float jumpBasePenalty = 10000f;
    [SerializeField] float jumpDistanceCostMultiplier = 50f;
    [SerializeField] bool drawWalkEdges = true;
    [SerializeField] bool drawJumpEdges = true;

    readonly List<SurfaceNavigationNode> nodes = new List<SurfaceNavigationNode>();
    Transform ignoredRoot;
    Vector3 lastBuildCenter;
    float nextBuildTime;
    int nextNodeId;

    public IReadOnlyList<SurfaceNavigationNode> Nodes { get { return nodes; } }
    public LayerMask WalkableMask { get { return walkableMask; } }
    public float NodeSurfaceOffset { get { return nodeSurfaceOffset; } }
    public float WalkCostMultiplier { get { return walkCostMultiplier; } }
    public float JumpBasePenalty { get { return jumpBasePenalty; } }
    public float JumpDistanceCostMultiplier { get { return jumpDistanceCostMultiplier; } }

    public void Configure(LayerMask mask, Transform rootToIgnore)
    {
        walkableMask = mask;
        ignoredRoot = rootToIgnore;
    }

    public bool NeedsRebuild(Vector3 center)
    {
        if (nodes.Count == 0) return true;
        if (Time.time < nextBuildTime) return false;
        return Vector3.Distance(center, lastBuildCenter) >= rebuildMoveDistance;
    }

    public void RebuildAround(Vector3 center)
    {
        nodes.Clear();
        nextNodeId = 0;
        lastBuildCenter = center;
        nextBuildTime = Time.time + Mathf.Max(0.05f, rebuildInterval);

        Collider[] colliders = Physics.OverlapSphere(center, graphRadius, walkableMask, QueryTriggerInteraction.Ignore);
        System.Array.Sort(colliders, (a, b) =>
            Vector3.SqrMagnitude(a.bounds.center - center).CompareTo(Vector3.SqrMagnitude(b.bounds.center - center)));

        int sampled = 0;
        for (int i = 0; i < colliders.Length; i++)
        {
            if (!IsUsableCollider(colliders[i])) continue;
            if (sampled >= maxSampledColliders || nodes.Count >= maxNodes) break;
            SampleCollider(colliders[i]);
            sampled++;
        }

        ConnectWalkEdges();
        ConnectJumpEdges();
    }

    public SurfaceNavigationNode FindNearestNode(Vector3 position)
    {
        SurfaceNavigationNode best = null;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < nodes.Count; i++)
        {
            float distance = (nodes[i].Position - position).sqrMagnitude;
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            best = nodes[i];
        }
        return best;
    }

    public SurfaceNavigationNode FindNearestNodeToward(Vector3 from, Vector3 target)
    {
        SurfaceNavigationNode best = null;
        float bestScore = float.MaxValue;
        for (int i = 0; i < nodes.Count; i++)
        {
            float targetDistance = Vector3.Distance(nodes[i].Position, target);
            float selfDistance = Vector3.Distance(nodes[i].Position, from) * 0.15f;
            float score = targetDistance + selfDistance;
            if (score >= bestScore) continue;
            bestScore = score;
            best = nodes[i];
        }
        return best;
    }

    bool IsUsableCollider(Collider col)
    {
        if (col == null || !col.enabled || col.isTrigger) return false;
        if (!col.gameObject.activeInHierarchy) return false;
        if (ignoredRoot != null && col.transform.IsChildOf(ignoredRoot)) return false;
        return true;
    }

    void SampleCollider(Collider col)
    {
        Bounds bounds = col.bounds;
        Vector3[] directions = new Vector3[]
        {
            Vector3.up,
            Vector3.down,
            Vector3.right,
            Vector3.left,
            Vector3.forward,
            Vector3.back,
            col.transform.up,
            -col.transform.up,
            col.transform.right,
            -col.transform.right,
            col.transform.forward,
            -col.transform.forward
        };

        for (int i = 0; i < directions.Length; i++)
        {
            Vector3 direction = directions[i];
            if (direction.sqrMagnitude < 0.001f) continue;
            RaycastHit hit;
            if (TrySampleColliderDirection(col, bounds, direction.normalized, out hit))
            {
                AddNodeIfSeparated(hit.point + hit.normal * nodeSurfaceOffset, hit.normal, col);
            }
        }

        if (sampleColliderCorners && nodes.Count < maxNodes)
        {
            AddBoundsCornerSamples(col, bounds);
        }
    }

    void AddBoundsCornerSamples(Collider col, Bounds bounds)
    {
        Vector3 center = bounds.center;
        Vector3 ext = bounds.extents;
        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 direction = new Vector3(ext.x * x, ext.y * y, ext.z * z);
                    if (direction.sqrMagnitude < 0.001f) continue;
                    RaycastHit hit;
                    if (TrySampleColliderDirection(col, bounds, direction.normalized, out hit))
                    {
                        AddNodeIfSeparated(hit.point + hit.normal * nodeSurfaceOffset, hit.normal, col);
                    }
                }
            }
        }
    }

    bool TrySampleColliderDirection(Collider col, Bounds bounds, Vector3 outward, out RaycastHit hit)
    {
        hit = default(RaycastHit);
        float castDistance = bounds.extents.magnitude * 2f + nodeSamplePadding * 2f;
        Vector3 origin = bounds.center + outward * (bounds.extents.magnitude + nodeSamplePadding);
        RaycastHit[] hits = Physics.SphereCastAll(
            origin,
            Mathf.Max(0.01f, nodeSampleProbeRadius),
            -outward,
            castDistance,
            walkableMask,
            QueryTriggerInteraction.Ignore);

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider != col) continue;
            hit = hits[i];
            return true;
        }
        return false;
    }

    void AddNodeIfSeparated(Vector3 position, Vector3 normal, Collider source)
    {
        if (nodes.Count >= maxNodes) return;
        float minSeparationSqr = minNodeSeparation * minNodeSeparation;
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].SourceCollider != source) continue;
            if (Vector3.SqrMagnitude(nodes[i].Position - position) > minSeparationSqr) continue;
            if (Vector3.Dot(nodes[i].Normal, normal.normalized) < 0.65f) continue;
            return;
        }
        nodes.Add(new SurfaceNavigationNode(nextNodeId++, position, normal, source));
    }

    void ConnectWalkEdges()
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            for (int j = i + 1; j < nodes.Count; j++)
            {
                if (!ShouldCreateWalkEdge(nodes[i], nodes[j])) continue;
                AddBidirectionalEdge(nodes[i], nodes[j], EdgeTraversalType.Walk);
            }
        }
    }

    bool ShouldCreateWalkEdge(SurfaceNavigationNode a, SurfaceNavigationNode b)
    {
        float distance = Vector3.Distance(a.Position, b.Position);
        if (a.SourceCollider == b.SourceCollider && distance <= walkConnectionDistance) return true;
        if (distance <= adjacentSurfaceWalkDistance) return true;
        return false;
    }

    void ConnectJumpEdges()
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            List<SurfaceNavigationNode> candidates = new List<SurfaceNavigationNode>();
            for (int j = 0; j < nodes.Count; j++)
            {
                if (i == j) continue;
                if (HasEdge(nodes[i], nodes[j], EdgeTraversalType.Walk)) continue;
                float distance = Vector3.Distance(nodes[i].Position, nodes[j].Position);
                if (distance > jumpConnectionMaxConsiderDistance) continue;
                candidates.Add(nodes[j]);
            }

            candidates.Sort((a, b) =>
                Vector3.SqrMagnitude(a.Position - nodes[i].Position).CompareTo(
                    Vector3.SqrMagnitude(b.Position - nodes[i].Position)));

            int edgeCount = Mathf.Min(maxJumpEdgesPerNode, candidates.Count);
            for (int c = 0; c < edgeCount; c++)
            {
                AddDirectedEdge(nodes[i], candidates[c], EdgeTraversalType.Jump);
            }
        }
    }

    bool HasEdge(SurfaceNavigationNode from, SurfaceNavigationNode to, EdgeTraversalType type)
    {
        for (int i = 0; i < from.Edges.Count; i++)
        {
            if (from.Edges[i].To == to && from.Edges[i].Type == type) return true;
        }
        return false;
    }

    void AddBidirectionalEdge(SurfaceNavigationNode a, SurfaceNavigationNode b, EdgeTraversalType type)
    {
        AddDirectedEdge(a, b, type);
        AddDirectedEdge(b, a, type);
    }

    void AddDirectedEdge(SurfaceNavigationNode from, SurfaceNavigationNode to, EdgeTraversalType type)
    {
        float distance = Vector3.Distance(from.Position, to.Position);
        float jumpDistance = type == EdgeTraversalType.Jump ? distance : 0f;
        float cost = type == EdgeTraversalType.Walk
            ? distance * walkCostMultiplier
            : jumpBasePenalty + jumpDistance * jumpDistance * jumpDistanceCostMultiplier;
        from.AddEdge(new SurfaceNavigationEdge(from, to, type, distance, jumpDistance, cost));
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 1f, 0.45f, 0.8f);
        for (int i = 0; i < nodes.Count; i++)
        {
            Gizmos.DrawWireSphere(nodes[i].Position, 0.35f);
            Gizmos.DrawLine(nodes[i].Position, nodes[i].Position + nodes[i].Normal * 1.2f);
        }

        for (int i = 0; i < nodes.Count; i++)
        {
            for (int j = 0; j < nodes[i].Edges.Count; j++)
            {
                SurfaceNavigationEdge edge = nodes[i].Edges[j];
                if (edge.Type == EdgeTraversalType.Walk && !drawWalkEdges) continue;
                if (edge.Type == EdgeTraversalType.Jump && !drawJumpEdges) continue;
                Gizmos.color = edge.Type == EdgeTraversalType.Walk
                    ? new Color(0.1f, 1f, 0.2f, 0.35f)
                    : new Color(1f, 0.85f, 0.1f, 0.18f);
                Gizmos.DrawLine(edge.From.Position, edge.To.Position);
            }
        }
    }
}
