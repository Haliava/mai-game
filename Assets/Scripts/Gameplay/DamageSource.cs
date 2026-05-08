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

    private readonly Dictionary<GameObject, float> lastHitTime = new();

    private void TryDamage(GameObject target)
    {
        if (!damagePlayerOnTouch || target == null) return;

        if (!lastHitTime.TryGetValue(target, out float t)) t = -999f;
        if (Time.time < t + contactDamageCooldown) return;

        var health = target.GetComponent<PlayerHealth>();
        if (health == null)
        {
            // maybe parented
            health = target.GetComponentInParent<PlayerHealth>();
        }
        if (health == null) return;

        float damage = contactDamage;
        if (useRelativeVelocityBonus)
        {
            var pdr = target.GetComponent<PlayerDamageReceiver>();
            if (pdr != null)
            {
                float extra = pdr.PreviousVelocity.magnitude * velocityDamageMultiplier;
                damage += extra;
            }
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
