using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(80)]
[RequireComponent(typeof(CentipedeBodyController))]
public sealed class CentipedeLegIK : MonoBehaviour
{
    private sealed class LegState
    {
        public int SegmentIndex;
        public int Side;
        public Transform Upper;
        public Transform Lower;
        public Vector3 FootPosition;
        public Vector3 FootNormal;
        public Vector3 StepStart;
        public Vector3 StepEnd;
        public Vector3 StepStartNormal;
        public Vector3 StepEndNormal;
        public float StepTime;
        public bool IsStepping;
        public bool HasPlant;
    }

    [Header("References")]
    [SerializeField] private LayerMask crawlableSurfaceMask = ~0;
    [SerializeField] private Transform ignoredRoot;

    [Header("Leg Shape")]
    [SerializeField, Min(0.05f)] private float legUpperLength = 0.72f;
    [SerializeField, Min(0.05f)] private float legLowerLength = 0.72f;
    [SerializeField, Min(0.01f)] private float legCylinderRadius = 0.045f;
    [SerializeField, Min(0f)] private float legSideOffset = 0.52f;
    [SerializeField] private float legForwardOffset = 0.05f;
    [SerializeField] private Material legMaterial;

    [Header("Foot Planting")]
    [SerializeField, Min(0.05f)] private float legStepDistance = 0.55f;
    [SerializeField, Min(0f)] private float legStepHeight = 0.22f;
    [SerializeField, Min(0.01f)] private float legStepDuration = 0.18f;
    [SerializeField, Min(0f)] private float maxLegExtensionExtra = 0.45f;
    [SerializeField, Min(0.05f)] private float legSurfaceProbeDistance = 2.8f;
    [SerializeField, Min(0f)] private float footSurfaceOffset = 0.035f;
    [SerializeField, Min(0f)] private float footPlantSmoothing = 24f;

    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = true;

    private readonly List<LegState> legs = new();
    private CentipedeBodyController body;
    private Transform legRoot;

    public int LegCount => legs.Count;

    private void Awake()
    {
        CacheReferences();
        EnsureLegs();
        ForcePlantAllFeet();
    }

    private void OnEnable()
    {
        CacheReferences();
        EnsureLegs();
        ForcePlantAllFeet();
    }

    private void OnValidate()
    {
        legUpperLength = Mathf.Max(0.05f, legUpperLength);
        legLowerLength = Mathf.Max(0.05f, legLowerLength);
        legCylinderRadius = Mathf.Max(0.01f, legCylinderRadius);
        legStepDistance = Mathf.Max(0.05f, legStepDistance);
        legStepDuration = Mathf.Max(0.01f, legStepDuration);
    }

    private void LateUpdate()
    {
        CacheReferences();
        EnsureLegs();

        float deltaTime = Time.deltaTime;
        for (int i = 0; i < legs.Count; i++)
        {
            UpdateLeg(legs[i], deltaTime);
        }
    }

    [ContextMenu("Rebuild Centipede Legs")]
    public void RebuildLegs()
    {
        CacheReferences();
        EnsureLegs();
        ForcePlantAllFeet();
        UpdateAllLegVisuals();
    }

