using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

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

        if (triggered || !playerInside) return;

#if ENABLE_INPUT_SYSTEM
        bool pressed = false;
        Key mappedKey;
        if (TryMapKeyCodeToInputKey(interactKey, out mappedKey))
        {
            var kb = Keyboard.current;
            if (kb != null && kb[mappedKey].wasPressedThisFrame)
            {
                pressed = true;
            }
        }
        if (pressed)
        {
            TriggerVictory();
        }
#else
        if (Input.GetKeyDown(interactKey))
        {
            TriggerVictory();
        }
#endif
    }

#if ENABLE_INPUT_SYSTEM
    private static bool TryMapKeyCodeToInputKey(KeyCode kc, out Key key)
    {
        if (kc >= KeyCode.A && kc <= KeyCode.Z)
        {
            key = (Key)((int)Key.A + (kc - KeyCode.A));
            return true;
        }

        if (kc >= KeyCode.Alpha0 && kc <= KeyCode.Alpha9)
        {
            key = (Key)((int)Key.Digit0 + (kc - KeyCode.Alpha0));
            return true;
        }

        switch (kc)
        {
            case KeyCode.Space:
                key = Key.Space; return true;
            case KeyCode.Return:
            case KeyCode.KeypadEnter:
                key = Key.Enter; return true;
            case KeyCode.Escape:
                key = Key.Escape; return true;
            case KeyCode.LeftArrow:
                key = Key.LeftArrow; return true;
            case KeyCode.RightArrow:
                key = Key.RightArrow; return true;
            case KeyCode.UpArrow:
                key = Key.UpArrow; return true;
            case KeyCode.DownArrow:
                key = Key.DownArrow; return true;
        }

        key = Key.None;
        return false;
    }
#endif

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
