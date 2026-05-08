using UnityEngine;

public sealed class CentipedeBurstController : MonoBehaviour
{
    [Header("Burst")]
    [SerializeField, Min(1f)] private float burstSpeedMultiplier = 2f;
    [SerializeField, Min(0.05f)] private float burstMinDuration = 0.5f;
    [SerializeField, Min(0.05f)] private float burstMaxDuration = 1.0f;
    [SerializeField, Min(0f)] private float burstCooldown = 15f;
    [SerializeField, Range(0f, 1f)] private float burstProbabilityPerCheck = 0.18f;
    [SerializeField, Min(0.05f)] private float burstCheckInterval = 1.25f;
    [SerializeField, Min(0f)] private float burstDistanceThreshold = 12f;

    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = true;
    [SerializeField] private bool logBurstEvents;

    private float nextCheckTime;
    private float nextAllowedBurstTime;
    private float burstEndTime;
    private bool bursting;

    public bool IsBursting => bursting;
    public float SpeedMultiplier => bursting ? burstSpeedMultiplier : 1f;

    private void OnValidate()
    {
        burstSpeedMultiplier = Mathf.Max(1f, burstSpeedMultiplier);
        burstMinDuration = Mathf.Max(0.05f, burstMinDuration);
        burstMaxDuration = Mathf.Max(burstMinDuration, burstMaxDuration);
        burstCheckInterval = Mathf.Max(0.05f, burstCheckInterval);
    }

    public float Tick(float targetDistance, bool canBurst)
    {
        if (bursting)
        {
            if (Time.time >= burstEndTime || !canBurst)
            {
                EndBurst();
            }

            return SpeedMultiplier;
        }

        if (!canBurst || Time.time < nextAllowedBurstTime || Time.time < nextCheckTime)
        {
            return 1f;
        }

        nextCheckTime = Time.time + burstCheckInterval;
        bool usefulDistance = targetDistance >= burstDistanceThreshold;
        bool randomPulse = Random.value <= burstProbabilityPerCheck;
        if (usefulDistance && randomPulse)
        {
            StartBurst();
        }

        return SpeedMultiplier;
    }

    private void StartBurst()
    {
        bursting = true;
        float duration = Random.Range(burstMinDuration, burstMaxDuration);
        burstEndTime = Time.time + duration;
        nextAllowedBurstTime = Time.time + burstCooldown;
        if (logBurstEvents)
        {
            Debug.Log($"CentipedeBurstController: burst started duration={duration:F2}.", this);
        }
    }

    private void EndBurst()
    {
        if (!bursting)
        {
            return;
        }

        bursting = false;
        if (logBurstEvents)
        {
            Debug.Log("CentipedeBurstController: burst ended.", this);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos)
        {
            return;
        }

        Gizmos.color = bursting ? Color.red : Color.blue;
        Gizmos.DrawWireSphere(transform.position, 0.75f);
    }
}
