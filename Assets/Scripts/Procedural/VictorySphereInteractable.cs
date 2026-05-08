using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class VictorySphereInteractable : MonoBehaviour
{
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private string victoryMessage = "Вы победили! Вы прошли демо.";
    [SerializeField] private VictoryPopupUI popupUI;
    [SerializeField] private bool triggerOnEnter;
    [SerializeField] private float rotateSpeed = 38f;
    [SerializeField] private float bobAmplitude = 0.16f;
    [SerializeField] private float bobFrequency = 1.2f;

    private bool playerInside;
    private bool triggered;
    private Vector3 startLocalPosition;

    private void Awake()
    {
        Collider ownCollider = GetComponent<Collider>();
        ownCollider.isTrigger = true;
        startLocalPosition = transform.localPosition;
        if (popupUI == null)
        {
            popupUI = VictoryPopupUI.EnsureInScene();
        }
    }

    public void Configure(KeyCode key, VictoryPopupUI popup)
    {
        interactKey = key;
        if (popup != null)
        {
            popupUI = popup;
        }
    }

    private void Update()
    {
        transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);
        transform.localPosition = startLocalPosition + Vector3.up * (Mathf.Sin(Time.time * bobFrequency) * bobAmplitude);

        if (!triggered && playerInside && Input.GetKeyDown(interactKey))
        {
            TriggerVictory();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other))
        {
            return;
        }

        playerInside = true;
        if (triggerOnEnter && !triggered)
        {
            TriggerVictory();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsPlayer(other))
        {
            playerInside = false;
        }
    }

    private void TriggerVictory()
    {
        triggered = true;
        if (popupUI == null)
        {
            popupUI = VictoryPopupUI.EnsureInScene();
        }

        popupUI.Show(victoryMessage);
    }

    private static bool IsPlayer(Collider other)
    {
        return other != null && (other.CompareTag("Player") || other.GetComponentInParent<FPSCharacterController3D>() != null);
    }
}
