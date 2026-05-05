using UnityEngine;

public class GrapplePoint : MonoBehaviour
{
    [SerializeField] Transform attachTransform;
    [SerializeField] bool hideRenderer = true;
    [SerializeField] bool disableMarkerColliders = true;

    public Transform AttachTransform
    {
        get { return attachTransform != null ? attachTransform : transform; }
    }

    void Awake()
    {
        if (hideRenderer)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = false;
            }
        }

        if (disableMarkerColliders)
        {
            Collider[] colliders = GetComponentsInChildren<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(AttachTransform.position, 0.45f);
    }
}
