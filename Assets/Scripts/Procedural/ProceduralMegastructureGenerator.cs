using System.Collections.Generic;
using UnityEngine;

public sealed class ProceduralMegastructureGenerator : MonoBehaviour
{
    private const string GeneratedRootName = "GeneratedLevel";
    private const string GrappleSurfaceLayerName = "GrappleSurface";

    private enum AnchorType
    {
        ShaftWall,
        Structure,
        Branch,
        Interior
    }

    private struct StructureAnchor
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Transform ParentStructure;
        public AnchorType AnchorType;
        public float SupportedScale;
        public int Depth;
        public bool CanSpawnMajorStructure;
        public bool CanSpawnMinorStructure;
    }

    private sealed class GeneratedStructureRecord
    {
        public GameObject Root;
        public Transform ParentStructure;
        public StructureAnchor Anchor;
        public Bounds Bounds;
        public GeneratedStructureLibrary.StructureKind Kind;
        public bool IsMajor;
        public bool IsOrphan;
    }

    [Header("Optional Settings Asset")]
    [SerializeField] private MegastructureGenerationSettings settings;

    [Header("Generation")]
    [SerializeField] private int seed = 1337;
    [SerializeField] private bool randomizeSeed;
    [SerializeField, Min(10f)] private float levelHeight = 150f;
    [SerializeField, Min(5f)] private float shaftRadius = 52f;
    [SerializeField, Min(4f)] private float chunkHeight = 10f;
    [SerializeField, Range(0f, 0.95f)] private float emptySpaceRatio = 0.62f;
    [SerializeField, Range(0f, 1f)] private float globalStructureDensity = 0.45f;
    [SerializeField, Min(0f)] private float minStructureDistance = 4f;
    [SerializeField, Min(0f)] private float maxStructureDistance = 13f;
    [SerializeField, Min(0f)] private float startSafeRadius = 7f;
    [SerializeField, Min(0f)] private float endArenaRadius = 12f;
    [SerializeField] private bool generateOnStart;
    [SerializeField] private bool generateInEditor = true;
    [SerializeField] private bool clearBeforeGenerate = true;

    [Header("Shaft")]
    [SerializeField, Range(12, 96)] private int shaftWallSegments = 20;
    [SerializeField, Min(0.1f)] private float shaftWallThickness = 1.2f;
    [SerializeField] private bool createBottomFloor = true;
    [SerializeField, Min(0.1f)] private float bottomFloorThickness = 1.2f;
    [SerializeField, Min(0f)] private float bottomFloorWallInset = 0.25f;
    [SerializeField, Range(0f, 1f)] private float wallLedgeChance = 0.38f;
    [SerializeField, Min(0f)] private float minDistanceFromShaftWall = 2.5f;
    [SerializeField, Range(0f, 0.85f)] private float centralVoidBias = 0.35f;

    [Header("Chunks")]
    [SerializeField, Min(0)] private int maxStructuresPerChunk = 3;
    [SerializeField, Range(0f, 1f)] private float emptyChunkChance = 0.35f;
    [SerializeField, Range(0f, 1f)] private float tinyPlatformChance = 0.22f;
    [SerializeField] private Vector2 smallPlatformSizeRange = new(1.8f, 3.2f);
    [SerializeField] private Vector2 mediumPlatformSizeRange = new(3.5f, 6.5f);
    [SerializeField] private Vector2 hugeStructureSizeRange = new(1.85f, 3.2f);
    [SerializeField, Range(0f, 1f)] private float hugeStructureChance = 0.18f;
    [SerializeField, Range(0f, 1f)] private float hollowCylinderChance = 0.18f;
    [SerializeField] private AnimationCurve densityByDepth = AnimationCurve.EaseInOut(0f, 0.85f, 1f, 1.1f);
    [SerializeField] private AnimationCurve scaleByDepth = AnimationCurve.EaseInOut(0f, 0.85f, 1f, 1.25f);

    [Header("Spacing")]
    [SerializeField, Min(0f)] private float minDistanceBetweenMajorStructures = 9f;

    [Header("Procedural Structures")]
    [SerializeField, Range(0f, 1f)] private float proceduralStructureChance = 0.78f;
    [SerializeField] private Vector2 complexStructureScaleRange = new(0.75f, 1.7f);

    [Header("Anchor / Connectivity")]
    [SerializeField] private bool requireStructuralAnchoring = true;
    [SerializeField] private bool allowIsolatedFloatingPlatforms;
    [SerializeField, Min(1)] private int maxChainDepth = 4;
    [SerializeField, Min(0)] private int maxBranchesPerMajorStructure = 3;
    [SerializeField, Min(0.5f)] private float anchorSearchRadius = 12f;
    [SerializeField, Min(0f)] private float minAttachmentOverlap = 0.35f;
    [SerializeField, Min(1f)] private float parentStructureInfluenceRadius = 16f;

    [Header("Shape Variation")]
    [SerializeField, Range(0f, 1f)] private float shapeVariationAmount = 0.55f;
    [SerializeField, Min(0.001f)] private float noiseScale = 0.12f;
    [SerializeField, Min(0f)] private float noiseAmplitude = 0.35f;
    [SerializeField, Min(0f)] private float domainWarpStrength = 0.65f;
    [SerializeField, Range(1, 5)] private int noiseOctaves = 3;
    [SerializeField, Range(0f, 1f)] private float artifactFormChance = 0.42f;
    [SerializeField, Range(0f, 1f)] private float irregularityChance = 0.65f;
    [SerializeField, Range(0f, 1f)] private float asymmetryAmount = 0.45f;
    [SerializeField, Range(0f, 1f)] private float breakageAmount = 0.5f;
    [SerializeField] private Vector2 structureLengthRange = new(3.2f, 12f);
    [SerializeField] private Vector2 structureWidthRange = new(1.5f, 6f);
    [SerializeField] private Vector2 structureHeightRange = new(0.45f, 4.5f);

    [Header("Anchor-Based Generation")]
    [SerializeField, Min(0f)] private float wallAnchorDensity = 0.8f;
    [SerializeField, Min(0f)] private float structureAnchorDensity = 0.7f;
    [SerializeField, Range(0f, 1f)] private float largeStructureChance = 0.28f;
    [SerializeField, Range(0f, 1f)] private float secondaryAttachmentChance = 0.72f;
    [SerializeField, Range(0f, 1f)] private float bridgeFragmentChance = 0.28f;
    [SerializeField, Range(0f, 1f)] private float hollowStructureChance = 0.26f;
    [SerializeField, Range(0f, 1f)] private float nestedStructureChance = 0.16f;

    [Header("Connectivity Validation")]
    [SerializeField] private bool validateAnchoring = true;
    [SerializeField] private bool removeOrphanStructures = true;
    [SerializeField] private bool validateShapeVariety = true;
    [SerializeField] private bool validateAnchoredGrappleability = true;

    [Header("Critical Descent")]
    [SerializeField] private bool generateCriticalPath = true;
    [SerializeField] private bool validateReachability = true;
    [SerializeField, Min(1f)] private float assumedMaxGrappleDistance = 30f;
    [SerializeField, Min(1f)] private float maxSafeVerticalDrop = 16f;
    [SerializeField, Min(1f)] private float maxHorizontalGap = 13f;

    [Header("Grapple Validation")]
    [SerializeField] private bool validateGrappleOpportunities = true;
    [SerializeField, Min(1f)] private float grappleScanStepY = 10f;
    [SerializeField, Min(1f)] private float grappleScanRadius = 25f;
    [SerializeField, Min(1)] private int minGrappleEdgesPerChunk = 6;
    [SerializeField, Min(1)] private int targetGrappleEdgesPerChunk = 12;
    [SerializeField, Min(1f)] private float maxVerticalGapWithoutGrappleTarget = 18f;
    [SerializeField] private bool addEmergencyGrappleLedges = true;

    [Header("Final Arena")]
    [SerializeField, Min(2f)] private float finalArenaRadius = 10f;
    [SerializeField, Min(0.5f)] private float pedestalRadius = 2.2f;
    [SerializeField, Min(0.2f)] private float pedestalHeight = 1.3f;
    [SerializeField, Min(1)] private int finalColumnCount = 8;
    [SerializeField, Min(0.1f)] private float finalColumnRadius = 0.55f;
    [SerializeField, Min(0.5f)] private float finalColumnHeight = 6f;
    [SerializeField, Min(0.1f)] private float victorySphereRadius = 0.85f;
    [SerializeField] private Color victorySphereColor = new(0.45f, 0.85f, 1f, 1f);
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Header("Layers")]
    [SerializeField] private string grappleSurfaceLayerName = GrappleSurfaceLayerName;
    [SerializeField] private string boundaryLayerName = "Default";

    [Header("Materials")]
    [SerializeField] private Material shaftWallMaterial;
    [SerializeField] private Material platformMaterial;
    [SerializeField] private Material ledgeMaterial;
    [SerializeField] private Material criticalPathMaterial;
    [SerializeField] private Material darkRuinMaterial;
    [SerializeField] private Material finalArenaMaterial;
    [SerializeField] private Material victorySphereMaterial;

    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = true;

    private readonly List<Vector3> criticalDescentPoints = new();
    private readonly List<Vector3> grappleTargetPoints = new();
    private readonly List<Bounds> generatedStructureBounds = new();
    private readonly List<Bounds> majorStructureBounds = new();
    private readonly List<int> chunkGrappleCounts = new();
    private readonly List<StructureAnchor> structureAnchors = new();
    private readonly List<StructureAnchor> wallAnchors = new();
    private readonly List<GeneratedStructureRecord> structureRecords = new();
    private int generatedStructureCount;
    private int generatedComplexStructureCount;
    private int generatedHugeStructureCount;
    private int generatedSmallPlatformCount;
    private int generatedCriticalCount;
    private int generatedWallLedgeCount;
    private int generatedEmergencyLedgeCount;
    private int generatedWallAttachedChainCount;
    private int generatedStructureAttachedChainCount;
    private int generatedSecondaryAttachmentCount;
    private int removedOrphanStructureCount;
    private int emptyChunkCount;
    private int lastChunkCount;
    private Vector3 lastFinalArenaPosition;
    private Transform lastEmergencyRoot;

    private void Start()
    {
        ApplySettingsAsset();
        if (generateOnStart)
        {
            GenerateLevel();
        }
    }

    [ContextMenu("Generate Level")]
    public void GenerateLevel()
    {
        ApplySettingsAsset();
        if (!Application.isPlaying && !generateInEditor)
        {
            Debug.LogWarning("ProceduralMegastructureGenerator: generateInEditor is disabled.", this);
            return;
        }

        if (randomizeSeed)
        {
            RandomizeSeed();
        }

        if (clearBeforeGenerate)
        {
            ClearGeneratedLevel();
        }

        ResetStats();
        EnsureFallbackMaterials();

        System.Random rng = new(seed);
        int grappleLayer = ResolveLayer(grappleSurfaceLayerName, LayerMask.NameToLayer(GrappleSurfaceLayerName));
        int boundaryLayer = ResolveLayer(boundaryLayerName, 0);

        GameObject root = new(GeneratedRootName);
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        Transform shaftRoot = CreateChildRoot(root.transform, "Shaft Boundary");
        Transform chunkRoot = CreateChildRoot(root.transform, "Sparse Ruin Chunks");
        Transform criticalRoot = CreateChildRoot(root.transform, "Critical Descent Supports");
        lastEmergencyRoot = CreateChildRoot(root.transform, "Emergency Grapple Ledges");
        Transform finalRoot = CreateChildRoot(root.transform, "Final Arena");

        CreateShaftBoundary(shaftRoot, boundaryLayer, rng);
        GenerateChunks(chunkRoot, criticalRoot, rng, grappleLayer);
        ValidateAndPatchGrappleOpportunities(lastEmergencyRoot, rng, grappleLayer);
        CreateFinalArena(finalRoot, grappleLayer);

        ValidateLevel();
        Debug.Log(BuildSummary("Generated"), this);
    }

    
    
    
    
    
    public LevelInstanceRoot GenerateLevelInstance(int levelIndex, float baseY, int seed, Transform parent)
    {
        ApplySettingsAsset();

        
        System.Random rng = new(seed);
        ResetStats();
        EnsureFallbackMaterials();

        int grappleLayer = ResolveLayer(grappleSurfaceLayerName, LayerMask.NameToLayer(GrappleSurfaceLayerName));
        int boundaryLayer = ResolveLayer(boundaryLayerName, 0);

        string rootName = $"Level_{levelIndex}";
        GameObject root = new(rootName);
        
        if (parent != null) root.transform.SetParent(parent, true);
        root.transform.position = new Vector3(0f, baseY, 0f);

        Transform shaftRoot = CreateChildRoot(root.transform, "Shaft Boundary");
        Transform chunkRoot = CreateChildRoot(root.transform, "Sparse Ruin Chunks");
        Transform criticalRoot = CreateChildRoot(root.transform, "Critical Descent Supports");
        lastEmergencyRoot = CreateChildRoot(root.transform, "Emergency Grapple Ledges");
        Transform finalRoot = CreateChildRoot(root.transform, "Final Arena");

        CreateShaftBoundary(shaftRoot, boundaryLayer, rng);
        GenerateChunks(chunkRoot, criticalRoot, rng, grappleLayer);
        ValidateAndPatchGrappleOpportunities(lastEmergencyRoot, rng, grappleLayer);
        CreateFinalArena(finalRoot, grappleLayer);

        ValidateLevel();
        Debug.Log(BuildSummary("GeneratedInstance"), this);

        
        Bounds totalBounds = new Bounds(root.transform.position, Vector3.zero);
        bool hasBounds = false;
        var rends = root.GetComponentsInChildren<Renderer>(true);
        if (rends != null && rends.Length > 0)
        {
            foreach (var r in rends)
            {
                if (r == null) continue;
                if (!hasBounds)
                {
                    totalBounds = r.bounds;
                    hasBounds = true;
                }
                else totalBounds.Encapsulate(r.bounds);
            }
        }
        if (!hasBounds)
        {
            var cols = root.GetComponentsInChildren<Collider>(true);
            if (cols != null && cols.Length > 0)
            {
                foreach (var c in cols)
                {
                    if (c == null) continue;
                    if (!hasBounds)
                    {
                        totalBounds = c.bounds;
                        hasBounds = true;
                    }
                    else totalBounds.Encapsulate(c.bounds);
                }
            }
        }

        var levelComp = root.AddComponent<LevelInstanceRoot>();
        levelComp.LevelIndex = levelIndex;
        levelComp.BaseY = baseY;
        if (hasBounds)
        {
            levelComp.LevelBounds = totalBounds;
            levelComp.TopY = totalBounds.max.y;
            levelComp.GameplayBounds = totalBounds;
        }

        
        var anchorsComp = root.AddComponent<LevelTransitionAnchors>();

        
        Transform foundEntry = null;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t == null) continue;
            if (t.name.Equals("Level Entry Anchor", System.StringComparison.OrdinalIgnoreCase))
            {
                foundEntry = t; break;
            }
        }

        if (foundEntry == null)
        {
            Vector3 entryPos;
            if (hasBounds)
            {
                entryPos = new Vector3(totalBounds.center.x, totalBounds.max.y - 2f, totalBounds.center.z);
            }
            else
            {
                entryPos = root.transform.position + Vector3.up * Mathf.Max(10f, levelHeight * 0.6f);
            }
            GameObject ep = new GameObject("Level Entry Anchor");
            ep.transform.SetParent(root.transform, true);
            ep.transform.position = entryPos;
            anchorsComp.EntryAnchor = ep.transform;
            levelComp.EntryPoint = ep.transform;
            levelComp.EntryAnchor = ep.transform;
            Debug.Log($"ProceduralMegastructureGenerator: created EntryAnchor at {entryPos} for '{root.name}'", this);
        }
        else
        {
            anchorsComp.EntryAnchor = foundEntry;
            if (levelComp.EntryPoint == null) levelComp.EntryPoint = foundEntry;
            levelComp.EntryAnchor = foundEntry;
            Debug.Log($"ProceduralMegastructureGenerator: found existing EntryAnchor '{foundEntry.name}' for '{root.name}' at {foundEntry.position}", this);
        }

        
        var ds = root.GetComponentInChildren<DescentSphereTrigger>(true);
        if (ds != null)
        {
            anchorsComp.ExitAnchor = ds.transform;
            anchorsComp.FinalArenaRoot = finalRoot;
            levelComp.ExitAnchor = ds.transform;
            levelComp.FinalArenaRoot = finalRoot;
            Debug.Log($"ProceduralMegastructureGenerator: assigned ExitAnchor from DescentSphereTrigger '{ds.gameObject.name}' for '{root.name}'", this);
        }

        
        levelComp.CentipedeSpawnAnchors = new List<Transform>();
        int spawnIndex = 0;
        int maxAnchors = 24;
        for (int i = 0; i < wallAnchors.Count && spawnIndex < maxAnchors; i++)
        {
            var a = wallAnchors[i];
            GameObject g = new($"Centipede Spawn Anchor {spawnIndex:00}");
            g.transform.SetParent(root.transform, true);
            g.transform.position = a.Position;
            if (a.Normal.sqrMagnitude > 0.0001f) g.transform.up = a.Normal;
            levelComp.CentipedeSpawnAnchors.Add(g.transform);
            spawnIndex++;
        }
        for (int i = 0; i < structureAnchors.Count && spawnIndex < maxAnchors; i++)
        {
            var a = structureAnchors[i];
            GameObject g = new($"Centipede Spawn Anchor {spawnIndex:00}");
            g.transform.SetParent(root.transform, true);
            g.transform.position = a.Position;
            if (a.Normal.sqrMagnitude > 0.0001f) g.transform.up = a.Normal;
            levelComp.CentipedeSpawnAnchors.Add(g.transform);
            spawnIndex++;
        }

        return levelComp;
    }

    [ContextMenu("Clear Generated Level")]
    public void ClearGeneratedLevel()
    {
        GameObject existing = GameObject.Find(GeneratedRootName);
        if (existing == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(existing);
        }
        else
        {
            DestroyImmediate(existing);
        }

        ResetStats();
        Debug.Log("ProceduralMegastructureGenerator: cleared GeneratedLevel.", this);
    }

    [ContextMenu("Randomize Seed")]
    public void RandomizeSeed()
    {
        seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
    }

    [ContextMenu("Rebuild Final Arena")]
    public void RebuildFinalArena()
    {
        GameObject root = GameObject.Find(GeneratedRootName);
        if (root == null)
        {
            Debug.LogWarning("ProceduralMegastructureGenerator: generate level before rebuilding final arena.", this);
            return;
        }

        Transform existing = root.transform.Find("Final Arena");
        if (existing != null)
        {
            if (Application.isPlaying)
            {
                Destroy(existing.gameObject);
            }
            else
            {
                DestroyImmediate(existing.gameObject);
            }
        }

        Transform finalRoot = CreateChildRoot(root.transform, "Final Arena");
        CreateFinalArena(finalRoot, ResolveLayer(grappleSurfaceLayerName, LayerMask.NameToLayer(GrappleSurfaceLayerName)));
    }

    [ContextMenu("Validate Level")]
    public void ValidateLevel()
    {
        GameObject root = GameObject.Find(GeneratedRootName);
        if (root == null)
        {
            Debug.LogWarning("ProceduralMegastructureGenerator: no GeneratedLevel found to validate.", this);
            return;
        }

        int grappleLayer = ResolveLayer(grappleSurfaceLayerName, LayerMask.NameToLayer(GrappleSurfaceLayerName));
        Collider[] colliders = root.GetComponentsInChildren<Collider>();
        int grappleColliders = 0;
        int outsideShaft = 0;
        int triggerColliders = 0;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null)
            {
                continue;
            }

            if (collider.isTrigger)
            {
                triggerColliders++;
                continue;
            }

            if (collider.gameObject.layer == grappleLayer)
            {
                grappleColliders++;
            }

            Vector3 center = collider.bounds.center;
            float horizontalRadius = new Vector2(center.x, center.z).magnitude;
            if (horizontalRadius > shaftRadius + shaftWallThickness + 2f)
            {
                outsideShaft++;
            }
        }

        if (validateReachability)
        {
            ValidateCriticalPathReachability();
        }

        if (validateGrappleOpportunities)
        {
            ValidateGrappleSummaryOnly();
        }

        if (validateAnchoring)
        {
            ValidateStructuralAnchoring();
        }

        Debug.Log(
            $"ProceduralMegastructureGenerator validation: colliders={colliders.Length}, grappleColliders={grappleColliders}, triggers={triggerColliders}, outsideShaftWarnings={outsideShaft}, criticalPoints={criticalDescentPoints.Count}, grappleTargets={grappleTargetPoints.Count}",
            this);
    }

    private void ApplySettingsAsset()
    {
        if (settings == null)
        {
            return;
        }

        levelHeight = settings.levelHeight;
        shaftRadius = settings.shaftRadius;
        chunkHeight = settings.chunkHeight;
        emptySpaceRatio = settings.emptySpaceRatio;
        globalStructureDensity = settings.globalStructureDensity;
        maxStructuresPerChunk = settings.maxStructuresPerChunk;
        emptyChunkChance = settings.emptyChunkChance;
        densityByDepth = settings.densityByDepth;
        scaleByDepth = settings.scaleByDepth;
        smallPlatformSizeRange = settings.smallPlatformSizeRange;
        mediumPlatformSizeRange = settings.mediumPlatformSizeRange;
        hugeStructureSizeRange = settings.hugeStructureSizeRange;
        hugeStructureChance = settings.hugeStructureChance;
        tinyPlatformChance = settings.tinyPlatformChance;
        hollowCylinderChance = settings.hollowCylinderChance;
        minStructureDistance = settings.minStructureDistance;
        minDistanceBetweenMajorStructures = settings.minDistanceBetweenMajorStructures;
        createBottomFloor = settings.createBottomFloor;
        bottomFloorThickness = settings.bottomFloorThickness;
        bottomFloorWallInset = settings.bottomFloorWallInset;
        minDistanceFromShaftWall = settings.minDistanceFromShaftWall;
        centralVoidBias = settings.centralVoidBias;
        finalArenaRadius = settings.finalArenaRadius;
        pedestalRadius = settings.pedestalRadius;
        pedestalHeight = settings.pedestalHeight;
        finalColumnCount = settings.finalColumnCount;
        finalColumnRadius = settings.finalColumnRadius;
        finalColumnHeight = settings.finalColumnHeight;
        victorySphereRadius = settings.victorySphereRadius;
        victorySphereColor = settings.victorySphereColor;
        shaftWallMaterial = settings.shaftWallMaterial != null ? settings.shaftWallMaterial : shaftWallMaterial;
        platformMaterial = settings.platformMaterial != null ? settings.platformMaterial : platformMaterial;
        ledgeMaterial = settings.ledgeMaterial != null ? settings.ledgeMaterial : ledgeMaterial;
        criticalPathMaterial = settings.criticalPathMaterial != null ? settings.criticalPathMaterial : criticalPathMaterial;
        finalArenaMaterial = settings.finalArenaMaterial != null ? settings.finalArenaMaterial : finalArenaMaterial;
        victorySphereMaterial = settings.victorySphereMaterial != null ? settings.victorySphereMaterial : victorySphereMaterial;
    }

    private void ResetStats()
    {
        criticalDescentPoints.Clear();
        grappleTargetPoints.Clear();
        generatedStructureBounds.Clear();
        majorStructureBounds.Clear();
        chunkGrappleCounts.Clear();
        structureAnchors.Clear();
        wallAnchors.Clear();
        structureRecords.Clear();
        generatedStructureCount = 0;
        generatedComplexStructureCount = 0;
        generatedHugeStructureCount = 0;
        generatedSmallPlatformCount = 0;
        generatedCriticalCount = 0;
        generatedWallLedgeCount = 0;
        generatedEmergencyLedgeCount = 0;
        generatedWallAttachedChainCount = 0;
        generatedStructureAttachedChainCount = 0;
        generatedSecondaryAttachmentCount = 0;
        removedOrphanStructureCount = 0;
        emptyChunkCount = 0;
        lastChunkCount = 0;
        lastFinalArenaPosition = new Vector3(0f, -levelHeight, 0f);
        lastEmergencyRoot = null;
    }

    private void CreateShaftBoundary(Transform parent, int boundaryLayer, System.Random rng)
    {
        float height = levelHeight;
        float y = -levelHeight * 0.5f;
        float angleStep = Mathf.PI * 2f / shaftWallSegments;
        float panelWidth = 2f * Mathf.PI * shaftRadius / shaftWallSegments * 1.08f;

        for (int i = 0; i < shaftWallSegments; i++)
        {
            float angle = i * angleStep;
            Vector3 radial = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vector3 position = radial * (shaftRadius + shaftWallThickness * 0.5f) + Vector3.up * y;
            Quaternion rotation = Quaternion.LookRotation(radial, Vector3.up);

            GameObject panel = CreateCube(
                parent,
                $"Shaft Wall Panel {i:00}",
                position,
                rotation,
                new Vector3(panelWidth, height, shaftWallThickness),
                shaftWallMaterial,
                boundaryLayer,
                -1,
                0);

            generatedStructureBounds.Add(panel.GetComponent<Collider>().bounds);

            if (i % 3 == 0 && Next01(rng) < 0.55f)
            {
                CreateWallScar(parent, rng, radial, angle, boundaryLayer);
            }
        }

        CreateShaftBottomFloor(parent, boundaryLayer);
    }

    private void CreateShaftBottomFloor(Transform parent, int boundaryLayer)
    {
        if (!createBottomFloor)
        {
            return;
        }

        float radius = Mathf.Max(1f, shaftRadius - bottomFloorWallInset);
        float thickness = Mathf.Max(0.1f, bottomFloorThickness);
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        floor.name = "Shaft Bottom Floor";
        floor.transform.SetParent(parent, false);
        floor.transform.SetPositionAndRotation(new Vector3(0f, -levelHeight - thickness * 0.5f, 0f), Quaternion.identity);
        floor.transform.localScale = new Vector3(radius * 2f, thickness * 0.5f, radius * 2f);
        SetLayerRecursively(floor, boundaryLayer);

        Collider primitiveCollider = floor.GetComponent<Collider>();
        if (primitiveCollider != null)
        {
            if (Application.isPlaying)
            {
                Destroy(primitiveCollider);
            }
            else
            {
                DestroyImmediate(primitiveCollider);
            }
        }

        MeshFilter meshFilter = floor.GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            MeshCollider meshCollider = floor.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = meshFilter.sharedMesh;
        }

        MeshRenderer renderer = floor.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = finalArenaMaterial != null ? finalArenaMaterial : shaftWallMaterial;
        }

        Collider collider = floor.GetComponent<Collider>();
        if (collider != null)
        {
            generatedStructureBounds.Add(collider.bounds);
        }

        
        try
        {
            var marker = floor.AddComponent<LevelTransitionRemovalObject>();
            marker.Phase = LevelTransitionRemovalObject.RemovalPhase.AfterNextLevelReady;
        }
        catch { }
    }

    private void CreateWallScar(Transform parent, System.Random rng, Vector3 radial, float angle, int boundaryLayer)
    {
        float y = RandomRange(rng, -levelHeight + chunkHeight, -chunkHeight);
        Vector3 tangent = new(-radial.z, 0f, radial.x);
        Vector3 position = radial * (shaftRadius - 0.2f) + Vector3.up * y;
        Quaternion rotation = Quaternion.LookRotation(-radial, Vector3.up);
        GameObject scar = CreateCube(
            parent,
            "Ancient Wall Rib",
            position + tangent * RandomRange(rng, -1.2f, 1.2f),
            rotation,
            new Vector3(RandomRange(rng, 0.35f, 0.75f), RandomRange(rng, 4f, 11f), RandomRange(rng, 0.35f, 0.8f)),
            darkRuinMaterial != null ? darkRuinMaterial : shaftWallMaterial,
            boundaryLayer,
            -1,
            0);
        generatedStructureBounds.Add(scar.GetComponent<Collider>().bounds);
    }

    private void GenerateChunks(Transform chunkRoot, Transform criticalRoot, System.Random rng, int grappleLayer)
    {
        int chunkCount = Mathf.CeilToInt(levelHeight / chunkHeight);
        lastChunkCount = chunkCount;

        Vector3 previousCriticalPoint = FindPlayerStartPoint();
        for (int chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
        {
            chunkGrappleCounts.Add(0);
            float chunkTop = -chunkIndex * chunkHeight;
            float chunkBottom = Mathf.Max(-levelHeight, chunkTop - chunkHeight);
            float chunkCenterY = (chunkTop + chunkBottom) * 0.5f;
            float depth01 = Mathf.Clamp01(Mathf.Abs(chunkCenterY) / Mathf.Max(1f, levelHeight));

            Transform currentChunkRoot = CreateChildRoot(chunkRoot, $"Chunk {chunkIndex:00}  Y {chunkTop:F0}..{chunkBottom:F0}");
            bool nearStart = Mathf.Abs(chunkTop) < chunkHeight * 0.8f;
            bool nearBottomReserve = Mathf.Abs(chunkBottom + levelHeight) < chunkHeight * 1.15f;
            SeedWallAnchorsForChunk(rng, chunkIndex, chunkTop, chunkBottom);

            if (generateCriticalPath && !nearStart)
            {
                previousCriticalPoint = CreateCriticalSupport(criticalRoot, rng, previousCriticalPoint, chunkCenterY, chunkIndex, grappleLayer, depth01);
            }

            bool chunkIsSparse = Next01(rng) < emptyChunkChance + emptySpaceRatio * 0.25f;
            if (chunkIsSparse)
            {
                emptyChunkCount++;
            }

            float densityFactor = EvaluateCurve(densityByDepth, depth01, 1f);
            int extraCount = chunkIsSparse ? RandomRange(rng, 0, 2) : RandomRange(rng, 1, maxStructuresPerChunk + 1);
            float densityGate = Mathf.Clamp01(globalStructureDensity * densityFactor * (1f - emptySpaceRatio * 0.45f));
            for (int i = 0; i < extraCount; i++)
            {
                if (Next01(rng) > densityGate || (nearStart && Next01(rng) < 0.65f) || (nearBottomReserve && Next01(rng) < 0.55f))
                {
                    continue;
                }

                CreateAnchoredStructure(currentChunkRoot, rng, chunkTop, chunkBottom, chunkIndex, grappleLayer, depth01, false);
            }

            if (!nearStart && !nearBottomReserve && chunkIndex % 3 == 1)
            {
                CreateAnchoredStructure(currentChunkRoot, rng, chunkTop, chunkBottom, chunkIndex, grappleLayer, depth01, true);
            }

            if (Next01(rng) < wallLedgeChance && !nearStart)
            {
                CreateWallLedge(currentChunkRoot, rng, RandomRange(rng, chunkBottom, chunkTop), chunkIndex, grappleLayer, false);
            }
        }
    }

    private Vector3 CreateCriticalSupport(Transform parent, System.Random rng, Vector3 previousPoint, float y, int chunkIndex, int grappleLayer, float depth01)
    {
        float horizontalStep = Mathf.Min(maxHorizontalGap, maxStructureDistance, assumedMaxGrappleDistance * 0.48f);
        Vector2 previous = new(previousPoint.x, previousPoint.z);
        Vector2 offset = RandomUnitCircle(rng) * RandomRange(rng, horizontalStep * 0.4f, horizontalStep);
        Vector2 next = previous + offset;
        float maxRadius = Mathf.Max(2f, shaftRadius - minDistanceFromShaftWall - 5f);
        if (next.magnitude > maxRadius)
        {
            next = next.normalized * maxRadius;
        }

        Vector3 position = new(next.x, y + RandomRange(rng, -chunkHeight * 0.18f, chunkHeight * 0.18f), next.y);
        float scaleBoost = EvaluateCurve(scaleByDepth, depth01, 1f);
        float width = RandomRange(rng, mediumPlatformSizeRange.x, mediumPlatformSizeRange.y) * scaleBoost;
        float depth = RandomRange(rng, mediumPlatformSizeRange.x, mediumPlatformSizeRange.y) * scaleBoost;
        float thickness = RandomRange(rng, 0.55f, 0.95f);
        Quaternion rotation = Quaternion.Euler(0f, RandomRange(rng, 0f, 360f), RandomRange(rng, -4f, 4f));

        GameObject platform = CreateCube(
            parent,
            $"Critical Grapple Platform {chunkIndex:00}",
            position,
            rotation,
            new Vector3(width, thickness, depth),
            criticalPathMaterial != null ? criticalPathMaterial : platformMaterial,
            grappleLayer,
            chunkIndex,
            8);

        AddEdgeTeeth(platform.transform, rng, grappleLayer, true, chunkIndex);
        AddWallSupportBeam(parent, rng, platform.transform, position, chunkIndex, grappleLayer, criticalPathMaterial != null ? criticalPathMaterial : platformMaterial);
        RegisterGrappleTarget(position, chunkIndex, 8);
        criticalDescentPoints.Add(position);
        RegisterStructureRecord(platform, null, CreateNearestWallAnchor(position, chunkIndex, true), platform.GetComponent<Collider>().bounds, GeneratedStructureLibrary.StructureKind.BrokenPlatformCluster, true);
        generatedCriticalCount++;
        return position;
    }

    private void SeedWallAnchorsForChunk(System.Random rng, int chunkIndex, float chunkTop, float chunkBottom)
    {
        int count = Mathf.Clamp(Mathf.RoundToInt(wallAnchorDensity + Next01(rng) * 2f), 1, 4);
        float goldenAngle = 2.399963f;
        for (int i = 0; i < count; i++)
        {
            float noise = FractalNoise(seed * 0.013f + chunkIndex * 0.37f, i * 0.71f, noiseOctaves);
            float angle = (chunkIndex * goldenAngle + i * Mathf.PI * 2f / count + noise * Mathf.PI * 0.8f) % (Mathf.PI * 2f);
            float y = Mathf.Lerp(chunkBottom + 1.2f, chunkTop - 1.2f, Mathf.Clamp01(Next01(rng)));
            Vector3 radial = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            StructureAnchor anchor = new()
            {
                Position = radial * (shaftRadius - shaftWallThickness * 0.55f) + Vector3.up * y,
                Normal = -radial,
                ParentStructure = null,
                AnchorType = AnchorType.ShaftWall,
                SupportedScale = RandomRange(rng, 0.9f, 1.5f),
                Depth = 0,
                CanSpawnMajorStructure = true,
                CanSpawnMinorStructure = true
            };

            wallAnchors.Add(anchor);
            structureAnchors.Add(anchor);
        }
    }

    private void CreateAnchoredStructure(Transform parent, System.Random rng, float chunkTop, float chunkBottom, int chunkIndex, int grappleLayer, float depth01, bool preferMajor)
    {
        StructureAnchor anchor = PickStructureAnchor(rng, chunkTop, chunkBottom, preferMajor);
        bool major = preferMajor || (anchor.CanSpawnMajorStructure && Next01(rng) < Mathf.Max(largeStructureChance, hugeStructureChance));
        bool useProceduralStructure = Next01(rng) < proceduralStructureChance || requireStructuralAnchoring;
        Vector3 position = CalculateAnchoredPosition(rng, anchor, chunkTop, chunkBottom, major);
        for (int i = 0; i < 6 && !CanPlaceStructureAt(position, major); i++)
        {
            anchor = PickStructureAnchor(rng, chunkTop, chunkBottom, major);
            position = CalculateAnchoredPosition(rng, anchor, chunkTop, chunkBottom, major);
        }

        Vector3 forward = anchor.Normal.sqrMagnitude > 0.0001f ? anchor.Normal.normalized : Vector3.forward;
        Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up) *
                              Quaternion.Euler(RandomRange(rng, -8f, 8f), RandomRange(rng, -18f, 18f), RandomRange(rng, -10f, 10f));

        if (useProceduralStructure)
        {
            float sizeMultiplier = major
                ? RandomRange(rng, hugeStructureSizeRange.x, hugeStructureSizeRange.y)
                : RandomRange(rng, complexStructureScaleRange.x, complexStructureScaleRange.y);
            sizeMultiplier *= Mathf.Max(0.4f, anchor.SupportedScale) * EvaluateCurve(scaleByDepth, depth01, 1f);
            sizeMultiplier *= Mathf.Lerp(0.85f, 1.25f, FractalNoise(position.x * noiseScale, position.z * noiseScale + depth01, noiseOctaves));

            GeneratedStructureLibrary.StructureKind kind = PickAnchoredStructureKind(rng, anchor, major);
            GeneratedStructureLibrary.BuildContext context = new()
            {
                Parent = parent,
                Rng = rng,
                Position = position,
                Rotation = rotation,
                GrappleLayer = grappleLayer,
                PrimaryMaterial = platformMaterial,
                AccentMaterial = ledgeMaterial != null ? ledgeMaterial : platformMaterial,
                DarkMaterial = darkRuinMaterial != null ? darkRuinMaterial : shaftWallMaterial,
                SizeMultiplier = sizeMultiplier,
                NamePrefix = anchor.AnchorType == AnchorType.ShaftWall ? $"Wall-Anchored Structure {chunkIndex:00}" : $"Chained Structure {chunkIndex:00}",
                ShapeVariationAmount = Next01(rng) < irregularityChance ? shapeVariationAmount : shapeVariationAmount * 0.45f,
                NoiseScale = noiseScale,
                NoiseAmplitude = noiseAmplitude,
                DomainWarpStrength = domainWarpStrength,
                NoiseOctaves = noiseOctaves,
                AsymmetryAmount = asymmetryAmount,
                BreakageAmount = breakageAmount
            };

            GeneratedStructureLibrary.Result result = GeneratedStructureLibrary.CreateStructure(kind, context);
            if (result.Root != null)
            {
                generatedStructureCount += result.PieceCount;
                generatedComplexStructureCount++;
                if (result.IsHuge || major)
                {
                    generatedHugeStructureCount++;
                    majorStructureBounds.Add(result.Bounds);
                }
                else
                {
                    generatedSecondaryAttachmentCount++;
                }

                generatedStructureBounds.Add(result.Bounds);
                RegisterGrappleTarget(result.Bounds.center, chunkIndex, Mathf.Max(1, result.GrappleScore));
                AddAttachmentSupport(parent, rng, anchor, result.Bounds, chunkIndex, grappleLayer);
                RegisterStructureRecord(result.Root, anchor.ParentStructure, anchor, result.Bounds, result.Kind, result.IsHuge || major);
                AddChildAnchorsFromStructure(result.Root.transform, result.Bounds, anchor, rng, major);
                if (Next01(rng) < secondaryAttachmentChance)
                {
                    CreateAttachedSecondary(parent, rng, result.Root.transform, result.Bounds, anchor, chunkIndex, grappleLayer);
                }

                return;
            }
        }

        CreateAnchoredBasicPlatform(parent, rng, anchor, position, rotation, chunkIndex, grappleLayer);
    }

    private StructureAnchor PickStructureAnchor(System.Random rng, float chunkTop, float chunkBottom, bool preferMajor)
    {
        List<StructureAnchor> candidates = new();
        for (int i = 0; i < structureAnchors.Count; i++)
        {
            StructureAnchor anchor = structureAnchors[i];
            if (anchor.Position.y < chunkBottom - chunkHeight * 0.75f || anchor.Position.y > chunkTop + chunkHeight * 0.75f)
            {
                continue;
            }

            if (preferMajor && !anchor.CanSpawnMajorStructure)
            {
                continue;
            }

            candidates.Add(anchor);
        }

        if (candidates.Count == 0)
        {
            return CreateWallAnchor(RandomRange(rng, chunkBottom + 1f, chunkTop - 1f), rng, true);
        }

        int index = RandomRange(rng, 0, candidates.Count);
        return candidates[index];
    }

    private StructureAnchor CreateWallAnchor(float y, System.Random rng, bool canSpawnMajor)
    {
        float angle = RandomRange(rng, 0f, Mathf.PI * 2f);
        Vector3 radial = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        StructureAnchor anchor = new()
        {
            Position = radial * (shaftRadius - shaftWallThickness * 0.55f) + Vector3.up * y,
            Normal = -radial,
            ParentStructure = null,
            AnchorType = AnchorType.ShaftWall,
            SupportedScale = RandomRange(rng, 0.85f, 1.45f),
            Depth = 0,
            CanSpawnMajorStructure = canSpawnMajor,
            CanSpawnMinorStructure = true
        };
        wallAnchors.Add(anchor);
        structureAnchors.Add(anchor);
        return anchor;
    }

    private StructureAnchor CreateNearestWallAnchor(Vector3 position, int chunkIndex, bool canSpawnMajor)
    {
        Vector3 horizontal = new(position.x, 0f, position.z);
        Vector3 radial = horizontal.sqrMagnitude > 0.0001f ? horizontal.normalized : Vector3.forward;
        return new StructureAnchor
        {
            Position = radial * (shaftRadius - shaftWallThickness * 0.55f) + Vector3.up * position.y,
            Normal = -radial,
            ParentStructure = null,
            AnchorType = AnchorType.ShaftWall,
            SupportedScale = 1f,
            Depth = 0,
            CanSpawnMajorStructure = canSpawnMajor,
            CanSpawnMinorStructure = true
        };
    }

    private Vector3 CalculateAnchoredPosition(System.Random rng, StructureAnchor anchor, float chunkTop, float chunkBottom, bool major)
    {
        Vector3 normal = anchor.Normal.sqrMagnitude > 0.0001f ? anchor.Normal.normalized : Vector3.forward;
        Vector3 tangent = new(-normal.z, 0f, normal.x);
        if (tangent.sqrMagnitude < 0.0001f)
        {
            tangent = Vector3.right;
        }

        float extension = major ? RandomRange(rng, 4.5f, 8.5f) : RandomRange(rng, 1.6f, 5.5f);
        extension = Mathf.Min(extension, Mathf.Max(1f, parentStructureInfluenceRadius));
        float y = Mathf.Clamp(anchor.Position.y + RandomRange(rng, -chunkHeight * 0.25f, chunkHeight * 0.2f), chunkBottom + 0.8f, chunkTop - 0.8f);
        Vector3 position = anchor.Position + normal * extension + tangent.normalized * RandomRange(rng, -3.0f, 3.0f) + Vector3.up * (y - anchor.Position.y);
        return ClampInsideShaft(position, major ? minDistanceFromShaftWall + 6f : minDistanceFromShaftWall + 2f);
    }

    private GeneratedStructureLibrary.StructureKind PickAnchoredStructureKind(System.Random rng, StructureAnchor anchor, bool major)
    {
        if (major)
        {
            float roll = Next01(rng);
            if (roll < hollowStructureChance)
            {
                return GeneratedStructureLibrary.StructureKind.HollowCylinderRuin;
            }
            if (roll < hollowStructureChance + nestedStructureChance)
            {
                return GeneratedStructureLibrary.StructureKind.NestedCylinderStructure;
            }
            if (roll < hollowStructureChance + nestedStructureChance + artifactFormChance)
            {
                return Next01(rng) < 0.5f ? GeneratedStructureLibrary.StructureKind.MassiveFallenSlab : GeneratedStructureLibrary.StructureKind.RingSegment;
            }
        }

        if (anchor.AnchorType == AnchorType.ShaftWall && Next01(rng) < bridgeFragmentChance)
        {
            return GeneratedStructureLibrary.StructureKind.AncientBridgeFragment;
        }

        GeneratedStructureLibrary.StructureKind kind = StructureSpawner.PickKind(rng);
        if (!allowIsolatedFloatingPlatforms && kind == GeneratedStructureLibrary.StructureKind.SmallLonelyPlatform)
        {
            kind = GeneratedStructureLibrary.StructureKind.IrregularGrappleOutcrop;
        }

        return kind;
    }

    private void CreateAnchoredBasicPlatform(Transform parent, System.Random rng, StructureAnchor anchor, Vector3 position, Quaternion rotation, int chunkIndex, int grappleLayer)
    {
        Vector3 scale = new(
            RandomRange(rng, structureLengthRange.x, structureLengthRange.y),
            RandomRange(rng, structureHeightRange.x, Mathf.Min(structureHeightRange.y, 1.4f)),
            RandomRange(rng, structureWidthRange.x, structureWidthRange.y));

        float n = FractalNoise(position.x * noiseScale + seed * 0.01f, position.z * noiseScale + chunkIndex, noiseOctaves);
        scale.x *= Mathf.Lerp(0.75f, 1.35f, n);
        scale.z *= Mathf.Lerp(0.7f, 1.25f, 1f - n);

        GameObject platform = CreateCube(
            parent,
            anchor.AnchorType == AnchorType.ShaftWall ? $"Wall-Attached Ruin Platform {chunkIndex:00}" : $"Chained Ruin Platform {chunkIndex:00}",
            position,
            rotation,
            scale,
            platformMaterial,
            grappleLayer,
            chunkIndex,
            8);

        Collider collider = platform.GetComponent<Collider>();
        Bounds bounds = collider != null ? collider.bounds : new Bounds(position, scale);
        AddEdgeTeeth(platform.transform, rng, grappleLayer, false, chunkIndex);
        AddAttachmentSupport(parent, rng, anchor, bounds, chunkIndex, grappleLayer);
        RegisterStructureRecord(platform, anchor.ParentStructure, anchor, bounds, GeneratedStructureLibrary.StructureKind.BrokenPlatformCluster, false);
        AddChildAnchorsFromStructure(platform.transform, bounds, anchor, rng, false);
    }

    private void CreateAttachedSecondary(Transform parent, System.Random rng, Transform parentStructure, Bounds parentBounds, StructureAnchor parentAnchor, int chunkIndex, int grappleLayer)
    {
        int branches = Mathf.Clamp(Mathf.RoundToInt(structureAnchorDensity * Mathf.Max(1, maxBranchesPerMajorStructure)), 1, Mathf.Max(1, maxBranchesPerMajorStructure));
        for (int i = 0; i < branches; i++)
        {
            if (Next01(rng) > secondaryAttachmentChance)
            {
                continue;
            }

            Vector3 direction = RandomAttachedDirection(rng, parentAnchor.Normal);
            Vector3 start = parentBounds.center + Vector3.Scale(direction.normalized, parentBounds.extents);
            Vector3 position = ClampInsideShaft(start + direction.normalized * RandomRange(rng, 1.2f, 3.8f), minDistanceFromShaftWall + 1.2f);
            Quaternion rotation = Quaternion.LookRotation(direction.normalized, Vector3.up) * Quaternion.Euler(RandomRange(rng, -8f, 8f), RandomRange(rng, -18f, 18f), RandomRange(rng, -8f, 8f));
            Vector3 scale = new(RandomRange(rng, 1.2f, 4.5f), RandomRange(rng, 0.3f, 0.85f), RandomRange(rng, 0.7f, 2.4f));

            GameObject child = CreateCube(parent, $"Attached Secondary Fragment {chunkIndex:00}", position, rotation, scale, ledgeMaterial != null ? ledgeMaterial : platformMaterial, grappleLayer, chunkIndex, 5);
            AddEdgeTeeth(child.transform, rng, grappleLayer, false, chunkIndex);
            CreateBeamBetween(parent, "Short Ruin Attachment Brace", parentBounds.ClosestPoint(position), position, 0.28f, ledgeMaterial != null ? ledgeMaterial : platformMaterial, grappleLayer, chunkIndex, 2, rng);

            StructureAnchor anchor = new()
            {
                Position = parentBounds.ClosestPoint(position),
                Normal = direction.normalized,
                ParentStructure = parentStructure,
                AnchorType = AnchorType.Branch,
                SupportedScale = 0.8f,
                Depth = parentAnchor.Depth + 1,
                CanSpawnMajorStructure = false,
                CanSpawnMinorStructure = parentAnchor.Depth + 1 < maxChainDepth
            };

            Collider collider = child.GetComponent<Collider>();
            Bounds bounds = collider != null ? collider.bounds : new Bounds(position, scale);
            RegisterStructureRecord(child, parentStructure, anchor, bounds, GeneratedStructureLibrary.StructureKind.IrregularGrappleOutcrop, false);
            AddChildAnchorsFromStructure(child.transform, bounds, anchor, rng, false);
            generatedSecondaryAttachmentCount++;
        }
    }

    private Vector3 RandomAttachedDirection(System.Random rng, Vector3 parentNormal)
    {
        Vector3 normal = parentNormal.sqrMagnitude > 0.0001f ? parentNormal.normalized : Vector3.forward;
        Vector3 tangent = new(-normal.z, 0f, normal.x);
        if (tangent.sqrMagnitude < 0.0001f)
        {
            tangent = Vector3.right;
        }

        Vector3 direction = normal * RandomRange(rng, 0.55f, 1.25f) +
                            tangent.normalized * RandomRange(rng, -0.9f, 0.9f) +
                            Vector3.down * RandomRange(rng, 0.0f, 0.55f);
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : normal;
    }

    private void AddAttachmentSupport(Transform parent, System.Random rng, StructureAnchor anchor, Bounds bounds, int chunkIndex, int grappleLayer)
    {
        Vector3 anchorPoint = anchor.Position;
        Vector3 targetPoint = bounds.ClosestPoint(anchorPoint);
        if (Vector3.Distance(anchorPoint, targetPoint) < Mathf.Max(0.2f, minAttachmentOverlap))
        {
            return;
        }

        CreateBeamBetween(
            parent,
            anchor.AnchorType == AnchorType.ShaftWall ? "Wall-to-Ruin Support Strut" : "Structure-to-Ruin Support Strut",
            anchorPoint,
            targetPoint,
            RandomRange(rng, 0.28f, 0.65f),
            darkRuinMaterial != null ? darkRuinMaterial : ledgeMaterial,
            grappleLayer,
            chunkIndex,
            3,
            rng);
    }

    private void AddWallSupportBeam(Transform parent, System.Random rng, Transform supported, Vector3 supportedPosition, int chunkIndex, int grappleLayer, Material material)
    {
        StructureAnchor wallAnchor = CreateNearestWallAnchor(supportedPosition, chunkIndex, false);
        Vector3 target = supportedPosition;
        Collider collider = supported != null ? supported.GetComponent<Collider>() : null;
        if (collider != null)
        {
            target = collider.bounds.ClosestPoint(wallAnchor.Position);
        }

        CreateBeamBetween(parent, "Critical Platform Wall Support", wallAnchor.Position, target, RandomRange(rng, 0.35f, 0.65f), material, grappleLayer, chunkIndex, 4, rng);
    }

    private GameObject CreateBeamBetween(Transform parent, string objectName, Vector3 start, Vector3 end, float thickness, Material material, int layer, int chunkIndex, int grappleScore, System.Random rng)
    {
        Vector3 delta = end - start;
        float length = delta.magnitude;
        if (length < 0.05f)
        {
            return null;
        }

        Vector3 direction = delta / length;
        Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up) * Quaternion.Euler(0f, 90f, 0f);
        return CreateCube(parent, objectName, (start + end) * 0.5f, rotation, new Vector3(length, thickness, thickness * RandomRange(rng, 0.7f, 1.35f)), material, layer, chunkIndex, grappleScore);
    }

    private void RegisterStructureRecord(GameObject root, Transform parentStructure, StructureAnchor anchor, Bounds bounds, GeneratedStructureLibrary.StructureKind kind, bool isMajor)
    {
        if (root == null)
        {
            return;
        }

        bool validAnchor = anchor.AnchorType == AnchorType.ShaftWall || parentStructure != null || anchor.ParentStructure != null || isMajor;
        GeneratedStructureRecord record = new()
        {
            Root = root,
            ParentStructure = parentStructure != null ? parentStructure : anchor.ParentStructure,
            Anchor = anchor,
            Bounds = bounds,
            Kind = kind,
            IsMajor = isMajor,
            IsOrphan = requireStructuralAnchoring && !allowIsolatedFloatingPlatforms && !validAnchor
        };

        structureRecords.Add(record);
        if (anchor.AnchorType == AnchorType.ShaftWall)
        {
            generatedWallAttachedChainCount++;
        }
        else
        {
            generatedStructureAttachedChainCount++;
        }
    }

    private void AddChildAnchorsFromStructure(Transform structure, Bounds bounds, StructureAnchor sourceAnchor, System.Random rng, bool major)
    {
        if (structure == null || sourceAnchor.Depth >= maxChainDepth)
        {
            return;
        }

        int branchLimit = major ? maxBranchesPerMajorStructure : Mathf.Max(1, maxBranchesPerMajorStructure - 1);
        int count = Mathf.Clamp(Mathf.RoundToInt(structureAnchorDensity * branchLimit), 1, Mathf.Max(1, branchLimit));
        for (int i = 0; i < count; i++)
        {
            Vector3 direction = RandomAttachedDirection(rng, sourceAnchor.Normal);
            Vector3 anchorPosition = bounds.center + Vector3.Scale(direction, bounds.extents);
            anchorPosition = ClampInsideShaft(anchorPosition, minDistanceFromShaftWall + 1f);
            structureAnchors.Add(new StructureAnchor
            {
                Position = anchorPosition,
                Normal = direction,
                ParentStructure = structure,
                AnchorType = major ? AnchorType.Structure : AnchorType.Branch,
                SupportedScale = major ? RandomRange(rng, 0.9f, 1.35f) : RandomRange(rng, 0.55f, 1.05f),
                Depth = sourceAnchor.Depth + 1,
                CanSpawnMajorStructure = major && sourceAnchor.Depth + 1 < maxChainDepth - 1,
                CanSpawnMinorStructure = sourceAnchor.Depth + 1 < maxChainDepth
            });
        }
    }

    private Vector3 ClampInsideShaft(Vector3 position, float padding)
    {
        Vector2 horizontal = new(position.x, position.z);
        float maxRadius = Mathf.Max(1f, shaftRadius - padding);
        if (horizontal.magnitude > maxRadius)
        {
            horizontal = horizontal.normalized * maxRadius;
            position.x = horizontal.x;
            position.z = horizontal.y;
        }

        return position;
    }

    private void CreateScatteredStructure(Transform parent, System.Random rng, float chunkTop, float chunkBottom, int chunkIndex, int grappleLayer, float depth01)
    {
        float y = RandomRange(rng, chunkBottom + 1.5f, chunkTop - 1f);
        bool huge = Next01(rng) < hugeStructureChance;
        bool useProceduralStructure = Next01(rng) < proceduralStructureChance;
        float padding = huge ? minDistanceFromShaftWall + 9f : minDistanceFromShaftWall + 3f;
        Vector3 position = RandomPointInShaft(rng, y, padding);
        for (int i = 0; i < 7 && !CanPlaceStructureAt(position, huge); i++)
        {
            position = RandomPointInShaft(rng, y, padding);
        }

        Quaternion rotation = Quaternion.Euler(RandomRange(rng, -8f, 8f), RandomRange(rng, 0f, 360f), RandomRange(rng, -10f, 10f));

        if (useProceduralStructure)
        {
            float sizeMultiplier = huge
                ? RandomRange(rng, hugeStructureSizeRange.x, hugeStructureSizeRange.y)
                : RandomRange(rng, complexStructureScaleRange.x, complexStructureScaleRange.y);
            sizeMultiplier *= EvaluateCurve(scaleByDepth, depth01, 1f);

            GeneratedStructureLibrary.StructureKind kind = Next01(rng) < hollowCylinderChance
                ? GeneratedStructureLibrary.StructureKind.HollowCylinderRuin
                : StructureSpawner.PickKind(rng);

            GeneratedStructureLibrary.BuildContext context = new()
            {
                Parent = parent,
                Rng = rng,
                Position = position,
                Rotation = rotation,
                GrappleLayer = grappleLayer,
                PrimaryMaterial = platformMaterial,
                AccentMaterial = ledgeMaterial != null ? ledgeMaterial : platformMaterial,
                DarkMaterial = darkRuinMaterial != null ? darkRuinMaterial : shaftWallMaterial,
                SizeMultiplier = sizeMultiplier,
                NamePrefix = $"Procedural Structure {chunkIndex:00}"
            };

            GeneratedStructureLibrary.Result result = GeneratedStructureLibrary.CreateStructure(kind, context);
            if (result.Root != null)
            {
                generatedStructureCount += result.PieceCount;
                generatedComplexStructureCount++;
                if (result.IsHuge || huge)
                {
                    generatedHugeStructureCount++;
                    majorStructureBounds.Add(result.Bounds);
                }

                generatedStructureBounds.Add(result.Bounds);
                RegisterGrappleTarget(result.Bounds.center, chunkIndex, Mathf.Max(1, result.GrappleScore));
                return;
            }
        }

        CreateBasicPlatform(parent, rng, position, rotation, chunkIndex, grappleLayer);
    }

    private void CreateBasicPlatform(Transform parent, System.Random rng, Vector3 position, Quaternion rotation, int chunkIndex, int grappleLayer)
    {
        bool tiny = Next01(rng) < tinyPlatformChance;
        Vector2 sizeRange = tiny ? smallPlatformSizeRange : mediumPlatformSizeRange;
        Vector3 scale = new(RandomRange(rng, sizeRange.x, sizeRange.y), RandomRange(rng, 0.45f, 0.9f), RandomRange(rng, sizeRange.x, sizeRange.y));

        GameObject platform = CreateCube(
            parent,
            tiny ? $"Small Lonely Platform {chunkIndex:00}" : $"Broken Basic Platform {chunkIndex:00}",
            position,
            rotation,
            scale,
            platformMaterial,
            grappleLayer,
            chunkIndex,
            tiny ? 5 : 8);

        if (tiny)
        {
            generatedSmallPlatformCount++;
        }

        if (!tiny || Next01(rng) < 0.5f)
        {
            AddEdgeTeeth(platform.transform, rng, grappleLayer, false, chunkIndex);
        }
    }

    private GameObject CreateWallLedge(Transform parent, System.Random rng, float y, int chunkIndex, int grappleLayer, bool emergency)
    {
        float angle = RandomRange(rng, 0f, Mathf.PI * 2f);
        Vector3 radial = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        Vector3 position = radial * (shaftRadius - 1.4f) + Vector3.up * y;
        Quaternion rotation = Quaternion.LookRotation(-radial, Vector3.up);
        Vector3 scale = emergency
            ? new Vector3(RandomRange(rng, 2.5f, 4.5f), RandomRange(rng, 0.45f, 0.75f), RandomRange(rng, 1.6f, 2.8f))
            : new Vector3(RandomRange(rng, 2.2f, 6.2f), RandomRange(rng, 0.4f, 0.8f), RandomRange(rng, 1.8f, 3.6f));

        GameObject ledge = CreateCube(
            parent,
            emergency ? $"Emergency Wall Grapple Ledge {chunkIndex:00}" : $"Wall Grapple Ledge {chunkIndex:00}",
            position,
            rotation,
            scale,
            ledgeMaterial != null ? ledgeMaterial : platformMaterial,
            grappleLayer,
            chunkIndex,
            emergency ? targetGrappleEdgesPerChunk : 7);

        AddEdgeTeeth(ledge.transform, rng, grappleLayer, false, chunkIndex);
        StructureAnchor anchor = new()
        {
            Position = radial * (shaftRadius - shaftWallThickness * 0.55f) + Vector3.up * y,
            Normal = -radial,
            ParentStructure = null,
            AnchorType = AnchorType.ShaftWall,
            SupportedScale = emergency ? 0.85f : 1f,
            Depth = 0,
            CanSpawnMajorStructure = false,
            CanSpawnMinorStructure = true
        };
        Collider ledgeCollider = ledge.GetComponent<Collider>();
        if (ledgeCollider != null)
        {
            RegisterStructureRecord(ledge, null, anchor, ledgeCollider.bounds, GeneratedStructureLibrary.StructureKind.IrregularGrappleOutcrop, false);
            AddChildAnchorsFromStructure(ledge.transform, ledgeCollider.bounds, anchor, rng, false);
        }
        generatedWallLedgeCount++;
        if (emergency)
        {
            generatedEmergencyLedgeCount++;
        }

        return ledge;
    }

    private void AddEdgeTeeth(Transform platform, System.Random rng, int grappleLayer, bool critical, int chunkIndex)
    {
        int teeth = critical ? 4 : RandomRange(rng, 1, 4);
        for (int i = 0; i < teeth; i++)
        {
            Vector3 side = Next01(rng) < 0.5f ? Vector3.right : Vector3.forward;
            float sign = Next01(rng) < 0.5f ? -1f : 1f;
            Vector3 localOffset = side * sign * 0.55f + Vector3.up * RandomRange(rng, 0.45f, 0.72f);
            Vector3 localScale = side == Vector3.forward
                ? new Vector3(RandomRange(rng, 0.8f, 1.25f), RandomRange(rng, 0.25f, 0.42f), RandomRange(rng, 0.35f, 0.58f))
                : new Vector3(RandomRange(rng, 0.35f, 0.58f), RandomRange(rng, 0.25f, 0.42f), RandomRange(rng, 0.8f, 1.25f));

            GameObject tooth = CreateLocalCube(
                platform,
                $"Edge Outcrop {i + 1}",
                localOffset,
                Quaternion.Euler(0f, RandomRange(rng, -12f, 12f), 0f),
                localScale,
                ledgeMaterial != null ? ledgeMaterial : platformMaterial,
                grappleLayer);

            RegisterGrappleTarget(tooth.transform.position, chunkIndex, 3);
        }
    }

    private void ValidateAndPatchGrappleOpportunities(Transform emergencyRoot, System.Random rng, int grappleLayer)
    {
        if (!validateGrappleOpportunities)
        {
            return;
        }

        for (int chunkIndex = 0; chunkIndex < chunkGrappleCounts.Count; chunkIndex++)
        {
            if (chunkIndex == 0)
            {
                continue;
            }

            int count = Mathf.Max(chunkGrappleCounts[chunkIndex], CountScannedGrappleTargets(chunkIndex));
            if (count >= minGrappleEdgesPerChunk)
            {
                continue;
            }

            Debug.LogWarning($"ProceduralMegastructureGenerator: chunk {chunkIndex:00} has low grapple edge score ({count}/{minGrappleEdgesPerChunk}).", this);
            if (!addEmergencyGrappleLedges || emergencyRoot == null)
            {
                continue;
            }

            int needed = Mathf.Max(1, Mathf.CeilToInt((targetGrappleEdgesPerChunk - count) / 6f));
            for (int i = 0; i < needed; i++)
            {
                float y = -chunkIndex * chunkHeight - RandomRange(rng, 1.2f, chunkHeight - 1.2f);
                CreateWallLedge(emergencyRoot, rng, y, chunkIndex, grappleLayer, true);
            }
        }

        ValidateVerticalGaps();
    }

    private void ValidateVerticalGaps()
    {
        if (grappleTargetPoints.Count == 0)
        {
            return;
        }

        grappleTargetPoints.Sort((a, b) => b.y.CompareTo(a.y));
        for (int i = 1; i < grappleTargetPoints.Count; i++)
        {
            float gap = Mathf.Abs(grappleTargetPoints[i - 1].y - grappleTargetPoints[i].y);
            if (gap > maxVerticalGapWithoutGrappleTarget)
            {
                Debug.LogWarning($"ProceduralMegastructureGenerator: vertical gap without grapple target may be too large: {gap:F1}m near Y={grappleTargetPoints[i].y:F1}.", this);
            }
        }
    }

    private void ValidateGrappleSummaryOnly()
    {
        int lowChunks = 0;
        for (int i = 0; i < chunkGrappleCounts.Count; i++)
        {
            if (i == 0)
            {
                continue;
            }

            int scannedCount = CountScannedGrappleTargets(i);
            if (Mathf.Max(chunkGrappleCounts[i], scannedCount) < minGrappleEdgesPerChunk)
            {
                lowChunks++;
            }
        }

        Debug.Log($"ProceduralMegastructureGenerator grapple validation: chunks={chunkGrappleCounts.Count}, lowChunks={lowChunks}, targetPoints={grappleTargetPoints.Count}, emergencyLedges={generatedEmergencyLedgeCount}", this);
    }

    private int CountScannedGrappleTargets(int chunkIndex)
    {
        float chunkCenterY = -chunkIndex * chunkHeight - chunkHeight * 0.5f;
        float halfStep = Mathf.Max(0.5f, grappleScanStepY * 0.5f);
        float radius = Mathf.Max(1f, grappleScanRadius);
        int count = 0;

        for (int i = 0; i < grappleTargetPoints.Count; i++)
        {
            Vector3 point = grappleTargetPoints[i];
            if (Mathf.Abs(point.y - chunkCenterY) > halfStep)
            {
                continue;
            }

            if (new Vector2(point.x, point.z).magnitude <= radius)
            {
                count++;
            }
        }

        return count;
    }

    private void ValidateStructuralAnchoring()
    {
        int orphanCount = 0;
        int majorCount = 0;
        int secondaryCount = 0;
        int repeatedKindRuns = 0;
        GeneratedStructureLibrary.StructureKind previousKind = GeneratedStructureLibrary.StructureKind.BrokenPlatformCluster;
        int currentKindRun = 0;

        for (int i = structureRecords.Count - 1; i >= 0; i--)
        {
            GeneratedStructureRecord record = structureRecords[i];
            if (record == null || record.Root == null)
            {
                continue;
            }

            bool supportedByWall = record.Anchor.AnchorType == AnchorType.ShaftWall && IsBoundsCloseToWall(record.Bounds, anchorSearchRadius);
            bool supportedByParent = record.ParentStructure != null || record.Anchor.ParentStructure != null;
            bool valid = !requireStructuralAnchoring || allowIsolatedFloatingPlatforms || supportedByWall || supportedByParent || record.IsMajor;
            record.IsOrphan = !valid;

            if (record.IsMajor)
            {
                majorCount++;
            }
            else
            {
                secondaryCount++;
            }

            if (i == structureRecords.Count - 1 || record.Kind != previousKind)
            {
                currentKindRun = 1;
            }
            else
            {
                currentKindRun++;
                if (currentKindRun >= 4)
                {
                    repeatedKindRuns++;
                }
            }

            previousKind = record.Kind;

            if (!record.IsOrphan)
            {
                continue;
            }

            orphanCount++;
            if (removeOrphanStructures)
            {
                if (Application.isPlaying)
                {
                    Destroy(record.Root);
                }
                else
                {
                    DestroyImmediate(record.Root);
                }

                structureRecords.RemoveAt(i);
                removedOrphanStructureCount++;
            }
        }

        if (validateShapeVariety && repeatedKindRuns > 0)
        {
            Debug.LogWarning($"ProceduralMegastructureGenerator: shape variety warning, repeated archetype runs={repeatedKindRuns}. Increase structureAnchorDensity/shapeVariationAmount or tune weights.", this);
        }

        if (validateAnchoredGrappleability)
        {
            ValidateGrappleSummaryOnly();
        }

        Debug.Log(
            $"ProceduralMegastructureGenerator anchoring validation: records={structureRecords.Count}, major={majorCount}, secondary={secondaryCount}, wallChains={generatedWallAttachedChainCount}, structureChains={generatedStructureAttachedChainCount}, orphans={orphanCount}, removed={removedOrphanStructureCount}, anchors={structureAnchors.Count}",
            this);
    }

    private bool IsBoundsCloseToWall(Bounds bounds, float tolerance)
    {
        Vector2 horizontal = new(bounds.center.x, bounds.center.z);
        float outer = horizontal.magnitude + Mathf.Max(bounds.extents.x, bounds.extents.z);
        return outer >= shaftRadius - Mathf.Max(0.5f, tolerance);
    }

    private void CreateFinalArena(Transform finalRoot, int grappleLayer)
    {
        lastFinalArenaPosition = new Vector3(0f, -levelHeight, 0f);
        FinalPedestalArena.Build(new FinalPedestalArena.BuildSettings
        {
            Parent = finalRoot,
            Center = lastFinalArenaPosition,
            GrappleLayer = grappleLayer,
            ArenaMaterial = finalArenaMaterial != null ? finalArenaMaterial : criticalPathMaterial,
            ColumnMaterial = ledgeMaterial != null ? ledgeMaterial : platformMaterial,
            VictoryMaterial = victorySphereMaterial,
            ArenaRadius = finalArenaRadius,
            PedestalRadius = pedestalRadius,
            PedestalHeight = pedestalHeight,
            ColumnCount = finalColumnCount,
            ColumnRadius = finalColumnRadius,
            ColumnHeight = finalColumnHeight,
            VictorySphereRadius = victorySphereRadius,
            VictorySphereColor = victorySphereColor,
            InteractKey = interactKey
        });

        RegisterGrappleTarget(lastFinalArenaPosition, Mathf.Max(0, lastChunkCount - 1), 18);
    }

    private GameObject CreateCube(Transform parent, string objectName, Vector3 position, Quaternion rotation, Vector3 scale, Material material, int layer, int chunkIndex, int grappleScore)
    {
        GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        gameObject.name = objectName;
        gameObject.transform.SetParent(parent, false);
        gameObject.transform.SetPositionAndRotation(position, rotation);
        gameObject.transform.localScale = scale;
        SetLayerRecursively(gameObject, layer);

        MeshRenderer renderer = gameObject.GetComponent<MeshRenderer>();
        if (renderer != null && material != null)
        {
            renderer.sharedMaterial = material;
        }

        generatedStructureCount++;
        Collider collider = gameObject.GetComponent<Collider>();
        if (collider != null)
        {
            Physics.SyncTransforms();
            generatedStructureBounds.Add(collider.bounds);
        }

        RegisterGrappleTarget(position, chunkIndex, grappleScore);
        return gameObject;
    }

    private GameObject CreateLocalCube(Transform parent, string objectName, Vector3 localPosition, Quaternion localRotation, Vector3 localScale, Material material, int layer)
    {
        GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        gameObject.name = objectName;
        gameObject.transform.SetParent(parent, false);
        gameObject.transform.localPosition = localPosition;
        gameObject.transform.localRotation = localRotation;
        gameObject.transform.localScale = localScale;
        SetLayerRecursively(gameObject, layer);

        MeshRenderer renderer = gameObject.GetComponent<MeshRenderer>();
        if (renderer != null && material != null)
        {
            renderer.sharedMaterial = material;
        }

        generatedStructureCount++;
        Collider collider = gameObject.GetComponent<Collider>();
        if (collider != null)
        {
            Physics.SyncTransforms();
            generatedStructureBounds.Add(collider.bounds);
        }

        return gameObject;
    }

    private void RegisterGrappleTarget(Vector3 position, int chunkIndex, int edgeScore)
    {
        if (edgeScore <= 0)
        {
            return;
        }

        grappleTargetPoints.Add(position);
        if (chunkIndex >= 0 && chunkIndex < chunkGrappleCounts.Count)
        {
            chunkGrappleCounts[chunkIndex] += Mathf.Max(1, edgeScore);
        }
    }

    private Vector3 RandomPointInShaft(System.Random rng, float y, float padding)
    {
        float maxRadius = Mathf.Max(1f, shaftRadius - padding);
        float minRadius = Mathf.Clamp(shaftRadius * centralVoidBias, 0f, maxRadius * 0.8f);
        float radius = RandomRange(rng, minRadius, maxRadius);
        float angle = RandomRange(rng, 0f, Mathf.PI * 2f);
        return new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
    }

    private bool CanPlaceStructureAt(Vector3 position, bool major)
    {
        Vector2 horizontal = new(position.x, position.z);
        if (position.y > -chunkHeight * 1.25f && horizontal.magnitude < startSafeRadius)
        {
            return false;
        }

        if (position.y < -levelHeight + chunkHeight * 1.8f && horizontal.magnitude < Mathf.Max(endArenaRadius, finalArenaRadius))
        {
            return false;
        }

        float minDistanceSquared = minStructureDistance * minStructureDistance;
        for (int i = 0; i < generatedStructureBounds.Count; i++)
        {
            if ((generatedStructureBounds[i].center - position).sqrMagnitude < minDistanceSquared)
            {
                return false;
            }
        }

        if (major)
        {
            float majorDistanceSquared = minDistanceBetweenMajorStructures * minDistanceBetweenMajorStructures;
            for (int i = 0; i < majorStructureBounds.Count; i++)
            {
                if ((majorStructureBounds[i].center - position).sqrMagnitude < majorDistanceSquared)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private Vector3 FindPlayerStartPoint()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            player = GameObject.Find("Player");
        }

        return player != null ? player.transform.position : Vector3.zero;
    }

    private void ValidateCriticalPathReachability()
    {
        if (criticalDescentPoints.Count < 2)
        {
            return;
        }

        for (int i = 1; i < criticalDescentPoints.Count; i++)
        {
            Vector3 previous = criticalDescentPoints[i - 1];
            Vector3 current = criticalDescentPoints[i];
            float distance = Vector3.Distance(previous, current);
            float verticalDrop = Mathf.Abs(previous.y - current.y);
            float horizontalGap = Vector2.Distance(new Vector2(previous.x, previous.z), new Vector2(current.x, current.z));

            if (distance > assumedMaxGrappleDistance || verticalDrop > maxSafeVerticalDrop || horizontalGap > maxHorizontalGap)
            {
                Debug.LogWarning($"ProceduralMegastructureGenerator: critical support gap {i - 1}->{i} may be too large. distance={distance:F1}, vertical={verticalDrop:F1}, horizontal={horizontalGap:F1}", this);
            }
        }

        Vector3 lastCritical = criticalDescentPoints[criticalDescentPoints.Count - 1];
        float finalDistance = Vector3.Distance(lastCritical, lastFinalArenaPosition);
        float finalReachDistance = Mathf.Max(assumedMaxGrappleDistance, assumedMaxGrappleDistance + finalArenaRadius);
        if (finalDistance > finalReachDistance)
        {
            Debug.LogWarning($"ProceduralMegastructureGenerator: final arena may be too far from last critical support. distance={finalDistance:F1}", this);
        }
    }

    private string BuildSummary(string prefix)
    {
        return $"ProceduralMegastructureGenerator {prefix}: seed={seed}, levelHeight={levelHeight:F0}, shaftRadius={shaftRadius:F0}, chunks={lastChunkCount}, structures={generatedStructureCount}, complexStructures={generatedComplexStructureCount}, hugeStructures={generatedHugeStructureCount}, smallPlatforms={generatedSmallPlatformCount}, criticalSupports={generatedCriticalCount}, wallLedges={generatedWallLedgeCount}, emergencyLedges={generatedEmergencyLedgeCount}, wallChains={generatedWallAttachedChainCount}, structureChains={generatedStructureAttachedChainCount}, secondaryAttachments={generatedSecondaryAttachmentCount}, removedOrphans={removedOrphanStructureCount}, emptyChunks={emptyChunkCount}, finalArena={lastFinalArenaPosition:F1}";
    }

    private void EnsureFallbackMaterials()
    {
        shaftWallMaterial ??= CreateFallbackMaterial("Generated Ancient Shaft Stone", new Color(0.2f, 0.2f, 0.19f));
        platformMaterial ??= CreateFallbackMaterial("Generated Weathered Platform Stone", new Color(0.32f, 0.31f, 0.29f));
        ledgeMaterial ??= CreateFallbackMaterial("Generated Pale Broken Edge Stone", new Color(0.42f, 0.4f, 0.36f));
        criticalPathMaterial ??= CreateFallbackMaterial("Generated Critical Grapple Stone", new Color(0.36f, 0.37f, 0.34f));
        darkRuinMaterial ??= CreateFallbackMaterial("Generated Dark Ruin Stone", new Color(0.14f, 0.15f, 0.15f));
        finalArenaMaterial ??= CreateFallbackMaterial("Generated Final Arena Stone", new Color(0.27f, 0.28f, 0.27f));
        victorySphereMaterial ??= CreateEmissiveMaterial("Generated Victory Sphere Emissive", victorySphereColor);
    }

    private static Material CreateFallbackMaterial(string materialName, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new(shader)
        {
            name = materialName
        };

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }
        else if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        return material;
    }

    private static Material CreateEmissiveMaterial(string materialName, Color color)
    {
        Material material = CreateFallbackMaterial(materialName, color);
        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 3f);
        }

        return material;
    }

    private static Transform CreateChildRoot(Transform parent, string objectName)
    {
        GameObject child = new(objectName);
        child.transform.SetParent(parent, false);
        return child.transform;
    }

    private static int ResolveLayer(string layerName, int fallback)
    {
        int layer = string.IsNullOrWhiteSpace(layerName) ? -1 : LayerMask.NameToLayer(layerName);
        return layer >= 0 ? layer : Mathf.Max(0, fallback);
    }

    private static void SetLayerRecursively(GameObject gameObject, int layer)
    {
        gameObject.layer = layer;
        for (int i = 0; i < gameObject.transform.childCount; i++)
        {
            SetLayerRecursively(gameObject.transform.GetChild(i).gameObject, layer);
        }
    }

    private static float EvaluateCurve(AnimationCurve curve, float time, float fallback)
    {
        return curve != null && curve.length > 0 ? curve.Evaluate(time) : fallback;
    }

    private static float FractalNoise(float x, float y, int octaves)
    {
        int count = Mathf.Clamp(octaves, 1, 5);
        float total = 0f;
        float amplitude = 1f;
        float frequency = 1f;
        float amplitudeSum = 0f;
        for (int i = 0; i < count; i++)
        {
            total += Mathf.PerlinNoise(x * frequency, y * frequency) * amplitude;
            amplitudeSum += amplitude;
            amplitude *= 0.5f;
            frequency *= 2f;
        }

        return amplitudeSum > 0f ? total / amplitudeSum : 0.5f;
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

    private void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos)
        {
            return;
        }

        Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.25f);
        Vector3 topCenter = Vector3.zero;
        Vector3 bottomCenter = new(0f, -levelHeight, 0f);
        DrawCircle(topCenter, shaftRadius, 64);
        DrawCircle(bottomCenter, shaftRadius, 64);
        Gizmos.DrawLine(new Vector3(shaftRadius, 0f, 0f), new Vector3(shaftRadius, -levelHeight, 0f));
        Gizmos.DrawLine(new Vector3(-shaftRadius, 0f, 0f), new Vector3(-shaftRadius, -levelHeight, 0f));
        Gizmos.DrawLine(new Vector3(0f, 0f, shaftRadius), new Vector3(0f, -levelHeight, shaftRadius));
        Gizmos.DrawLine(new Vector3(0f, 0f, -shaftRadius), new Vector3(0f, -levelHeight, -shaftRadius));

        Gizmos.color = new Color(0.4f, 0.4f, 1f, 0.16f);
        for (float y = 0f; y >= -levelHeight; y -= Mathf.Max(1f, chunkHeight))
        {
            DrawCircle(new Vector3(0f, y, 0f), shaftRadius, 48);
        }

        Gizmos.color = Color.yellow;
        for (int i = 0; i < criticalDescentPoints.Count; i++)
        {
            Gizmos.DrawWireSphere(criticalDescentPoints[i], 0.6f);
            if (i > 0)
            {
                Gizmos.DrawLine(criticalDescentPoints[i - 1], criticalDescentPoints[i]);
            }
        }

        Gizmos.color = Color.green;
        for (int i = 0; i < grappleTargetPoints.Count; i++)
        {
            Gizmos.DrawWireSphere(grappleTargetPoints[i], 0.18f);
        }

        Gizmos.color = new Color(1f, 0.4f, 0.15f, 0.2f);
        for (int i = 0; i < generatedStructureBounds.Count; i++)
        {
            Gizmos.DrawWireCube(generatedStructureBounds[i].center, generatedStructureBounds[i].size);
        }

        Gizmos.color = Color.cyan;
        for (int i = 0; i < wallAnchors.Count; i++)
        {
            Gizmos.DrawWireSphere(wallAnchors[i].Position, 0.28f);
            Gizmos.DrawLine(wallAnchors[i].Position, wallAnchors[i].Position + wallAnchors[i].Normal.normalized * 1.2f);
        }

        for (int i = 0; i < structureRecords.Count; i++)
        {
            GeneratedStructureRecord record = structureRecords[i];
            if (record == null || record.Root == null)
            {
                continue;
            }

            Gizmos.color = record.IsOrphan ? Color.red : record.IsMajor ? Color.magenta : Color.white;
            Gizmos.DrawLine(record.Anchor.Position, record.Bounds.center);
            Gizmos.DrawWireCube(record.Bounds.center, record.Bounds.size);
        }

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(lastFinalArenaPosition, finalArenaRadius);
    }

    private static void DrawCircle(Vector3 center, float radius, int segments)
    {
        Vector3 previous = center + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float angle = Mathf.PI * 2f * i / segments;
            Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }
}
