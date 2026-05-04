using UnityEngine;
using UnityEngine.Events;

public class PlayerDamageController : MonoBehaviour
{
    [SerializeField] float maxHealth = 100f;
    [SerializeField] float currentHealth = 100f;
    [SerializeField] bool godMode = false;
    [SerializeField] bool logDamage = true;

    public UnityEvent OnDied;
    public float CurrentHealth { get { return currentHealth; } }
    public float MaxHealth { get { return maxHealth; } }
    public bool IsDead { get; private set; }

    void Awake()
    {
        currentHealth = Mathf.Clamp(currentHealth <= 0f ? maxHealth : currentHealth, 0f, maxHealth);
    }

    public void TakeDamage(float amount, DamageType type)
    {
        if (godMode || IsDead || amount <= 0f) return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        if (logDamage)
        {
            Debug.Log("Player took " + amount.ToString("0.0") + " " + type + " damage. HP: " + currentHealth.ToString("0.0"));
        }

        if (currentHealth <= 0f) Die();
    }

    public void Heal(float amount)
    {
        if (amount <= 0f || IsDead) return;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
    }

    public void Revive()
    {
        IsDead = false;
        currentHealth = maxHealth;
    }

    public void Die()
    {
        if (IsDead) return;
        IsDead = true;
        Debug.Log("Player died.");
        if (OnDied != null) OnDied.Invoke();
    }
}
