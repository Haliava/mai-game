using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class FallDamageDetector : MonoBehaviour
{
    [SerializeField] PlayerDamageController damageController;
    [SerializeField] float safeFallSpeed = 18f;
    [SerializeField] float lethalFallSpeed = 45f;
    [SerializeField] float maxFallDamage = 12f;
    [SerializeField] LayerMask groundMask = ~0;

    Rigidbody rb;
    bool wasGrounded;
    float worstDownSpeed;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (damageController == null) damageController = GetComponent<PlayerDamageController>();
    }

    void FixedUpdate()
    {
        bool grounded = Physics.Raycast(transform.position, Vector3.down, 1.25f, groundMask, QueryTriggerInteraction.Ignore);
        if (!grounded)
        {
            worstDownSpeed = Mathf.Max(worstDownSpeed, -rb.linearVelocity.y);
        }
        else if (!wasGrounded)
        {
            ApplyFallDamage(worstDownSpeed);
            worstDownSpeed = 0f;
        }

        wasGrounded = grounded;
    }

    void ApplyFallDamage(float impactSpeed)
    {
        if (damageController == null || impactSpeed <= safeFallSpeed) return;
        float t = Mathf.InverseLerp(safeFallSpeed, lethalFallSpeed, impactSpeed);
        t *= t;
        damageController.TakeDamage(Mathf.Lerp(0f, maxFallDamage, t), DamageType.Fall);
    }
}
