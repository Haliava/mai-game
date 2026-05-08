using UnityEngine;

[CreateAssetMenu(menuName = "MAI Game/Megastructure Generation Settings", fileName = "MegastructureGenerationSettings")]
public sealed class MegastructureGenerationSettings : ScriptableObject
{
    [Header("Size")]
    public float levelHeight = 150f;
    public float shaftRadius = 28f;
    public float chunkHeight = 10f;

    [Header("Density")]
    [Range(0f, 0.95f)] public float emptySpaceRatio = 0.62f;
    [Range(0f, 1f)] public float globalStructureDensity = 0.45f;
    public int maxStructuresPerChunk = 3;
    [Range(0f, 1f)] public float emptyChunkChance = 0.35f;
    public AnimationCurve densityByDepth = AnimationCurve.EaseInOut(0f, 0.85f, 1f, 1.1f);
    public AnimationCurve scaleByDepth = AnimationCurve.EaseInOut(0f, 0.85f, 1f, 1.25f);

    [Header("Scale")]
    public Vector2 smallPlatformSizeRange = new(1.8f, 3.2f);
    public Vector2 mediumPlatformSizeRange = new(3.5f, 6.5f);
    public Vector2 hugeStructureSizeRange = new(1.85f, 3.2f);
    [Range(0f, 1f)] public float hugeStructureChance = 0.18f;
    [Range(0f, 1f)] public float tinyPlatformChance = 0.22f;
    [Range(0f, 1f)] public float hollowCylinderChance = 0.18f;

    [Header("Void")]
    public float minStructureDistance = 4f;
    public float minDistanceBetweenMajorStructures = 9f;
    public bool createBottomFloor = true;
    public float bottomFloorThickness = 1.2f;
    public float bottomFloorWallInset = 0.25f;
    public float minDistanceFromShaftWall = 2.5f;
    [Range(0f, 0.85f)] public float centralVoidBias = 0.35f;

    [Header("Final Arena")]
    public float finalArenaRadius = 10f;
    public float pedestalRadius = 2.2f;
    public float pedestalHeight = 1.3f;
    public int finalColumnCount = 8;
    public float finalColumnRadius = 0.55f;
    public float finalColumnHeight = 6f;
    public float victorySphereRadius = 0.85f;
    public Color victorySphereColor = new(0.45f, 0.85f, 1f, 1f);

    [Header("Materials")]
    public Material shaftWallMaterial;
    public Material platformMaterial;
    public Material ledgeMaterial;
    public Material criticalPathMaterial;
    public Material finalArenaMaterial;
    public Material victorySphereMaterial;
}
