using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CentipedeMonsterController : MonoBehaviour
{
    [SerializeField] Transform player;
    [SerializeField] float moveSpeed = 5f;
    [SerializeField] float turnSpeed = 8f;
    [SerializeField] float groundRayDistance = 8f;
    [SerializeField] LayerMask groundMask = ~0;
    [SerializeField] float walkableDistance = 8f;
    [SerializeField] float attackRange = 2.5f;
    [SerializeField] float monsterAggressionByDepth = 0.01f;
    [SerializeField] MonsterPathfinder pathfinder;
    [SerializeField] MonsterJumpController jumpController;
    [SerializeField] CentipedeBodyController bodyController;

    Rigidbody rb;
    List<NavigationNode> currentPath = new List<NavigationNode>();
    int pathIndex;
    float repathTime;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
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
        StickToGround();

        Vector3 target = GetTarget();
        Vector3 toTarget = target - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude > 0.25f)
        {
            Quaternion look = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
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

    void StickToGround()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position + Vector3.up * 2f, Vector3.down, out hit, groundRayDistance, groundMask, QueryTriggerInteraction.Ignore))
        {
            if (rb.linearVelocity.y <= 0f)
            {
                Vector3 position = rb.position;
                position.y = Mathf.Lerp(position.y, hit.point.y + 0.8f, 12f * Time.fixedDeltaTime);
                rb.MovePosition(position);
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        if (player != null) Gizmos.DrawLine(transform.position, player.position);
    }
}
