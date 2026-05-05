using System.Collections.Generic;
using UnityEngine;

public class CentipedeBody : MonoBehaviour
{
    struct TrailPoint
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Vector3 Forward;
        public bool Airborne;

        public TrailPoint(Vector3 position, Vector3 normal, Vector3 forward, bool airborne)
        {
            Position = position;
            Normal = normal;
            Forward = forward;
            Airborne = airborne;
        }
    }

    [SerializeField] Transform head;
    [SerializeField] List<CentipedeSegment> segments = new List<CentipedeSegment>();
    [SerializeField] int segmentCount = 18;
    [SerializeField] float segmentSpacing = 2.7f;
    [SerializeField] float headDiameter = 3.4f;
    [SerializeField] float segmentDiameter = 3f;
    [SerializeField] float segmentMoveSpeed = 9f;
    [SerializeField] float trailRecordDistance = 0.15f;
    [SerializeField] int maxTrailPoints = 1600;
    [SerializeField] float maxSegmentStretch = 1.45f;
    [SerializeField] float segmentCorrectionStrength = 20f;
    [SerializeField] LayerMask walkableMask = ~0;
    [SerializeField] Material bodyMaterial;
    [SerializeField] Material legMaterial;

    readonly List<TrailPoint> trail = new List<TrailPoint>();
    SurfaceWalkerMotor headMotor;

    public Transform Head { get { return head; } }
    public SurfaceWalkerMotor HeadMotor { get { return headMotor; } }
    public IReadOnlyList<CentipedeSegment> Segments { get { return segments; } }
    public float SegmentSpacing { get { return segmentSpacing; } }

    public void Configure(LayerMask mask, Material monsterMaterial)
    {
        walkableMask = mask;
        if (monsterMaterial != null)
        {
            bodyMaterial = monsterMaterial;
            legMaterial = monsterMaterial;
        }
    }

    public void CreateBody()
    {
        EnsureHead();
        EnsureSegments();
        ResetTrail();
    }

    public void TickBody(float deltaTime, bool headAirborne)
    {
        if (head == null) return;
        if (headMotor == null) headMotor = head.GetComponent<SurfaceWalkerMotor>();

        RecordHeadPose(headAirborne);
        for (int i = 0; i < segments.Count; i++)
        {
            if (segments[i] == null) continue;
            TrailPoint target = SampleTrail(segmentSpacing * (i + 1));
            TrailPoint ahead = SampleTrail(Mathf.Max(0f, segmentSpacing * (i + 0.5f)));
            Vector3 forward = ahead.Position - target.Position;
            if (forward.sqrMagnitude < 0.001f) forward = target.Forward;

            segments[i].SetTrailTarget(target.Position, forward, target.Normal, target.Airborne);
            Transform anchor = i == 0 ? head : segments[i - 1].transform;
            segments[i].SetStretchLimit(anchor, segmentSpacing, maxSegmentStretch, segmentCorrectionStrength);
        }
    }

    void EnsureHead()
    {
        if (head == null)
        {
            Transform existing = transform.Find("Head");
            if (existing != null) head = existing;
        }

        if (head == null)
        {
            GameObject headObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            headObject.name = "Head";
            headObject.transform.SetParent(transform, true);
            headObject.transform.position = transform.position;
            head = headObject.transform;
        }

        head.localScale = Vector3.one * headDiameter;
        ApplyMaterial(head.gameObject, bodyMaterial);

        Rigidbody rb = head.GetComponent<Rigidbody>();
        if (rb == null) rb = head.gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        headMotor = head.GetComponent<SurfaceWalkerMotor>();
        if (headMotor == null) headMotor = head.gameObject.AddComponent<SurfaceWalkerMotor>();
        headMotor.Configure(walkableMask, transform, headDiameter * 0.42f);
    }

    void EnsureSegments()
    {
        for (int i = segments.Count - 1; i >= 0; i--)
        {
            if (segments[i] == null) segments.RemoveAt(i);
        }

        for (int i = 0; i < segmentCount; i++)
        {
            CentipedeSegment segment = i < segments.Count ? segments[i] : null;
            if (segment == null)
            {
                Transform existing = transform.Find("Segment_" + (i + 1).ToString("00"));
                if (existing != null) segment = existing.GetComponent<CentipedeSegment>();
            }

            if (segment == null)
            {
                GameObject segmentObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                segmentObject.name = "Segment_" + (i + 1).ToString("00");
                segmentObject.transform.SetParent(transform, true);
                segmentObject.transform.position = head.position - head.forward * segmentSpacing * (i + 1);
                segment = segmentObject.AddComponent<CentipedeSegment>();
            }

            if (!segments.Contains(segment)) segments.Add(segment);
            SetupSegment(segment, i);
        }
    }

    void SetupSegment(CentipedeSegment segment, int index)
    {
        if (segment == null) return;
        Transform segmentTransform = segment.transform;
        segmentTransform.localScale = Vector3.one * segmentDiameter;
        ApplyMaterial(segment.gameObject, bodyMaterial);

        Rigidbody rb = segment.GetComponent<Rigidbody>();
        if (rb == null) rb = segment.gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        SurfaceWalkerMotor motor = segment.GetComponent<SurfaceWalkerMotor>();
        if (motor == null) motor = segment.gameObject.AddComponent<SurfaceWalkerMotor>();
        motor.Configure(walkableMask, transform, segmentDiameter * 0.42f);

        CentipedeLegCoordinator coordinator = segment.GetComponent<CentipedeLegCoordinator>();
        if (coordinator == null) coordinator = segment.gameObject.AddComponent<CentipedeLegCoordinator>();
        coordinator.EnsureLegs(index, segmentDiameter * 0.5f, walkableMask, legMaterial != null ? legMaterial : bodyMaterial);
        segment.SetLegCoordinator(coordinator);
        segment.Configure(index == 0 ? head : segments[Mathf.Max(0, index - 1)].transform, segmentSpacing, segmentMoveSpeed);
        segment.ConfigureSurface(walkableMask, 4f, segmentDiameter * 0.42f);
    }

    void ResetTrail()
    {
        trail.Clear();
        if (head == null) return;

        Vector3 normal = headMotor != null ? headMotor.SurfaceNormal : head.up;
        Vector3 forward = head.forward.sqrMagnitude > 0.001f ? head.forward.normalized : Vector3.forward;
        for (int i = 0; i <= segmentCount + 8; i++)
        {
            trail.Add(new TrailPoint(head.position - forward * segmentSpacing * i, normal, forward, false));
        }
    }

    void RecordHeadPose(bool airborne)
    {
        Vector3 normal = headMotor != null ? headMotor.SurfaceNormal : head.up;
        Vector3 forward = head.forward.sqrMagnitude > 0.001f ? head.forward.normalized : Vector3.forward;
        if (trail.Count > 0 && Vector3.Distance(trail[0].Position, head.position) < trailRecordDistance)
        {
            trail[0] = new TrailPoint(head.position, normal, forward, airborne);
            return;
        }

        trail.Insert(0, new TrailPoint(head.position, normal, forward, airborne));
        while (trail.Count > maxTrailPoints) trail.RemoveAt(trail.Count - 1);
    }

    TrailPoint SampleTrail(float distance)
    {
        if (trail.Count == 0)
        {
            Vector3 normal = headMotor != null ? headMotor.SurfaceNormal : Vector3.up;
            return new TrailPoint(head != null ? head.position : transform.position, normal, transform.forward, false);
        }

        float travelled = 0f;
        for (int i = 1; i < trail.Count; i++)
        {
            float segmentDistance = Vector3.Distance(trail[i - 1].Position, trail[i].Position);
            if (travelled + segmentDistance >= distance)
            {
                float t = segmentDistance > 0.001f ? (distance - travelled) / segmentDistance : 0f;
                Vector3 position = Vector3.Lerp(trail[i - 1].Position, trail[i].Position, t);
                Vector3 normal = Vector3.Slerp(trail[i - 1].Normal, trail[i].Normal, t).normalized;
                Vector3 forward = Vector3.Slerp(trail[i - 1].Forward, trail[i].Forward, t).normalized;
                bool airborne = trail[i - 1].Airborne || trail[i].Airborne;
                return new TrailPoint(position, normal, forward, airborne);
            }
            travelled += segmentDistance;
        }

        return trail[trail.Count - 1];
    }

    void ApplyMaterial(GameObject target, Material material)
    {
        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer != null && material != null) renderer.sharedMaterial = material;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.55f);
        for (int i = 1; i < trail.Count; i++)
        {
            Gizmos.DrawLine(trail[i - 1].Position, trail[i].Position);
        }

        Gizmos.color = Color.white;
        for (int i = 0; i < segments.Count; i++)
        {
            if (segments[i] == null) continue;
            Transform anchor = i == 0 ? head : segments[i - 1].transform;
            if (anchor != null) Gizmos.DrawLine(anchor.position, segments[i].transform.position);
        }
    }
}