    private void CacheReferences()
    {
        if (body == null)
        {
            body = GetComponent<CentipedeBodyController>();
        }

        if (ignoredRoot == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                ignoredRoot = player.transform;
            }
        }
    }

    private void EnsureLegs()
    {
        if (body == null)
        {
            return;
        }

        if (body.SegmentCount == 0)
        {
            body.RebuildBody();
        }

        if (legRoot == null)
        {
            Transform existing = transform.Find("Legs");
            if (existing != null)
            {
                legRoot = existing;
            }
            else
            {
                GameObject root = new("Legs");
                legRoot = root.transform;
                legRoot.SetParent(transform, false);
            }
        }

        int expectedCount = body.SegmentCount * 2;
        while (legs.Count < expectedCount)
        {
            int legIndex = legs.Count;
            int segmentIndex = legIndex / 2;
            int side = legIndex % 2 == 0 ? -1 : 1;
            legs.Add(CreateLeg(segmentIndex, side));
        }

        while (legs.Count > expectedCount)
        {
            LegState leg = legs[legs.Count - 1];
            if (leg.Upper != null)
            {
                DestroySafe(leg.Upper.gameObject);
            }
            if (leg.Lower != null)
            {
                DestroySafe(leg.Lower.gameObject);
            }
            legs.RemoveAt(legs.Count - 1);
        }

        for (int i = 0; i < legs.Count; i++)
        {
            legs[i].SegmentIndex = i / 2;
            legs[i].Side = i % 2 == 0 ? -1 : 1;
            EnsureLegTransforms(legs[i]);
        }
    }

    private LegState CreateLeg(int segmentIndex, int side)
    {
        LegState leg = new()
        {
            SegmentIndex = segmentIndex,
            Side = side,
            FootNormal = Vector3.up,
            StepStartNormal = Vector3.up,
            StepEndNormal = Vector3.up
        };

        EnsureLegTransforms(leg);
        return leg;
    }

    private void EnsureLegTransforms(LegState leg)
    {
        string prefix = GetLegNamePrefix(leg);
        if (leg.Upper == null)
        {
            Transform existing = legRoot.Find(prefix + " Upper");
            leg.Upper = existing != null ? existing : CreateLegCylinder(prefix + " Upper");
        }
        if (leg.Lower == null)
        {
            Transform existing = legRoot.Find(prefix + " Lower");
            leg.Lower = existing != null ? existing : CreateLegCylinder(prefix + " Lower");
        }

        leg.Upper.name = prefix + " Upper";
        leg.Lower.name = prefix + " Lower";
        leg.Upper.SetParent(legRoot, true);
        leg.Lower.SetParent(legRoot, true);
    }

    private Transform CreateLegCylinder(string objectName)
    {
        GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylinder.name = objectName;
        Collider collider = cylinder.GetComponent<Collider>();
        if (collider != null)
        {
            DestroySafe(collider);
        }

        MeshRenderer renderer = cylinder.GetComponent<MeshRenderer>();
        if (renderer != null && legMaterial != null)
        {
            renderer.sharedMaterial = legMaterial;
        }

        return cylinder.transform;
    }

    private void ForcePlantAllFeet()
    {
        for (int i = 0; i < legs.Count; i++)
        {
            LegState leg = legs[i];
            Transform segment = body != null ? body.GetSegment(leg.SegmentIndex) : null;
            if (segment == null)
            {
                continue;
            }

            Vector3 anchor = GetLegAnchor(segment, leg);
            Vector3 desired = GetDesiredFootPosition(segment, leg);
            if (TryFindFootPlant(desired, segment.up, out Vector3 foot, out Vector3 normal))
            {
                leg.FootPosition = foot;
                leg.FootNormal = normal;
                leg.HasPlant = true;
                leg.IsStepping = false;
            }
            else
            {
                leg.FootPosition = ClampFootToReach(anchor, desired, segment.up);
                leg.FootNormal = segment.up;
                leg.HasPlant = true;
                leg.IsStepping = false;
            }
        }
    }

    private void UpdateLeg(LegState leg, float deltaTime)
    {
        Transform segment = body != null ? body.GetSegment(leg.SegmentIndex) : null;
        if (segment == null)
        {
            return;
        }

        Vector3 anchor = GetLegAnchor(segment, leg);
        Vector3 desired = GetDesiredFootPosition(segment, leg);
        if (!leg.HasPlant)
        {
            PlantImmediately(leg, anchor, desired, segment.up);
        }

        float maxReach = GetMaxLegReach();
        float anchorDistance = Vector3.Distance(anchor, leg.FootPosition);
        float desiredDistance = Vector3.Distance(desired, leg.FootPosition);
        bool overExtended = anchorDistance > maxReach;

        if (!leg.IsStepping && (desiredDistance > legStepDistance || overExtended))
        {
            BeginStep(leg, anchor, desired, segment.up);
        }

        if (leg.IsStepping)
        {
            leg.StepTime += deltaTime / legStepDuration;
            float t = Mathf.Clamp01(leg.StepTime);
            float smoothT = t * t * (3f - 2f * t);
            Vector3 foot = Vector3.Lerp(leg.StepStart, leg.StepEnd, smoothT);
            foot += segment.up * (Mathf.Sin(Mathf.PI * smoothT) * legStepHeight);
            leg.FootPosition = ClampFootToReach(anchor, foot, segment.up);
            leg.FootNormal = Vector3.Slerp(leg.StepStartNormal, leg.StepEndNormal, smoothT);

            if (t >= 1f)
            {
                leg.FootPosition = ClampFootToReach(anchor, leg.StepEnd, segment.up);
                leg.FootNormal = leg.StepEndNormal;
                leg.IsStepping = false;
            }
        }
        else
        {
            leg.FootPosition = ClampFootToReach(anchor, leg.FootPosition, segment.up);
            float weight = 1f - Mathf.Exp(-footPlantSmoothing * deltaTime);
            if (TryFindFootPlant(leg.FootPosition + leg.FootNormal * 0.12f, leg.FootNormal, out Vector3 refreshedFoot, out Vector3 refreshedNormal))
            {
                leg.FootPosition = Vector3.Lerp(leg.FootPosition, refreshedFoot, weight);
                leg.FootNormal = Vector3.Slerp(leg.FootNormal, refreshedNormal, weight);
            }
        }

        UpdateLegVisual(leg, anchor, segment.up);
    }

    private void PlantImmediately(LegState leg, Vector3 anchor, Vector3 desired, Vector3 normalHint)
    {
        if (TryFindFootPlant(desired, normalHint, out Vector3 foot, out Vector3 normal))
        {
            leg.FootPosition = ClampFootToReach(anchor, foot, normalHint);
            leg.FootNormal = normal;
        }
        else
        {
            leg.FootPosition = ClampFootToReach(anchor, desired, normalHint);
            leg.FootNormal = normalHint;
        }

        leg.HasPlant = true;
        leg.IsStepping = false;
    }

    private void BeginStep(LegState leg, Vector3 anchor, Vector3 desired, Vector3 normalHint)
    {
        leg.StepStart = leg.FootPosition;
        leg.StepStartNormal = leg.FootNormal.sqrMagnitude > 0.0001f ? leg.FootNormal.normalized : normalHint;
        if (TryFindFootPlant(desired, normalHint, out Vector3 foot, out Vector3 normal))
        {
            leg.StepEnd = ClampFootToReach(anchor, foot, normalHint);
            leg.StepEndNormal = normal;
        }
        else
        {
            leg.StepEnd = ClampFootToReach(anchor, desired, normalHint);
            leg.StepEndNormal = normalHint;
        }

        leg.StepTime = 0f;
        leg.IsStepping = true;
    }

    private Vector3 GetLegAnchor(Transform segment, LegState leg)
    {
        float radius = body != null ? body.SegmentDiameter * 0.5f : 0.45f;
        return segment.position +
               segment.right * (leg.Side * radius * 0.45f) +
               segment.forward * legForwardOffset -
               segment.up * (radius * 0.12f);
    }

    private Vector3 GetDesiredFootPosition(Transform segment, LegState leg)
    {
        float radius = body != null ? body.SegmentDiameter * 0.5f : 0.45f;
        return segment.position +
               segment.right * (leg.Side * Mathf.Max(legSideOffset, radius * 1.1f)) +
               segment.forward * (legForwardOffset + leg.Side * 0.02f) -
               segment.up * (radius + 0.15f);
    }

    private bool TryFindFootPlant(Vector3 desired, Vector3 normalHint, out Vector3 foot, out Vector3 normal)
    {
        Vector3 direction = normalHint.sqrMagnitude > 0.0001f ? -normalHint.normalized : -transform.up;
        Vector3 origin = desired - direction * (legSurfaceProbeDistance * 0.45f);
        RaycastHit[] hits = Physics.SphereCastAll(origin, 0.07f, direction, legSurfaceProbeDistance, crawlableSurfaceMask, QueryTriggerInteraction.Ignore);
        int bestIndex = -1;
        float bestDistance = float.PositiveInfinity;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider collider = hits[i].collider;
            if (collider == null || IsIgnored(collider))
            {
                continue;
            }

            if (hits[i].distance < bestDistance)
            {
                bestDistance = hits[i].distance;
                bestIndex = i;
            }
        }

        if (bestIndex >= 0)
        {
            normal = hits[bestIndex].normal.normalized;
            foot = hits[bestIndex].point + normal * footSurfaceOffset;
            return true;
        }

        foot = desired;
        normal = normalHint.sqrMagnitude > 0.0001f ? normalHint.normalized : Vector3.up;
        return false;
    }

    private Vector3 ClampFootToReach(Vector3 anchor, Vector3 foot, Vector3 normalHint)
    {
        float maxReach = GetMaxLegReach();
        Vector3 fromAnchor = foot - anchor;
        float distance = fromAnchor.magnitude;
        if (distance <= maxReach || distance <= 0.0001f)
        {
            return foot;
        }

        Vector3 clamped = anchor + fromAnchor / distance * maxReach;
        if (TryFindFootPlant(clamped, normalHint, out Vector3 planted, out _))
        {
            return planted;
        }

        return clamped;
    }

    private float GetMaxLegReach()
    {
        return legUpperLength + legLowerLength + Mathf.Max(0f, maxLegExtensionExtra);
    }

    private void UpdateLegVisual(LegState leg, Vector3 anchor, Vector3 segmentNormal)
    {
        Vector3 foot = leg.FootPosition;
        Vector3 toFoot = foot - anchor;
        float targetDistance = Mathf.Max(0.0001f, toFoot.magnitude);
        Vector3 direction = toFoot / targetDistance;
        Vector3 bendNormal = Vector3.Cross(direction, segmentNormal);
        if (bendNormal.sqrMagnitude < 0.0001f)
        {
            bendNormal = Vector3.Cross(direction, transform.forward);
        }
        Vector3 bendDirection = Vector3.Cross(bendNormal.normalized, direction).normalized * leg.Side;

        float upper = legUpperLength;
        float lower = legLowerLength;
        float clampedDistance = Mathf.Min(targetDistance, upper + lower - 0.001f);
        float along = (upper * upper - lower * lower + clampedDistance * clampedDistance) / (2f * clampedDistance);
        float heightSquared = Mathf.Max(0f, upper * upper - along * along);
        Vector3 knee = anchor + direction * along + bendDirection * Mathf.Sqrt(heightSquared);

        PlaceCylinder(leg.Upper, anchor, knee);
        PlaceCylinder(leg.Lower, knee, foot);
    }

    private void UpdateAllLegVisuals()
    {
        for (int i = 0; i < legs.Count; i++)
        {
            Transform segment = body != null ? body.GetSegment(legs[i].SegmentIndex) : null;
            if (segment == null)
            {
                continue;
            }

            UpdateLegVisual(legs[i], GetLegAnchor(segment, legs[i]), segment.up);
        }
    }

    private void PlaceCylinder(Transform cylinder, Vector3 start, Vector3 end)
    {
        if (cylinder == null)
        {
            return;
        }

        Vector3 delta = end - start;
        float length = delta.magnitude;
        if (length <= 0.0001f)
        {
            cylinder.gameObject.SetActive(false);
            return;
        }

        cylinder.gameObject.SetActive(true);
        cylinder.position = (start + end) * 0.5f;
        cylinder.rotation = Quaternion.FromToRotation(Vector3.up, delta / length);
        cylinder.localScale = new Vector3(legCylinderRadius * 2f, length * 0.5f, legCylinderRadius * 2f);
    }

    private bool IsIgnored(Collider collider)
    {
        return ignoredRoot != null && collider.transform.IsChildOf(ignoredRoot);
    }

    private string GetLegNamePrefix(LegState leg)
    {
        return $"Segment {leg.SegmentIndex:00} {(leg.Side < 0 ? "Left" : "Right")} Leg";
    }

    private static void DestroySafe(Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos)
        {
            return;
        }

        Gizmos.color = Color.cyan;
        for (int i = 0; i < legs.Count; i++)
        {
            LegState leg = legs[i];
            Transform segment = body != null ? body.GetSegment(leg.SegmentIndex) : null;
            if (segment == null)
            {
                continue;
            }

            Vector3 anchor = GetLegAnchor(segment, leg);
            Gizmos.DrawLine(anchor, leg.FootPosition);
            Gizmos.DrawWireSphere(leg.FootPosition, 0.06f);
            Gizmos.DrawLine(leg.FootPosition, leg.FootPosition + leg.FootNormal * 0.3f);
        }
    }
}
