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
    [SerializeField] float slideDamping = 1f;
    [SerializeField] float slideDownForce = 12f;
    [SerializeField] float hookDrag = 0f;

    Collider hookCollider;
    PhysicsMaterial frictionlessMaterial;

    public Rigidbody Rigidbody { get { return rb; } }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        hookCollider = GetComponent<Collider>();
        EnsureFrictionlessCollider();
        EnsureLight();
    }

    public void Fire(GrapplingHookController hookOwner, Vector3 position, Vector3 velocity, LayerMask attachMask)
    {
        owner = hookOwner;
        grappleMask = attachMask;
        armed = true;
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (hookCollider == null) hookCollider = GetComponent<Collider>();
        EnsureFrictionlessCollider();
        EnsureLight();
        gameObject.SetActive(true);
        transform.position = position;
        transform.rotation = Quaternion.LookRotation(velocity.normalized, Vector3.up);
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.mass = hookMass;
        rb.linearDamping = hookDrag;
        rb.angularDamping = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.linearVelocity = velocity;
        hookLight.enabled = true;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!armed || owner == null) return;

        TryAttachOrSlide(collision, true);
    }

    void OnCollisionStay(Collision collision)
    {
        if (!armed || owner == null) return;

        TryAttachOrSlide(collision, true);
    }

    void TryAttachOrSlide(Collision collision, bool slideIfRejected)
    {
        Vector3 attachPosition;
        if (owner.TryResolveHookAttach(collision, this, out attachPosition))
        {
            armed = false;
            PinTo(attachPosition);
            owner.ConfirmHookAttached(attachPosition, collision.collider.GetComponentInParent<GrapplePoint>());
        }
        else if (slideIfRejected)
        {
            ContactPoint contact = collision.contacts.Length > 0 ? collision.contacts[0] : default(ContactPoint);
            SlideFromSurface(contact.normal);
        }
    }

    public void PinTo(Vector3 position)
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        armed = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
        transform.position = position;
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

    void EnsureFrictionlessCollider()
    {
        if (hookCollider == null) return;

        if (frictionlessMaterial == null)
        {
            frictionlessMaterial = new PhysicsMaterial("Hook_Frictionless");
            frictionlessMaterial.dynamicFriction = 0f;
            frictionlessMaterial.staticFriction = 0f;
            frictionlessMaterial.bounciness = 0f;
            frictionlessMaterial.frictionCombine = PhysicsMaterialCombine.Minimum;
            frictionlessMaterial.bounceCombine = PhysicsMaterialCombine.Minimum;
        }

        hookCollider.sharedMaterial = frictionlessMaterial;
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
