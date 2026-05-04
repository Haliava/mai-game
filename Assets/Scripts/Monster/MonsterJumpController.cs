using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class MonsterJumpController : MonoBehaviour
{
    [SerializeField] float jumpForce = 16f;
    [SerializeField] float jumpUpwardForce = 8f;
    [SerializeField] float maxJumpDistance = 25f;
    [SerializeField] float jumpCooldown = 2f;

    Rigidbody rb;
    float nextJumpTime;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public bool TryJumpTo(Vector3 target)
    {
        if (Time.time < nextJumpTime) return false;
        Vector3 toTarget = target - transform.position;
        if (toTarget.magnitude > maxJumpDistance) return false;

        rb.isKinematic = false;
        rb.linearVelocity = Vector3.zero;
        Vector3 impulse = toTarget.normalized * jumpForce + Vector3.up * jumpUpwardForce;
        rb.AddForce(impulse, ForceMode.VelocityChange);
        nextJumpTime = Time.time + jumpCooldown;
        return true;
    }
}
