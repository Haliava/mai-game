using UnityEngine;

public class MegastructureBoundaryBuilder : MonoBehaviour
{
    [SerializeField] ProceduralLevelGenerator generator;
    [SerializeField] Material wallMaterial;
    [SerializeField] int diameterInChunks = 4;
    [SerializeField] int wallSegments = 32;
    [SerializeField] float chunkHeight = 40f;
    [SerializeField] int heightInChunks = 12;
    [SerializeField] float wallThickness = 3f;
    [SerializeField] float wallTopY = 20f;
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
            if (renderer != null && wallMaterial != null) renderer.sharedMaterial = wallMaterial;
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

    void OnDrawGizmosSelected()
    {
        float chunkWidth = generator != null ? generator.ChunkSize.x : 80f;
        float radius = chunkWidth * diameterInChunks * 0.5f;
        Gizmos.color = new Color(0.7f, 0.9f, 1f, 0.45f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
