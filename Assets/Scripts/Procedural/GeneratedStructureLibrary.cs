using UnityEngine;

public static class GeneratedStructureLibrary
{
    private static BuildContext activeContext;

    public enum StructureKind
    {
        BrokenPlatformCluster,
        FloatingSteps,
        HollowCylinderRuin,
        RingSegment,
        PillarGroup,
        AncientBridgeFragment,
        VerticalWallWithLedges,
        SpiralLedgeFragment,
        MassiveFallenSlab,
        SmallLonelyPlatform,
        NestedCylinderStructure,
        BrokenColumnArc,
        IrregularGrappleOutcrop
    }

    public struct BuildContext
    {
        public Transform Parent;
        public System.Random Rng;
        public Vector3 Position;
        public Quaternion Rotation;
        public int GrappleLayer;
        public Material PrimaryMaterial;
        public Material AccentMaterial;
        public Material DarkMaterial;
        public float SizeMultiplier;
        public string NamePrefix;
        public float ShapeVariationAmount;
        public float NoiseScale;
        public float NoiseAmplitude;
        public float DomainWarpStrength;
        public int NoiseOctaves;
        public float AsymmetryAmount;
        public float BreakageAmount;
    }

    public struct Result
    {
        public GameObject Root;
        public StructureKind Kind;
        public int PieceCount;
        public int GrappleScore;
        public bool IsHuge;
        public Bounds Bounds;
    }

    public static Result CreateRandomStructure(BuildContext context)
    {
        return CreateStructure(StructureSpawner.PickKind(context.Rng), context);
    }

    public static Result CreateStructure(StructureKind kind, BuildContext context)
    {
        activeContext = context;
        GameObject root = new($"{context.NamePrefix} {kind}");
        root.transform.SetParent(context.Parent, false);
        root.transform.SetPositionAndRotation(context.Position, context.Rotation);
        SetLayerRecursively(root, context.GrappleLayer);

        Result result = new()
        {
            Root = root,
            Kind = kind,
            IsHuge = context.SizeMultiplier >= 1.85f
        };

        switch (kind)
        {
            case StructureKind.BrokenPlatformCluster:
                BuildBrokenPlatformCluster(root.transform, context, ref result);
                break;
            case StructureKind.FloatingSteps:
                BuildFloatingSteps(root.transform, context, ref result);
                break;
            case StructureKind.HollowCylinderRuin:
                BuildHollowCylinderRuin(root.transform, context, ref result);
                break;
            case StructureKind.RingSegment:
                BuildRingSegment(root.transform, context, ref result);
                break;
            case StructureKind.PillarGroup:
                BuildPillarGroup(root.transform, context, ref result);
                break;
            case StructureKind.AncientBridgeFragment:
                BuildAncientBridgeFragment(root.transform, context, ref result);
                break;
            case StructureKind.VerticalWallWithLedges:
                BuildVerticalWallWithLedges(root.transform, context, ref result);
                break;
            case StructureKind.SpiralLedgeFragment:
                BuildSpiralLedgeFragment(root.transform, context, ref result);
                break;
            case StructureKind.MassiveFallenSlab:
                BuildMassiveFallenSlab(root.transform, context, ref result);
                break;
            case StructureKind.SmallLonelyPlatform:
                BuildSmallLonelyPlatform(root.transform, context, ref result);
                break;
            case StructureKind.NestedCylinderStructure:
                BuildNestedCylinderStructure(root.transform, context, ref result);
                break;
            case StructureKind.BrokenColumnArc:
                BuildBrokenColumnArc(root.transform, context, ref result);
                break;
            case StructureKind.IrregularGrappleOutcrop:
                BuildIrregularGrappleOutcrop(root.transform, context, ref result);
                break;
            default:
                BuildBrokenPlatformCluster(root.transform, context, ref result);
                break;
        }

        result.Bounds = CalculateBounds(root);
        return result;
    }

