using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class FirstPersonController : MonoBehaviour
{
    [SerializeField] float walkSpeed = 5f;
    [SerializeField] float sprintMultiplier = 1.65f;
    [SerializeField] float airControlMultiplier = 0.45f;
    [SerializeField] float jumpForce = 6.5f;
    [SerializeField] float customGravityMultiplier = 1.25f;
    [SerializeField] float groundCheckDistance = 1.15f;
    [SerializeField] LayerMask groundMask = ~0;
    [SerializeField] float maxGroundSlopeAngle = 55f;
    [SerializeField] RopeController ropeController;

    Rigidbody rb;
    CapsuleCollider capsule;
    bool isGrounded;
    Vector3 groundNormal = Vector3.up;

    public bool IsGrounded { get { return isGrounded; } }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        rb.freezeRotation = true;
        rb.useGravity = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    void FixedUpdate()
    {
        UpdateGrounded();
        if (ropeController == null || !ropeController.IsAttached || isGrounded)
        {
            Move();
        }

        if (customGravityMultiplier > 1f)
        {
            rb.AddForce(Physics.gravity * (customGravityMultiplier - 1f), ForceMode.Acceleration);
        }
    }

    void Update()
    {
        if (PrototypeInput.JumpPressed && isGrounded && (ropeController == null || !ropeController.IsAttached))
        {
            Vector3 velocity = rb.linearVelocity;
            if (velocity.y < 0f) velocity.y = 0f;
            rb.linearVelocity = velocity;
            rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);
        }
    }

    void Move()
    {
        Vector2 move = PrototypeInput.Move;
        float x = move.x;
        float z = move.y;
        Vector3 input = Vector3.ClampMagnitude(transform.right * x + transform.forward * z, 1f);
        bool canSprint = PrototypeInput.ShiftHeld && (ropeController == null || !ropeController.IsAttached);
        float targetSpeed = walkSpeed * (canSprint ? sprintMultiplier : 1f);
        Vector3 desiredVelocity = input * targetSpeed;
        Vector3 currentHorizontal = Vector3.ProjectOnPlane(rb.linearVelocity, Vector3.up);
        float control = isGrounded ? 1f : airControlMultiplier;
        Vector3 delta = desiredVelocity - currentHorizontal;

        if (isGrounded) delta = Vector3.ProjectOnPlane(delta, groundNormal);
        rb.AddForce(delta * control, ForceMode.VelocityChange);
    }

    void UpdateGrounded()
    {
        float radius = Mathf.Max(0.05f, capsule.radius * 0.9f);
        RaycastHit hit;
        isGrounded = Physics.SphereCast(transform.position + Vector3.up * 0.1f, radius, Vector3.down, out hit, groundCheckDistance, groundMask, QueryTriggerInteraction.Ignore)
            && Vector3.Angle(hit.normal, Vector3.up) <= maxGroundSlopeAngle;
        groundNormal = isGrounded ? hit.normal : Vector3.up;
    }
}
