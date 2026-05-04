using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class HookProjectile : MonoBehaviour
{
    Rigidbody rb;
    Light hookLight;
    GrapplingHookController owner;
    LayerMask grappleMask;
    bool armed;
    [SerializeField] float hookMass = 0.25f;
    [SerializeField] float slideDamping = 0.8f;
    [SerializeField] float slideDownForce = 12f;

    public Rigidbody Rigidbody { get { return rb; } }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        EnsureLight();
    }

    public void Fire(GrapplingHookController hookOwner, Vector3 position, Vector3 velocity, LayerMask attachMask)
    {
        owner = hookOwner;
        grappleMask = attachMask;
        armed = true;
        if (rb == null) rb = GetComponent<Rigidbody>();
        EnsureLight();
        gameObject.SetActive(true);
        transform.position = position;
        transform.rotation = Quaternion.LookRotation(velocity.normalized, Vector3.up);
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.mass = hookMass;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.linearVelocity = velocity;
        hookLight.enabled = true;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!armed || owner == null) return;

        ContactPoint contact = collision.contacts.Length > 0 ? collision.contacts[0] : default(ContactPoint);
        GrapplePoint point = collision.collider.GetComponentInParent<GrapplePoint>();
        bool layerAllowed = ((1 << collision.gameObject.layer) & grappleMask.value) != 0;
        bool canAttachHere = owner != null && owner.IsAttachSurfaceAllowed(contact.normal);
        if ((point != null || layerAllowed) && canAttachHere)
        {
            armed = false;
            rb.linearVelocity = Vector3.zero;
            rb.isKinematic = true;
            Vector3 attachPosition = point != null ? point.AttachTransform.position : contact.point;
            transform.position = attachPosition;
            owner.AttachAt(attachPosition, point);
        }
        else
        {
            SlideFromSurface(contact.normal);
        }
    }

    void SlideFromSurface(Vector3 normal)
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        Vector3 incoming = rb.linearVelocity;
        Vector3 slideVelocity = Vector3.ProjectOnPlane(incoming, normal) * slideDamping;
        Vector3 downSlide = Vector3.ProjectOnPlane(Vector3.down * slideDownForce, normal);

        rb.isKinematic = false;
        rb.linearVelocity = slideVelocity + downSlide;
    }

    void OnDisable()
    {
        if (hookLight != null) hookLight.enabled = false;
    }

    void EnsureLight()
    {
        if (hookLight == null) hookLight = GetComponent<Light>();
        if (hookLight == null) hookLight = gameObject.AddComponent<Light>();

        hookLight.type = LightType.Point;
        hookLight.color = new Color(0.55f, 0.82f, 1f);
        hookLight.range = 18f;
        hookLight.intensity = 2.2f;
        hookLight.shadows = LightShadows.None;
        hookLight.enabled = gameObject.activeInHierarchy;
    }
}
