using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-20)]
[RequireComponent(typeof(SurfaceCrawlerMotor))]
public sealed class CentipedePathfinder : MonoBehaviour
{
    private sealed class Node
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Collider Collider;
        public int SurfaceId;
        public readonly List<Edge> Edges = new();
    }

    private readonly struct Edge
    {
        public readonly int To;
        public readonly float Cost;
        public readonly bool IsJump;

        public Edge(int to, float cost, bool isJump)
        {
            To = to;
            Cost = cost;
            IsJump = isJump;
        }
    }

    [Header("References")]
    [SerializeField] private Transform target;
    [SerializeField] private Transform ignoredRoot;
    [SerializeField] private LayerMask crawlableSurfaceMask = ~0;

    [Header("Pathfinding")]
    [SerializeField, Min(0.05f)] private float repathInterval = 0.7f;
    [SerializeField, Min(0.25f)] private float nodeSpacing = 4f;
    [SerializeField, Min(2f)] private float surfaceScanRadius = 36f;
    [SerializeField, Min(0.25f)] private float maxWalkLinkDistance = 6f;
    [SerializeField, Min(0.25f)] private float maxTransitionLinkDistance = 3.5f;
    [SerializeField, Min(0.25f)] private float maxJumpDistance = 30f;
    [SerializeField] private bool limitJumpDistanceByPlayArea = true;
    [SerializeField, Range(0.05f, 1f)] private float maxJumpDistanceAsPlayAreaFraction = 0.333f;
    [SerializeField, Min(1f)] private float fallbackPlayAreaDiameter = 90f;
    [SerializeField, Min(0f)] private float introJumpLockDuration = 5f;
    [SerializeField, Min(0f)] private float jumpPenaltyBase = 30f;
    [SerializeField, Min(0f)] private float jumpPenaltyDistanceMultiplier = 3f;
    [SerializeField, Min(0f)] private float noJumpPreferenceWeight = 75f;
    [SerializeField, Min(0f)] private float targetPredictionTime = 0.35f;
    [SerializeField, Min(0.05f)] private float waypointReachedDistance = 1.1f;
    [SerializeField, Min(0f)] private float surfaceOffset = 0.48f;
    [SerializeField, Range(16, 256)] private int maxNodes = 96;
    [SerializeField, Range(4, 128)] private int maxScannedColliders = 48;

    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = true;
    [SerializeField] private bool logPathEvents;

    private readonly List<Node> nodes = new();
    private readonly List<Vector3> currentPath = new();
    private readonly List<bool> currentPathJumpFlags = new();
    private SurfaceCrawlerMotor motor;
    private Vector3 lastTargetPosition;
    private float nextRepathTime;
    private int currentPathIndex;
    private int currentPathJumpCount;
    private float nextJumpRejectLogTime;
    private bool hasPath;

    public Transform Target
    {
        get => target;
        set => target = value;
    }

    public LayerMask CrawlableSurfaceMask
    {
        get => crawlableSurfaceMask;
        set => crawlableSurfaceMask = value;
    }

    public Transform IgnoredRoot
    {
        get => ignoredRoot;
        set => ignoredRoot = value;
    }

    public int CurrentPathJumpCount => currentPathJumpCount;
    public bool HasPath => hasPath;

    private void Awake()
    {
        CacheReferences();
    }

    private void OnValidate()
    {
        repathInterval = Mathf.Max(0.05f, repathInterval);
        nodeSpacing = Mathf.Max(0.25f, nodeSpacing);
        maxWalkLinkDistance = Mathf.Max(0.25f, maxWalkLinkDistance);
        maxTransitionLinkDistance = Mathf.Max(0.25f, maxTransitionLinkDistance);
        maxJumpDistance = Mathf.Max(maxTransitionLinkDistance, maxJumpDistance);
        fallbackPlayAreaDiameter = Mathf.Max(1f, fallbackPlayAreaDiameter);
        maxNodes = Mathf.Max(16, maxNodes);
    }

    public void Tick(float deltaTime)
    {
        CacheReferences();
        if (target == null)
        {
            hasPath = false;
            return;
        }

        Vector3 predictedTarget = PredictTargetPosition();
        bool targetMoved = Vector3.Distance(predictedTarget, lastTargetPosition) > nodeSpacing * 0.65f;
        if (Time.time >= nextRepathTime || !hasPath || targetMoved)
        {
            RebuildPath(predictedTarget);
            nextRepathTime = Time.time + repathInterval;
            lastTargetPosition = predictedTarget;
        }

        AdvanceWaypoint();
    }

    public bool TryGetMoveTarget(out Vector3 moveTarget)
    {
        return TryGetMoveTarget(out moveTarget, out _);
    }

    public bool TryGetMoveTarget(out Vector3 moveTarget, out bool requiresJump)
    {
        if (hasPath && currentPath.Count > 0)
        {
            int index = Mathf.Clamp(currentPathIndex, 0, currentPath.Count - 1);
            moveTarget = currentPath[index];
            requiresJump = index < currentPathJumpFlags.Count && currentPathJumpFlags[index];
            return true;
        }

        moveTarget = target != null ? target.position : transform.position + transform.forward * 4f;
        requiresJump = false;
        return false;
    }

    [ContextMenu("Rebuild Centipede Path")]
    public void RebuildPathNow()
    {
        CacheReferences();
        if (target != null)
        {
            RebuildPath(PredictTargetPosition());
        }
    }

    private void CacheReferences()
    {
        if (motor == null)
        {
            motor = GetComponent<SurfaceCrawlerMotor>();
        }

        if (ignoredRoot == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                ignoredRoot = player.transform;
            }
        }
    }

    private Vector3 PredictTargetPosition()
    {
        if (target == null)
        {
            return transform.position;
        }

        CharacterController characterController = target.GetComponent<CharacterController>();
        Vector3 velocity = characterController != null ? characterController.velocity : Vector3.zero;
        return target.position + velocity * targetPredictionTime;
    }

    private void RebuildPath(Vector3 targetPosition)
    {
        nodes.Clear();
        currentPath.Clear();
        currentPathJumpFlags.Clear();
        currentPathIndex = 0;
        currentPathJumpCount = 0;
        hasPath = false;

        Vector3 startSurface = motor != null && motor.HasSurface ? motor.SurfacePoint : transform.position - transform.up * surfaceOffset;
        Vector3 startNormal = motor != null && motor.HasSurface ? motor.SurfaceNormal : transform.up;
        Collider startCollider = null;
        if (TryFindNearestSurface(transform.position, out Vector3 foundStartSurface, out Vector3 foundStartNormal, out Collider foundStartCollider))
        {
            startSurface = foundStartSurface;
            startNormal = foundStartNormal;
            startCollider = foundStartCollider;
        }
        int startIndex = AddNode(startSurface + startNormal * surfaceOffset, startNormal, startCollider, GetSurfaceId(startCollider, startNormal));

        int targetIndex = -1;
        if (TryFindNearestSurface(targetPosition, out Vector3 targetSurface, out Vector3 targetNormal, out Collider targetCollider))
        {
            targetIndex = AddNode(targetSurface + targetNormal * surfaceOffset, targetNormal, targetCollider, GetSurfaceId(targetCollider, targetNormal));
        }
        else
        {
            targetIndex = AddNode(targetPosition, Vector3.up, null, -2);
        }

        AddSurfaceSamples(startSurface, targetPosition);
        BuildEdges();

        if (RunAStar(startIndex, targetIndex))
        {
            hasPath = true;
            if (logPathEvents)
            {
                Debug.Log($"CentipedePathfinder: path found nodes={currentPath.Count}, jumps={currentPathJumpCount}.", this);
            }
        }
        else
        {
            hasPath = false;
            if (logPathEvents)
            {
                Debug.LogWarning("CentipedePathfinder: no path found, controller will use direct fallback.", this);
            }
        }
    }

    private void AddSurfaceSamples(Vector3 startSurface, Vector3 targetPosition)
    {
        Vector3 center = (startSurface + targetPosition) * 0.5f;
        float searchRadius = Mathf.Max(surfaceScanRadius, Vector3.Distance(startSurface, targetPosition) * 0.55f + 4f);
        Collider[] colliders = Physics.OverlapSphere(center, searchRadius, crawlableSurfaceMask, QueryTriggerInteraction.Ignore);
        int scanned = 0;

        for (int i = 0; i < colliders.Length && scanned < maxScannedColliders && nodes.Count < maxNodes; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || collider.isTrigger || IsIgnored(collider))
            {
                continue;
            }

            scanned++;
            AddColliderSamples(collider);
        }
    }

    private void AddColliderSamples(Collider collider)
    {
        if (collider is BoxCollider box)
        {
            AddBoxSamples(box);
            return;
        }

        Bounds bounds = collider.bounds;
        AddProbeSample(collider, bounds.center + Vector3.up * bounds.extents.y * 1.35f, Vector3.down);
        AddProbeSample(collider, bounds.center - Vector3.up * bounds.extents.y * 1.35f, Vector3.up);
        AddProbeSample(collider, bounds.center + Vector3.right * bounds.extents.x * 1.35f, Vector3.left);
        AddProbeSample(collider, bounds.center - Vector3.right * bounds.extents.x * 1.35f, Vector3.right);
        AddProbeSample(collider, bounds.center + Vector3.forward * bounds.extents.z * 1.35f, Vector3.back);
        AddProbeSample(collider, bounds.center - Vector3.forward * bounds.extents.z * 1.35f, Vector3.forward);
    }

    private void AddBoxSamples(BoxCollider box)
    {
        Vector3 size = Vector3.Scale(box.size, box.transform.lossyScale);
        Vector3Int counts = new(
            Mathf.Clamp(Mathf.CeilToInt(size.x / nodeSpacing), 1, 3),
            Mathf.Clamp(Mathf.CeilToInt(size.y / nodeSpacing), 1, 3),
            Mathf.Clamp(Mathf.CeilToInt(size.z / nodeSpacing), 1, 3));

        AddBoxFaceSamples(box, Vector3.right, counts.z, counts.y);
        AddBoxFaceSamples(box, Vector3.left, counts.z, counts.y);
        AddBoxFaceSamples(box, Vector3.up, counts.x, counts.z);
        AddBoxFaceSamples(box, Vector3.down, counts.x, counts.z);
        AddBoxFaceSamples(box, Vector3.forward, counts.x, counts.y);
        AddBoxFaceSamples(box, Vector3.back, counts.x, counts.y);
    }

    private void AddBoxFaceSamples(BoxCollider box, Vector3 localNormal, int uCount, int vCount)
    {
        Vector3 localExtents = box.size * 0.5f;
        Vector3 axisU = Mathf.Abs(localNormal.y) > 0.5f ? Vector3.right : Vector3.up;
        Vector3 axisV = Vector3.Cross(localNormal, axisU).normalized;
        axisU = Vector3.Cross(axisV, localNormal).normalized;

        for (int u = 0; u <= uCount && nodes.Count < maxNodes; u++)
        {
            for (int v = 0; v <= vCount && nodes.Count < maxNodes; v++)
            {
                float fu = uCount == 0 ? 0f : (u / (float)uCount - 0.5f) * 2f;
                float fv = vCount == 0 ? 0f : (v / (float)vCount - 0.5f) * 2f;
                Vector3 localPoint = box.center +
                                     Vector3.Scale(localNormal, localExtents) +
                                     Vector3.Scale(axisU, localExtents) * fu +
                                     Vector3.Scale(axisV, localExtents) * fv;
                Vector3 worldNormal = box.transform.TransformDirection(localNormal).normalized;
                Vector3 worldPoint = box.transform.TransformPoint(localPoint);
                AddNode(worldPoint + worldNormal * surfaceOffset, worldNormal, box, GetSurfaceId(box, worldNormal));
            }
        }
    }

    private void AddProbeSample(Collider collider, Vector3 origin, Vector3 direction)
    {
        if (nodes.Count >= maxNodes)
        {
            return;
        }

        if (Physics.Raycast(origin, direction, out RaycastHit hit, Mathf.Max(collider.bounds.size.magnitude, 1f) * 2f, crawlableSurfaceMask, QueryTriggerInteraction.Ignore) &&
            hit.collider == collider)
        {
            AddNode(hit.point + hit.normal * surfaceOffset, hit.normal, collider, GetSurfaceId(collider, hit.normal));
        }
    }

    private int AddNode(Vector3 position, Vector3 normal, Collider collider, int surfaceId)
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            if (Vector3.Distance(nodes[i].Position, position) < nodeSpacing * 0.35f)
            {
                return i;
            }
        }

        if (nodes.Count >= maxNodes)
        {
            return nodes.Count - 1;
        }

        nodes.Add(new Node
        {
            Position = position,
            Normal = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up,
            Collider = collider,
            SurfaceId = surfaceId
        });
        return nodes.Count - 1;
    }

    private void BuildEdges()
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            nodes[i].Edges.Clear();
        }

        float allowedJumpDistance = GetMaxAllowedJumpDistance();
        bool jumpLocked = Time.time < introJumpLockDuration;

        for (int i = 0; i < nodes.Count; i++)
        {
            for (int j = i + 1; j < nodes.Count; j++)
            {
                float distance = Vector3.Distance(nodes[i].Position, nodes[j].Position);
                if (distance <= maxWalkLinkDistance && nodes[i].Collider != null && nodes[i].Collider == nodes[j].Collider)
                {
                    AddBidirectionalEdge(i, j, distance, false);
                }
                else if (distance <= maxTransitionLinkDistance)
                {
                    AddBidirectionalEdge(i, j, distance * 1.25f, false);
                }
                else if (!jumpLocked && distance <= allowedJumpDistance)
                {
                    float jumpCost = distance + jumpPenaltyBase + distance * jumpPenaltyDistanceMultiplier + noJumpPreferenceWeight;
                    AddBidirectionalEdge(i, j, jumpCost, true);
                }
                else if (logPathEvents && distance <= maxJumpDistance && Time.time >= nextJumpRejectLogTime)
                {
                    nextJumpRejectLogTime = Time.time + 1f;
                    string reason = jumpLocked ? "intro jump lock" : $"jump too far ({distance:F1}>{allowedJumpDistance:F1})";
                    Debug.Log($"CentipedePathfinder: rejected jump link, reason={reason}.", this);
                }
            }
        }
    }

    private float GetMaxAllowedJumpDistance()
    {
        if (!limitJumpDistanceByPlayArea)
        {
            return maxJumpDistance;
        }

        float playAreaDiameter = ResolvePlayAreaDiameter();
        return Mathf.Min(maxJumpDistance, playAreaDiameter * maxJumpDistanceAsPlayAreaFraction);
    }

    private float ResolvePlayAreaDiameter()
    {
        GameObject generatedLevel = GameObject.Find("GeneratedLevel");
        if (generatedLevel != null)
        {
            Collider[] colliders = generatedLevel.GetComponentsInChildren<Collider>();
            if (colliders.Length > 0)
            {
                Bounds bounds = colliders[0].bounds;
                for (int i = 1; i < colliders.Length; i++)
                {
                    bounds.Encapsulate(colliders[i].bounds);
                }

                float diameter = Mathf.Min(bounds.size.x, bounds.size.z);
                if (diameter > 1f)
                {
                    return diameter;
                }
            }
        }

        ProceduralMegastructureGenerator generator = FindAnyObjectByType<ProceduralMegastructureGenerator>();
        if (generator != null)
        {
            System.Reflection.FieldInfo field = typeof(ProceduralMegastructureGenerator).GetField("shaftRadius", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (field != null && field.GetValue(generator) is float radius && radius > 0f)
            {
                return radius * 2f;
            }
        }

        return fallbackPlayAreaDiameter;
    }

    private void AddBidirectionalEdge(int a, int b, float cost, bool isJump)
    {
        nodes[a].Edges.Add(new Edge(b, cost, isJump));
        nodes[b].Edges.Add(new Edge(a, cost, isJump));
    }

    private bool RunAStar(int startIndex, int targetIndex)
    {
        int count = nodes.Count;
        if (startIndex < 0 || targetIndex < 0 || startIndex >= count || targetIndex >= count)
        {
            return false;
        }

        float[] gScore = new float[count];
        float[] fScore = new float[count];
        int[] cameFrom = new int[count];
        bool[] closed = new bool[count];
        List<int> open = new();

        for (int i = 0; i < count; i++)
        {
            gScore[i] = float.PositiveInfinity;
            fScore[i] = float.PositiveInfinity;
            cameFrom[i] = -1;
        }

        gScore[startIndex] = 0f;
        fScore[startIndex] = Heuristic(startIndex, targetIndex);
        open.Add(startIndex);

        while (open.Count > 0)
        {
            int current = PopBestOpen(open, fScore);
            if (current == targetIndex)
            {
                BuildCurrentPath(cameFrom, current);
                return true;
            }

            closed[current] = true;
            List<Edge> edges = nodes[current].Edges;
            for (int i = 0; i < edges.Count; i++)
            {
                Edge edge = edges[i];
                if (closed[edge.To])
                {
                    continue;
                }

                float tentative = gScore[current] + edge.Cost;
                if (tentative >= gScore[edge.To])
                {
                    continue;
                }

                cameFrom[edge.To] = current;
                gScore[edge.To] = tentative;
                fScore[edge.To] = tentative + Heuristic(edge.To, targetIndex);
                if (!open.Contains(edge.To))
                {
                    open.Add(edge.To);
                }
            }
        }

        return false;
    }

    private int PopBestOpen(List<int> open, float[] fScore)
    {
        int bestListIndex = 0;
        float bestScore = fScore[open[0]];
        for (int i = 1; i < open.Count; i++)
        {
            float score = fScore[open[i]];
            if (score < bestScore)
            {
                bestScore = score;
                bestListIndex = i;
            }
        }

        int best = open[bestListIndex];
        open.RemoveAt(bestListIndex);
        return best;
    }

    private float Heuristic(int from, int to)
    {
        return Vector3.Distance(nodes[from].Position, nodes[to].Position);
    }

    private void BuildCurrentPath(int[] cameFrom, int current)
    {
        List<int> reversed = new();
        reversed.Add(current);
        while (cameFrom[current] >= 0)
        {
            current = cameFrom[current];
            reversed.Add(current);
        }

        List<int> forward = new();
        for (int i = reversed.Count - 1; i >= 0; i--)
        {
            forward.Add(reversed[i]);
        }

        currentPath.Clear();
        currentPathJumpFlags.Clear();
        currentPathJumpCount = 0;
        for (int i = 0; i < forward.Count; i++)
        {
            int nodeIndex = forward[i];
            currentPath.Add(nodes[nodeIndex].Position);
            bool isJump = i > 0 && IsJumpEdge(forward[i - 1], nodeIndex);
            currentPathJumpFlags.Add(isJump);
            if (isJump)
            {
                currentPathJumpCount++;
            }
        }

        currentPathIndex = Mathf.Min(1, currentPath.Count - 1);
    }

    private bool IsJumpEdge(int from, int to)
    {
        List<Edge> edges = nodes[from].Edges;
        for (int i = 0; i < edges.Count; i++)
        {
            if (edges[i].To == to)
            {
                return edges[i].IsJump;
            }
        }

        return false;
    }

    private void AdvanceWaypoint()
    {
        if (!hasPath || currentPath.Count == 0)
        {
            return;
        }

        while (currentPathIndex < currentPath.Count - 1 &&
               Vector3.Distance(transform.position, currentPath[currentPathIndex]) <= waypointReachedDistance)
        {
            currentPathIndex++;
        }
    }

    private bool TryFindNearestSurface(Vector3 position, out Vector3 point, out Vector3 normal, out Collider surfaceCollider)
    {
        Collider[] colliders = Physics.OverlapSphere(position, surfaceScanRadius, crawlableSurfaceMask, QueryTriggerInteraction.Ignore);
        float bestDistance = float.PositiveInfinity;
        point = Vector3.zero;
        normal = Vector3.up;
        surfaceCollider = null;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || collider.isTrigger || IsIgnored(collider))
            {
                continue;
            }

            if (!TryGetSurfacePoint(collider, position, out Vector3 closest, out Vector3 candidateNormal))
            {
                continue;
            }

            float distance = Vector3.Distance(position, closest);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                point = closest;
                normal = candidateNormal;
                surfaceCollider = collider;
            }
        }

        return surfaceCollider != null;
    }

    private bool TryGetSurfacePoint(Collider collider, Vector3 position, out Vector3 point, out Vector3 normal)
    {
        point = Vector3.zero;
        normal = Vector3.up;
        if (collider == null)
        {
            return false;
        }

        MeshCollider meshCollider = collider as MeshCollider;
        if (meshCollider != null && !meshCollider.convex)
        {
            Vector3 toCenter = collider.bounds.center - position;
            if (toCenter.sqrMagnitude > 0.0001f && collider.Raycast(new Ray(position, toCenter.normalized), out RaycastHit hit, toCenter.magnitude + collider.bounds.extents.magnitude + 1f))
            {
                point = hit.point;
                normal = hit.normal.normalized;
                return true;
            }

            point = collider.bounds.ClosestPoint(position);
            normal = position - point;
            if (normal.sqrMagnitude < 0.0001f)
            {
                normal = position - collider.bounds.center;
            }
            if (normal.sqrMagnitude < 0.0001f)
            {
                normal = Vector3.up;
            }
            normal.Normalize();
            return true;
        }

        point = collider.ClosestPoint(position);
        normal = position - point;
        if (normal.sqrMagnitude < 0.0001f)
        {
            normal = point - collider.bounds.center;
        }
        if (normal.sqrMagnitude < 0.0001f)
        {
            normal = Vector3.up;
        }
        normal.Normalize();
        return true;
    }

    private bool IsIgnored(Collider collider)
    {
        return ignoredRoot != null && collider.transform.IsChildOf(ignoredRoot);
    }

    private int GetSurfaceId(Collider collider, Vector3 normal)
    {
        if (collider == null)
        {
            return -1;
        }

        Vector3 rounded = new(
            Mathf.Round(normal.x * 10f),
            Mathf.Round(normal.y * 10f),
            Mathf.Round(normal.z * 10f));
        return collider.GetHashCode() * 31 + rounded.GetHashCode();
    }

    private void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos)
        {
            return;
        }

        Gizmos.color = Color.gray;
        for (int i = 0; i < nodes.Count; i++)
        {
            Gizmos.DrawWireSphere(nodes[i].Position, 0.1f);
            List<Edge> edges = nodes[i].Edges;
            for (int e = 0; e < edges.Count; e++)
            {
                if (edges[e].To < i)
                {
                    continue;
                }

                Gizmos.color = edges[e].IsJump ? Color.red : Color.gray;
                Gizmos.DrawLine(nodes[i].Position, nodes[edges[e].To].Position);
            }
        }

        for (int i = 0; i < currentPath.Count - 1; i++)
        {
            Gizmos.color = i + 1 < currentPathJumpFlags.Count && currentPathJumpFlags[i + 1] ? Color.red : Color.green;
            Gizmos.DrawLine(currentPath[i], currentPath[i + 1]);
            Gizmos.DrawWireSphere(currentPath[i], 0.18f);
        }

        if (currentPath.Count > 0)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(currentPath[Mathf.Clamp(currentPathIndex, 0, currentPath.Count - 1)], 0.28f);
        }
    }
}
