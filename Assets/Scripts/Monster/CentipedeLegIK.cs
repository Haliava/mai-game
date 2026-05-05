using UnityEngine;

public enum LegState
{
    Planted,
    Stepping,
    Airborne
}

public class CentipedeLegIK : MonoBehaviour
{
    [SerializeField] Transform segmentRoot;
    [SerializeField] Transform hip;
    [SerializeField] Transform upperLeg;
    [SerializeField] Transform lowerLeg;
    [SerializeField] Transform foot;
    [SerializeField] bool topLeg;
    [SerializeField] Vector3 localHipOffset;
    [SerializeField] Vector3 localDesiredFootOffset;
    [SerializeField] float stepDistance = 0.7f;
    [SerializeField] float stepHeight = 0.25f;
    [SerializeField] float stepDuration = 0.12f;
    [SerializeField] float footSurfaceOffset = 0.05f;
    [SerializeField] float probeRadius = 0.12f;
    [SerializeField] float probeDistance = 2.4f;
    [SerializeField] float gaitCycleSpeed = 4f;
    [SerializeField] float phaseOffset;
    [SerializeField] float surfaceProbeInterval = 0.08f;
    [SerializeField] float maxLegStretchDistance = 3f;
    [SerializeField] LayerMask walkableMask = ~0;

    static readonly RaycastHit[] ProbeHits = new RaycastHit[8];

    Vector3 plantedPosition;
    Vector3 plantedNormal = Vector3.up;
    Vector3 stepStart;
    Vector3 stepEnd;
    Vector3 stepEndNormal;
    Vector3 cachedDesiredFootPoint;
    Vector3 cachedDesiredNormal = Vector3.up;
    float stepTimer;
    float nextProbeTime;
    bool hasPlant;
    bool cachedFootFound;

    public LegState State { get; private set; }
    public Vector3 FootTarget { get { return plantedPosition; } }

    public void Initialize(
        Transform owner,
        bool isTopLeg,
        Vector3 hipOffset,
        Vector3 desiredFootOffset,
        float phase,
        LayerMask mask,
        Material material,
        float maxStretchDistance,
        float probeInterval)
    {
        segmentRoot = owner;
        topLeg = isTopLeg;
        localHipOffset = hipOffset;
        localDesiredFootOffset = desiredFootOffset;
        phaseOffset = phase;
        walkableMask = mask;
        maxLegStretchDistance = Mathf.Max(0.25f, maxStretchDistance);
        surfaceProbeInterval = Mathf.Max(0.02f, probeInterval);

        if (hip == null) hip = CreateMarker("Hip", 0.08f, material);
        if (upperLeg == null) upperLeg = CreateLimb("UpperLeg", material);
        if (lowerLeg == null) lowerLeg = CreateLimb("LowerLeg", material);
        if (foot == null) foot = CreateMarker("Foot", 0.11f, material);
        nextProbeTime = Time.time + Mathf.Repeat(phaseOffset, surfaceProbeInterval);
        State = LegState.Airborne;
    }

    public void Tick(bool bodyAirborne)
    {
        if (segmentRoot == null) segmentRoot = transform.parent;
        if (segmentRoot == null) return;

        Vector3 hipPosition = segmentRoot.TransformPoint(localHipOffset);
        hip.position = hipPosition;

        if (bodyAirborne)
        {
            State = LegState.Airborne;
            hasPlant = false;
            Vector3 tucked = hipPosition + segmentRoot.TransformDirection(localDesiredFootOffset.normalized) * 0.35f;
            plantedPosition = Vector3.Lerp(foot.position, tucked, 14f * Time.deltaTime);
            plantedNormal = segmentRoot.up;
            foot.position = ClampToLegReach(hipPosition, plantedPosition);
            UpdateLegVisuals(hipPosition, plantedPosition);
            return;
        }

        Vector3 desired;
        Vector3 normal;
        bool found = TryGetDesiredFootPoint(out desired, out normal);
        if (!hasPlant && found)
        {
            hasPlant = true;
            plantedPosition = desired;
            plantedNormal = normal;
            foot.position = plantedPosition;
            State = LegState.Planted;
        }

        if (State == LegState.Stepping)
        {
            stepTimer += Time.deltaTime;
            float t = Mathf.Clamp01(stepTimer / Mathf.Max(0.01f, stepDuration));
            Vector3 arc = Vector3.Lerp(stepStart, stepEnd, t);
            arc += Vector3.Lerp(plantedNormal, stepEndNormal, t).normalized * Mathf.Sin(t * Mathf.PI) * stepHeight;
            foot.position = ClampToLegReach(hipPosition, arc);
            if (t >= 1f)
            {
                plantedPosition = ClampToLegReach(hipPosition, stepEnd);
                plantedNormal = stepEndNormal;
                foot.position = plantedPosition;
                State = LegState.Planted;
            }
        }
        else if (found)
        {
            float phase = Mathf.Repeat(Time.time * gaitCycleSpeed + phaseOffset, 1f);
            bool phaseAllowsStep = phase < 0.58f;
            float distance = Vector3.Distance(plantedPosition, desired);
            bool overStretched = HasOverstretched(hipPosition, plantedPosition);
            if (overStretched)
            {
                foot.position = ClampToLegReach(hipPosition, foot.position);
                StartStep(foot.position, desired, normal);
            }
            else if (distance > stepDistance && phaseAllowsStep)
            {
                StartStep(foot.position, desired, normal);
            }
            else
            {
                foot.position = ClampToLegReach(hipPosition, plantedPosition);
                State = LegState.Planted;
            }
        }
        else if (hasPlant && HasOverstretched(hipPosition, plantedPosition))
        {
            hasPlant = false;
            State = LegState.Airborne;
            foot.position = ClampToLegReach(hipPosition, foot.position);
        }

        UpdateLegVisuals(hipPosition, foot.position);
    }

