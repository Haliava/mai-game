using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerHealth : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth = 100f;
    [SerializeField, Min(0f)] private float damageInvulnerabilityTime = 0.5f;
    [SerializeField] private bool godMode = false;
    [SerializeField] private bool showDebugLogs = true;

    [Header("Death / Respawn")]
    [SerializeField] private bool respawnOnDeath = true;
    [SerializeField] private Transform respawnPoint;
    [SerializeField, Min(0f)] private float respawnDelay = 2f;

    private float lastDamageTime = -999f;
    private Vector3 startPosition;

    public event Action<float, DamageType, GameObject> OnDamaged;
    public event Action OnDeath;
    public event Action<float> OnHealthChanged;

    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;
    public bool IsAlive => currentHealth > 0f;

    private void Awake()
    {
        startPosition = transform.position;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        PlayerHealthUI.EnsureInScene()?.RegisterHealth(this);
    }

    public void Heal(float amount)
    {
        if (amount <= 0f) return;
        currentHealth = Mathf.Clamp(currentHealth + amount, 0f, maxHealth);
        OnHealthChanged?.Invoke(currentHealth);
        if (showDebugLogs) Debug.Log($"[PlayerHealth] Healed {amount}. Current={currentHealth}");
    }

    public bool CanTakeDamage => Time.time >= lastDamageTime + damageInvulnerabilityTime;

    public void TakeDamage(float amount, DamageType type = DamageType.Unknown, GameObject source = null)
    {
        if (godMode)
        {
            if (showDebugLogs) Debug.Log($"[PlayerHealth] (GodMode) Ignored {amount} damage ({type}) from {source}");
            return;
        }

        if (amount <= 0f || !CanTakeDamage)
        {
            if (showDebugLogs && amount > 0f) Debug.Log($"[PlayerHealth] Ignored damage because invulnerable or zero (amount={amount})");
            return;
        }

        lastDamageTime = Time.time;
        float prev = currentHealth;
        currentHealth = Mathf.Clamp(currentHealth - amount, 0f, maxHealth);
        OnDamaged?.Invoke(amount, type, source);
        OnHealthChanged?.Invoke(currentHealth);

        if (showDebugLogs) Debug.Log($"[PlayerHealth] Took {amount} ({type}) from {source?.name ?? "null"}. {prev}->{currentHealth}");

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    private void Die()
    {
        if (showDebugLogs) Debug.Log("[PlayerHealth] Player died");
        OnDeath?.Invoke();
        StartCoroutine(DoDeathBehavior());
    }

    private System.Collections.IEnumerator DoDeathBehavior()
    {
        // disable player control where possible
        var fps = FindAnyObjectByType<FPSCharacterController3D>();
        if (fps != null)
        {
            fps.SetGrappleMovementPaused(true);
            fps.SetLookPaused(true);
            fps.enabled = false;
        }

        if (respawnOnDeath)
        {
            yield return new WaitForSeconds(respawnDelay);
            Respawn();
        }
    }

    private void Respawn()
    {
        if (respawnPoint != null)
        {
            transform.position = respawnPoint.position;
            transform.rotation = respawnPoint.rotation;
        }
        else
        {
            transform.position = startPosition;
        }

        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth);

        var fps = FindAnyObjectByType<FPSCharacterController3D>();
        if (fps != null)
        {
            fps.enabled = true;
            fps.SetLookPaused(false);
            fps.SetGrappleMovementPaused(false);
        }

        if (showDebugLogs) Debug.Log("[PlayerHealth] Player respawned");
    }
}
