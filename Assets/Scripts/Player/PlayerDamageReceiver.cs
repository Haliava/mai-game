using UnityEngine;

[RequireComponent(typeof(PlayerHealth))]
public sealed class PlayerDamageReceiver : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FPSCharacterController3D fpsController;
    [SerializeField] private GrapplingHookController3D grapplingHook;
    [SerializeField] private GrapplePlayerConstraint3D grappleConstraint;
    [SerializeField] private PlayerHealth playerHealth;

    [Header("Fall Damage")]
    [SerializeField] private bool enableFallDamage = true;
    [SerializeField, Min(0f)] private float minFallDamageSpeed = 12f;
    [SerializeField, Min(0f)] private float lethalFallSpeed = 38f;
    [SerializeField, Min(0f)] private float fallDamageMultiplier = 1.2f;
    [SerializeField, Min(0f)] private float maxFallDamage = 45f;
    [SerializeField] private AnimationCurve fallDamageCurve;
    [SerializeField] private bool ignoreFallDamageWhileGrappling = true;
    [SerializeField, Min(0f)] private float fallDamageGraceAfterGrappleRelease = 0.35f;
    [SerializeField, Min(0f)] private float minFallTrackingAirTimeAfterGrapple = 0.15f;

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
    private bool wasGrappleActiveLastFrame = false;
    private float suppressFallDamageUntil = 0f;
    private float lastGrappleReleaseTime = -999f;
    private Vector3 lastPosition;

    private void Reset()
    {
        fpsController = GetComponent<FPSCharacterController3D>() ?? FindAnyObjectByType<FPSCharacterController3D>();
        playerHealth = GetComponent<PlayerHealth>();
        grapplingHook = GetComponent<GrapplingHookController3D>() ?? FindAnyObjectByType<GrapplingHookController3D>();
        grappleConstraint = GetComponent<GrapplePlayerConstraint3D>() ?? GetComponentInChildren<GrapplePlayerConstraint3D>();
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
        if (grapplingHook == null)
        {
            grapplingHook = GetComponent<GrapplingHookController3D>() ?? UnityEngine.Object.FindAnyObjectByType<GrapplingHookController3D>();
        }
        if (grappleConstraint == null)
        {
            grappleConstraint = GetComponent<GrapplePlayerConstraint3D>() ?? GetComponentInChildren<GrapplePlayerConstraint3D>();
        }

        lastPosition = transform.position;
    }

    private void Update()
    {
        if (fpsController == null) return;

        float dt = Time.deltaTime;
        Vector3 frameMovementVel = Vector3.zero;
        if (dt > 0f)
        {
            frameMovementVel = (transform.position - lastPosition) / dt;
        }

        
        bool grappleActive = false;
        if (grapplingHook != null)
        {
            var st = grapplingHook.State;
            if (st != GrapplingHookController3D.GrappleState.Idle && st != GrapplingHookController3D.GrappleState.Released) grappleActive = true;
            if (grapplingHook.IsLatched) grappleActive = true;
        }

        if (grappleConstraint != null && (grappleConstraint.IsMantling || grappleConstraint.IsGrappleControlActive))
        {
            grappleActive = true;
        }

        
        if (ignoreFallDamageWhileGrappling && grappleActive)
        {
            if (!wasGrappleActiveLastFrame && showDebugLogs) Debug.Log("[FallDamage] Grapple active - resetting fall tracking");
            wasGrappleActiveLastFrame = true;
            ResetFallTracking("grapple active");
        }

        
        if (wasGrappleActiveLastFrame && !grappleActive)
        {
            wasGrappleActiveLastFrame = false;
            lastGrappleReleaseTime = Time.time;
            suppressFallDamageUntil = Time.time + fallDamageGraceAfterGrappleRelease;
            ResetFallTracking("grapple released");
            if (showDebugLogs) Debug.Log($"[FallDamage] Grapple released - suppress until {suppressFallDamageUntil:F2}");
        }

        bool suppressFall = ignoreFallDamageWhileGrappling && Time.time < suppressFallDamageUntil;

        
        Vector3 currentVelocity = fpsController.CurrentVelocity;
        if (grappleActive || Time.time < suppressFallDamageUntil)
        {
            currentVelocity = frameMovementVel;
        }

        bool grounded = fpsController.IsGrounded;

        if (suppressFall)
        {
            
            maxDownwardSpeed = 0f;
            previousVelocity = currentVelocity;
            wasGrounded = grounded;
            lastPosition = transform.position;
            return;
        }

        
        if (!grounded)
        {
            maxDownwardSpeed = Mathf.Min(maxDownwardSpeed, currentVelocity.y);
        }

        
        if (!wasGrounded && grounded)
        {
            HandleLanding();
            maxDownwardSpeed = 0f;
        }

        wasGrounded = grounded;
        previousVelocity = currentVelocity;
        lastPosition = transform.position;
    }

    private void ResetFallTracking(string reason = null)
    {
        maxDownwardSpeed = 0f;
        previousVelocity = fpsController != null ? fpsController.CurrentVelocity : Vector3.zero;
        wasGrounded = fpsController != null ? fpsController.IsGrounded : true;
        lastPosition = transform.position;
        if (showDebugLogs && !string.IsNullOrEmpty(reason))
        {
            Debug.Log($"[FallDamage] ResetFallTracking: {reason}");
        }
    }

    private void HandleLanding()
    {
        if (!enableFallDamage || playerHealth == null) return;

        float impactSpeed = -maxDownwardSpeed; 
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

        
        if (((1 << hit.gameObject.layer) & impactDamageMask) == 0) return;

        
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

    
    public Vector3 PreviousVelocity => previousVelocity;
}
