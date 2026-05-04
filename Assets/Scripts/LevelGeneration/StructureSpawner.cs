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
        return instance;
    }
}
