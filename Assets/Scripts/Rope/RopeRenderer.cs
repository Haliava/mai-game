using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class RopeRenderer : MonoBehaviour
{
    [SerializeField] LineRenderer line;
    [SerializeField] float width = 0.018f;
    [SerializeField] Material material;

    void Awake()
    {
        if (line == null) line = GetComponent<LineRenderer>();
        line.widthMultiplier = width;
        if (material != null) line.material = material;
    }
}
