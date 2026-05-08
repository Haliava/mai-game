using System.Collections;
using UnityEngine;

public sealed class CentipedeIntroFocus : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FPSCharacterController3D playerController;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform focusTarget;

    [Header("Intro")]
    [SerializeField] private bool playIntroOnStart = true;
    [SerializeField, Min(0.1f)] private float introDuration = 5f;
    [SerializeField, Min(1f)] private float introZoomMultiplier = 2f;
    [SerializeField, Min(0.01f)] private float introCameraLerpTime = 0.35f;
    [SerializeField, Min(0.01f)] private float returnCameraLerpTime = 0.35f;
    [SerializeField, Min(0f)] private float startDelay = 0.15f;

    private Coroutine introRoutine;

    private void Awake()
    {
        CacheReferences();
    }

    private void Start()
    {
        if (playIntroOnStart)
        {
            PlayIntro();
        }
    }

    [ContextMenu("Play Centipede Intro")]
    public void PlayIntro()
    {
        CacheReferences();
        if (introRoutine != null)
        {
            StopCoroutine(introRoutine);
        }

        introRoutine = StartCoroutine(PlayIntroRoutine());
    }

    private void CacheReferences()
    {
        if (playerController == null)
        {
            playerController = FindAnyObjectByType<FPSCharacterController3D>();
        }

        if (playerCamera == null)
        {
            playerCamera = playerController != null ? playerController.PlayerCamera : Camera.main;
        }

        if (focusTarget == null)
        {
            CentipedeController centipede = GetComponent<CentipedeController>();
            if (centipede != null)
            {
                focusTarget = centipede.LookTarget;
            }
        }
    }

    private IEnumerator PlayIntroRoutine()
    {
        if (playerController == null || playerCamera == null || focusTarget == null)
        {
            Debug.LogWarning("CentipedeIntroFocus: cannot play intro, missing player camera/controller or focus target.", this);
            yield break;
        }

        if (startDelay > 0f)
        {
            yield return new WaitForSeconds(startDelay);
        }

        Quaternion initialBodyRotation = playerController.transform.rotation;
        Quaternion initialCameraLocalRotation = playerCamera.transform.localRotation;
        float initialFov = playerCamera.fieldOfView;
        float zoomFov = Mathf.Max(10f, initialFov / Mathf.Max(1f, introZoomMultiplier));

        playerController.SetLookPaused(true);
        playerController.SetGrappleMovementPaused(true);
        playerController.SetCurrentVelocity(Vector3.zero);

        Debug.Log("CentipedeIntroFocus: intro started.", this);

        float elapsed = 0f;
        while (elapsed < introDuration)
        {
            elapsed += Time.deltaTime;
            float lookWeight = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.01f, introCameraLerpTime));
            SmoothLookAt(focusTarget.position, lookWeight);
            playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, zoomFov, lookWeight);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < returnCameraLerpTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, returnCameraLerpTime));
            Quaternion bodyRotation = Quaternion.Slerp(playerController.transform.rotation, initialBodyRotation, t);
            Quaternion cameraLocalRotation = Quaternion.Slerp(playerCamera.transform.localRotation, initialCameraLocalRotation, t);
            playerController.ForceViewRotation(bodyRotation, cameraLocalRotation);
            playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, initialFov, t);
            yield return null;
        }

        playerCamera.fieldOfView = initialFov;
        playerController.ForceViewRotation(initialBodyRotation, initialCameraLocalRotation);
        playerController.SetGrappleMovementPaused(false);
        playerController.SetLookPaused(false);
        introRoutine = null;

        Debug.Log("CentipedeIntroFocus: intro finished.", this);
    }

    private void SmoothLookAt(Vector3 worldPoint, float weight)
    {
        Vector3 cameraPosition = playerCamera.transform.position;
        Vector3 toTarget = worldPoint - cameraPosition;
        if (toTarget.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Vector3 horizontalDirection = Vector3.ProjectOnPlane(toTarget, Vector3.up);
        Quaternion targetBodyRotation = playerController.transform.rotation;
        if (horizontalDirection.sqrMagnitude > 0.0001f)
        {
            targetBodyRotation = Quaternion.LookRotation(horizontalDirection.normalized, Vector3.up);
        }

        Vector3 localDirection = Quaternion.Inverse(targetBodyRotation) * toTarget.normalized;
        float targetPitch = -Mathf.Asin(Mathf.Clamp(localDirection.y, -1f, 1f)) * Mathf.Rad2Deg;
        Quaternion targetCameraLocalRotation = Quaternion.Euler(Mathf.Clamp(targetPitch, -85f, 85f), 0f, 0f);

        Quaternion bodyRotation = Quaternion.Slerp(playerController.transform.rotation, targetBodyRotation, weight);
        Quaternion cameraLocalRotation = Quaternion.Slerp(playerCamera.transform.localRotation, targetCameraLocalRotation, weight);
        playerController.ForceViewRotation(bodyRotation, cameraLocalRotation);
    }
}
