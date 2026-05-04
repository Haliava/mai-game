using UnityEngine;

public class CentipedeSegment : MonoBehaviour
{
    [SerializeField] Transform followTarget;
    [SerializeField] float spacing = 1.2f;
    [SerializeField] float followSpeed = 8f;

    public void Configure(Transform target, float segmentSpacing, float speed)
    {
        followTarget = target;
        spacing = segmentSpacing;
        followSpeed = speed;
    }

    void Update()
    {
        if (followTarget == null) return;
        Vector3 offset = transform.position - followTarget.position;
        if (offset.sqrMagnitude < 0.001f) offset = -followTarget.forward;
        Vector3 desired = followTarget.position + offset.normalized * spacing;
        transform.position = Vector3.Lerp(transform.position, desired, followSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation((followTarget.position - transform.position).normalized, Vector3.up), followSpeed * Time.deltaTime);
    }
}
