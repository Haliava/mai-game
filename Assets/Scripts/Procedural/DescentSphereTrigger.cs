using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class DescentSphereTrigger : MonoBehaviour
{
    [SerializeField] private bool requireTouchFromBelow = true;
    [SerializeField] private float belowTouchTolerance = 0.25f;
    [SerializeField] private bool deactivateAfterTriggered = true;
    [SerializeField] private bool enableOverlapFallback = true;
    [SerializeField] private float overlapRadiusOverride = 0f;

    private bool triggered = false;
    private Collider ownCollider;

    private void Awake()
    {
        ownCollider = GetComponent<Collider>();
        if (ownCollider != null) ownCollider.isTrigger = true;
        Debug.Log($"DescentSphereTrigger Awake: name={gameObject.name}, pos={transform.position}, hasCollider={ownCollider!=null}");
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"DescentSphereTrigger OnTriggerEnter: other={other.gameObject.name}");
        if (triggered) return;
        if (!IsPlayer(other)) return;
        HandleTriggered(other.transform);
    }

    private void Update()
    {
        if (triggered) return;
        if (!enableOverlapFallback) return;

        // fallback for CharacterController cases where triggers may not fire reliably
        Collider col = ownCollider;
        Vector3 center = transform.position;
        float radius = overlapRadiusOverride > 0f ? overlapRadiusOverride : (col != null ? Mathf.Max(0.25f, col.bounds.extents.magnitude) : 0.5f);
        Collider[] hits = Physics.OverlapSphere(center, radius);
        foreach (var h in hits)
        {
            if (IsPlayer(h))
            {
                Debug.Log("DescentSphereTrigger: overlap detected player collider.");
                HandleTriggered(h.transform);
                break;
            }
        }
    }

    // Make this callable from other objects (collision fallbacks)
    public void HandleTriggered(Transform playerTransform)
    {
        if (triggered) return;
        triggered = true;

        // disable existing VictorySphereInteractable if present
        var old = GetComponent<VictorySphereInteractable>();
        if (old != null) old.enabled = false;

        // check from-below logic
        bool fromBelow = true;
        if (requireTouchFromBelow && playerTransform != null)
        {
            fromBelow = playerTransform.position.y < transform.position.y - belowTouchTolerance;
            if (!fromBelow)
            {
                var fps = playerTransform.GetComponentInParent<FPSCharacterController3D>();
                if (fps != null)
                {
                    Vector3 vel = fps.CurrentVelocity;
                    if (vel.y < -0.5f) fromBelow = true;
                }
            }
        }

        if (!fromBelow)
        {
            triggered = false;
            return;
        }

        Debug.Log("DescentSphereTrigger: triggered valid touch from below.");

        // inform game manager
        if (EndlessDescentGameManager.Instance != null)
        {
            EndlessDescentGameManager.Instance.CompleteCurrentLevel(this);
        }

        if (deactivateAfterTriggered)
        {
            if (ownCollider != null) ownCollider.enabled = false;
        }
    }

    private static bool IsPlayer(Collider other)
    {
        return other != null && (other.CompareTag("Player") || other.GetComponentInParent<FPSCharacterController3D>() != null);
    }
}
