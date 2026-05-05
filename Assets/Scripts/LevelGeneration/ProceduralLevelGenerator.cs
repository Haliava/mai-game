using System.Collections.Generic;
using UnityEngine;

public class ProceduralLevelGenerator : MonoBehaviour
{
    [SerializeField] Vector3 chunkSize = new Vector3(80f, 40f, 80f);
    [SerializeField] int minPlatformsPerChunk = 8;
    [SerializeField] int maxPlatformsPerChunk = 18;
    [SerializeField] int minColumnsPerChunk = 3;
    [SerializeField] int maxColumnsPerChunk = 9;
    [SerializeField] int minRuinsPerChunk = 2;
    [SerializeField] int maxRuinsPerChunk = 6;
    [SerializeField] GameObject[] structurePrefabs;
    [SerializeField] GameObject grapplePointPrefab;
    [SerializeField] int seed = 12345;
    [SerializeField] bool useRandomSeed = true;
    [SerializeField] int gameAreaDiameterInChunks = 2;
    [SerializeField] float gameAreaBoundaryMargin = 8f;
    [SerializeField] float wallEmbedDepth = 8f;
    [SerializeField, Range(0f, 1f)] float wallEmbeddedObstacleChance = 0.45f;
    [SerializeField] float maxReachableDistance = 28f;
    [SerializeField] float verticalStepMin = 5f;
    [SerializeField] float verticalStepMax = 14f;
    [SerializeField] float centerClearRadius = 24f;
    [SerializeField] float centerSparseRadius = 38f;
    [SerializeField, Range(0f, 1f)] float centerSpawnChance = 0.02f;
    [SerializeField] Vector3 startPlatformPosition = new Vector3(46f, 0f, 0f);
    [SerializeField] Vector3 playerStartPosition = new Vector3(46f, 2.1f, 0f);
    [SerializeField] int maxRandomPointAttempts = 16;
    [SerializeField, Range(0f, 1f)] float overlappingStructureChance = 0.35f;
    [SerializeField] int maxOverlappingStructures = 1;
    [SerializeField] float overlappingStructureMinRadius = 6f;
    [SerializeField] float overlappingStructureRadius = 11f;
    [SerializeField] Material stoneMaterial;
    [SerializeField] Material grappleMaterial;
    [SerializeField] bool clearBakedChunksOnAwake = true;

    readonly Dictionary<int, LevelChunk> chunks = new Dictionary<int, LevelChunk>();
    readonly List<NavigationNode> allNodes = new List<NavigationNode>();
    int runtimeSeed;

    public IReadOnlyList<NavigationNode> AllNodes { get { return allNodes; } }
    public Vector3 ChunkSize { get { return chunkSize; } }
    public float GameAreaRadius { get { return chunkSize.x * gameAreaDiameterInChunks * 0.5f; } }
    public float SpawnAreaRadius { get { return Mathf.Max(8f, GameAreaRadius - gameAreaBoundaryMargin); } }
    public Vector3 PlayerStartPosition { get { return playerStartPosition; } }

    void Awake()
    {
        if (clearBakedChunksOnAwake) ClearBakedChunks();
        runtimeSeed = useRandomSeed ? Random.Range(int.MinValue, int.MaxValue) : seed;
    }

