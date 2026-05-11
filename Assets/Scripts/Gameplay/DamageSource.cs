using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DamageSource : MonoBehaviour
{
    [Header("Damage")]
    [SerializeField] private float contactDamage = 10f;
    [SerializeField, Min(0f)] private float contactDamageCooldown = 1f;
    [SerializeField] private bool damagePlayerOnTouch = true;
    [SerializeField] private bool useRelativeVelocityBonus = false;
    [SerializeField, Min(0f)] private float velocityDamageMultiplier = 0.2f;
    [SerializeField, Min(0f)] private float minContactDamageSpeed = 0f;

    private readonly Dictionary<GameObject, float> lastHitTime = new();

    private void TryDamage(GameObject target)
    {
        if (!damagePlayerOnTouch || target == null) return;

        if (!lastHitTime.TryGetValue(target, out float t)) t = -999f;
        if (Time.time < t + contactDamageCooldown) return;

        var health = target.GetComponent<PlayerHealth>();
        if (health == null)
        {
            
            health = target.GetComponentInParent<PlayerHealth>();
        }
        if (health == null) return;

        
        
        PlayerDamageReceiver pdr = health.GetComponent<PlayerDamageReceiver>();
        if (pdr == null) pdr = health.GetComponentInParent<PlayerDamageReceiver>();
        Rigidbody rb = null;
        if (pdr == null)
        {
            rb = target.GetComponent<Rigidbody>() ?? target.GetComponentInParent<Rigidbody>();
        }

        float targetSpeed = 0f;
        if (pdr != null) targetSpeed = pdr.PreviousVelocity.magnitude;
        else if (rb != null) targetSpeed = rb.linearVelocity.magnitude;

        
        if (targetSpeed < minContactDamageSpeed) return;

        float damage = contactDamage;
        if (useRelativeVelocityBonus)
        {
            float speedForBonus = (pdr != null) ? pdr.PreviousVelocity.magnitude : (rb != null ? rb.linearVelocity.magnitude : 0f);
            damage += speedForBonus * velocityDamageMultiplier;
        }

        health.TakeDamage(damage, DamageType.EnemyContact, gameObject);
        lastHitTime[target] = Time.time;
    }

    private void OnTriggerEnter(Collider other)
    {
        TryDamage(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryDamage(collision.gameObject);
    }
}
