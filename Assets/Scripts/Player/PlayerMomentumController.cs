using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMomentumController : MonoBehaviour
{
    [SerializeField] float maxSpeed = 38f;

    Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        if (rb.linearVelocity.magnitude > maxSpeed)
        {
            rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
        }
    }
}
