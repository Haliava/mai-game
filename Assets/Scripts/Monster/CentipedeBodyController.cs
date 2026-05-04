using System.Collections.Generic;
using UnityEngine;

public class CentipedeBodyController : MonoBehaviour
{
    [SerializeField] Transform head;
    [SerializeField] List<Transform> segments = new List<Transform>();
    [SerializeField] int segmentCount = 10;
    [SerializeField] float segmentSpacing = 1.2f;
    [SerializeField] float followSpeed = 8f;
    [SerializeField] float bodySmoothing = 0.15f;
    [SerializeField] Material bodyMaterial;

    public Transform Head { get { return head; } }
    public List<Transform> Segments { get { return segments; } }

    void Start()
    {
        if (head == null) CreateBody();
    }

    public void CreateBody()
    {
        if (head == null)
        {
            GameObject headGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            headGo.name = "Head";
            headGo.transform.SetParent(transform, false);
            headGo.transform.localScale = Vector3.one * 1.2f;
            head = headGo.transform;
            ApplyMaterial(headGo);
        }

        Transform previous = head;
        for (int i = segments.Count; i < segmentCount; i++)
        {
            GameObject segment = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            segment.name = "Segment_" + (i + 1).ToString("00");
            segment.transform.SetParent(transform, true);
            segment.transform.position = head.position - transform.forward * segmentSpacing * (i + 1);
            segment.transform.localScale = Vector3.one;
            ApplyMaterial(segment);
            CentipedeSegment follower = segment.AddComponent<CentipedeSegment>();
            follower.Configure(previous, segmentSpacing, followSpeed * Mathf.Clamp01(1f - bodySmoothing));
            segments.Add(segment.transform);
            previous = segment.transform;
        }
    }

    void ApplyMaterial(GameObject go)
    {
        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer != null && bodyMaterial != null) renderer.sharedMaterial = bodyMaterial;
    }
}
