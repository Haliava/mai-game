using UnityEngine;

public class CollisionDamageDetector : MonoBehaviour
{
    [SerializeField] PlayerDamageController damageController;
    [SerializeField] float safeImpactSpeed = 12f;
    [SerializeField] float lethalImpactSpeed = 30f;
    [SerializeField] float maxImpactDamage = 80f;
    [SerializeField] float damageCooldown = 0.35f;
    [SerializeField] float glancingImpactContribution = 0.35f;

    float nextDamageTime;

    void Awake()
    {
        if (damageController == null) damageController = GetComponent<PlayerDamageController>();
    }

    void OnCollisionEnter(Collision collision)
    {
        if (Time.time < nextDamageTime || damageController == null) return;

        float speed = GetImpactSpeed(collision);
        if (speed <= safeImpactSpeed) return;

        float t = Mathf.InverseLerp(safeImpactSpeed, lethalImpactSpeed, speed);
        damageController.TakeDamage(Mathf.Lerp(0f, maxImpactDamage, t), DamageType.Impact);
        nextDamageTime = Time.time + damageCooldown;
    }

    float GetImpactSpeed(Collision collision)
    {
        float normalImpact = 0f;
        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint contact = collision.GetContact(i);
            normalImpact = Mathf.Max(normalImpact, Mathf.Abs(Vector3.Dot(collision.relativeVelocity, contact.normal)));
        }

        float glancingImpact = collision.relativeVelocity.magnitude * glancingImpactContribution;
        return Mathf.Max(normalImpact, glancingImpact);
    }
}
