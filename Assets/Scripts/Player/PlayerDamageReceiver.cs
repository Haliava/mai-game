using UnityEngine;

[RequireComponent(typeof(PlayerHealth))]
public sealed class PlayerDamageReceiver : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FPSCharacterController3D fpsController;
    [SerializeField] private PlayerHealth playerHealth;

    [Header("Fall Damage")]
    [SerializeField] private bool enableFallDamage = true;
    [SerializeField, Min(0f)] private float minFallDamageSpeed = 12f;
    [SerializeField, Min(0f)] private float lethalFallSpeed = 38f;
    [SerializeField, Min(0f)] private float fallDamageMultiplier = 1.2f;
    [SerializeField, Min(0f)] private float maxFallDamage = 45f;
    [SerializeField] private AnimationCurve fallDamageCurve;

    [Header("Impact Damage (walls/obstacles)")]
    [SerializeField] private bool enableImpactDamage = true;
    [SerializeField, Min(0f)] private float minImpactDamageSpeed = 18f;
    [SerializeField, Min(0f)] private float strongImpactSpeed = 35f;
    [SerializeField, Min(0f)] private float impactDamageMultiplier = 1.5f;
    [SerializeField, Min(0f)] private float maxImpactDamage = 55f;
    [SerializeField, Min(0f)] private float impactDamageCooldown = 0.35f;
    [SerializeField] private LayerMask impactDamageMask = ~0;
    [SerializeField] private AnimationCurve impactDamageCurve;

    [Header("General")]
    [SerializeField] private bool showDebugLogs = true;

    private bool wasGrounded = true;
    private float maxDownwardSpeed = 0f;
    private Vector3 previousVelocity = Vector3.zero;
    private float lastImpactTime = -999f;

    private void Reset()
    {
        fpsController = GetComponent<FPSCharacterController3D>() ?? FindAnyObjectByType<FPSCharacterController3D>();
        playerHealth = GetComponent<PlayerHealth>();
    }

    private void Awake()
    {
        if (fpsController == null)
        {
            fpsController = GetComponent<FPSCharacterController3D>() ?? UnityEngine.Object.FindAnyObjectByType<FPSCharacterController3D>();
        }
        if (playerHealth == null)
        {
            playerHealth = GetComponent<PlayerHealth>();
        }
    }

    private void Update()
    {
        if (fpsController == null) return;

        Vector3 currentVelocity = fpsController.CurrentVelocity;

        // Track downward (vertical) speed while airborne
        bool grounded = fpsController.IsGrounded;
        if (!grounded)
        {
            maxDownwardSpeed = Mathf.Min(maxDownwardSpeed, currentVelocity.y);
        }

        // Landing detection
        if (!wasGrounded && grounded)
        {
            HandleLanding();
            maxDownwardSpeed = 0f;
        }

        wasGrounded = grounded;
        previousVelocity = currentVelocity;
    }

    private void HandleLanding()
    {
        if (!enableFallDamage || playerHealth == null) return;

        float impactSpeed = -maxDownwardSpeed; // positive value
        if (impactSpeed < minFallDamageSpeed)
        {
            if (showDebugLogs) Debug.Log($"[FallDamage] Ignored. impactSpeed={impactSpeed}");
            return;
        }

        float t = (impactSpeed - minFallDamageSpeed) / Mathf.Max(0.0001f, (lethalFallSpeed - minFallDamageSpeed));
        t = Mathf.Clamp01(t);

        float damage;
        if (fallDamageCurve != null && fallDamageCurve.length > 0)
        {
            damage = fallDamageCurve.Evaluate(t) * maxFallDamage;
        }
        else
        {
            damage = (impactSpeed - minFallDamageSpeed) * fallDamageMultiplier;
        }

        damage = Mathf.Clamp(damage, 0f, maxFallDamage);
        if (damage > 0f)
        {
            playerHealth.TakeDamage(damage, DamageType.Fall, gameObject);
            if (showDebugLogs) Debug.Log($"[FallDamage] impactSpeed={impactSpeed:F2}, damage={damage:F2}");
        }
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (!enableImpactDamage || playerHealth == null) return;

        // ignore layers
        if (((1 << hit.gameObject.layer) & impactDamageMask) == 0) return;

        // avoid mantling: check grapple mantle state if available
        var constraint = FindAnyObjectByType<GrapplePlayerConstraint3D>();
        if (constraint != null && constraint.IsMantling) return;

        float impactSpeed = Vector3.Dot(previousVelocity, -hit.normal);
        if (impactSpeed < minImpactDamageSpeed) return;

        if (Time.time < lastImpactTime + impactDamageCooldown) return;

        float t = Mathf.InverseLerp(minImpactDamageSpeed, strongImpactSpeed, impactSpeed);
        float damage;
        if (impactDamageCurve != null && impactDamageCurve.length > 0)
        {
            damage = impactDamageCurve.Evaluate(t) * maxImpactDamage;
        }
        else
        {
            damage = (impactSpeed - minImpactDamageSpeed) * impactDamageMultiplier;
        }

        damage = Mathf.Clamp(damage, 0f, maxImpactDamage);
        if (damage > 0f)
        {
            lastImpactTime = Time.time;
            playerHealth.TakeDamage(damage, DamageType.Impact, hit.gameObject);
            if (showDebugLogs) Debug.Log($"[ImpactDamage] impactSpeed={impactSpeed:F2}, damage={damage:F2}, hit={hit.gameObject.name}");
        }
    }

    // Public accessor for other systems that need previous velocity
    public Vector3 PreviousVelocity => previousVelocity;
}
