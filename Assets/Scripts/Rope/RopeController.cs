using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class RopeController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Transform player;
    [SerializeField] Rigidbody playerRb;
    [SerializeField] Transform ropeOrigin;
    [SerializeField] LineRenderer ropeLine;
    [SerializeField] RopeCollisionTracker collisionTracker;

    [Header("Rope")]
    [SerializeField] float visualRopeWidth = 0.018f;
    [SerializeField] float maxRopeLength = 35f;
    [SerializeField] float minRopeLength = 2f;
    [SerializeField] float ropeClimbSpeed = 6f;
    [SerializeField] float lengthChangeInterval = 0.085f;
    [SerializeField] float attachedVelocityDamping = 0.12f;
    [SerializeField] LayerMask ropeCollisionMask = ~0;

    [Header("Physical Chain")]
    [SerializeField] RopeSegment ropeSegmentPrefab;
    [SerializeField] Material ropeSegmentMaterial;
    [SerializeField] float physicalSegmentLength = 0.4f;
    [SerializeField] float physicalSegmentRadius = 0.085f;
    [SerializeField] int maxPhysicalSegments = 90;
    [SerializeField] bool showPhysicalSegmentRenderers = false;
    [SerializeField] bool disablePlayerRopeCollision = true;
    [SerializeField] bool disableHookRopeCollision = true;
    [SerializeField] bool disableRopeSelfCollision = true;
    [SerializeField] bool drawContactGizmos = true;

    readonly List<RopeSegment> segments = new List<RopeSegment>();

    Transform flyingHook;
    Rigidbody flyingHookRb;
    Collider ropeEndCollider;
    Transform physicalRoot;
    Rigidbody startAnchorRb;
    Rigidbody endAnchorRb;
    ConfigurableJoint startJoint;
    ConfigurableJoint endJoint;
    Vector3 anchorPoint;
    GrappleEdgeCandidate attachedEdge;
    Collider playerCollider;

    bool isFiring;
    bool isAttached;
    float targetRopeLength;
    float currentRopeLength;
    float currentTension;
    float nextLengthChangeTime;

    public bool IsAttached { get { return isAttached; } }
    public float CurrentRopeLength { get { return currentRopeLength; } }
    public float TargetRopeLength { get { return targetRopeLength; } }
    public float CurrentTension { get { return currentTension; } }
    public float CurrentSwingEffort { get { return 0f; } }
    public Vector3 ActiveAnchor { get { return isAttached || isFiring ? GetEndPosition() : Vector3.zero; } }
    public int WrapCount { get { return 0; } }
    public int PhysicalSegmentCount { get { return segments.Count; } }
    public RopeCollisionTracker CollisionTracker { get { return collisionTracker; } }

    void Awake()
    {
        if (ropeLine == null) ropeLine = GetComponent<LineRenderer>();
        if (playerRb == null && player != null) playerRb = player.GetComponent<Rigidbody>();
        if (playerCollider == null && player != null) playerCollider = player.GetComponent<Collider>();
        if (collisionTracker == null) collisionTracker = GetComponent<RopeCollisionTracker>();
        if (collisionTracker == null) collisionTracker = gameObject.AddComponent<RopeCollisionTracker>();

        ropeLine.positionCount = 0;
        ropeLine.widthMultiplier = visualRopeWidth;
        targetRopeLength = minRopeLength;
        currentRopeLength = 0f;
        ropeCollisionMask = BuildWorldGeometryMask();
    }

    void Update()
    {
        if (isAttached) UpdateLengthInput();
        UpdateLine();
    }

    void FixedUpdate()
    {
        if (!isFiring && !isAttached)
        {
            currentTension = 0f;
            PushTensionToTracker();
            return;
        }

        UpdateEndpointAnchors();
        UpdateEndpointJointAnchors();

        if (isFiring)
        {
            ExtendFlyingChainToHook();
        }
        else
        {
            ApplyLengthChanges();
            ApplyAttachedDamping();
        }

        IgnoreOwnerCollisions();
        SampleSegmentContacts();
        UpdateCurrentLengthAndTension();
        PushTensionToTracker();
    }

    public void BeginFiring(Transform hook, float ropeLength)
    {
        ResetPhysicalRope();

        flyingHook = hook;
        flyingHookRb = hook != null ? hook.GetComponent<Rigidbody>() : null;
        ropeEndCollider = hook != null ? hook.GetComponent<Collider>() : null;
        maxRopeLength = Mathf.Max(minRopeLength, ropeLength);
        targetRopeLength = physicalSegmentLength;
        currentRopeLength = 0f;
        currentTension = 0f;
        isFiring = true;
        isAttached = false;
        attachedEdge = default(GrappleEdgeCandidate);
        if (collisionTracker != null) collisionTracker.Clear();

        Vector3 start = GetOrigin();
        Vector3 end = GetEndPosition();
        CreateChain(start, end, Mathf.Max(physicalSegmentLength, Vector3.Distance(start, end)));
    }

    public void Attach(Vector3 point, float ropeLength, LayerMask collisionMask)
    {
        Attach(point, ropeLength, collisionMask, default(GrappleEdgeCandidate));
    }

    public void Attach(Vector3 point, float ropeLength, LayerMask collisionMask, GrappleEdgeCandidate edgeCandidate)
    {
        anchorPoint = point;
        attachedEdge = edgeCandidate;
        ropeCollisionMask = collisionMask;
        maxRopeLength = Mathf.Max(minRopeLength, ropeLength);
        targetRopeLength = Mathf.Clamp(Vector3.Distance(GetOrigin(), point) + physicalSegmentLength, minRopeLength, maxRopeLength);
        currentRopeLength = Mathf.Max(currentRopeLength, targetRopeLength);
        isAttached = true;
        isFiring = false;
        flyingHook = null;
        flyingHookRb = null;
        ropeEndCollider = null;
        nextLengthChangeTime = Time.time;

        EnsureRootAndAnchors();
        UpdateEndpointAnchors();
        if (segments.Count == 0)
        {
            CreateChain(GetOrigin(), GetEndPosition(), targetRopeLength);
        }

        int desiredCount = DesiredSegmentCount(targetRopeLength);
        while (segments.Count < desiredCount)
        {
            InsertSegmentAtStart();
        }

        RebuildAllJoints();
        IgnoreOwnerCollisions();
    }

    public void Detach()
    {
        isAttached = false;
        isFiring = false;
        flyingHook = null;
        flyingHookRb = null;
        ropeEndCollider = null;
        attachedEdge = default(GrappleEdgeCandidate);
        targetRopeLength = minRopeLength;
        currentRopeLength = 0f;
        currentTension = 0f;

        if (collisionTracker != null) collisionTracker.Clear();
        ResetPhysicalRope();
        if (ropeLine != null) ropeLine.positionCount = 0;
    }

    public bool TryAttachToFiringWrap(LayerMask collisionMask)
    {
        ropeCollisionMask = collisionMask;
        return false;
    }

    void UpdateLengthInput()
    {
        float delta = 0f;
        if (PrototypeInput.ShiftHeld) delta -= ropeClimbSpeed * Time.deltaTime;
        if (PrototypeInput.ControlHeld) delta += ropeClimbSpeed * Time.deltaTime;
        if (Mathf.Abs(delta) <= 0.0001f) return;

        targetRopeLength = Mathf.Clamp(targetRopeLength + delta, minRopeLength, maxRopeLength);
    }

    void ApplyLengthChanges()
    {
        if (Time.time < nextLengthChangeTime) return;

        int desiredCount = DesiredSegmentCount(targetRopeLength);
        if (desiredCount < segments.Count)
        {
            RemoveSegmentAtStart();
            nextLengthChangeTime = Time.time + lengthChangeInterval;
        }
        else if (desiredCount > segments.Count)
        {
            InsertSegmentAtStart();
            nextLengthChangeTime = Time.time + lengthChangeInterval;
        }
    }

    void ExtendFlyingChainToHook()
    {
        if (flyingHook == null) return;

        float distance = Vector3.Distance(GetOrigin(), GetEndPosition());
        targetRopeLength = Mathf.Clamp(distance + physicalSegmentLength, physicalSegmentLength, maxRopeLength);
        int desiredCount = DesiredSegmentCount(targetRopeLength);
        while (segments.Count < desiredCount)
        {
            InsertSegmentAtStart();
        }
    }

    void CreateChain(Vector3 start, Vector3 end, float length)
    {
        EnsureRootAndAnchors();
        int count = DesiredSegmentCount(length);
        count = Mathf.Max(1, count);

        Vector3 direction = end - start;
        if (direction.sqrMagnitude < 0.001f) direction = Vector3.forward;
        direction.Normalize();

        for (int i = 0; i < count; i++)
        {
            float t = (i + 0.5f) / count;
            RopeSegment segment = CreateSegment(i, Vector3.Lerp(start, end, t), direction);
            segments.Add(segment);
        }

        RebuildAllJoints();
        IgnoreOwnerCollisions();
        UpdateCurrentLengthAndTension();
    }

    RopeSegment CreateSegment(int index, Vector3 position, Vector3 direction)
    {
        EnsureRootAndAnchors();

        RopeSegment segment;
        if (ropeSegmentPrefab != null)
        {
            segment = Instantiate(ropeSegmentPrefab, physicalRoot);
        }
        else
        {
            GameObject segmentObject = new GameObject("RopeSegment_" + index.ToString("00"));
            segmentObject.transform.SetParent(physicalRoot, true);
            segment = segmentObject.AddComponent<RopeSegment>();
        }

        segment.name = "RopeSegment_" + index.ToString("00");
        int ropeLayer = LayerMask.NameToLayer("Rope");
        if (ropeLayer >= 0) segment.gameObject.layer = ropeLayer;

        Vector3 safeDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.up;
        segment.transform.position = position;
        segment.transform.rotation = Quaternion.FromToRotation(Vector3.up, safeDirection);
        segment.Configure(index, collisionTracker, physicalSegmentRadius, physicalSegmentLength);

        Renderer renderer = segment.GetComponent<Renderer>();
        if (renderer != null && ropeSegmentMaterial != null) renderer.sharedMaterial = ropeSegmentMaterial;
        SetSegmentRendererVisible(segment, showPhysicalSegmentRenderers);
        return segment;
    }

    void InsertSegmentAtStart()
    {
        if (segments.Count >= maxPhysicalSegments) return;

        Vector3 start = GetOrigin();
        Vector3 next = segments.Count > 0 ? segments[0].WorldStart : GetEndPosition();
        Vector3 direction = next - start;
        if (direction.sqrMagnitude < 0.001f) direction = GetEndPosition() - start;
        if (direction.sqrMagnitude < 0.001f) direction = Vector3.forward;
        direction.Normalize();

        Vector3 position = start + direction * physicalSegmentLength * 0.5f;
        RopeSegment segment = CreateSegment(0, position, direction);
        segments.Insert(0, segment);
        ReindexSegments();
        RebuildAllJoints();
        IgnoreOwnerCollisions();
    }

    void RemoveSegmentAtStart()
    {
        int minCount = DesiredSegmentCount(minRopeLength);
        if (segments.Count <= minCount || segments.Count == 0) return;

        RopeSegment removed = segments[0];
        segments.RemoveAt(0);
        DestroySegmentObject(removed);
        ReindexSegments();
        RebuildAllJoints();
        IgnoreOwnerCollisions();
    }

    void ReindexSegments()
    {
        for (int i = 0; i < segments.Count; i++)
        {
            if (segments[i] == null) continue;
            segments[i].name = "RopeSegment_" + i.ToString("00");
            segments[i].SetIndex(i);
        }
    }

    void RebuildAllJoints()
    {
        RemoveSegmentJoints();
        if (segments.Count == 0) return;

        UpdateEndpointAnchors();

        Rigidbody startBody = isAttached && playerRb != null ? playerRb : startAnchorRb;
        Vector3 startAnchor = isAttached && playerRb != null ? GetOrigin() : GetStartAnchorPosition();
        startJoint = AddLockedJoint(segments[0].gameObject, startBody, segments[0].WorldStart, startAnchor);

        for (int i = 1; i < segments.Count; i++)
        {
            AddLockedJoint(segments[i].gameObject, segments[i - 1].Rigidbody, segments[i].WorldStart, segments[i - 1].WorldEnd);
        }

        Rigidbody endBody = GetEndBody();
        endJoint = AddLockedJoint(segments[segments.Count - 1].gameObject, endBody, segments[segments.Count - 1].WorldEnd, GetEndPosition());
    }

    void UpdateEndpointJointAnchors()
    {
        if (segments.Count == 0) return;

        if (startJoint != null && segments[0] != null)
        {
            startJoint.anchor = segments[0].transform.InverseTransformPoint(segments[0].WorldStart);
            Rigidbody body = startJoint.connectedBody;
            Vector3 anchor = isAttached && playerRb != null ? GetOrigin() : GetStartAnchorPosition();
            startJoint.connectedAnchor = body != null ? body.transform.InverseTransformPoint(anchor) : anchor;
        }

        if (endJoint != null && segments[segments.Count - 1] != null)
        {
            RopeSegment last = segments[segments.Count - 1];
            endJoint.anchor = last.transform.InverseTransformPoint(last.WorldEnd);
            Rigidbody body = endJoint.connectedBody;
            Vector3 anchor = GetEndPosition();
            endJoint.connectedAnchor = body != null ? body.transform.InverseTransformPoint(anchor) : anchor;
        }
    }

    ConfigurableJoint AddLockedJoint(GameObject owner, Rigidbody connectedBody, Vector3 anchorWorld, Vector3 connectedAnchorWorld)
    {
        ConfigurableJoint joint = owner.AddComponent<ConfigurableJoint>();
        joint.connectedBody = connectedBody;
        joint.autoConfigureConnectedAnchor = false;
        joint.anchor = owner.transform.InverseTransformPoint(anchorWorld);
        joint.connectedAnchor = connectedBody != null
            ? connectedBody.transform.InverseTransformPoint(connectedAnchorWorld)
            : connectedAnchorWorld;

        joint.xMotion = ConfigurableJointMotion.Locked;
        joint.yMotion = ConfigurableJointMotion.Locked;
        joint.zMotion = ConfigurableJointMotion.Locked;
        joint.angularXMotion = ConfigurableJointMotion.Free;
        joint.angularYMotion = ConfigurableJointMotion.Free;
        joint.angularZMotion = ConfigurableJointMotion.Free;
        joint.projectionMode = JointProjectionMode.None;
        joint.enablePreprocessing = false;
        joint.enableCollision = false;
        joint.massScale = 1f;
        joint.connectedMassScale = 1f;
        return joint;
    }

    void RemoveSegmentJoints()
    {
        for (int i = 0; i < segments.Count; i++)
        {
            if (segments[i] == null) continue;
            ConfigurableJoint[] joints = segments[i].GetComponents<ConfigurableJoint>();
            for (int j = joints.Length - 1; j >= 0; j--)
            {
                DestroyImmediate(joints[j]);
            }
        }
    }

    void EnsureRootAndAnchors()
    {
        if (physicalRoot == null)
        {
            GameObject root = new GameObject("PhysicalRopeChain");
            root.transform.SetParent(transform, false);
            physicalRoot = root.transform;
        }

        if (startAnchorRb == null) startAnchorRb = CreateAnchor("RopeStartAnchor", GetStartAnchorPosition());
        if (endAnchorRb == null) endAnchorRb = CreateAnchor("RopeEndAnchor", GetEndPosition());
    }

    Rigidbody CreateAnchor(string anchorName, Vector3 position)
    {
        GameObject anchor = new GameObject(anchorName);
        anchor.transform.SetParent(physicalRoot, true);
        anchor.transform.position = position;

        Rigidbody body = anchor.AddComponent<Rigidbody>();
        body.useGravity = false;
        body.isKinematic = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        return body;
    }

    void UpdateEndpointAnchors()
    {
        EnsureRootAndAnchors();
        MoveAnchor(startAnchorRb, GetStartAnchorPosition());
        MoveAnchor(endAnchorRb, GetEndPosition());
    }

    void MoveAnchor(Rigidbody body, Vector3 position)
    {
        if (body == null) return;
        if (Application.isPlaying) body.MovePosition(position);
        else body.position = position;
        body.transform.position = position;
    }

    Rigidbody GetEndBody()
    {
        if (isFiring && flyingHookRb != null) return flyingHookRb;
        return endAnchorRb;
    }

    Vector3 GetOrigin()
    {
        if (ropeOrigin != null) return ropeOrigin.position;
        if (player != null) return player.position;
        return transform.position;
    }

    Vector3 GetStartAnchorPosition()
    {
        return GetOrigin();
    }

    Vector3 GetEndPosition()
    {
        if (isFiring && flyingHook != null) return flyingHook.position;
        return anchorPoint;
    }

    int DesiredSegmentCount(float length)
    {
        float safeSegmentLength = Mathf.Max(0.1f, physicalSegmentLength);
        return Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(safeSegmentLength, length) / safeSegmentLength), 1, maxPhysicalSegments);
    }

    void UpdateCurrentLengthAndTension()
    {
        currentRopeLength = segments.Count * Mathf.Max(0.1f, physicalSegmentLength);
        float directDistance = isFiring || isAttached ? Vector3.Distance(GetOrigin(), GetEndPosition()) : 0f;
        currentTension = Mathf.Max(0f, directDistance - currentRopeLength) * 30f;
    }

    void ApplyAttachedDamping()
    {
        if (playerRb == null || attachedVelocityDamping <= 0f) return;
        playerRb.linearVelocity *= Mathf.Clamp01(1f - attachedVelocityDamping * Time.fixedDeltaTime);
    }

    void IgnoreOwnerCollisions()
    {
        if (segments.Count == 0) return;
        if (playerCollider == null && player != null) playerCollider = player.GetComponent<Collider>();

        for (int i = 0; i < segments.Count; i++)
        {
            if (segments[i] == null) continue;
            Collider segmentCollider = segments[i].GetComponent<Collider>();
            if (segmentCollider == null) continue;

            if (disablePlayerRopeCollision && playerCollider != null)
            {
                Physics.IgnoreCollision(segmentCollider, playerCollider, true);
            }

            if (disableHookRopeCollision && ropeEndCollider != null)
            {
                Physics.IgnoreCollision(segmentCollider, ropeEndCollider, true);
            }

            if (!disableRopeSelfCollision) continue;
            for (int j = i + 1; j < segments.Count; j++)
            {
                if (segments[j] == null) continue;
                Collider other = segments[j].GetComponent<Collider>();
                if (other != null) Physics.IgnoreCollision(segmentCollider, other, true);
            }
        }
    }

    void SampleSegmentContacts()
    {
        if (collisionTracker == null) return;

        for (int i = 0; i < segments.Count; i++)
        {
            RopeSegment segment = segments[i];
            if (segment == null) continue;

            CapsuleCollider capsule = segment.Capsule;
            Vector3 pointA = segment.WorldStart;
            Vector3 pointB = segment.WorldEnd;
            Collider[] hits = Physics.OverlapCapsule(pointA, pointB, capsule.radius, ropeCollisionMask, QueryTriggerInteraction.Ignore);
            for (int j = 0; j < hits.Length; j++)
            {
                Collider hit = hits[j];
                if (hit == null || hit.isTrigger) continue;
                if (hit.GetComponentInParent<RopeSegment>() != null) continue;
                if (playerCollider != null && hit == playerCollider) continue;
                if (ropeEndCollider != null && hit == ropeEndCollider) continue;

                Vector3 closest = hit.ClosestPoint(segment.transform.position);
                Vector3 normal = segment.transform.position - closest;
                if (normal.sqrMagnitude < 0.0001f) normal = segment.transform.position - hit.bounds.center;
                if (normal.sqrMagnitude < 0.0001f) normal = Vector3.up;
                collisionTracker.ReportContact(hit, closest, normal.normalized, segment.SegmentIndex);
            }
        }
    }

    void PushTensionToTracker()
    {
        if (collisionTracker == null) return;

        Vector3 direction = GetEndPosition() - GetOrigin();
        collisionTracker.SetTension(currentTension, direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.zero);
    }

    void UpdateLine()
    {
        if (ropeLine == null) return;
        if (!isFiring && !isAttached)
        {
            ropeLine.positionCount = 0;
            return;
        }

        ropeLine.widthMultiplier = visualRopeWidth;
        ropeLine.positionCount = segments.Count + 2;
        ropeLine.SetPosition(0, GetOrigin());
        for (int i = 0; i < segments.Count; i++)
        {
            ropeLine.SetPosition(i + 1, segments[i] != null ? segments[i].transform.position : GetOrigin());
        }
        ropeLine.SetPosition(segments.Count + 1, GetEndPosition());
    }

    void SetSegmentRendererVisible(RopeSegment segment, bool visible)
    {
        if (segment == null) return;
        Renderer[] renderers = segment.GetComponentsInChildren<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].enabled = visible;
        }
    }

    void ResetPhysicalRope()
    {
        segments.Clear();
        startAnchorRb = null;
        endAnchorRb = null;
        startJoint = null;
        endJoint = null;
        if (physicalRoot != null)
        {
            physicalRoot.gameObject.SetActive(false);
            DestroyUnityObject(physicalRoot.gameObject);
            physicalRoot = null;
        }
    }

    void DestroySegmentObject(RopeSegment segment)
    {
        if (segment == null) return;
        segment.gameObject.SetActive(false);
        DestroyUnityObject(segment.gameObject);
    }

    void DestroyUnityObject(Object target)
    {
        if (target == null) return;
        if (Application.isPlaying) Destroy(target);
        else DestroyImmediate(target);
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

    void OnDrawGizmos()
    {
        if (!isAttached && !isFiring) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(GetEndPosition(), 0.25f);
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(GetOrigin(), 0.12f);
        if (attachedEdge.IsValid)
        {
            Gizmos.color = attachedEdge.AttachSide == GrappleAttachSide.BottomSide ? Color.red : Color.green;
            Gizmos.DrawWireSphere(attachedEdge.EdgePoint, 0.28f);
            Gizmos.DrawRay(attachedEdge.EdgePoint, attachedEdge.EdgeDirection * 0.8f);
        }
        if (drawContactGizmos && collisionTracker != null) collisionTracker.DrawDebugGizmos();
    }
}
