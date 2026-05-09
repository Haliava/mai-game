using UnityEngine;

public static class FinalPedestalArena
{
    public struct BuildSettings
    {
        public Transform Parent;
        public Vector3 Center;
        public int GrappleLayer;
        public Material ArenaMaterial;
        public Material ColumnMaterial;
        public Material VictoryMaterial;
        public float ArenaRadius;
        public float PedestalRadius;
        public float PedestalHeight;
        public int ColumnCount;
        public float ColumnRadius;
        public float ColumnHeight;
        public float VictorySphereRadius;
        public Color VictorySphereColor;
        public KeyCode InteractKey;
    }

    public static GameObject Build(BuildSettings settings)
    {
        GameObject root = new("Final Pedestal Arena");
        root.transform.SetParent(settings.Parent, false);
        root.transform.position = settings.Center;

        int segments = 16;
        for (int i = 0; i < segments; i++)
        {
            float angle = Mathf.PI * 2f * i / segments;
            Vector3 radial = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vector3 position = radial * (settings.ArenaRadius * 0.5f);
            Quaternion rotation = Quaternion.LookRotation(radial, Vector3.up);
            GameObject slab = CreateCube(root.transform, $"Final Arena Slab {i:00}", position, rotation, new Vector3(settings.ArenaRadius * 0.42f, 0.8f, settings.ArenaRadius * 0.18f), settings.ArenaMaterial, settings.GrappleLayer);
            AddOutcrop(slab.transform, settings, i);
        }

        GameObject pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pedestal.name = "Victory Pedestal";
        pedestal.transform.SetParent(root.transform, false);
        pedestal.transform.localPosition = Vector3.up * (settings.PedestalHeight * 0.5f + 0.4f);
        pedestal.transform.localScale = new Vector3(settings.PedestalRadius * 2f, settings.PedestalHeight * 0.5f, settings.PedestalRadius * 2f);
        pedestal.layer = settings.GrappleLayer;
        AssignMaterial(pedestal, settings.ArenaMaterial);

        int columnCount = Mathf.Max(1, settings.ColumnCount);
        for (int i = 0; i < columnCount; i++)
        {
            float angle = Mathf.PI * 2f * i / columnCount;
            Vector3 radial = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            GameObject column = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            column.name = $"Final Arena Column {i + 1:00}";
            column.transform.SetParent(root.transform, false);
            column.transform.localPosition = radial * (settings.ArenaRadius * 0.68f) + Vector3.up * (settings.ColumnHeight * 0.5f + 0.4f);
            column.transform.localScale = new Vector3(settings.ColumnRadius * 2f, settings.ColumnHeight * 0.5f, settings.ColumnRadius * 2f);
            column.transform.localRotation = Quaternion.Euler(RandomSigned(i) * 2f, 0f, RandomSigned(i + 3) * 2f);
            column.layer = settings.GrappleLayer;
            AssignMaterial(column, settings.ColumnMaterial != null ? settings.ColumnMaterial : settings.ArenaMaterial);

            CreateCube(column.transform, "Column Grapple Collar", Vector3.up * 0.42f, Quaternion.identity, new Vector3(1.65f, 0.08f, 1.65f), settings.ColumnMaterial != null ? settings.ColumnMaterial : settings.ArenaMaterial, settings.GrappleLayer);
        }

        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "Victory Sphere";
        sphere.transform.SetParent(root.transform, false);
        sphere.transform.localPosition = Vector3.up * (settings.PedestalHeight + settings.VictorySphereRadius + 1.0f);
        sphere.transform.localScale = Vector3.one * (settings.VictorySphereRadius * 2f);
        sphere.layer = 0;
        Collider sphereCollider = sphere.GetComponent<Collider>();
        if (sphereCollider != null)
        {
            sphereCollider.isTrigger = true;
        }
        AssignMaterial(sphere, settings.VictoryMaterial != null ? settings.VictoryMaterial : CreateEmissiveMaterial(settings.VictorySphereColor));
        // replace legacy victory popup with descent trigger for endless mode
        sphere.AddComponent<DescentSphereTrigger>();
        // defaults: require touch from below, deactivate after trigger

        Light light = sphere.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = settings.VictorySphereColor;
        light.intensity = 3.5f;
        light.range = 9f;

        // explicit exit anchor for manager alignment (placed at the victory sphere position)
        GameObject exitAnchor = new("Level Exit Anchor");
        exitAnchor.transform.SetParent(root.transform, false);
        exitAnchor.transform.position = sphere.transform.position;

        return root;
    }

    private static void AddOutcrop(Transform parent, BuildSettings settings, int index)
    {
        float side = index % 2 == 0 ? 1f : -1f;
        CreateCube(parent, "Final Arena Grapple Lip", new Vector3(side * 0.48f, 0.85f, 0f), Quaternion.identity, new Vector3(0.18f, 0.22f, 0.85f), settings.ColumnMaterial != null ? settings.ColumnMaterial : settings.ArenaMaterial, settings.GrappleLayer);
    }

    private static GameObject CreateCube(Transform parent, string name, Vector3 localPosition, Quaternion localRotation, Vector3 localScale, Material material, int layer)
    {
        GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        gameObject.name = name;
        gameObject.transform.SetParent(parent, false);
        gameObject.transform.localPosition = localPosition;
        gameObject.transform.localRotation = localRotation;
        gameObject.transform.localScale = localScale;
        gameObject.layer = layer;
        AssignMaterial(gameObject, material);
        return gameObject;
    }

    private static void AssignMaterial(GameObject gameObject, Material material)
    {
        MeshRenderer renderer = gameObject.GetComponent<MeshRenderer>();
        if (renderer != null && material != null)
        {
            renderer.sharedMaterial = material;
        }
    }

    private static Material CreateEmissiveMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new(shader)
        {
            name = "Generated Victory Sphere Emissive"
        };
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }
        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 3f);
        }
        return material;
    }

    private static float RandomSigned(int seed)
    {
        return (Mathf.PerlinNoise(seed * 7.31f, seed * 2.17f) - 0.5f) * 2f;
    }

}
