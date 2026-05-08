using UnityEngine;

public class StructureSpawner : MonoBehaviour
{
    [SerializeField] GameObject[] prefabs;
    [SerializeField] Vector2 scaleRange = new Vector2(0.8f, 1.4f);

    public GameObject Spawn(Vector3 position, Transform parent)
    {
        if (prefabs == null || prefabs.Length == 0) return null;
        GameObject prefab = prefabs[Random.Range(0, prefabs.Length)];
        if (prefab == null) return null;

        GameObject instance = Instantiate(prefab, position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), parent);
        float scale = Random.Range(scaleRange.x, scaleRange.y);
        instance.transform.localScale *= scale;
        PrepareGrappleGeometry(instance);
        return instance;
    }

    void PrepareGrappleGeometry(GameObject root)
    {
        Collider[] colliders = root.GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            GameObject go = colliders[i].gameObject;
            SetLayerIfExists(go, "GrappleGeometry", "GrappleSurface");
            if (go.GetComponent<GrappleSurface>() == null) go.AddComponent<GrappleSurface>();
            if (go.GetComponent<RopeCollisionWrapper>() == null) go.AddComponent<RopeCollisionWrapper>();
        }
    }

    void SetLayerIfExists(GameObject go, params string[] layerNames)
    {
        for (int i = 0; i < layerNames.Length; i++)
        {
            int layer = LayerMask.NameToLayer(layerNames[i]);
            if (layer < 0) continue;
            go.layer = layer;
            return;
        }
    }
}
