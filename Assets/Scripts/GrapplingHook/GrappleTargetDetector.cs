using UnityEngine;

public class GrappleTargetDetector : MonoBehaviour
{
    [SerializeField] Transform cameraTransform;
    [SerializeField] float detectionRange = 35f;
    [SerializeField] LayerMask grappleMask = ~0;

    public GrapplePoint CurrentTarget { get; private set; }

    void Update()
    {
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        CurrentTarget = null;
        if (cameraTransform == null) return;

        RaycastHit hit;
        if (Physics.Raycast(cameraTransform.position, cameraTransform.forward, out hit, detectionRange, grappleMask, QueryTriggerInteraction.Ignore))
        {
            CurrentTarget = hit.collider.GetComponentInParent<GrapplePoint>();
        }
    }
}