    private static void BuildHollowCylinderRuin(Transform root, BuildContext context, ref Result result)
    {
        float size = SafeSize(context);
        float radius = RandomRange(context.Rng, 4.5f, 8.5f) * size;
        float height = RandomRange(context.Rng, 4.5f, 10f) * size;
        int segments = RandomRange(context.Rng, 10, 18);
        int missingStart = RandomRange(context.Rng, 0, segments);
        int missingCount = RandomRange(context.Rng, 2, Mathf.Max(3, segments / 3));

        for (int i = 0; i < segments; i++)
        {
            if (IsWithinWrappedRange(i, missingStart, missingCount, segments) && Next01(context.Rng) < 0.75f)
            {
                continue;
            }

            float angle = Mathf.PI * 2f * i / segments;
            Vector3 radial = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vector3 position = radial * radius + Vector3.up * RandomRange(context.Rng, -0.4f, 0.4f) * size;
            Quaternion rotation = Quaternion.LookRotation(radial, Vector3.up);
            Vector3 scale = new(2f * Mathf.PI * radius / segments * RandomRange(context.Rng, 0.55f, 0.9f), height * RandomRange(context.Rng, 0.6f, 1.1f), RandomRange(context.Rng, 0.45f, 0.85f) * size);
            CreateCube(root, $"Broken Cylinder Wall {i:00}", position, rotation, scale, context.PrimaryMaterial, context.GrappleLayer, ref result, 3);
        }

        int ledges = RandomRange(context.Rng, 5, 10);
        for (int i = 0; i < ledges; i++)
        {
            float angle = RandomRange(context.Rng, 0f, Mathf.PI * 2f);
            Vector3 radial = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vector3 position = radial * (radius - 0.75f * size) + Vector3.up * RandomRange(context.Rng, -height * 0.45f, height * 0.45f);
            Quaternion rotation = Quaternion.LookRotation(-radial, Vector3.up);
            Vector3 scale = new(RandomRange(context.Rng, 1.5f, 4.2f) * size, RandomRange(context.Rng, 0.28f, 0.55f) * size, RandomRange(context.Rng, 1.0f, 2.6f) * size);
            Transform ledge = CreateCube(root, $"Inner Top-Side Ledge {i:00}", position, rotation, scale, context.AccentMaterial, context.GrappleLayer, ref result, 3).transform;
            AddSmallEdgeOutcrop(ledge, context, ref result);
        }
    }

    private static void BuildBrokenPlatformCluster(Transform root, BuildContext context, ref Result result)
    {
        float size = SafeSize(context);
        int count = RandomRange(context.Rng, 4, 9);
        for (int i = 0; i < count; i++)
        {
            Vector3 position = new Vector3(RandomRange(context.Rng, -5.5f, 5.5f), RandomRange(context.Rng, -2.4f, 2.2f), RandomRange(context.Rng, -5.5f, 5.5f)) * size;
            Quaternion rotation = Quaternion.Euler(RandomRange(context.Rng, -7f, 7f), RandomRange(context.Rng, 0f, 360f), RandomRange(context.Rng, -10f, 10f));
            Vector3 scale = new Vector3(RandomRange(context.Rng, 2.2f, 6.2f), RandomRange(context.Rng, 0.4f, 0.9f), RandomRange(context.Rng, 1.8f, 5.4f)) * size;
            Transform slab = CreateCube(root, $"Offset Broken Slab {i:00}", position, rotation, scale, context.PrimaryMaterial, context.GrappleLayer, ref result, 3).transform;
            if (Next01(context.Rng) < 0.75f)
            {
                AddSmallEdgeOutcrop(slab, context, ref result);
            }
        }
    }

