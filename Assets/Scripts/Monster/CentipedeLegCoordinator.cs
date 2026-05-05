using System.Collections.Generic;
using UnityEngine;

public class CentipedeLegCoordinator : MonoBehaviour
{
    [SerializeField] List<CentipedeLegIK> legs = new List<CentipedeLegIK>();
    [SerializeField] float segmentPhaseOffset = 0.35f;
    [SerializeField] float legSpread = 0.8f;
    [SerializeField] float forwardOffset = 0.35f;
    [SerializeField] float maxLegStretchWidthMultiplier = 1f;
    [SerializeField] float legProbeInterval = 0.1f;
    [SerializeField] LayerMask walkableMask = ~0;
    [SerializeField] Material legMaterial;
    [SerializeField] int segmentIndex;

    public void EnsureLegs(int index, float segmentRadius, LayerMask mask, Material material)
    {
        segmentIndex = index;
        walkableMask = mask;
        legMaterial = material;
        legSpread = Mathf.Max(0.25f, segmentRadius * 0.45f);
        forwardOffset = Mathf.Max(0.15f, segmentRadius * 0.25f);

        while (legs.Count < 3)
        {
            GameObject legObject = new GameObject("Leg_" + legs.Count.ToString("00"));
            legObject.transform.SetParent(transform, false);
            legs.Add(legObject.AddComponent<CentipedeLegIK>());
        }

        float phaseBase = segmentIndex * segmentPhaseOffset;
        float bodyWidth = Mathf.Max(0.25f, segmentRadius * 2f);
        float maxLegStretch = bodyWidth * Mathf.Max(0.2f, maxLegStretchWidthMultiplier);
        legs[0].name = "Leg_LeftLower";
        legs[0].Initialize(transform, false, new Vector3(-legSpread, -segmentRadius * 0.2f, 0f), new Vector3(-legSpread, -segmentRadius * 0.75f, forwardOffset), phaseBase, walkableMask, legMaterial, maxLegStretch, legProbeInterval);
        legs[1].name = "Leg_RightLower";
        legs[1].Initialize(transform, false, new Vector3(legSpread, -segmentRadius * 0.2f, 0f), new Vector3(legSpread, -segmentRadius * 0.75f, forwardOffset), phaseBase + 0.5f, walkableMask, legMaterial, maxLegStretch, legProbeInterval);
        legs[2].name = "Leg_Top";
        legs[2].Initialize(transform, true, new Vector3(0f, segmentRadius * 0.45f, 0f), new Vector3(0f, segmentRadius * 0.95f, forwardOffset), phaseBase + 0.25f, walkableMask, legMaterial, maxLegStretch, legProbeInterval);
    }

    public void Tick(bool airborne)
    {
        for (int i = 0; i < legs.Count; i++)
        {
            if (legs[i] != null) legs[i].Tick(airborne);
        }
    }
}
