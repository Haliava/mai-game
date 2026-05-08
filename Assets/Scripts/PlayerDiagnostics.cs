using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerDiagnostics : MonoBehaviour
{
    public float checkRadius = 1.5f;
    public LayerMask layerMask = ~0;
    public float updateInterval = 0.5f;
    public bool includeTriggers = true;

    private CharacterController cc;
    private float timer;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= updateInterval)
        {
            timer = 0f;
            LogStateAndOverlap();
        }
    }

    void LogStateAndOverlap()
    {
        Vector3 pos = transform.position;
        Bounds ccBounds = (cc != null) ? cc.bounds : new Bounds(pos, Vector3.zero);
        Collider[] nearby = Physics.OverlapSphere(pos, checkRadius, layerMask, includeTriggers ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore);

        Debug.Log($"[Diagnostics][State] pos={pos} | CC center={ccBounds.center} size={ccBounds.size} | nearbyCount={nearby.Length}");

        if (nearby.Length == 0)
        {
            Debug.Log("[Diagnostics] No colliders within radius.");
            return;
        }

        for (int i = 0; i < nearby.Length; i++)
        {
            Collider c = nearby[i];
            Vector3 closest = c.ClosestPoint(pos);
            float dist = Vector3.Distance(pos, closest);
            string layerName = LayerMask.LayerToName(c.gameObject.layer);
            string layerDisplay = string.IsNullOrEmpty(layerName) ? c.gameObject.layer.ToString() : layerName;
            Debug.Log($"[Diagnostics][Nearby] idx={i} name={c.name} tag={c.tag} layer={layerDisplay} isTrigger={c.isTrigger} distance={dist:F3} closestPoint={closest}");
        }
    }

    void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[Diagnostics][OnTriggerEnter] other={other.name} at={other.transform.position}");
        LogStateAndOverlap();
    }

    void OnCollisionEnter(Collision collision)
    {
        Debug.Log($"[Diagnostics][OnCollisionEnter] other={collision.collider.name} contactCount={collision.contactCount}");
        LogStateAndOverlap();
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Debug.Log($"[Diagnostics][OnControllerColliderHit] other={hit.collider.name} point={hit.point} normal={hit.normal}");
        LogStateAndOverlap();

        float searchRadius = 0.6f;
        Collider[] hits = Physics.OverlapSphere(hit.point, searchRadius);
        foreach (var c in hits)
        {
            if (c == null) continue;
            var trigger = c.GetComponent<DescentSphereTrigger>() ?? c.GetComponentInParent<DescentSphereTrigger>();
            if (trigger != null)
            {
                Debug.Log($"[Diagnostics] Found DescentSphereTrigger '{trigger.gameObject.name}' near contact; invoking HandleTriggered.");
                trigger.HandleTriggered(transform);
                break;
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, checkRadius);

        if (cc != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(cc.bounds.center, cc.bounds.size);
        }
    }
}