    private static void BuildFloatingSteps(Transform root, BuildContext context, ref Result result)
    {
        float size = SafeSize(context);
        int count = RandomRange(context.Rng, 7, 14);
        float radius = RandomRange(context.Rng, 3.0f, 7.0f) * size;
        float angleStep = RandomRange(context.Rng, 22f, 45f) * Mathf.Deg2Rad;
        float yStep = RandomRange(context.Rng, 0.7f, 1.45f) * size;
        float startAngle = RandomRange(context.Rng, 0f, Mathf.PI * 2f);

        for (int i = 0; i < count; i++)
        {
            float angle = startAngle + angleStep * i;
            Vector3 radial = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vector3 position = radial * radius + Vector3.down * (i * yStep);
            Quaternion rotation = Quaternion.LookRotation(radial, Vector3.up);
            Vector3 scale = new Vector3(RandomRange(context.Rng, 1.5f, 2.8f), RandomRange(context.Rng, 0.3f, 0.55f), RandomRange(context.Rng, 2.1f, 4.0f)) * size;
            CreateCube(root, $"Spiral Broken Step {i:00}", position, rotation, scale, context.PrimaryMaterial, context.GrappleLayer, ref result, 2);
        }
    }

    private static void BuildPillarGroup(Transform root, BuildContext context, ref Result result)
    {
        float size = SafeSize(context);
        int count = RandomRange(context.Rng, 4, 10);
        for (int i = 0; i < count; i++)
        {
            Vector2 offset = RandomUnitCircle(context.Rng) * RandomRange(context.Rng, 1.0f, 6.2f) * size;
            float height = RandomRange(context.Rng, 3.5f, 10f) * size;
            Vector3 position = new(offset.x, -height * 0.5f + RandomRange(context.Rng, -1.0f, 1.0f) * size, offset.y);
            Quaternion rotation = Quaternion.Euler(RandomRange(context.Rng, -9f, 9f), RandomRange(context.Rng, 0f, 360f), RandomRange(context.Rng, -9f, 9f));
            Vector3 scale = new(RandomRange(context.Rng, 0.55f, 1.3f) * size, height, RandomRange(context.Rng, 0.55f, 1.3f) * size);
            Transform pillar = CreateCube(root, $"Fractured Pillar {i:00}", position, rotation, scale, context.PrimaryMaterial, context.GrappleLayer, ref result, 3).transform;
            AddPillarCapAndLedges(pillar, context, ref result, size);
        }
    }

    private static void BuildAncientBridgeFragment(Transform root, BuildContext context, ref Result result)
    {
        float size = SafeSize(context);
        int deckPieces = RandomRange(context.Rng, 2, 5);
        float cursor = -deckPieces * 1.8f * size;
        for (int i = 0; i < deckPieces; i++)
        {
            float length = RandomRange(context.Rng, 3.2f, 6.5f) * size;
            Vector3 position = new(cursor + length * 0.5f, RandomRange(context.Rng, -0.3f, 0.3f) * size, RandomRange(context.Rng, -0.5f, 0.5f) * size);
            Quaternion rotation = Quaternion.Euler(RandomRange(context.Rng, -4f, 4f), RandomRange(context.Rng, -5f, 5f), RandomRange(context.Rng, -8f, 8f));
            Vector3 scale = new(length, RandomRange(context.Rng, 0.5f, 0.95f) * size, RandomRange(context.Rng, 2.4f, 4.5f) * size);
            Transform deck = CreateCube(root, $"Bridge Deck Fragment {i:00}", position, rotation, scale, context.PrimaryMaterial, context.GrappleLayer, ref result, 3).transform;
            AddSmallEdgeOutcrop(deck, context, ref result);
            if (Next01(context.Rng) < 0.7f)
            {
                Vector3 supportScale = new(RandomRange(context.Rng, 0.5f, 1f) * size, RandomRange(context.Rng, 2f, 6f) * size, RandomRange(context.Rng, 0.5f, 1f) * size);
                CreateCube(deck, "Broken Bridge Support", new Vector3(RandomRange(context.Rng, -0.35f, 0.35f), -0.65f, RandomRange(context.Rng, -0.35f, 0.35f)), Quaternion.Euler(RandomRange(context.Rng, -8f, 8f), 0f, RandomRange(context.Rng, -8f, 8f)), supportScale, context.AccentMaterial, context.GrappleLayer, ref result, 2);
            }

            cursor += length + RandomRange(context.Rng, 0.6f, 1.8f) * size;
        }
    }

