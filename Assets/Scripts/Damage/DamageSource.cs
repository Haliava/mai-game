using UnityEngine;

public class DamageSource : MonoBehaviour
{
    [SerializeField] float damage = 25f;
    [SerializeField] float damageInterval = 1f;

    float nextDamageTime;

    void OnTriggerStay(Collider other)
    {
        if (Time.time < nextDamageTime) return;

        PlayerDamageController player = other.GetComponentInParent<PlayerDamageController>();
        if (player == null) return;

        player.TakeDamage(damage, DamageType.Monster);
        nextDamageTime = Time.time + damageInterval;
    }
}
