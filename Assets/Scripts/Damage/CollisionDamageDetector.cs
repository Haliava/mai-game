using UnityEngine;

public class CollisionDamageDetector : MonoBehaviour
{
    [SerializeField] PlayerDamageController damageController;
    [SerializeField] float safeImpactSpeed = 12f;
    [SerializeField] float lethalImpactSpeed = 30f;
    [SerializeField] float maxImpactDamage = 80f;
    [SerializeField] float damageCooldown = 0.35f;

    float nextDamageTime;

    void Awake()
    {
        if (damageController == null) damageController = GetComponent<PlayerDamageController>();
    }

    void OnCollisionEnter(Collision collision)
    {
        if (Time.time < nextDamageTime || damageController == null) return;

        float speed = collision.relativeVelocity.magnitude;
        if (speed <= safeImpactSpeed) return;

        float t = Mathf.InverseLerp(safeImpactSpeed, lethalImpactSpeed, speed);
        damageController.TakeDamage(Mathf.Lerp(0f, maxImpactDamage, t), DamageType.Impact);
        nextDamageTime = Time.time + damageCooldown;
    }
}
