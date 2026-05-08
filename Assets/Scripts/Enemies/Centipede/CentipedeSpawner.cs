using UnityEngine;

[RequireComponent(typeof(CentipedeController))]
public sealed class CentipedeSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private Transform shaftCenter;

    [Header("Spawn")]
    [SerializeField] private bool autoSpawnOnStart = true;
    [SerializeField] private float shaftWallSpawnHeight = -12f;
    [SerializeField] private float spawnDistanceFromPlayer = 18f;
    [SerializeField] private float spawnAngleAroundShaft = 35f;
    [SerializeField, Min(1f)] private float fallbackShaftRadius = 28f;
    [SerializeField, Min(0.5f)] private float wallProbeDistance = 8f;
    [SerializeField] private LayerMask crawlableSurfaceMask = ~0;

    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = true;

    private CentipedeController controller;
    private Vector3 lastSpawnPoint;
    private Vector3 lastSpawnNormal = Vector3.forward;
    private bool spawned;

    public bool Spawned => spawned;

    private void Awake()
    {
        controller = GetComponent<CentipedeController>();
        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                player = playerObject.transform;
            }
        }
    }

    private void Start()
    {
        if (autoSpawnOnStart)
        {
            SpawnOnWall();
        }
    }

    [ContextMenu("Spawn On Shaft Wall")]
    public void SpawnOnWall()
    {
        if (controller == null)
        {
            controller = GetComponent<CentipedeController>();
        }

        Vector3 center = shaftCenter != null ? shaftCenter.position : Vector3.zero;
        float angle = ResolveSpawnAngle(center);
        Vector3 radial = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        Vector3 origin = center + Vector3.up * shaftWallSpawnHeight;
        Vector3 probeStart = origin + radial * Mathf.Max(1f, fallbackShaftRadius - wallProbeDistance);
        Vector3 probeDirection = radial;

        if (Physics.Raycast(probeStart, probeDirection, out RaycastHit hit, wallProbeDistance * 2f, crawlableSurfaceMask, QueryTriggerInteraction.Ignore))
        {
            Vector3 preferredForward = Vector3.ProjectOnPlane(player != null ? player.position - hit.point : Vector3.down, hit.normal);
            controller.AttachToSurface(hit.point, hit.normal, preferredForward);
            lastSpawnPoint = hit.point;
            lastSpawnNormal = hit.normal;
            spawned = true;
            Debug.Log($"CentipedeSpawner: spawned on shaft wall at {hit.point:F2}.", this);
            return;
        }

        Vector3 fallbackPoint = center + Vector3.up * shaftWallSpawnHeight + radial * fallbackShaftRadius;
        Vector3 fallbackNormal = -radial;
        controller.AttachToSurface(fallbackPoint, fallbackNormal, Vector3.down);
        lastSpawnPoint = fallbackPoint;
        lastSpawnNormal = fallbackNormal;
        spawned = true;
        Debug.LogWarning("CentipedeSpawner: wall raycast failed, used fallback shaft position.", this);
    }

    private float ResolveSpawnAngle(Vector3 center)
    {
        if (player == null)
        {
            return spawnAngleAroundShaft * Mathf.Deg2Rad;
        }

        Vector3 fromCenterToPlayer = Vector3.ProjectOnPlane(player.position - center, Vector3.up);
        if (fromCenterToPlayer.sqrMagnitude < 0.0001f)
        {
            fromCenterToPlayer = Vector3.back;
        }

        float playerAngle = Mathf.Atan2(fromCenterToPlayer.z, fromCenterToPlayer.x);
        float offset = Mathf.Sign(spawnAngleAroundShaft == 0f ? 1f : spawnAngleAroundShaft) * Mathf.Abs(spawnAngleAroundShaft) * Mathf.Deg2Rad;
        float distanceBias = Mathf.Clamp01(spawnDistanceFromPlayer / Mathf.Max(1f, fallbackShaftRadius));
        return playerAngle + Mathf.Lerp(offset, offset * 1.8f, distanceBias);
    }

    private void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos)
        {
            return;
        }

        Gizmos.color = spawned ? Color.magenta : Color.gray;
        Gizmos.DrawWireSphere(lastSpawnPoint, 0.35f);
        Gizmos.DrawLine(lastSpawnPoint, lastSpawnPoint + lastSpawnNormal * 1.5f);
    }
}
