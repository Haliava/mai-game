using UnityEngine;

public class GrapplingHookController : MonoBehaviour
{
    [SerializeField] Transform cameraTransform;
    [SerializeField] Transform ropeOrigin;
    [SerializeField] HookProjectile hookPrefab;
    [SerializeField] RopeController ropeController;
    [SerializeField] float hookFireSpeed = 45f;
    [SerializeField] float maxRopeLength = 35f;
    [SerializeField] LayerMask grappleMask = ~0;
    [SerializeField] LayerMask ropeCollisionMask = ~0;
    [SerializeField] float hookRadius = 0.15f;
    [SerializeField] float directAttachProbeRadius = 0.35f;
    [SerializeField] float hookSpawnForwardOffset = 0.75f;
    [SerializeField, Range(0f, 1f)] float minAttachSurfaceUpDot = 0.72f;

    HookProjectile activeHook;
    Vector3 fireOrigin;

    public GrappleState State { get; private set; }
    public Vector3 HookPosition { get { return activeHook != null ? activeHook.transform.position : Vector3.zero; } }

    void Awake()
    {
        State = GrappleState.Idle;
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
    }

    void Update()
    {
        if (PrototypeInput.FirePressed)
        {
            if (State == GrappleState.Idle) Fire();
        }

        if (State != GrappleState.Idle && !PrototypeInput.FireHeld)
        {
            CancelGrapple();
            return;
        }

        if (PrototypeInput.AltFirePressed) Detach();
        if (PrototypeInput.JumpPressed && (State == GrappleState.Attached || State == GrappleState.Wrapped)) Detach();

        if (State == GrappleState.Flying && activeHook != null)
        {
            if (Vector3.Distance(fireOrigin, activeHook.transform.position) >= maxRopeLength)
            {
                CancelGrapple();
            }
        }
    }

    public void Fire()
    {
        if (cameraTransform == null || hookPrefab == null) return;

        fireOrigin = cameraTransform.position;
        if (activeHook == null)
        {
            activeHook = Instantiate(hookPrefab);
            activeHook.transform.localScale = Vector3.one * hookRadius;
            IgnorePlayerCollision(activeHook);
        }

        State = GrappleState.Flying;
        Vector3 spawnPosition = (ropeOrigin != null ? ropeOrigin.position : cameraTransform.position) + cameraTransform.forward * hookSpawnForwardOffset;
        activeHook.Fire(this, spawnPosition, cameraTransform.forward * hookFireSpeed, grappleMask);
        if (ropeController != null) ropeController.BeginFiring(activeHook.transform, maxRopeLength);

        RaycastHit hit;
        if (TryFindDirectAttach(out hit))
        {
            GrapplePoint point = hit.collider.GetComponentInParent<GrapplePoint>();
            AttachAt(point != null ? point.AttachTransform.position : hit.point, point);
            activeHook.transform.position = point != null ? point.AttachTransform.position : hit.point;
            activeHook.Rigidbody.isKinematic = true;
            activeHook.Rigidbody.linearVelocity = Vector3.zero;
        }
    }

    bool TryFindDirectAttach(out RaycastHit attachHit)
    {
        RaycastHit[] hits = Physics.SphereCastAll(cameraTransform.position, directAttachProbeRadius, cameraTransform.forward, maxRopeLength, grappleMask, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider == null || hits[i].collider.GetComponentInParent<GrapplingHookController>() == this) continue;
            if (!IsAttachSurfaceAllowed(hits[i].normal)) continue;
            if (hits[i].collider.GetComponentInParent<GrapplePoint>() != null || hits[i].collider.gameObject.layer != gameObject.layer)
            {
                attachHit = hits[i];
                return true;
            }
        }

        attachHit = default(RaycastHit);
        return false;
    }

    public void AttachAt(Vector3 position, GrapplePoint point)
    {
        State = GrappleState.Attached;
        if (ropeController != null) ropeController.Attach(position, maxRopeLength, ropeCollisionMask);
    }

    public bool IsAttachSurfaceAllowed(Vector3 surfaceNormal)
    {
        return Vector3.Dot(surfaceNormal.normalized, Vector3.up) >= minAttachSurfaceUpDot;
    }

    public void CancelGrapple()
    {
        State = GrappleState.Idle;
        if (ropeController != null) ropeController.Detach();
        if (activeHook != null) activeHook.gameObject.SetActive(false);
    }

    public void Detach()
    {
        CancelGrapple();
    }

    void IgnorePlayerCollision(HookProjectile hook)
    {
        Collider hookCollider = hook.GetComponent<Collider>();
        if (hookCollider == null) return;

        Collider[] playerColliders = GetComponentsInChildren<Collider>();
        for (int i = 0; i < playerColliders.Length; i++)
        {
            if (playerColliders[i] != null) Physics.IgnoreCollision(hookCollider, playerColliders[i], true);
        }
    }
}