    private static void BuildRingSegment(Transform root, BuildContext context, ref Result result)
    {
        float size = SafeSize(context);
        float radius = RandomRange(context.Rng, 5f, 10f) * size;
        int pieces = RandomRange(context.Rng, 4, 8);
        float arcStart = RandomRange(context.Rng, 0f, Mathf.PI * 2f);
        float arcStep = RandomRange(context.Rng, 12f, 22f) * Mathf.Deg2Rad;
        for (int i = 0; i < pieces; i++)
        {
            float angle = arcStart + arcStep * i;
            Vector3 radial = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            CreateCube(root, $"Broken Ring Block {i:00}", radial * radius + Vector3.up * RandomRange(context.Rng, -0.5f, 0.5f) * size, Quaternion.LookRotation(radial, Vector3.up), new Vector3(RandomRange(context.Rng, 2.2f, 4.0f), RandomRange(context.Rng, 0.45f, 0.8f), RandomRange(context.Rng, 1.2f, 2.4f)) * size, context.PrimaryMaterial, context.GrappleLayer, ref result, 3);
        }
    }

    private static void BuildVerticalWallWithLedges(Transform root, BuildContext context, ref Result result)
    {
        float size = SafeSize(context);
        Transform wall = CreateCube(root, "Huge Broken Vertical Wall", Vector3.zero, Quaternion.Euler(RandomRange(context.Rng, -4f, 4f), 0f, RandomRange(context.Rng, -5f, 5f)), new Vector3(RandomRange(context.Rng, 5f, 10f), RandomRange(context.Rng, 8f, 16f), RandomRange(context.Rng, 0.7f, 1.2f)) * size, context.DarkMaterial != null ? context.DarkMaterial : context.PrimaryMaterial, context.GrappleLayer, ref result, 4).transform;
        int ledges = RandomRange(context.Rng, 4, 9);
        for (int i = 0; i < ledges; i++)
        {
            float side = Next01(context.Rng) < 0.5f ? -1f : 1f;
            Vector3 pos = new(RandomRange(context.Rng, -0.45f, 0.45f), RandomRange(context.Rng, -0.45f, 0.45f), side * 0.65f);
            Vector3 scale = new(RandomRange(context.Rng, 0.25f, 0.75f), RandomRange(context.Rng, 0.04f, 0.08f), RandomRange(context.Rng, 0.35f, 0.75f));
            CreateCube(wall, $"Wall Carved Ledge {i:00}", pos, Quaternion.identity, scale, context.AccentMaterial, context.GrappleLayer, ref result, 2);
        }
    }

    private static void BuildSpiralLedgeFragment(Transform root, BuildContext context, ref Result result)
    {
        float size = SafeSize(context);
        int count = RandomRange(context.Rng, 5, 10);
        float radius = RandomRange(context.Rng, 5f, 9f) * size;
        float startAngle = RandomRange(context.Rng, 0f, Mathf.PI * 2f);
        for (int i = 0; i < count; i++)
        {
            float angle = startAngle + i * RandomRange(context.Rng, 14f, 24f) * Mathf.Deg2Rad;
            Vector3 radial = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            CreateCube(root, $"Spiral Ledge Shard {i:00}", radial * radius + Vector3.down * i * RandomRange(context.Rng, 0.45f, 0.85f) * size, Quaternion.LookRotation(-radial, Vector3.up), new Vector3(RandomRange(context.Rng, 2.0f, 4.8f), RandomRange(context.Rng, 0.35f, 0.65f), RandomRange(context.Rng, 1.0f, 2.2f)) * size, context.AccentMaterial, context.GrappleLayer, ref result, 3);
        }
    }