    void ClearBakedChunks()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child == null || !child.name.StartsWith("LevelChunk_")) continue;

            if (Application.isPlaying) Destroy(child.gameObject);
            else DestroyImmediate(child.gameObject);
        }

        chunks.Clear();
        allNodes.Clear();
    }

    public LevelChunk GenerateChunk(int chunkIndex)
    {
        LevelChunk existing;
        if (chunks.TryGetValue(chunkIndex, out existing)) return existing;

        Random.InitState(runtimeSeed + chunkIndex * 7919);
        GameObject root = new GameObject("LevelChunk_" + chunkIndex);
        root.transform.SetParent(transform, false);
        LevelChunk chunk = root.AddComponent<LevelChunk>();
        chunk.chunkIndex = chunkIndex;
        chunk.chunkSize = chunkSize;
        chunk.chunkOrigin = new Vector3(0f, -chunkIndex * chunkSize.y, 0f);
        chunks.Add(chunkIndex, chunk);

        List<Vector3> route = BuildRoute(chunk);
        for (int i = 0; i < route.Count; i++) CreatePlatform(chunk, route[i], Random.Range(7f, 13f), Random.Range(7f, 13f), true);

        int platformCount = Mathf.Max(1, Mathf.RoundToInt(Random.Range(minPlatformsPerChunk, maxPlatformsPerChunk + 1) * 0.45f));
        for (int i = 0; i < platformCount; i++)
        {
            Vector3 p = RandomPointInChunk(chunk, true);
            CreatePlatform(chunk, p, Random.Range(4f, 14f), Random.Range(4f, 14f), Random.value < 0.45f);
        }

        int columnCount = Mathf.Max(1, Mathf.RoundToInt(Random.Range(minColumnsPerChunk, maxColumnsPerChunk + 1) * 0.35f));
        for (int i = 0; i < columnCount; i++) CreateColumn(chunk, RandomPointInChunk(chunk, true, true));

        int ruinCount = Mathf.Max(1, Mathf.RoundToInt(Random.Range(minRuinsPerChunk, maxRuinsPerChunk + 1) * 0.35f));
        for (int i = 0; i < ruinCount; i++) CreateRuinCluster(chunk, RandomPointInChunk(chunk, true, true));

        ConnectNearbyNodes(chunk.navigationNodes);
        return chunk;
    }

    public void SetChunkActive(int chunkIndex, bool active)
    {
        LevelChunk chunk;
        if (chunks.TryGetValue(chunkIndex, out chunk) && chunk != null) chunk.gameObject.SetActive(active);
    }

    public bool HasChunk(int chunkIndex)
    {
        return chunks.ContainsKey(chunkIndex);
    }

    List<Vector3> BuildRoute(LevelChunk chunk)
    {
        List<Vector3> route = new List<Vector3>();
        Vector2 startPlanar;
        Vector3 current;
        if (chunk.chunkIndex == 0)
        {
            current = ClampToGameArea(startPlatformPosition);
            startPlanar = new Vector2(current.x, current.z).normalized;
            if (startPlanar.sqrMagnitude < 0.01f) startPlanar = Vector2.right;
        }
        else
        {
            startPlanar = Random.insideUnitCircle.normalized;
            if (startPlanar.sqrMagnitude < 0.01f) startPlanar = Vector2.right;
            startPlanar *= Random.Range(centerSparseRadius, Mathf.Min(SpawnAreaRadius, centerSparseRadius + 18f));
            current = chunk.chunkOrigin + new Vector3(startPlanar.x, -3f, startPlanar.y);
        }
        route.Add(current);
        float bottom = chunk.chunkOrigin.y - chunkSize.y + 5f;
        while (current.y > bottom)
        {
            Vector2 planar = Random.insideUnitCircle.normalized * Random.Range(8f, Mathf.Min(maxReachableDistance * 0.75f, 20f));
            current += new Vector3(planar.x, -Random.Range(verticalStepMin, verticalStepMax), planar.y);
            current = ClampToGameArea(current);
            if (!CanSpawnNearCenter(current))
            {
                Vector2 outward = new Vector2(current.x, current.z).normalized;
                if (outward.sqrMagnitude < 0.01f) outward = startPlanar.normalized;
                current.x = outward.x * centerSparseRadius;
                current.z = outward.y * centerSparseRadius;
                current = ClampToGameArea(current);
            }
            route.Add(current);
        }
        return route;
    }

    Vector3 RandomPointInChunk(LevelChunk chunk, bool avoidCenter)
    {
        return RandomPointInChunk(chunk, avoidCenter, false);
    }

    Vector3 RandomPointInChunk(LevelChunk chunk, bool avoidCenter, bool allowWallEmbed)
    {
        Vector3 point = chunk.chunkOrigin;
        for (int i = 0; i < maxRandomPointAttempts; i++)
        {
            float radius = GetRandomSpawnRadius(allowWallEmbed);
            Vector2 planar = Random.insideUnitCircle.normalized * Random.Range(0f, radius);
            point = chunk.chunkOrigin + new Vector3(
                planar.x,
                Random.Range(-chunkSize.y + 4f, -2f),
                planar.y);

            if (!avoidCenter || CanSpawnNearCenter(point)) return point;
        }

        Vector2 direction = Random.insideUnitCircle.normalized;
        if (direction.sqrMagnitude < 0.01f) direction = Vector2.right;
        float fallbackRadius = GetRandomSpawnRadius(allowWallEmbed);
        return chunk.chunkOrigin + new Vector3(
            direction.x * Random.Range(centerSparseRadius, fallbackRadius),
            Random.Range(-chunkSize.y + 4f, -2f),
            direction.y * Random.Range(centerSparseRadius, fallbackRadius));
    }

    float GetRandomSpawnRadius(bool allowWallEmbed)
    {
        if (!allowWallEmbed || Random.value > wallEmbeddedObstacleChance) return SpawnAreaRadius;
        return GameAreaRadius + wallEmbedDepth;
    }

    Vector3 ClampToGameArea(Vector3 point)
    {
        return ClampToRadius(point, SpawnAreaRadius);
    }

    Vector3 ClampToObstacleArea(Vector3 point)
    {
        return ClampToRadius(point, GameAreaRadius + wallEmbedDepth);
    }

    Vector3 ClampToRadius(Vector3 point, float radius)
    {
        Vector2 planar = new Vector2(point.x, point.z);
        if (planar.magnitude > radius)
        {
            planar = planar.normalized * radius;
            point.x = planar.x;
            point.z = planar.y;
        }
        return point;
    }

    bool CanSpawnNearCenter(Vector3 point)
    {
        float planarDistance = new Vector2(point.x, point.z).magnitude;
        if (planarDistance < centerClearRadius) return false;
        if (planarDistance < centerSparseRadius) return Random.value <= centerSpawnChance;
        return true;
    }

    GameObject CreatePlatform(LevelChunk chunk, Vector3 position, float width, float depth, bool addGrapple)
    {
        GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
        platform.name = "Platform";
        platform.transform.SetParent(chunk.transform, true);
        platform.transform.position = position;
        platform.transform.localScale = new Vector3(width, Random.Range(1f, 2.6f), depth);
        ApplyMaterial(platform, stoneMaterial);

        NavigationNode node = new GameObject("NavigationNode").AddComponent<NavigationNode>();
        node.transform.SetParent(platform.transform, false);
        node.transform.localPosition = Vector3.up * 0.75f;
        node.Platform = platform;
        node.ChunkIndex = chunk.chunkIndex;
        chunk.navigationNodes.Add(node);
        allNodes.Add(node);

        if (addGrapple) CreateGrapplePoint(chunk, ClampToGameArea(position + new Vector3(Random.Range(-width * 0.35f, width * 0.35f), 2.2f, Random.Range(-depth * 0.35f, depth * 0.35f))));
        return platform;
    }

    void CreateGrapplePoint(LevelChunk chunk, Vector3 position)
    {
        GameObject point = grapplePointPrefab != null ? Instantiate(grapplePointPrefab) : GameObject.CreatePrimitive(PrimitiveType.Sphere);
        point.name = "GrapplePoint";
        point.transform.SetParent(chunk.transform, true);
        point.transform.position = position;
        point.transform.localScale = Vector3.one * 0.65f;
        if (point.GetComponent<GrapplePoint>() == null) point.AddComponent<GrapplePoint>();
        ApplyMaterial(point, grappleMaterial);
        chunk.grapplePoints.Add(point.transform);
    }

    void CreateColumn(LevelChunk chunk, Vector3 position)
    {
        GameObject column = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        column.name = "WrapColumn";
        column.transform.SetParent(chunk.transform, true);
        float height = Random.Range(10f, chunkSize.y);
        column.transform.position = position + Vector3.down * height * 0.4f;
        column.transform.localScale = new Vector3(Random.Range(1f, 4f), height * 0.5f, Random.Range(1f, 4f));
        if (column.GetComponent<RopeCollisionWrapper>() == null) column.AddComponent<RopeCollisionWrapper>();
        ApplyMaterial(column, stoneMaterial);
    }

    void CreateRuinCluster(LevelChunk chunk, Vector3 position)
    {
        CreateRuin(chunk, position, 1f);

        if (Random.value > overlappingStructureChance) return;

        int overlapCount = Random.Range(1, maxOverlappingStructures + 1);
        for (int i = 0; i < overlapCount; i++)
        {
            Vector2 direction = Random.insideUnitCircle.normalized;
            if (direction.sqrMagnitude < 0.01f) direction = Vector2.right;
            Vector2 offset = direction * Random.Range(overlappingStructureMinRadius, overlappingStructureRadius);
            Vector3 overlapPosition = position + new Vector3(offset.x, Random.Range(-0.6f, 1.4f), offset.y);
            overlapPosition = ClampToObstacleArea(overlapPosition);
            CreateRuin(chunk, overlapPosition, Random.Range(0.85f, 1.2f));
        }
    }

    void CreateRuin(LevelChunk chunk, Vector3 position, float scaleMultiplier)
    {
        if (structurePrefabs != null && structurePrefabs.Length > 0 && structurePrefabs[0] != null)
        {
            GameObject prefab = structurePrefabs[Random.Range(0, structurePrefabs.Length)];
            GameObject instance = Instantiate(prefab, position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), chunk.transform);
            instance.name = prefab.name + "_Instance";
            instance.transform.localScale *= scaleMultiplier;
            return;
        }

        GameObject root = new GameObject("PrototypeRuin");
        root.transform.SetParent(chunk.transform, true);
        root.transform.position = position;
        int blocks = Random.Range(3, 7);
        for (int i = 0; i < blocks; i++)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.transform.SetParent(root.transform, false);
            block.transform.localPosition = new Vector3(Random.Range(-4f, 4f), i * 1.2f, Random.Range(-4f, 4f));
            block.transform.localScale = new Vector3(Random.Range(1f, 4f), Random.Range(1f, 3f), Random.Range(1f, 4f)) * scaleMultiplier;
            block.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            ApplyMaterial(block, stoneMaterial);
        }
    }

    void ConnectNearbyNodes(List<NavigationNode> nodes)
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            for (int j = i + 1; j < nodes.Count; j++)
            {
                if (Vector3.Distance(nodes[i].Position, nodes[j].Position) <= maxReachableDistance)
                {
                    nodes[i].Connect(nodes[j]);
                }
            }
        }
    }

    void ApplyMaterial(GameObject go, Material mat)
    {
        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer != null && mat != null) renderer.sharedMaterial = mat;
    }
}
