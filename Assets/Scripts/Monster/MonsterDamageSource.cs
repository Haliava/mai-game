using UnityEngine;

public class MonsterDamageSource : MonoBehaviour
{
    [SerializeField] float contactDamage = 20f;
    [SerializeField] float attackCooldown = 1f;
    [SerializeField] float attackRange = 2.5f;

    float nextAttackTime;
    SphereCollider trigger;

    void Awake()
    {
        trigger = GetComponent<SphereCollider>();
        if (trigger == null) trigger = gameObject.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = attackRange;
    }

    void OnValidate()
    {
        SphereCollider sphere = GetComponent<SphereCollider>();
        if (sphere != null)
        {
            sphere.isTrigger = true;
            sphere.radius = attackRange;
        }
    }

    void OnTriggerStay(Collider other)
    {
        if (Time.time < nextAttackTime) return;
        PlayerDamageController playerDamage = other.GetComponentInParent<PlayerDamageController>();
        if (playerDamage == null) return;

        playerDamage.TakeDamage(contactDamage, DamageType.Monster);
        nextAttackTime = Time.time + attackCooldown;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
