using UnityEngine;
using UnityEngine.Rendering;

public class MegastructureBoundaryBuilder : MonoBehaviour
{
    [SerializeField] ProceduralLevelGenerator generator;
    [SerializeField] Material wallMaterial;
    [SerializeField] int diameterInChunks = 2;
    [SerializeField] int wallSegments = 32;
    [SerializeField] float chunkHeight = 40f;
    [SerializeField] int heightInChunks = 12;
    [SerializeField] float wallThickness = 3f;
    [SerializeField] float wallTopY = 20f;
    [Header("Wall Visibility")]
    [SerializeField] Color wallBaseColor = new Color(0.09f, 0.1f, 0.11f, 1f);
    [SerializeField] Color wallEmissionColor = new Color(0.025f, 0.03f, 0.035f, 1f);
    [SerializeField] float wallEmissionIntensity = 0.25f;
    [SerializeField] float wallPanelVariation = 0.18f;
    [SerializeField] bool wallsReceiveShadows = true;
    [SerializeField] bool wallsCastShadows = true;
    [SerializeField] bool buildWallFillLights = true;
    [SerializeField] int wallFillLightCount = 5;
    [SerializeField] float wallFillLightIntensity = 0.55f;
    [SerializeField] float wallFillLightRangeMultiplier = 1.7f;
    [SerializeField] Color wallFillLightColor = new Color(0.48f, 0.55f, 0.62f, 1f);
    [SerializeField] bool buildBottomOccluder = true;
    [SerializeField] float bottomOccluderExtraDepth = 12f;
    [SerializeField] float bottomOccluderThickness = 3f;
    [SerializeField] Color bottomOccluderColor = new Color(0.002f, 0.002f, 0.004f, 1f);
    [SerializeField] bool buildOnStart = true;

    Transform wallRoot;

    void Start()
    {
        if (buildOnStart) Build();
    }

    public void Build()
    {
        if (generator == null) generator = GetComponent<ProceduralLevelGenerator>();
        if (wallRoot == null)
        {
            Transform existing = transform.Find("MegastructureBoundary");
            if (existing != null) wallRoot = existing;
        }
        if (wallRoot != null)
        {
            if (Application.isPlaying) Destroy(wallRoot.gameObject);
            else DestroyImmediate(wallRoot.gameObject);
        }

        GameObject root = new GameObject("MegastructureBoundary");
        root.transform.SetParent(transform, false);
        wallRoot = root.transform;

        float chunkWidth = generator != null ? generator.ChunkSize.x : 80f;
        float radius = chunkWidth * diameterInChunks * 0.5f;
        float height = chunkHeight * heightInChunks;
        float centerY = wallTopY - height * 0.5f;
        int segments = Mathf.Max(8, wallSegments);
        float chord = 2f * Mathf.PI * radius / segments * 1.05f;

        for (int i = 0; i < segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;
            Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "BoundaryWall_" + i.ToString("00");
            wall.transform.SetParent(wallRoot, false);
            wall.transform.position = radial * radius + Vector3.up * centerY;
            wall.transform.rotation = Quaternion.LookRotation(-radial, Vector3.up);
            wall.transform.localScale = new Vector3(chord, height, wallThickness);

            Renderer renderer = wall.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = CreateVisibleWallMaterial(i / (float)segments);
                renderer.receiveShadows = wallsReceiveShadows;
                renderer.shadowCastingMode = wallsCastShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            }
        }

        if (buildWallFillLights)
        {
            BuildWallFillLights(root.transform, radius, height, centerY);
        }

        if (buildBottomOccluder)
        {
            GameObject bottom = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bottom.name = "AbyssBottomOccluder";
            bottom.transform.SetParent(wallRoot, false);
            bottom.transform.position = Vector3.up * (wallTopY - height - bottomOccluderExtraDepth);
            bottom.transform.localScale = new Vector3(radius * 2.05f, bottomOccluderThickness * 0.5f, radius * 2.05f);

            Renderer renderer = bottom.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material material = wallMaterial != null ? new Material(wallMaterial) : new Material(Shader.Find("Standard"));
                material.color = bottomOccluderColor;
                renderer.sharedMaterial = material;
            }
        }
    }

    Material CreateVisibleWallMaterial(float panel01)
    {
        Material material = wallMaterial != null ? new Material(wallMaterial) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.name = "MegastructureWall_RuntimeVisible";
        float variation = 1f + (Mathf.PerlinNoise(panel01 * 9.3f, 0.41f) - 0.5f) * wallPanelVariation;
        Color baseColor = wallBaseColor * variation;
        baseColor.a = wallBaseColor.a;
        material.color = baseColor;

        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", baseColor);
        if (material.HasProperty("_Color")) material.SetColor("_Color", baseColor);

        Color emission = wallEmissionColor * Mathf.Max(0f, wallEmissionIntensity);
        if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", emission);
        material.EnableKeyword("_EMISSION");
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;

        if (material.HasProperty("_ReceiveShadows")) material.SetFloat("_ReceiveShadows", wallsReceiveShadows ? 1f : 0f);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.18f);
        if (material.HasProperty("_SpecularHighlights")) material.SetFloat("_SpecularHighlights", 0f);
        return material;
    }

    void BuildWallFillLights(Transform parent, float radius, float height, float centerY)
    {
        int count = Mathf.Max(1, wallFillLightCount);
        float top = centerY + height * 0.5f;
        float bottom = centerY - height * 0.5f;
        for (int i = 0; i < count; i++)
        {
            float t = count == 1 ? 0.5f : i / (float)(count - 1);
            GameObject lightObject = new GameObject("WallSoftFill_" + i.ToString("00"));
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.position = new Vector3(0f, Mathf.Lerp(top, bottom, t), 0f);

            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = wallFillLightColor;
            light.intensity = wallFillLightIntensity;
            light.range = radius * wallFillLightRangeMultiplier;
            light.shadows = LightShadows.None;
        }
    }

    void OnDrawGizmosSelected()
    {
        float chunkWidth = generator != null ? generator.ChunkSize.x : 80f;
        float radius = chunkWidth * diameterInChunks * 0.5f;
        Gizmos.color = new Color(0.7f, 0.9f, 1f, 0.45f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