    private static void BuildMassiveFallenSlab(Transform root, BuildContext context, ref Result result)
    {
        float size = SafeSize(context) * RandomRange(context.Rng, 1.2f, 1.9f);
        Transform slab = CreateCube(root, "Massive Fallen Slab", Vector3.zero, Quaternion.Euler(RandomRange(context.Rng, -18f, 18f), RandomRange(context.Rng, 0f, 360f), RandomRange(context.Rng, -22f, 22f)), new Vector3(RandomRange(context.Rng, 7f, 14f), RandomRange(context.Rng, 0.7f, 1.4f), RandomRange(context.Rng, 4f, 9f)) * size, context.PrimaryMaterial, context.GrappleLayer, ref result, 5).transform;
        for (int i = 0; i < 4; i++)
        {
            AddSmallEdgeOutcrop(slab, context, ref result);
        }

        result.IsHuge = true;
    }

    private static void BuildSmallLonelyPlatform(Transform root, BuildContext context, ref Result result)
    {
        float size = SafeSize(context);
        Transform platform = CreateCube(root, "Small Lonely Platform", Vector3.zero, Quaternion.Euler(0f, RandomRange(context.Rng, 0f, 360f), RandomRange(context.Rng, -6f, 6f)), new Vector3(RandomRange(context.Rng, 1.8f, 3.4f), RandomRange(context.Rng, 0.45f, 0.8f), RandomRange(context.Rng, 1.8f, 3.4f)) * size, context.PrimaryMaterial, context.GrappleLayer, ref result, 3).transform;
        AddSmallEdgeOutcrop(platform, context, ref result);
    }

    private static void BuildNestedCylinderStructure(Transform root, BuildContext context, ref Result result)
    {
        BuildHollowCylinderRuin(root, context, ref result);
        BuildRingSegment(root, context, ref result);
        for (int i = 0; i < 3; i++)
        {
            Transform child = CreateCube(root, $"Nested Inner Beam {i:00}", new Vector3(RandomRange(context.Rng, -3f, 3f), RandomRange(context.Rng, -2f, 2f), RandomRange(context.Rng, -3f, 3f)) * SafeSize(context), Quaternion.Euler(RandomRange(context.Rng, -15f, 15f), RandomRange(context.Rng, 0f, 360f), RandomRange(context.Rng, -15f, 15f)), new Vector3(RandomRange(context.Rng, 3f, 6f), RandomRange(context.Rng, 0.35f, 0.7f), RandomRange(context.Rng, 0.5f, 1.1f)) * SafeSize(context), context.AccentMaterial, context.GrappleLayer, ref result, 3).transform;
            AddSmallEdgeOutcrop(child, context, ref result);
        }
    }

    private static void BuildBrokenColumnArc(Transform root, BuildContext context, ref Result result)
    {
        float size = SafeSize(context);
        int count = RandomRange(context.Rng, 5, 9);
        float radius = RandomRange(context.Rng, 4f, 8f) * size;
        float startAngle = RandomRange(context.Rng, 0f, Mathf.PI * 2f);
        for (int i = 0; i < count; i++)
        {
            float angle = startAngle + i * RandomRange(context.Rng, 14f, 24f) * Mathf.Deg2Rad;
            Vector3 radial = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            float height = RandomRange(context.Rng, 2.5f, 8f) * size;
            Transform pillar = CreateCube(root, $"Arc Broken Column {i:00}", radial * radius + Vector3.down * height * 0.5f, Quaternion.Euler(RandomRange(context.Rng, -10f, 10f), RandomRange(context.Rng, 0f, 360f), RandomRange(context.Rng, -10f, 10f)), new Vector3(RandomRange(context.Rng, 0.55f, 1.1f), height, RandomRange(context.Rng, 0.55f, 1.1f)) * size, context.PrimaryMaterial, context.GrappleLayer, ref result, 3).transform;
            AddPillarCapAndLedges(pillar, context, ref result, size);
        }
    }

