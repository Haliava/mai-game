using UnityEngine;

public class GrapplingHookController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Transform cameraTransform;
    [SerializeField] Transform ropeOrigin;
    [SerializeField] HookProjectile hookPrefab;
    [SerializeField] RopeController ropeController;
    [SerializeField] HookEdgeDetector edgeDetector;
    [SerializeField] HookAttachValidator attachValidator;
    [SerializeField] RopeCollisionTracker ropeCollisionTracker;

    [Header("Hook")]
    [SerializeField] float hookFireSpeed = 45f;
    [SerializeField] float maxRopeLength = 35f;
    [SerializeField] LayerMask grappleMask = ~0;
    [SerializeField] LayerMask ropeCollisionMask = ~0;
    [SerializeField] float hookRadius = 0.15f;
    [SerializeField] float hookSpawnForwardOffset = 0.35f;
    [SerializeField] float autoDetachAfterMissTime = 3f;

    [Header("Debug")]
    [SerializeField] bool drawDebugGizmos = true;
    [SerializeField] bool showDebugUi = true;

    HookProjectile activeHook;
    Rigidbody playerRb;
    Vector3 fireOrigin;
    float missedAtTime;
    GrappleEdgeCandidate lastCandidate;
    GrappleEdgeCandidate lastRejectedCandidate;
    string lastAttachReason = "Idle";

    public GrappleState State { get; private set; }
    public Vector3 HookPosition { get { return activeHook != null ? activeHook.transform.position : Vector3.zero; } }
    public string LastAttachReason { get { return lastAttachReason; } }

    void Awake()
    {
        State = GrappleState.Idle;
        playerRb = GetComponent<Rigidbody>();
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        if (ropeController == null) ropeController = GetComponent<RopeController>();
        if (edgeDetector == null) edgeDetector = GetComponent<HookEdgeDetector>();
        if (edgeDetector == null) edgeDetector = gameObject.AddComponent<HookEdgeDetector>();
        if (attachValidator == null) attachValidator = GetComponent<HookAttachValidator>();
        if (attachValidator == null) attachValidator = gameObject.AddComponent<HookAttachValidator>();
        if (ropeCollisionTracker == null && ropeController != null) ropeCollisionTracker = ropeController.CollisionTracker;
        if (ropeCollisionTracker == null) ropeCollisionTracker = GetComponent<RopeCollisionTracker>();
        ropeCollisionMask = BuildWorldGeometryMask();
        grappleMask = ropeCollisionMask;
    }

    void Update()
    {
        if (PrototypeInput.FirePressed)
        {
            if (State == GrappleState.Idle) Fire();
            else Detach();
        }

        if (State != GrappleState.Idle && !PrototypeInput.FireHeld)
        {
            CancelGrapple();
            return;
        }

        if (PrototypeInput.AltFirePressed) Detach();
        if (PrototypeInput.JumpPressed && (State == GrappleState.Attached || State == GrappleState.Wrapped || State == GrappleState.Retracting)) Detach();

        if ((State == GrappleState.Flying || State == GrappleState.Retracting) && activeHook != null)
        {
            if (TryAttachFromRopeWrap())
            {
                return;
            }

            float distanceFromOrigin = Vector3.Distance(fireOrigin, activeHook.transform.position);
            if (State == GrappleState.Flying && distanceFromOrigin >= maxRopeLength)
            {
                State = GrappleState.Retracting;
                missedAtTime = Time.time;
                lastAttachReason = "Missed: max rope length reached";
            }

            if (State == GrappleState.Retracting && Time.time - missedAtTime >= autoDetachAfterMissTime)
            {
                CancelGrapple();
            }
        }
    }

    public void Fire()
    {
        if (cameraTransform == null || hookPrefab == null) return;

        Transform originTransform = ropeOrigin != null ? ropeOrigin : cameraTransform;
        fireOrigin = originTransform.position;
        lastCandidate = default(GrappleEdgeCandidate);
        lastRejectedCandidate = default(GrappleEdgeCandidate);
        lastAttachReason = "Firing";

        if (activeHook == null)
        {
            activeHook = Instantiate(hookPrefab);
            IgnorePlayerCollision(activeHook);
        }

        activeHook.transform.localScale = Vector3.one * hookRadius;
        int hookLayer = LayerMask.NameToLayer("Hook");
        if (hookLayer >= 0) activeHook.gameObject.layer = hookLayer;
        Vector3 inheritedVelocity = playerRb != null ? playerRb.linearVelocity : Vector3.zero;
        Vector3 launchVelocity = inheritedVelocity + cameraTransform.forward * hookFireSpeed;
        Vector3 spawnPosition = fireOrigin + cameraTransform.forward * hookSpawnForwardOffset;

        State = GrappleState.Flying;
        activeHook.Fire(this, spawnPosition, launchVelocity, grappleMask);
        if (ropeController != null) ropeController.BeginFiring(activeHook.transform, maxRopeLength);
    }

    public bool TryResolveHookAttach(Collision collision, HookProjectile hook, out Vector3 attachPosition)
    {
        attachPosition = Vector3.zero;
        if (collision == null || hook == null || edgeDetector == null || attachValidator == null)
        {
            lastAttachReason = "Rejected: missing attach services";
            return false;
        }

        GrapplePoint point = collision.collider.GetComponentInParent<GrapplePoint>();
        if (point != null)
        {
            attachPosition = point.AttachTransform.position;
            lastAttachReason = "Allowed: GrapplePoint";
            return true;
        }

        if (!IsWorldGeometryCollider(collision.collider))
        {
            lastAttachReason = "Rejected: not world geometry";
            return false;
        }

        GrappleEdgeCandidate candidate;
        if (!edgeDetector.TryFindEdge(collision, hook.transform.position, out candidate))
        {
            lastAttachReason = "Rejected: not near edge";
            return false;
        }

        string reason;
        float tension = ropeController != null ? ropeController.CurrentTension : 0f;
        bool allowed = attachValidator.Validate(
            candidate,
            transform.position,
            hook.Rigidbody != null ? hook.Rigidbody.linearVelocity : Vector3.zero,
            ropeCollisionTracker,
            tension,
            false,
            out reason);

        if (!allowed)
        {
            lastRejectedCandidate = candidate;
            lastAttachReason = reason;
            return false;
        }

        lastCandidate = candidate;
        lastAttachReason = reason;
        attachPosition = candidate.EdgePoint;
        return true;
    }

    bool IsWorldGeometryCollider(Collider targetCollider)
    {
        if (targetCollider == null || targetCollider.isTrigger) return false;
        if (targetCollider.GetComponentInParent<RopeSegment>() != null) return false;
        if (targetCollider.GetComponentInParent<HookProjectile>() != null) return false;
        if (targetCollider.transform.IsChildOf(transform)) return false;

        int layer = targetCollider.gameObject.layer;
        string layerName = LayerMask.LayerToName(layer);
        if (layerName == "Player" || layerName == "Hook" || layerName == "Rope" || layerName == "IgnoreRopeSelfCollision") return false;

        return true;
    }

    LayerMask BuildWorldGeometryMask()
    {
        int mask = ~0;
        RemoveLayerFromMask(ref mask, "Player");
        RemoveLayerFromMask(ref mask, "Hook");
        RemoveLayerFromMask(ref mask, "Rope");
        RemoveLayerFromMask(ref mask, "IgnoreRopeSelfCollision");
        return mask;
    }

    void RemoveLayerFromMask(ref int mask, string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer >= 0) mask &= ~(1 << layer);
    }

    public void AttachAt(Vector3 position, GrapplePoint point)
    {
        State = GrappleState.Attached;
        if (ropeController != null) ropeController.Attach(position, maxRopeLength, ropeCollisionMask);
        lastAttachReason = point != null ? "Allowed: GrapplePoint" : "Allowed: direct attach";
    }

    public void AttachAtEdge(GrappleEdgeCandidate candidate)
    {
        if (!candidate.IsValid) return;

        State = candidate.AttachSide == GrappleAttachSide.BottomSide ? GrappleState.Wrapped : GrappleState.Attached;
        if (ropeController != null) ropeController.Attach(candidate.EdgePoint, maxRopeLength, ropeCollisionMask, candidate);
        lastCandidate = candidate;
        lastAttachReason = "Allowed: " + candidate.AttachSide;
    }

    public bool TryAttachFromRopeWrap()
    {
        if (activeHook == null || ropeCollisionTracker == null || edgeDetector == null || attachValidator == null) return false;

        Collider wrappedCollider;
        Vector3 wrapPoint;
        Vector3 wrapNormal;
        if (!ropeCollisionTracker.TryGetBestWrappedCollider(out wrappedCollider, out wrapPoint, out wrapNormal)) return false;

        GrappleEdgeCandidate candidate;
        if (!edgeDetector.TryFindNearestEdge(wrappedCollider, activeHook.transform.position, wrapNormal, out candidate)) return false;

        string reason;
        float tension = ropeController != null ? ropeController.CurrentTension : ropeCollisionTracker.CurrentRopeTension;
        bool allowed = attachValidator.Validate(
            candidate,
            transform.position,
            activeHook.Rigidbody != null ? activeHook.Rigidbody.linearVelocity : Vector3.zero,
            ropeCollisionTracker,
            tension,
            true,
            out reason);

        if (!allowed)
        {
            lastRejectedCandidate = candidate;
            lastAttachReason = reason;
            return false;
        }

        activeHook.PinTo(candidate.EdgePoint);
        AttachAtEdge(candidate);
        State = GrappleState.Wrapped;
        lastAttachReason = "Allowed from wrap: " + candidate.AttachSide;
        return true;
    }

    public void ConfirmHookAttached(Vector3 attachPosition, GrapplePoint point)
    {
        if (point != null)
        {
            AttachAt(attachPosition, point);
            return;
        }

        if (lastCandidate.IsValid)
        {
            AttachAtEdge(lastCandidate);
            return;
        }

        AttachAt(attachPosition, null);
    }

    public void CancelGrapple()
    {
        State = GrappleState.Idle;
        missedAtTime = 0f;
        if (ropeController != null) ropeController.Detach();
        if (activeHook != null) activeHook.gameObject.SetActive(false);
        lastAttachReason = "Idle";
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

    void OnGUI()
    {
        if (!showDebugUi) return;

        GUI.color = Color.white;
        GUILayout.BeginArea(new Rect(12f, 12f, 360f, 190f), GUI.skin.box);
        GUILayout.Label("GrappleState: " + State);
        GUILayout.Label("Reason: " + lastAttachReason);
        if (ropeController != null)
        {
            GUILayout.Label("Rope Current Length: " + ropeController.CurrentRopeLength.ToString("0.00"));
            GUILayout.Label("Rope Target Length: " + ropeController.TargetRopeLength.ToString("0.00"));
            GUILayout.Label("Rope Tension: " + ropeController.CurrentTension.ToString("0.00"));
            GUILayout.Label("Active Anchor: " + ropeController.ActiveAnchor.ToString("F2"));
            GUILayout.Label("Wrap Count: " + ropeController.WrapCount);
            GUILayout.Label("Physical Segments: " + ropeController.PhysicalSegmentCount);
        }
        if (activeHook != null && activeHook.Rigidbody != null)
        {
            GUILayout.Label("Hook Speed: " + activeHook.Rigidbody.linearVelocity.magnitude.ToString("0.00"));
        }
        GUILayout.EndArea();
    }

    void OnDrawGizmos()
    {
        if (!drawDebugGizmos) return;

        Gizmos.color = Color.white;
        Transform originTransform = ropeOrigin != null ? ropeOrigin : transform;
        Gizmos.DrawWireSphere(originTransform.position, 0.12f);

        if (cameraTransform != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(originTransform.position, cameraTransform.forward * maxRopeLength);
        }

        if (lastCandidate.IsValid)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(lastCandidate.EdgePoint, 0.25f);
            Gizmos.DrawRay(lastCandidate.EdgePoint, lastCandidate.EdgeDirection * 0.75f);
        }

        if (lastRejectedCandidate.IsValid)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(lastRejectedCandidate.EdgePoint, 0.2f);
        }
    }
}
