using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SurfaceCrawlerMotor))]
public sealed class CentipedeBodyController : MonoBehaviour
{
    private struct TrailPoint
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Vector3 Forward;

        public TrailPoint(Vector3 position, Vector3 normal, Vector3 forward)
        {
            Position = position;
            Normal = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;
            Forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        }
    }

    private struct SegmentSafePose
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 SurfaceNormal;
        public Collider SurfaceCollider;
        public bool HasPose;
    }

    [Header("Body")]
    [SerializeField, Min(2)] private int segmentCount = 14;
    [SerializeField, Min(0.05f)] private float segmentDiameter = 0.9f;
    [SerializeField, Range(0.5f, 1.2f)] private float segmentSpacingMultiplier = 0.875f;
    [SerializeField, Min(0f)] private float bodyFollowSmoothing = 18f;
    [SerializeField, Min(0f)] private float bodyTurnSmoothing = 14f;
    [SerializeField, Min(0.02f)] private float trailSampleSpacing = 0.22f;
    [SerializeField, Min(16)] private int maxTrailPoints = 256;
    [SerializeField] private Material bodyMaterial;
    [SerializeField] private Material headMaterial;

    [Header("Collision Correction")]
    [SerializeField] private bool enableSegmentCollisionCorrection = true;
    [SerializeField, Range(0.1f, 1f)] private float segmentCollisionRadiusMultiplier = 0.48f;
    [SerializeField, Min(0.05f)] private float segmentSurfaceSnapDistance = 1.5f;
    [SerializeField, Min(1)] private int maxSegmentDepenetrationIterations = 4;
    [SerializeField, Min(0f)] private float segmentWallOffset = 0.03f;
    [SerializeField] private LayerMask environmentCollisionMask = ~0;
    [SerializeField] private LayerMask crawlableSurfaceMask = ~0;

    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = true;
    [SerializeField] private bool logSegmentCorrectionEvents;

    private readonly List<TrailPoint> trail = new();
    private readonly List<Transform> segments = new();
    private SurfaceCrawlerMotor motor;
    private Transform bodyRoot;
    private SegmentSafePose[] segmentSafePoses;
    private bool[] segmentCollisionState;
    private bool[] segmentCorrectedState;
    private Vector3[] segmentSurfaceNormals;
    private float nextSegmentCorrectionLogTime;

    public Transform HeadSegment => segments.Count > 0 ? segments[0] : null;
    public float SegmentSpacing => segmentDiameter * segmentSpacingMultiplier;
    public int SegmentCount => segments.Count;
    public float SegmentDiameter => segmentDiameter;
    public float FollowSpeedMultiplier { get; set; } = 1f;

    public Transform GetSegment(int index)
    {
        return index >= 0 && index < segments.Count ? segments[index] : null;
    }

    private void Awake()
    {
        CacheReferences();
        EnsureBodySegments();
        RebuildTrail();
    }

    private void OnEnable()
    {
        CacheReferences();
        EnsureBodySegments();
        RebuildTrail();
    }

    private void OnValidate()
    {
        segmentCount = Mathf.Max(2, segmentCount);
        segmentDiameter = Mathf.Max(0.05f, segmentDiameter);
        trailSampleSpacing = Mathf.Max(0.02f, trailSampleSpacing);
        maxTrailPoints = Mathf.Max(segmentCount * 4, maxTrailPoints);
        segmentSurfaceSnapDistance = Mathf.Max(0.05f, segmentSurfaceSnapDistance);
        maxSegmentDepenetrationIterations = Mathf.Max(1, maxSegmentDepenetrationIterations);
    }

    private void LateUpdate()
    {
        CacheReferences();
        EnsureBodySegments();
        AddHeadTrailPoint();
        UpdateSegmentTransforms(Time.deltaTime);
    }

    [ContextMenu("Rebuild Centipede Body")]
    public void RebuildBody()
    {
        CacheReferences();
        EnsureBodySegments();
        RebuildTrail();
        UpdateSegmentTransforms(1f);
    }

    private void CacheReferences()
    {
        if (motor == null)
        {
            motor = GetComponent<SurfaceCrawlerMotor>();
        }
    }

    private void EnsureBodySegments()
    {
        if (bodyRoot == null)
        {
            Transform existingRoot = transform.Find("Body Segments");
            if (existingRoot != null)
            {
                bodyRoot = existingRoot;
            }
            else
            {
                GameObject rootObject = new("Body Segments");
                bodyRoot = rootObject.transform;
                bodyRoot.SetParent(transform, false);
            }
        }

        segments.Clear();
        for (int i = 0; i < segmentCount; i++)
        {
            Transform segment = FindSegment(i);
            if (segment == null)
            {
                segment = CreateSegment(i);
            }

            segment.SetParent(bodyRoot, true);
            segment.name = i == 0 ? "Head Segment" : $"Body Segment {i:00}";
            segment.localScale = Vector3.one * segmentDiameter;
            ApplySegmentMaterial(segment.gameObject, i == 0);
            EnsureTriggerCollider(segment.gameObject);
            segments.Add(segment);
        }

        ResizeSegmentSafetyData();

        for (int i = bodyRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = bodyRoot.GetChild(i);
            if (!segments.Contains(child))
            {
                DestroySafe(child.gameObject);
            }
        }
    }

    private void ResizeSegmentSafetyData()
    {
        if (segmentSafePoses == null || segmentSafePoses.Length != segmentCount)
        {
            segmentSafePoses = new SegmentSafePose[segmentCount];
        }
        if (segmentCollisionState == null || segmentCollisionState.Length != segmentCount)
        {
            segmentCollisionState = new bool[segmentCount];
        }
        if (segmentCorrectedState == null || segmentCorrectedState.Length != segmentCount)
        {
            segmentCorrectedState = new bool[segmentCount];
        }
        if (segmentSurfaceNormals == null || segmentSurfaceNormals.Length != segmentCount)
        {
            segmentSurfaceNormals = new Vector3[segmentCount];
        }
    }

    private Transform FindSegment(int index)
    {
        string targetName = index == 0 ? "Head Segment" : $"Body Segment {index:00}";
        Transform segment = bodyRoot != null ? bodyRoot.Find(targetName) : null;
        if (segment == null && index == 0)
        {
            segment = transform.Find("Head Segment");
        }

        return segment;
    }

    private Transform CreateSegment(int index)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = index == 0 ? "Head Segment" : $"Body Segment {index:00}";
        sphere.transform.position = transform.position - transform.forward * (SegmentSpacing * index);
        sphere.transform.rotation = transform.rotation;
        Collider col = sphere.GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
        var ds = sphere.GetComponent<DamageSource>();
        if (ds == null)
        {
            ds = sphere.AddComponent<DamageSource>();
        }
        return sphere.transform;
    }

    private void ApplySegmentMaterial(GameObject segment, bool isHead)
    {
        MeshRenderer renderer = segment.GetComponent<MeshRenderer>();
        if (renderer == null)
        {
            return;
        }

        Material material = isHead && headMaterial != null ? headMaterial : bodyMaterial;
        if (material == null && isHead)
        {
            material = renderer.sharedMaterial;
        }
        if (material != null)
        {
            renderer.sharedMaterial = material;
        }
    }

    private static void EnsureTriggerCollider(GameObject segment)
    {
        Collider collider = segment.GetComponent<Collider>();
        if (collider != null)
        {
            collider.isTrigger = true;
        }
    }

    private void RebuildTrail()
    {
        trail.Clear();
        Vector3 normal = motor != null && motor.HasSurface ? motor.SurfaceNormal : transform.up;
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, normal);
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.forward;
        }

        forward.Normalize();
        int count = Mathf.Max(maxTrailPoints, segmentCount * 8);
        for (int i = 0; i < count; i++)
        {
            trail.Add(new TrailPoint(transform.position - forward * (trailSampleSpacing * i), normal, forward));
        }
    }

    private void AddHeadTrailPoint()
    {
        Vector3 normal = motor != null && motor.HasSurface ? motor.SurfaceNormal : transform.up;
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, normal);
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = transform.forward;
        }

        TrailPoint current = new(transform.position, normal, forward);
        if (trail.Count == 0)
        {
            trail.Add(current);
            return;
        }

        float distanceFromNewest = Vector3.Distance(current.Position, trail[0].Position);
        if (distanceFromNewest >= trailSampleSpacing)
        {
            trail.Insert(0, current);
        }
        else
        {
            trail[0] = current;
        }

        while (trail.Count > maxTrailPoints)
        {
            trail.RemoveAt(trail.Count - 1);
        }
    }

    private void UpdateSegmentTransforms(float deltaTime)
    {
        if (trail.Count == 0)
        {
            return;
        }

        ResizeSegmentSafetyData();
        float speedMultiplier = Mathf.Max(0.01f, FollowSpeedMultiplier);
        float positionWeight = deltaTime >= 1f ? 1f : 1f - Mathf.Exp(-bodyFollowSmoothing * speedMultiplier * deltaTime);
        float rotationWeight = deltaTime >= 1f ? 1f : 1f - Mathf.Exp(-bodyTurnSmoothing * speedMultiplier * deltaTime);

        for (int i = 0; i < segments.Count; i++)
        {
            Transform segment = segments[i];
            if (segment == null)
            {
                continue;
            }

            TrailPoint sample = SampleTrailAtDistance(SegmentSpacing * i);
            segment.position = Vector3.Lerp(segment.position, sample.Position, positionWeight);

            Vector3 forward = Vector3.ProjectOnPlane(sample.Forward, sample.Normal);
            if (forward.sqrMagnitude < 0.0001f && i > 0)
            {
                forward = segments[i - 1].position - segment.position;
                forward = Vector3.ProjectOnPlane(forward, sample.Normal);
            }
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = transform.forward;
            }

            Quaternion targetRotation = Quaternion.LookRotation(forward.normalized, sample.Normal.normalized);
            Vector3 targetPosition = sample.Position;
            Quaternion correctedRotation = targetRotation;
            if (enableSegmentCollisionCorrection)
            {
                targetPosition = CorrectSegmentPlacement(i, segment, sample.Position, targetRotation, sample.Normal, out correctedRotation);
            }

            segment.position = Vector3.Lerp(segment.position, targetPosition, positionWeight);
            segment.rotation = Quaternion.Slerp(segment.rotation, correctedRotation, rotationWeight);
            SaveSafePoseIfValid(i, segment, segment.position, segment.rotation, sample.Normal);
        }
    }

    private Vector3 CorrectSegmentPlacement(int index, Transform segment, Vector3 desiredPosition, Quaternion desiredRotation, Vector3 normalHint, out Quaternion correctedRotation)
    {
        correctedRotation = desiredRotation;
        segmentCollisionState[index] = false;
        segmentCorrectedState[index] = false;
        segmentSurfaceNormals[index] = normalHint.sqrMagnitude > 0.0001f ? normalHint.normalized : transform.up;

        Collider segmentCollider = segment.GetComponent<Collider>();
        float radius = GetSegmentCollisionRadius();
        Vector3 correctedPosition = desiredPosition;

        if (TryDepenetrateSegment(index, segment, segmentCollider, ref correctedPosition, desiredRotation, radius))
        {
            segmentCorrectedState[index] = true;
        }

        if (TrySnapSegmentToSurface(correctedPosition, normalHint, radius, out Vector3 snappedPosition, out Vector3 snappedNormal, out Collider surfaceCollider))
        {
            correctedPosition = snappedPosition;
            segmentSurfaceNormals[index] = snappedNormal;
            Vector3 forward = Vector3.ProjectOnPlane(desiredRotation * Vector3.forward, snappedNormal);
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.ProjectOnPlane(transform.forward, snappedNormal);
            }
            if (forward.sqrMagnitude > 0.0001f)
            {
                correctedRotation = Quaternion.LookRotation(forward.normalized, snappedNormal);
            }

            segmentSafePoses[index] = new SegmentSafePose
            {
                Position = correctedPosition,
                Rotation = correctedRotation,
                SurfaceNormal = snappedNormal,
                SurfaceCollider = surfaceCollider,
                HasPose = true
            };
        }

        if (IsSegmentOverlapping(segment, correctedPosition, radius, out Collider blockingCollider))
        {
            segmentCollisionState[index] = true;
            if (segmentSafePoses[index].HasPose)
            {
                SegmentSafePose safePose = segmentSafePoses[index];
                correctedPosition = safePose.Position;
                correctedRotation = safePose.Rotation;
                segmentSurfaceNormals[index] = safePose.SurfaceNormal;
                segmentCorrectedState[index] = true;
                LogSegmentCorrection(index, "restored last safe pose", blockingCollider);
            }
            else
            {
                LogSegmentCorrection(index, "still overlapping and no safe pose", blockingCollider);
            }
        }

        return correctedPosition;
    }

    private bool TryDepenetrateSegment(int index, Transform segment, Collider segmentCollider, ref Vector3 position, Quaternion rotation, float radius)
    {
        if (segmentCollider == null)
        {
            return false;
        }

        bool corrected = false;
        for (int iteration = 0; iteration < maxSegmentDepenetrationIterations; iteration++)
        {
            Collider[] overlaps = Physics.OverlapSphere(position, radius, environmentCollisionMask, QueryTriggerInteraction.Ignore);
            bool movedThisIteration = false;
            for (int i = 0; i < overlaps.Length; i++)
            {
                Collider other = overlaps[i];
                if (ShouldIgnoreCollider(segment, other))
                {
                    continue;
                }

                if (Physics.ComputePenetration(
                    segmentCollider,
                    position,
                    rotation,
                    other,
                    other.transform.position,
                    other.transform.rotation,
                    out Vector3 direction,
                    out float distance))
                {
                    position += direction.normalized * (distance + segmentWallOffset);
                    corrected = true;
                    movedThisIteration = true;
                    segmentCollisionState[index] = true;
                    LogSegmentCorrection(index, "depenetrated", other);
                }
            }

            if (!movedThisIteration)
            {
                break;
            }
        }

        return corrected;
    }

    private static Vector3 GetSafeClosestPoint(Collider collider, Vector3 position)
    {
        if (collider == null)
        {
            return position;
        }

        if (CanUseClosestPoint(collider))
        {
            return collider.ClosestPoint(position);
        }

        return collider.bounds.ClosestPoint(position);
    }

    private static bool CanUseClosestPoint(Collider collider)
    {
        if (collider is BoxCollider)
        {
            return true;
        }

        if (collider is SphereCollider)
        {
            return true;
        }

        if (collider is CapsuleCollider)
        {
            return true;
        }

        if (collider is MeshCollider meshCollider)
        {
            return meshCollider.convex;
        }

        return false;
    }

    private bool TrySnapSegmentToSurface(Vector3 position, Vector3 normalHint, float radius, out Vector3 snappedPosition, out Vector3 snappedNormal, out Collider surfaceCollider)
    {
        snappedPosition = position;
        snappedNormal = normalHint.sqrMagnitude > 0.0001f ? normalHint.normalized : transform.up;
        surfaceCollider = null;

        Vector3[] directions =
        {
            -snappedNormal,
            Vector3.down,
            -transform.up,
            transform.up,
            -transform.forward,
            transform.forward
        };

        float bestDistance = float.PositiveInfinity;
        bool found = false;
        for (int i = 0; i < directions.Length; i++)
        {
            Vector3 direction = directions[i].sqrMagnitude > 0.0001f ? directions[i].normalized : Vector3.down;
            Vector3 origin = position - direction * segmentSurfaceSnapDistance * 0.5f;
            RaycastHit[] hits = Physics.SphereCastAll(origin, radius * 0.35f, direction, segmentSurfaceSnapDistance, crawlableSurfaceMask, QueryTriggerInteraction.Ignore);
            for (int h = 0; h < hits.Length; h++)
            {
                RaycastHit hit = hits[h];
                if (hit.collider == null || hit.collider.isTrigger || hit.collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                float distance = Vector3.Distance(position, hit.point);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    snappedNormal = hit.normal.sqrMagnitude > 0.0001f ? hit.normal.normalized : snappedNormal;
                    snappedPosition = hit.point + snappedNormal * (radius + segmentWallOffset);
                    surfaceCollider = hit.collider;
                    found = true;
                }
            }
        }

        if (found)
        {
            return true;
        }

        Collider[] nearby = Physics.OverlapSphere(
            position,
            segmentSurfaceSnapDistance,
            crawlableSurfaceMask,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < nearby.Length; i++)
        {
            Collider collider = nearby[i];

            if (collider == null || collider.isTrigger || collider.transform.IsChildOf(transform))
            {
                continue;
            }

            Vector3 closest = GetSafeClosestPoint(collider, position);
            Vector3 normal = position - closest;

            if (normal.sqrMagnitude < 0.0001f)
            {
                normal = position - collider.bounds.center;
            }

            if (normal.sqrMagnitude < 0.0001f)
            {
                normal = snappedNormal;
            }

            float distance = Vector3.Distance(position, closest);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                snappedNormal = normal.normalized;
                snappedPosition = closest + snappedNormal * (radius + segmentWallOffset);
                surfaceCollider = collider;
                found = true;
            }
        }

        return found;
    }

    private bool IsSegmentOverlapping(Transform segment, Vector3 position, float radius, out Collider blockingCollider)
    {
        Collider[] overlaps = Physics.OverlapSphere(position, radius, environmentCollisionMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < overlaps.Length; i++)
        {
            Collider other = overlaps[i];
            if (ShouldIgnoreCollider(segment, other))
            {
                continue;
            }

            blockingCollider = other;
            return true;
        }

        blockingCollider = null;
        return false;
    }

    private bool ShouldIgnoreCollider(Transform segment, Collider collider)
    {
        if (collider == null || collider.isTrigger)
        {
            return true;
        }

        if (collider.transform == segment || collider.transform.IsChildOf(transform))
        {
            return true;
        }

        return false;
    }

    private void SaveSafePoseIfValid(int index, Transform segment, Vector3 position, Quaternion rotation, Vector3 normal)
    {
        if (!enableSegmentCollisionCorrection)
        {
            return;
        }

        float radius = GetSegmentCollisionRadius();
        if (IsSegmentOverlapping(segment, position, radius, out _))
        {
            return;
        }

        segmentSafePoses[index] = new SegmentSafePose
        {
            Position = position,
            Rotation = rotation,
            SurfaceNormal = normal.sqrMagnitude > 0.0001f ? normal.normalized : transform.up,
            SurfaceCollider = segmentSafePoses[index].SurfaceCollider,
            HasPose = true
        };
    }

    private float GetSegmentCollisionRadius()
    {
        return segmentDiameter * 0.5f * segmentCollisionRadiusMultiplier;
    }

    private void LogSegmentCorrection(int index, string action, Collider collider)
    {
        if (!logSegmentCorrectionEvents || Time.time < nextSegmentCorrectionLogTime)
        {
            return;
        }

        nextSegmentCorrectionLogTime = Time.time + 0.5f;
        string colliderName = collider != null ? collider.name : "none";
        Debug.Log($"CentipedeBodyController: segment {index} {action}, collider={colliderName}.", this);
    }

    private TrailPoint SampleTrailAtDistance(float distance)
    {
        if (trail.Count == 0 || distance <= 0f)
        {
            return trail.Count > 0 ? trail[0] : new TrailPoint(transform.position, transform.up, transform.forward);
        }

        float walked = 0f;
        for (int i = 0; i < trail.Count - 1; i++)
        {
            TrailPoint a = trail[i];
            TrailPoint b = trail[i + 1];
            float segmentLength = Vector3.Distance(a.Position, b.Position);
            if (segmentLength <= 0.0001f)
            {
                continue;
            }

            if (walked + segmentLength >= distance)
            {
                float t = Mathf.Clamp01((distance - walked) / segmentLength);
                return new TrailPoint(
                    Vector3.Lerp(a.Position, b.Position, t),
                    Vector3.Slerp(a.Normal, b.Normal, t),
                    Vector3.Slerp(a.Forward, b.Forward, t));
            }

            walked += segmentLength;
        }

        TrailPoint last = trail[trail.Count - 1];
        float remaining = distance - walked;
        return new TrailPoint(last.Position - last.Forward * remaining, last.Normal, last.Forward);
    }

    private static void DestroySafe(GameObject gameObject)
    {
        if (Application.isPlaying)
        {
            Destroy(gameObject);
        }
        else
        {
            DestroyImmediate(gameObject);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos)
        {
            return;
        }

        Gizmos.color = Color.yellow;
        for (int i = 0; i < trail.Count - 1; i++)
        {
            Gizmos.DrawLine(trail[i].Position, trail[i + 1].Position);
        }

        Gizmos.color = Color.white;
        for (int i = 0; i < segments.Count; i++)
        {
            Transform segment = segments[i];
            if (segment != null)
            {
                if (segmentCollisionState != null && i < segmentCollisionState.Length && segmentCollisionState[i])
                {
                    Gizmos.color = Color.red;
                }
                else if (segmentCorrectedState != null && i < segmentCorrectedState.Length && segmentCorrectedState[i])
                {
                    Gizmos.color = Color.yellow;
                }
                else
                {
                    Gizmos.color = Color.green;
                }

                Gizmos.DrawWireSphere(segment.position, GetSegmentCollisionRadius());
                if (segmentSurfaceNormals != null && i < segmentSurfaceNormals.Length && segmentSurfaceNormals[i].sqrMagnitude > 0.0001f)
                {
                    Gizmos.DrawLine(segment.position, segment.position + segmentSurfaceNormals[i].normalized * 0.75f);
                }
            }
        }
    }
}
