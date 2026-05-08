using UnityEngine;

public static class StructureSpawner
{
    private static readonly GeneratedStructureLibrary.StructureKind[] Kinds =
    {
        GeneratedStructureLibrary.StructureKind.BrokenPlatformCluster,
        GeneratedStructureLibrary.StructureKind.FloatingSteps,
        GeneratedStructureLibrary.StructureKind.HollowCylinderRuin,
        GeneratedStructureLibrary.StructureKind.RingSegment,
        GeneratedStructureLibrary.StructureKind.PillarGroup,
        GeneratedStructureLibrary.StructureKind.AncientBridgeFragment,
        GeneratedStructureLibrary.StructureKind.VerticalWallWithLedges,
        GeneratedStructureLibrary.StructureKind.SpiralLedgeFragment,
        GeneratedStructureLibrary.StructureKind.MassiveFallenSlab,
        GeneratedStructureLibrary.StructureKind.SmallLonelyPlatform,
        GeneratedStructureLibrary.StructureKind.NestedCylinderStructure,
        GeneratedStructureLibrary.StructureKind.BrokenColumnArc,
        GeneratedStructureLibrary.StructureKind.IrregularGrappleOutcrop
    };

    private static readonly float[] Weights =
    {
        1.25f,
        1.05f,
        0.9f,
        0.85f,
        1f,
        0.95f,
        0.85f,
        0.8f,
        0.55f,
        0.7f,
        0.55f,
        0.7f,
        0.95f
    };

    public static GeneratedStructureLibrary.StructureKind PickKind(System.Random rng)
    {
        float total = 0f;
        for (int i = 0; i < Weights.Length; i++)
        {
            total += Mathf.Max(0f, Weights[i]);
        }

        float roll = (float)rng.NextDouble() * total;
        for (int i = 0; i < Kinds.Length; i++)
        {
            roll -= Mathf.Max(0f, Weights[i]);
            if (roll <= 0f)
            {
                return Kinds[i];
            }
        }

        return GeneratedStructureLibrary.StructureKind.BrokenPlatformCluster;
    }
}
