using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CentipedeMonsterController : MonoBehaviour
{
    [SerializeField] Transform player;
    [SerializeField] float moveSpeed = 5f;
    [SerializeField] float turnSpeed = 8f;
    [SerializeField] float surfaceRayDistance = 12f;
    [SerializeField] float surfaceOffset = 0.85f;
    [SerializeField] float surfaceStickSpeed = 14f;
    [SerializeField] LayerMask groundMask = ~0;
    [SerializeField] float walkableDistance = 8f;
    [SerializeField] float attackRange = 2.5f;
    [SerializeField] float monsterAggressionByDepth = 0.01f;
    [SerializeField] bool canClimbWalls = true;
    [SerializeField] MonsterPathfinder pathfinder;
    [SerializeField] MonsterJumpController jumpController;
    [SerializeField] CentipedeBodyController bodyController;

    Rigidbody rb;
    List<NavigationNode> currentPath = new List<NavigationNode>();
    Vector3 surfaceNormal = Vector3.up;
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
        StickToSurface();

        Vector3 target = GetTarget();
        Vector3 toTarget = Vector3.ProjectOnPlane(target - transform.position, surfaceNormal);
        if (toTarget.sqrMagnitude > 0.25f)
        {
            Quaternion look = Quaternion.LookRotation(toTarget.normalized, surfaceNormal);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, turnSpeed * Time.fixedDeltaTime);
            float depthBoost = Mathf.Max(0f, -player.position.y) * monsterAggressionByDepth;
            rb.MovePosition(rb.position + transform.forward * (moveSpeed + depthBoost) * Time.fixedDeltaTime);
        }
    }

    Vector3 GetTarget()
    {
        if (Time.time >= repathTime && pathfinder != null)
        {
            currentPath = pathfinder.FindPath(transform.position, player.position);
            pathIndex = currentPath != null && currentPath.Count > 1 ? 1 : 0;
            repathTime = Time.time + 0.75f;
        }

        if (currentPath != null && pathIndex >= 0 && pathIndex < currentPath.Count && currentPath[pathIndex] != null)
        {
            Vector3 nodeTarget = currentPath[pathIndex].Position;
            if (Vector3.Distance(transform.position, nodeTarget) < 2f) pathIndex = Mathf.Min(pathIndex + 1, currentPath.Count - 1);
            if (Vector3.Distance(transform.position, nodeTarget) > walkableDistance && jumpController != null) jumpController.TryJumpTo(nodeTarget);
            return nodeTarget;
        }

        return player.position;
    }

    void StickToSurface()
    {
        RaycastHit hit;
        if (!TryFindSurface(out hit)) return;

        surfaceNormal = Vector3.Slerp(surfaceNormal, hit.normal, surfaceStickSpeed * Time.fixedDeltaTime).normalized;
        Vector3 targetPosition = hit.point + surfaceNormal * surfaceOffset;
        rb.MovePosition(Vector3.Lerp(rb.position, targetPosition, surfaceStickSpeed * Time.fixedDeltaTime));
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
