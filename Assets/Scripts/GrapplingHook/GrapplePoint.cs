using UnityEngine;

public class GrapplePoint : MonoBehaviour
{
    [SerializeField] Transform attachTransform;

    public Transform AttachTransform
    {
        get { return attachTransform != null ? attachTransform : transform; }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(AttachTransform.position, 0.45f);
    }
}