    bool TryGetDesiredFootPoint(out Vector3 footPoint, out Vector3 normal)
    {
        if (Time.time >= nextProbeTime || !cachedFootFound || !hasPlant)
        {
            cachedFootFound = TryFindDesiredFootPoint(out cachedDesiredFootPoint, out cachedDesiredNormal);
            nextProbeTime = Time.time + surfaceProbeInterval;
        }

        footPoint = cachedDesiredFootPoint;
        normal = cachedDesiredNormal;
        return cachedFootFound;
    }

    void StartStep(Vector3 from, Vector3 to, Vector3 normal)
    {
        stepStart = from;
        stepEnd = to;
        stepEndNormal = normal;
        stepTimer = 0f;
        State = LegState.Stepping;
    }

    bool TryFindDesiredFootPoint(out Vector3 footPoint, out Vector3 normal)
    {
        footPoint = segmentRoot.TransformPoint(localDesiredFootOffset);
        normal = segmentRoot.up;

        Vector3 origin = footPoint;
        Vector3[] directions = topLeg
            ? new Vector3[] { segmentRoot.up, segmentRoot.right, -segmentRoot.right, segmentRoot.forward, -segmentRoot.forward }
            : new Vector3[] { -segmentRoot.up, -segmentRoot.up + segmentRoot.forward * 0.35f, -segmentRoot.up - segmentRoot.forward * 0.25f };

        float bestDistance = float.MaxValue;
        RaycastHit bestHit = default(RaycastHit);
        for (int i = 0; i < directions.Length; i++)
        {
            Vector3 direction = directions[i].normalized;
            int hitCount = Physics.SphereCastNonAlloc(
                origin - direction * 0.25f,
                probeRadius,
                direction,
                ProbeHits,
                probeDistance,
                walkableMask,
                QueryTriggerInteraction.Ignore);
            for (int j = 0; j < hitCount; j++)
            {
                if (!SurfaceProbe.IsUsableHit(ProbeHits[j], segmentRoot.root)) continue;
                if (ProbeHits[j].distance >= bestDistance) continue;
                bestDistance = ProbeHits[j].distance;
                bestHit = ProbeHits[j];
            }
        }

        if (bestHit.collider == null) return false;
        normal = bestHit.normal;
        footPoint = bestHit.point + normal * footSurfaceOffset;
        return true;
    }

    void UpdateLegVisuals(Vector3 hipPosition, Vector3 footPosition)
    {
        footPosition = ClampToLegReach(hipPosition, footPosition);
        if (foot != null) foot.position = footPosition;
        Vector3 knee = (hipPosition + footPosition) * 0.5f + segmentRoot.up * 0.18f;
        PlaceLimb(upperLeg, hipPosition, knee);
        PlaceLimb(lowerLeg, knee, footPosition);
    }

    bool HasOverstretched(Vector3 hipPosition, Vector3 target)
    {
        return Vector3.SqrMagnitude(target - hipPosition) > maxLegStretchDistance * maxLegStretchDistance;
    }

    Vector3 ClampToLegReach(Vector3 hipPosition, Vector3 target)
    {
        Vector3 delta = target - hipPosition;
        float maxDistance = Mathf.Max(0.1f, maxLegStretchDistance);
        if (delta.sqrMagnitude <= maxDistance * maxDistance) return target;
        if (delta.sqrMagnitude < 0.0001f) return hipPosition;
        return hipPosition + delta.normalized * maxDistance;
    }

    Transform CreateMarker(string markerName, float radius, Material material)
    {
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.name = markerName;
        marker.transform.SetParent(transform, false);
        marker.transform.localScale = Vector3.one * radius;
        Collider col = marker.GetComponent<Collider>();
        if (col != null) col.enabled = false;
        Renderer renderer = marker.GetComponent<Renderer>();
        if (renderer != null && material != null) renderer.sharedMaterial = material;
        return marker.transform;
    }

    Transform CreateLimb(string limbName, Material material)
    {
        GameObject limb = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        limb.name = limbName;
        limb.transform.SetParent(transform, false);
        Collider col = limb.GetComponent<Collider>();
        if (col != null) col.enabled = false;
        Renderer renderer = limb.GetComponent<Renderer>();
        if (renderer != null && material != null) renderer.sharedMaterial = material;
        return limb.transform;
    }

    void PlaceLimb(Transform limb, Vector3 from, Vector3 to)
    {
        if (limb == null) return;
        Vector3 delta = to - from;
        float length = delta.magnitude;
        if (length < 0.001f) return;

        limb.position = (from + to) * 0.5f;
        limb.rotation = Quaternion.FromToRotation(Vector3.up, delta.normalized);
        limb.localScale = new Vector3(0.05f, length * 0.5f, 0.05f);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = State == LegState.Airborne ? Color.magenta : (State == LegState.Stepping ? Color.yellow : Color.green);
        Gizmos.DrawWireSphere(plantedPosition, 0.12f);
    }
}
