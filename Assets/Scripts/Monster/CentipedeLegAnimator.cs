using UnityEngine;

public class CentipedeLegAnimator : MonoBehaviour
{
    [SerializeField] float swingAmplitude = 25f;
    [SerializeField] float swingSpeed = 6f;
    [SerializeField] float phaseOffset = 0.35f;

    Transform[] legs;

    void Awake()
    {
        legs = GetComponentsInChildren<Transform>();
    }

    void Update()
    {
        if (legs == null) return;
        for (int i = 0; i < legs.Length; i++)
        {
            if (legs[i] == transform) continue;
            float phase = Time.time * swingSpeed + i * phaseOffset;
            legs[i].localRotation = Quaternion.Euler(Mathf.Sin(phase) * swingAmplitude, 0f, legs[i].localEulerAngles.z);
        }
    }
}