    private static void BuildIrregularGrappleOutcrop(Transform root, BuildContext context, ref Result result)
    {
        float size = SafeSize(context);
        int count = RandomRange(context.Rng, 4, 8);
        for (int i = 0; i < count; i++)
        {
            Transform chunk = CreateCube(root, $"Irregular Edge Block {i:00}", new Vector3(RandomRange(context.Rng, -2.5f, 2.5f), RandomRange(context.Rng, -2f, 2f), RandomRange(context.Rng, -2.5f, 2.5f)) * size, Quaternion.Euler(RandomRange(context.Rng, -20f, 20f), RandomRange(context.Rng, 0f, 360f), RandomRange(context.Rng, -20f, 20f)), new Vector3(RandomRange(context.Rng, 0.8f, 2.5f), RandomRange(context.Rng, 0.5f, 1.4f), RandomRange(context.Rng, 0.8f, 2.6f)) * size, i % 2 == 0 ? context.PrimaryMaterial : context.AccentMaterial, context.GrappleLayer, ref result, 2).transform;
            if (Next01(context.Rng) < 0.65f)
            {
                AddSmallEdgeOutcrop(chunk, context, ref result);
            }
        }
    }

    private static void AddPillarCapAndLedges(Transform pillar, BuildContext context, ref Result result, float size)
    {
        if (Next01(context.Rng) < 0.8f)
        {
            CreateCube(pillar, "Broken Pillar Cap", Vector3.up * 0.53f, Quaternion.identity, new Vector3(RandomRange(context.Rng, 1.3f, 2.2f), RandomRange(context.Rng, 0.18f, 0.35f), RandomRange(context.Rng, 1.3f, 2.2f)) * size, context.AccentMaterial, context.GrappleLayer, ref result, 2);
        }

        int ledges = RandomRange(context.Rng, 1, 4);
        for (int i = 0; i < ledges; i++)
        {
            AddSmallEdgeOutcrop(pillar, context, ref result);
        }
    }

    private static void AddSmallEdgeOutcrop(Transform parent, BuildContext context, ref Result result)
    {
        float sign = Next01(context.Rng) < 0.5f ? -1f : 1f;
        bool alongX = Next01(context.Rng) < 0.5f;
        Vector3 position = alongX
            ? new Vector3(sign * 0.58f, RandomRange(context.Rng, -0.2f, 0.45f), RandomRange(context.Rng, -0.35f, 0.35f))
            : new Vector3(RandomRange(context.Rng, -0.35f, 0.35f), RandomRange(context.Rng, -0.2f, 0.45f), sign * 0.58f);
        Vector3 scale = alongX
            ? new Vector3(0.42f, RandomRange(context.Rng, 0.18f, 0.34f), RandomRange(context.Rng, 0.65f, 1.35f))
            : new Vector3(RandomRange(context.Rng, 0.65f, 1.35f), RandomRange(context.Rng, 0.18f, 0.34f), 0.42f);
        CreateCube(parent, "Grapple Edge Outcrop", position, Quaternion.Euler(0f, RandomRange(context.Rng, -12f, 12f), 0f), scale, context.AccentMaterial, context.GrappleLayer, ref result, 2);
    }

    private static GameObject CreateCube(
        Transform parent,
        string objectName,
        Vector3 localPosition,
        Quaternion localRotation,
        Vector3 localScale,
        Material material,
        int layer,
        ref Result result,
        int grappleScore)
    {
        GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        gameObject.name = objectName;
        gameObject.transform.SetParent(parent, false);

        ApplyNoiseVariation(activeContext, result.PieceCount, ref localPosition, ref localRotation, ref localScale);
        gameObject.transform.localPosition = localPosition;
        gameObject.transform.localRotation = localRotation;
        gameObject.transform.localScale = localScale;
        SetLayerRecursively(gameObject, layer);

        MeshRenderer renderer = gameObject.GetComponent<MeshRenderer>();
        if (renderer != null && material != null)
        {
            renderer.sharedMaterial = material;
        }

        result.PieceCount++;
        result.GrappleScore += Mathf.Max(1, grappleScore);
        return gameObject;
    }

