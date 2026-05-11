using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class DescentSphereTrigger : MonoBehaviour
{
    [SerializeField] private bool requireTouchFromBelow = true;
    [SerializeField] private float belowTouchTolerance = 0.25f;
    [SerializeField] private bool deactivateAfterTriggered = true;
    [SerializeField] private bool enableOverlapFallback = true;
    [SerializeField] private float overlapRadiusOverride = 0f;
    [Header("Fallback")]
    [SerializeField] private bool autoCreateManagerIfMissing = false;

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

    
    public void HandleTriggered(Transform playerTransform)
    {
        if (triggered) return;

        
        var old = GetComponent<VictorySphereInteractable>();
        if (old != null) old.enabled = false;

        
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

        
        var mgr = ResolveManager();
        if (mgr == null)
        {
            Debug.LogError("DescentSphereTrigger: valid touch detected, but EndlessDescentGameManager.Instance is NULL. Level completion cannot continue.");
            
            triggered = false;
            return;
        }

        Debug.Log("DescentSphereTrigger: calling EndlessDescentGameManager.CompleteCurrentLevel");

        bool accepted = false;
        try
        {
            
            var tryMethod = typeof(EndlessDescentGameManager).GetMethod("TryCompleteCurrentLevel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            if (tryMethod != null)
            {
                object result = tryMethod.Invoke(mgr, new object[] { this });
                if (result is bool b) accepted = b;
            }
            else
            {
                mgr.CompleteCurrentLevel(this);
                accepted = true;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"DescentSphereTrigger: exception while calling manager: {ex}");
            accepted = false;
        }

        Debug.Log("DescentSphereTrigger: CompleteCurrentLevel call finished/started");

        if (accepted)
        {
            triggered = true;
            if (deactivateAfterTriggered)
            {
                if (ownCollider != null) ownCollider.enabled = false;
            }
        }
        else
        {
            
            triggered = false;
        }
    }

    private EndlessDescentGameManager ResolveManager()
    {
        if (EndlessDescentGameManager.Instance != null) return EndlessDescentGameManager.Instance;
        EndlessDescentGameManager found = FindAnyObjectByType<EndlessDescentGameManager>();
        if (found != null) return found;
        if (autoCreateManagerIfMissing)
        {
            Debug.LogWarning("DescentSphereTrigger: auto-creating EndlessDescentGameManager because autoCreateManagerIfMissing=true. This manager may lack scene references.");
            GameObject go = new GameObject("EndlessDescentManager_AutoCreated");
            var mgr = go.AddComponent<EndlessDescentGameManager>();
            return mgr;
        }
        Debug.LogError("DescentSphereTrigger: No EndlessDescentGameManager found in scene.");
        return null;
    }

    private static bool IsPlayer(Collider other)
    {
        return other != null && (other.CompareTag("Player") || other.GetComponentInParent<FPSCharacterController3D>() != null);
    }
}
