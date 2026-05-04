using UnityEngine;

public class GrapplePointSpawner : MonoBehaviour
{
    [SerializeField] GameObject grapplePointPrefab;

    public GrapplePoint Spawn(Vector3 position, Transform parent)
    {
        GameObject instance = grapplePointPrefab != null ? Instantiate(grapplePointPrefab, position, Quaternion.identity, parent) : GameObject.CreatePrimitive(PrimitiveType.Sphere);
        instance.name = "GrapplePoint";
        instance.transform.position = position;
        instance.transform.localScale = Vector3.one * 0.65f;
        GrapplePoint point = instance.GetComponent<GrapplePoint>();
        if (point == null) point = instance.AddComponent<GrapplePoint>();
        return point;
    }
}
