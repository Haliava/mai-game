using UnityEngine;

public class PlayerCameraController : MonoBehaviour
{
    [SerializeField] float mouseSensitivity = 2f;
    [SerializeField] float minPitch = -85f;
    [SerializeField] float maxPitch = 85f;
    [SerializeField] Transform cameraHolder;

    float pitch;
    float yaw;

    void Awake()
    {
        if (cameraHolder == null && Camera.main != null) cameraHolder = Camera.main.transform;
        yaw = transform.eulerAngles.y;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        Vector2 mouse = PrototypeInput.MouseDelta;
        float mouseX = mouse.x * mouseSensitivity * 0.12f;
        float mouseY = mouse.y * mouseSensitivity * 0.12f;

        yaw += mouseX;
        pitch = Mathf.Clamp(pitch - mouseY, minPitch, maxPitch);
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        if (cameraHolder != null) cameraHolder.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }
}