    private static void ApplyNoiseVariation(BuildContext context, int pieceIndex, ref Vector3 localPosition, ref Quaternion localRotation, ref Vector3 localScale)
    {
        float variation = Mathf.Clamp01(context.ShapeVariationAmount);
        if (variation <= 0f)
        {
            return;
        }

        float noiseScale = Mathf.Max(0.001f, context.NoiseScale);
        int octaves = Mathf.Clamp(context.NoiseOctaves, 1, 5);
        Vector3 sample = localPosition * noiseScale + new Vector3(pieceIndex * 1.37f, pieceIndex * 2.11f, pieceIndex * 0.73f);
        float n1 = FractalNoise(sample.x, sample.z, octaves);
        float n2 = FractalNoise(sample.z + 17.3f, sample.y + 3.1f, octaves);
        float n3 = FractalNoise(sample.y + 9.7f, sample.x + 21.5f, octaves);

        float warp = context.DomainWarpStrength * variation * SafeSize(context);
        localPosition += new Vector3(n1 - 0.5f, n2 - 0.5f, n3 - 0.5f) * warp;

        float amplitude = Mathf.Max(0f, context.NoiseAmplitude) * variation;
        float asymmetry = Mathf.Max(0f, context.AsymmetryAmount) * variation;
        localScale = new Vector3(
            Mathf.Max(0.05f, localScale.x * (1f + (n1 - 0.5f) * amplitude + (n2 - 0.5f) * asymmetry)),
            Mathf.Max(0.05f, localScale.y * (1f + (n2 - 0.5f) * amplitude * 0.6f)),
            Mathf.Max(0.05f, localScale.z * (1f + (n3 - 0.5f) * amplitude + (n1 - 0.5f) * asymmetry)));

        float breakage = Mathf.Max(0f, context.BreakageAmount) * variation;
        if (breakage > 0f)
        {
            localRotation *= Quaternion.Euler((n2 - 0.5f) * breakage * 8f, (n1 - 0.5f) * breakage * 10f, (n3 - 0.5f) * breakage * 8f);
        }
    }

    private static float FractalNoise(float x, float y, int octaves)
    {
        float total = 0f;
        float amplitude = 1f;
        float frequency = 1f;
        float amplitudeSum = 0f;
        for (int i = 0; i < octaves; i++)
        {
            total += Mathf.PerlinNoise(x * frequency, y * frequency) * amplitude;
            amplitudeSum += amplitude;
            amplitude *= 0.5f;
            frequency *= 2f;
        }

        return amplitudeSum > 0f ? total / amplitudeSum : 0.5f;
    }

    private static Bounds CalculateBounds(GameObject root)
    {
        Physics.SyncTransforms();
        Collider[] colliders = root.GetComponentsInChildren<Collider>();
        if (colliders.Length == 0)
        {
            return new Bounds(root.transform.position, Vector3.zero);
        }

        Bounds bounds = colliders[0].bounds;
        for (int i = 1; i < colliders.Length; i++)
        {
            bounds.Encapsulate(colliders[i].bounds);
        }

        return bounds;
    }

    private static bool IsWithinWrappedRange(int value, int start, int count, int modulo)
    {
        for (int i = 0; i < count; i++)
        {
            if ((start + i) % modulo == value)
            {
                return true;
            }
        }

        return false;
    }

    private static float SafeSize(BuildContext context)
    {
        return Mathf.Max(0.1f, context.SizeMultiplier);
    }

    private static void SetLayerRecursively(GameObject gameObject, int layer)
    {
        gameObject.layer = layer;
        for (int i = 0; i < gameObject.transform.childCount; i++)
        {
            SetLayerRecursively(gameObject.transform.GetChild(i).gameObject, layer);
        }
    }

    private static float Next01(System.Random rng)
    {
        return (float)rng.NextDouble();
    }

    private static float RandomRange(System.Random rng, float min, float max)
    {
        return Mathf.Lerp(min, max, Next01(rng));
    }

    private static int RandomRange(System.Random rng, int minInclusive, int maxExclusive)
    {
        return rng.Next(minInclusive, Mathf.Max(minInclusive + 1, maxExclusive));
    }

    private static Vector2 RandomUnitCircle(System.Random rng)
    {
        float angle = RandomRange(rng, 0f, Mathf.PI * 2f);
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    }
}
