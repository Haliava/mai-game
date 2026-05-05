using System.Collections.Generic;
using UnityEngine;

public class CentipedeBodyController : MonoBehaviour
{
    [SerializeField] Transform head;
    [SerializeField] List<Transform> segments = new List<Transform>();
    [SerializeField] int segmentCount = 10;
    [SerializeField] float segmentSpacing = 2.4f;
    [SerializeField] float headDiameter = 3.4f;
    [SerializeField] float segmentDiameter = 3f;
    [SerializeField] float followSpeed = 8f;
    [SerializeField] float bodySmoothing = 0.15f;
    [SerializeField] LayerMask surfaceMask = ~0;
    [SerializeField] float surfaceRayDistance = 10f;
    [SerializeField] float surfaceOffset = 0.55f;
    [SerializeField] float trailRecordMinDistance = 0.2f;
    [SerializeField] int maxTrailPoints = 600;
    [SerializeField] Material bodyMaterial;

    readonly List<Vector3> headTrail = new List<Vector3>();

    public Transform Head { get { return head; } }
    public List<Transform> Segments { get { return segments; } }

    void Start()
    {
        if (head == null) CreateBody();
        ResetTrail();
    }

    void FixedUpdate()
    {
        if (head == null || segments.Count == 0) return;
        RecordHeadPosition();

        for (int i = 0; i < segments.Count; i++)
        {
            CentipedeSegment segment = segments[i] != null ? segments[i].GetComponent<CentipedeSegment>() : null;
            if (segment == null) continue;

            float distanceBehindHead = segmentSpacing * (i + 1);
            Vector3 target = SampleTrail(distanceBehindHead);
            Vector3 ahead = SampleTrail(Mathf.Max(0f, distanceBehindHead - segmentSpacing * 0.5f));
            segment.SetPathTarget(target, ahead - target);
        }
    }

    public void CreateBody()
    {
        if (head == null)
        {
            GameObject headGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            headGo.name = "Head";
            headGo.transform.SetParent(transform, false);
            head = headGo.transform;
            Rigidbody headRb = headGo.GetComponent<Rigidbody>();
            if (headRb == null) headRb = headGo.AddComponent<Rigidbody>();
            headRb.isKinematic = true;
            headRb.useGravity = false;
            ApplyMaterial(headGo);
        }
        head.localScale = Vector3.one * headDiameter;

        Transform previous = head;
        for (int i = segments.Count; i < segmentCount; i++)
        {
            GameObject segment = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            segment.name = "Segment_" + (i + 1).ToString("00");
            segment.transform.SetParent(transform, true);
            segment.transform.position = head.position - transform.forward * segmentSpacing * (i + 1);
            Rigidbody rb = segment.GetComponent<Rigidbody>();
            if (rb == null) rb = segment.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            ApplyMaterial(segment);
            CentipedeSegment follower = segment.AddComponent<CentipedeSegment>();
            follower.Configure(previous, segmentSpacing, followSpeed * Mathf.Clamp01(1f - bodySmoothing));
            follower.ConfigureSurface(surfaceMask, surfaceRayDistance, surfaceOffset);
            segments.Add(segment.transform);
            previous = segment.transform;
        }

        for (int i = 0; i < segments.Count; i++)
        {
            if (segments[i] == null) continue;
            segments[i].localScale = Vector3.one * segmentDiameter;
            CentipedeSegment follower = segments[i].GetComponent<CentipedeSegment>();
            if (follower != null)
            {
                follower.Configure(i == 0 ? head : segments[i - 1], segmentSpacing, followSpeed * Mathf.Clamp01(1f - bodySmoothing));
                follower.ConfigureSurface(surfaceMask, surfaceRayDistance, surfaceOffset + segmentDiameter * 0.3f);
            }
        }

        ResetTrail();
    }

    void ResetTrail()
    {
        headTrail.Clear();
        if (head == null) return;
        headTrail.Add(head.position);
        for (int i = 1; i <= segmentCount + 4; i++)
        {
            headTrail.Add(head.position - transform.forward * segmentSpacing * i);
        }
    }

    void RecordHeadPosition()
    {
        if (headTrail.Count == 0)
        {
            headTrail.Add(head.position);
            return;
        }

        if (Vector3.Distance(headTrail[0], head.position) < trailRecordMinDistance) return;
        headTrail.Insert(0, head.position);
        while (headTrail.Count > maxTrailPoints) headTrail.RemoveAt(headTrail.Count - 1);
    }

    Vector3 SampleTrail(float distance)
    {
        if (headTrail.Count == 0) return head != null ? head.position : transform.position;

        float travelled = 0f;
        for (int i = 1; i < headTrail.Count; i++)
        {
            float segmentDistance = Vector3.Distance(headTrail[i - 1], headTrail[i]);
            if (travelled + segmentDistance >= distance)
            {
                float t = segmentDistance > 0.001f ? (distance - travelled) / segmentDistance : 0f;
                return Vector3.Lerp(headTrail[i - 1], headTrail[i], t);
            }
            travelled += segmentDistance;
        }

        return headTrail[headTrail.Count - 1];
    }

    void ApplyMaterial(GameObject go)
    {
        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer != null && bodyMaterial != null) renderer.sharedMaterial = bodyMaterial;
    }
}
