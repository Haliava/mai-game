using System.Collections.Generic;
using UnityEngine;

public enum CentipedeState
{
    ChasingOnSurface,
    TransitioningSurface,
    Jumping,
    RecoveringAfterJump
}

public class CentipedeBrain : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Transform player;
    [SerializeField] CentipedeBody body;
    [SerializeField] SurfaceNavigationGraph graph;
    [SerializeField] CentipedePathfinder pathfinder;
    [SerializeField] JumpPlanner jumpPlanner;
    [SerializeField] JumpExecutor jumpExecutor;

    [Header("Movement")]
    [SerializeField] float moveSpeed = 1.65f;
    [SerializeField] float targetReachDistance = 1.4f;
    [SerializeField] float repathInterval = 0.4f;
    [SerializeField] float directChaseFallbackWeight = 0.25f;
    [SerializeField] LayerMask walkableMask = ~0;

    [Header("Jumping")]
    [SerializeField] float jumpCooldown = 2.5f;
    [SerializeField] float minJumpTriggerDistance = 3.5f;
    [SerializeField] float forceJumpIfStuckTime = 2.5f;
    [SerializeField] float stuckCheckInterval = 1f;
    [SerializeField] float stuckDistanceThreshold = 0.5f;

    [Header("Debug")]
    [SerializeField] bool drawCurrentPath = true;

    readonly List<SurfaceNavigationNode> currentPath = new List<SurfaceNavigationNode>();
    SurfaceWalkerMotor headMotor;
    SurfaceNavigationNode currentTargetNode;
    int pathIndex;
    float nextRepathTime;
    float nextJumpTime;
    float nextStuckCheckTime;
    float stuckTime;
    Vector3 lastStuckCheckPosition;
    Vector3 lastMoveDirection = Vector3.forward;
    bool currentPathUsesJump;

    public CentipedeState State { get; private set; }

    void Awake()
    {
        walkableMask = ResolveWalkableMask(walkableMask);
        DisableLegacyControllers();

        if (body == null) body = GetComponent<CentipedeBody>();
        if (body == null) body = gameObject.AddComponent<CentipedeBody>();
        if (graph == null) graph = GetComponent<SurfaceNavigationGraph>();
        if (graph == null) graph = gameObject.AddComponent<SurfaceNavigationGraph>();
        if (pathfinder == null) pathfinder = GetComponent<CentipedePathfinder>();
        if (pathfinder == null) pathfinder = gameObject.AddComponent<CentipedePathfinder>();
        if (jumpPlanner == null) jumpPlanner = GetComponent<JumpPlanner>();
        if (jumpPlanner == null) jumpPlanner = gameObject.AddComponent<JumpPlanner>();
        if (jumpExecutor == null) jumpExecutor = GetComponent<JumpExecutor>();
        if (jumpExecutor == null) jumpExecutor = gameObject.AddComponent<JumpExecutor>();
    }

    LayerMask ResolveWalkableMask(LayerMask source)
    {
        int mask = source.value == 0 ? ~0 : source.value;
        string[] excludedLayers = new string[] { "Player", "Monster", "DamageVolume", "Ignore Raycast", "UI" };
        for (int i = 0; i < excludedLayers.Length; i++)
        {
            int layer = LayerMask.NameToLayer(excludedLayers[i]);
            if (layer >= 0) mask &= ~(1 << layer);
        }
        return mask;
    }

    void Start()
    {
        ResolvePlayer();
        body.Configure(walkableMask, null);
        body.CreateBody();
        headMotor = body.HeadMotor;
        if (headMotor != null) headMotor.Configure(walkableMask, transform, headMotor.SurfaceOffset);
        graph.Configure(walkableMask, transform);
        pathfinder.SetGraph(graph);
        jumpExecutor.Configure(headMotor, jumpPlanner, walkableMask);
        AttachAttackZoneToHead();

        if (body.Head != null)
        {
            lastStuckCheckPosition = body.Head.position;
            graph.RebuildAround(body.Head.position);
        }
    }

    void FixedUpdate()
    {
        ResolvePlayer();
        if (player == null || body == null || body.Head == null) return;
        if (headMotor == null) headMotor = body.HeadMotor;
        if (headMotor == null) return;

        if (graph.NeedsRebuild(body.Head.position))
        {
            graph.RebuildAround(body.Head.position);
        }

        if (jumpExecutor.IsJumping)
        {
            State = CentipedeState.Jumping;
            jumpExecutor.Tick(Time.fixedDeltaTime);
            body.TickBody(Time.fixedDeltaTime, true);
            AttachAttackZoneToHead();
            return;
        }

        if (Time.time >= nextRepathTime)
        {
            Repath();
            nextRepathTime = Time.time + Mathf.Max(0.05f, repathInterval);
        }

        SurfaceNavigationNode targetNode = ChooseTargetNode();
        Vector3 target = targetNode != null ? targetNode.Position : GetDirectFallbackTarget();

        SurfaceNavigationEdge incomingEdge = GetIncomingEdge(targetNode);
        bool shouldJump = incomingEdge != null && incomingEdge.Type == EdgeTraversalType.Jump && incomingEdge.Distance > minJumpTriggerDistance;
        if (shouldJump && Time.time >= nextJumpTime)
        {
            StartJumpTo(targetNode);
        }
        else
        {
            State = currentTargetNode != null ? CentipedeState.ChasingOnSurface : CentipedeState.TransitioningSurface;
            lastMoveDirection = headMotor.StepTowards(target, moveSpeed, Time.fixedDeltaTime);
        }

        UpdateStuckLogic(targetNode, target);
        body.TickBody(Time.fixedDeltaTime, false);
        AttachAttackZoneToHead();
    }

    void DisableLegacyControllers()
    {
        CentipedeMonsterController oldController = GetComponent<CentipedeMonsterController>();
        if (oldController != null) oldController.enabled = false;

        MonsterPathfinder oldPathfinder = GetComponent<MonsterPathfinder>();
        if (oldPathfinder != null) oldPathfinder.enabled = false;

        MonsterJumpController oldJump = GetComponent<MonsterJumpController>();
        if (oldJump != null) oldJump.enabled = false;

        CentipedeBodyController oldBody = GetComponent<CentipedeBodyController>();
        if (oldBody != null) oldBody.enabled = false;
    }

    void ResolvePlayer()
    {
        if (player != null) return;
        PlayerDamageController damageController = FindAnyObjectByType<PlayerDamageController>();
        if (damageController != null) player = damageController.transform;
    }

    void Repath()
    {
        currentPath.Clear();
        currentTargetNode = null;
        pathIndex = 0;

        bool usesJump;
        List<SurfaceNavigationNode> path = pathfinder.FindBestPath(body.Head.position, player.position, out usesJump);
        currentPathUsesJump = usesJump;
        if (path != null && path.Count > 0)
        {
            currentPath.AddRange(path);
            pathIndex = currentPath.Count > 1 ? 1 : 0;
        }
    }

    SurfaceNavigationNode ChooseTargetNode()
    {
        if (currentPath.Count > 0)
        {
            if (currentPath.Count == 1 && Vector3.Distance(body.Head.position, currentPath[0].Position) < targetReachDistance)
            {
                currentTargetNode = null;
                return null;
            }

            pathIndex = Mathf.Clamp(pathIndex, 0, currentPath.Count - 1);
            SurfaceNavigationNode node = currentPath[pathIndex];
            while (node != null && Vector3.Distance(body.Head.position, node.Position) < targetReachDistance && pathIndex < currentPath.Count - 1)
            {
                pathIndex++;
                node = currentPath[pathIndex];
            }

            currentTargetNode = node;
            return node;
        }

        currentTargetNode = pathfinder.FindUsefulFallbackNode(body.Head.position, player.position);
        return currentTargetNode;
    }

    SurfaceNavigationEdge GetIncomingEdge(SurfaceNavigationNode node)
    {
        if (node == null || currentPath.Count < 2 || pathIndex <= 0 || pathIndex >= currentPath.Count) return null;
        return pathfinder.FindEdge(currentPath[pathIndex - 1], node);
    }

    Vector3 GetDirectFallbackTarget()
    {
        if (player == null) return body.Head.position + lastMoveDirection * 4f;
        SurfaceNavigationNode useful = graph.FindNearestNodeToward(body.Head.position, player.position);
        if (useful != null)
        {
            return Vector3.Lerp(player.position, useful.Position, 1f - directChaseFallbackWeight);
        }
        return player.position;
    }

    void StartJumpTo(SurfaceNavigationNode targetNode)
    {
        if (targetNode == null || headMotor == null) return;

        JumpPlan plan = jumpPlanner.CreatePlan(
            body.Head.position,
            targetNode.Position,
            headMotor.SurfaceNormal,
            targetNode.Normal,
            targetNode);
        jumpExecutor.StartJump(plan);
        nextJumpTime = Time.time + Mathf.Max(0.05f, jumpCooldown);
        State = CentipedeState.Jumping;
    }

    void StartJumpToward(Vector3 target)
    {
        if (headMotor == null || Time.time < nextJumpTime) return;

        RaycastHit landingHit;
        Vector3 end = target;
        Vector3 endNormal = Vector3.up;
        if (SurfaceProbe.TryFindSurfaceAround(target, transform, walkableMask, 0.35f, jumpPlanner.LandingSearchRadius * 2f, out landingHit))
        {
            end = landingHit.point + landingHit.normal * headMotor.SurfaceOffset;
            endNormal = landingHit.normal;
        }

        JumpPlan plan = jumpPlanner.CreatePlan(body.Head.position, end, headMotor.SurfaceNormal, endNormal, null);
        jumpExecutor.StartJump(plan);
        nextJumpTime = Time.time + Mathf.Max(0.05f, jumpCooldown);
        State = CentipedeState.Jumping;
    }

    void UpdateStuckLogic(SurfaceNavigationNode targetNode, Vector3 target)
    {
        if (Time.time < nextStuckCheckTime) return;
        nextStuckCheckTime = Time.time + Mathf.Max(0.1f, stuckCheckInterval);

        float moved = Vector3.Distance(lastStuckCheckPosition, body.Head.position);
        lastStuckCheckPosition = body.Head.position;
        if (moved >= stuckDistanceThreshold)
        {
            stuckTime = 0f;
            return;
        }

        stuckTime += stuckCheckInterval;
        if (stuckTime < forceJumpIfStuckTime) return;

        if (Time.time >= nextJumpTime)
        {
            if (targetNode != null) StartJumpTo(targetNode);
            else StartJumpToward(target);
            stuckTime = 0f;
        }
    }

    void AttachAttackZoneToHead()
    {
        if (body == null || body.Head == null) return;
        Transform attack = transform.Find("AttackZone");
        if (attack == null) return;
        attack.SetParent(body.Head, true);
        attack.localPosition = Vector3.zero;
    }

    void OnDrawGizmosSelected()
    {
        if (!drawCurrentPath) return;
        Gizmos.color = currentPathUsesJump ? Color.yellow : Color.green;
        for (int i = 1; i < currentPath.Count; i++)
        {
            if (currentPath[i - 1] != null && currentPath[i] != null)
            {
                Gizmos.DrawLine(currentPath[i - 1].Position, currentPath[i].Position);
            }
        }

        if (body != null && body.Head != null && player != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(body.Head.position, player.position);
        }
    }
}
