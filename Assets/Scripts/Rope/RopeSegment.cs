using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class RopeSegment : MonoBehaviour
{
    [SerializeField] float radius = 0.05f;
    [SerializeField] int segmentIndex;

    Rigidbody rb;
    CapsuleCollider capsule;
    RopeCollisionTracker collisionTracker;

    public float Radius { get { return radius; } }
    public int SegmentIndex { get { return segmentIndex; } }
    public float Length { get { return Capsule.height; } }
    public Vector3 WorldStart { get { return transform.TransformPoint(Vector3.down * Length * 0.5f); } }
    public Vector3 WorldEnd { get { return transform.TransformPoint(Vector3.up * Length * 0.5f); } }
    public Rigidbody Rigidbody
    {
        get
        {
            if (rb == null) rb = GetComponent<Rigidbody>();
            return rb;
        }
    }

    public CapsuleCollider Capsule
    {
        get
        {
            if (capsule == null) capsule = GetComponent<CapsuleCollider>();
            return capsule;
        }
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
    }

    public void Configure(int index, RopeCollisionTracker tracker, float segmentRadius, float length)
    {
        segmentIndex = index;
        collisionTracker = tracker;
        radius = Mathf.Max(0.01f, segmentRadius);

        CapsuleCollider segmentCapsule = Capsule;
        segmentCapsule.direction = 1;
        segmentCapsule.radius = radius;
        segmentCapsule.height = Mathf.Max(radius * 2f, length);

        Rigidbody body = Rigidbody;
        body.mass = Mathf.Max(0.08f, length * 0.25f);
        body.useGravity = true;
        body.linearDamping = 0.18f;
        body.angularDamping = 0.45f;
        body.isKinematic = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.solverIterations = 20;
        body.solverVelocityIterations = 8;
        body.maxDepenetrationVelocity = 8f;
    }

    public void SetIndex(int index)
    {
        segmentIndex = index;
    }

    void OnCollisionEnter(Collision collision)
    {
        ReportContacts(collision);
    }

    void OnCollisionStay(Collision collision)
    {
        ReportContacts(collision);
    }

    void ReportContacts(Collision collision)
    {
        if (collisionTracker == null || collision == null) return;

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint contact = collision.GetContact(i);
            collisionTracker.ReportContact(contact.otherCollider, contact.point, contact.normal, segmentIndex);
        }
    }
}
