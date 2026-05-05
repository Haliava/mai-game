using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CentipedeMonsterController : MonoBehaviour
{
    [SerializeField] Transform player;
    [SerializeField] float moveSpeed = 1.65f;
    [SerializeField] float turnSpeed = 8f;
    [SerializeField] float surfaceRayDistance = 12f;
    [SerializeField] float surfaceOffset = 0.85f;
    [SerializeField] float surfaceStickSpeed = 14f;
    [SerializeField] LayerMask groundMask = ~0;
    [SerializeField] float walkableDistance = 8f;
    [SerializeField] float attackRange = 2.5f;
    [SerializeField] float monsterAggressionByDepth = 0.002f;
    [SerializeField] bool canClimbWalls = true;
    [SerializeField] float stalledMoveJumpDistance = 4f;
    [SerializeField] float minMoveDirectionSqrMagnitude = 0.01f;
    [SerializeField] MonsterPathfinder pathfinder;
    [SerializeField] MonsterJumpController jumpController;
    [SerializeField] CentipedeBodyController bodyController;

    Rigidbody rb;
    List<NavigationNode> currentPath = new List<NavigationNode>();
    Vector3 surfaceNormal = Vector3.up;
    bool hasSurface;
    int pathIndex;
    float repathTime;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        if (jumpController == null) jumpController = GetComponent<MonsterJumpController>();
        if (bodyController == null) bodyController = GetComponent<CentipedeBodyController>();
    }

    void Start()
    {
        if (player == null)
        {
            PlayerDamageController p = FindAnyObjectByType<PlayerDamageController>();
            if (p != null) player = p.transform;
        }
        if (pathfinder == null) pathfinder = FindAnyObjectByType<MonsterPathfinder>();
    }

    void FixedUpdate()
    {
        if (player == null) return;

        hasSurface = StickToSurface();
        if (!hasSurface)
        {
            TryRecoverSurface();
            return;
        }

        Vector3 target = GetTarget();
        Vector3 moveDirection = GetSurfaceMoveDirection(target);

        if (moveDirection.sqrMagnitude < minMoveDirectionSqrMagnitude)
        {
            moveDirection = GetSurfaceMoveDirection(player.position);
        }

        if (moveDirection.sqrMagnitude < minMoveDirectionSqrMagnitude)
        {
            moveDirection = GetFallbackMoveDirection(target);
            if (jumpController != null && Vector3.Distance(transform.position, player.position) > stalledMoveJumpDistance)
            {
                jumpController.TryJumpTo(player.position, groundMask, surfaceOffset);
            }
        }

        if (moveDirection.sqrMagnitude > 0.001f)
        {
            Quaternion look = Quaternion.LookRotation(moveDirection.normalized, surfaceNormal);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, turnSpeed * Time.fixedDeltaTime);
            float depthBoost = Mathf.Max(0f, -player.position.y) * monsterAggressionByDepth;
            float jumpSpeedMultiplier = jumpController != null ? jumpController.MoveSpeedMultiplier : 1f;
            rb.MovePosition(rb.position + transform.forward * (moveSpeed + depthBoost) * jumpSpeedMultiplier * Time.fixedDeltaTime);
        }
    }

    Vector3 GetTarget()
    {
        if (jumpController != null && jumpController.HasJumpAssist)
        {
            return jumpController.ActiveJumpTarget;
        }

        if (Time.time >= repathTime && pathfinder != null)
        {
            currentPath = pathfinder.FindPath(transform.position, player.position);
            pathIndex = currentPath != null && currentPath.Count > 1 ? 1 : 0;
            repathTime = Time.time + 0.75f;
        }

        if (currentPath != null && pathIndex >= 0 && pathIndex < currentPath.Count && currentPath[pathIndex] != null)
        {
            Vector3 nodeTarget = currentPath[pathIndex].Position;
            float distanceToNode = Vector3.Distance(transform.position, nodeTarget);
            if (distanceToNode < 2f)
            {
                if (pathIndex < currentPath.Count - 1)
                {
                    pathIndex++;
                    nodeTarget = currentPath[pathIndex].Position;
                    distanceToNode = Vector3.Distance(transform.position, nodeTarget);
                }
                else
                {
                    return player.position;
                }
            }

            float verticalDelta = Mathf.Abs(nodeTarget.y - transform.position.y);
            if ((distanceToNode > walkableDistance || verticalDelta > 2.5f) && jumpController != null)
            {
                if (jumpController.TryJumpTo(nodeTarget, groundMask, surfaceOffset))
                {
                    return nodeTarget;
                }
            }
            return nodeTarget;
        }

        return player.position;
    }

    Vector3 GetSurfaceMoveDirection(Vector3 target)
    {
        return Vector3.ProjectOnPlane(target - transform.position, surfaceNormal);
    }

    Vector3 GetFallbackMoveDirection(Vector3 target)
    {
        Vector3 toTarget = target - transform.position;
        Vector3 vertical = Vector3.ProjectOnPlane(toTarget.y >= 0f ? Vector3.up : Vector3.down, surfaceNormal);
        if (vertical.sqrMagnitude > 0.001f) return vertical;

        Vector3 radial = new Vector3(transform.position.x, 0f, transform.position.z);
        if (radial.sqrMagnitude < 0.01f) radial = transform.right;
        Vector3 aroundWall = Vector3.Cross(surfaceNormal, radial.normalized);
        if (Vector3.Dot(aroundWall, toTarget) < 0f) aroundWall = -aroundWall;
        return Vector3.ProjectOnPlane(aroundWall, surfaceNormal);
    }

    bool StickToSurface()
    {
        RaycastHit hit;
        if (!TryFindSurface(out hit)) return false;

        surfaceNormal = Vector3.Slerp(surfaceNormal, hit.normal, surfaceStickSpeed * Time.fixedDeltaTime).normalized;
        Vector3 targetPosition = hit.point + surfaceNormal * surfaceOffset;
        rb.MovePosition(Vector3.Lerp(rb.position, targetPosition, surfaceStickSpeed * Time.fixedDeltaTime));
        return true;
    }

    void TryRecoverSurface()
    {
        RaycastHit hit;
        if (!Physics.Raycast(transform.position, Vector3.down, out hit, surfaceRayDistance * 2f, groundMask, QueryTriggerInteraction.Ignore)) return;
        if (hit.collider != null && hit.collider.transform.IsChildOf(transform)) return;

        surfaceNormal = hit.normal;
        Vector3 targetPosition = hit.point + surfaceNormal * surfaceOffset;
        rb.MovePosition(Vector3.MoveTowards(rb.position, targetPosition, surfaceStickSpeed * Time.fixedDeltaTime));
    }

    bool TryFindSurface(out RaycastHit bestHit)
    {
        bestHit = default(RaycastHit);
        float bestDistance = float.MaxValue;
        Vector3 position = transform.position;
        Vector3 radial = new Vector3(position.x, 0f, position.z);
        if (radial.sqrMagnitude < 0.01f) radial = transform.right;
        radial.Normalize();

        Vector3[] directions = canClimbWalls
            ? new Vector3[] { -surfaceNormal, Vector3.down, radial, -radial, transform.forward, -transform.forward }
            : new Vector3[] { Vector3.down };

        for (int i = 0; i < directions.Length; i++)
        {
            RaycastHit[] hits = Physics.RaycastAll(position + directions[i] * -0.25f, directions[i], surfaceRayDistance, groundMask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int j = 0; j < hits.Length; j++)
            {
                if (hits[j].collider == null) continue;
                if (hits[j].collider.transform.IsChildOf(transform)) continue;
                if (hits[j].distance >= bestDistance) continue;
                bestDistance = hits[j].distance;
                bestHit = hits[j];
                break;
            }
        }

        return bestHit.collider != null;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        if (player != null) Gizmos.DrawLine(transform.position, player.position);
    }
}
