using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

public static class PrototypeInput
{
    public static Vector2 Move
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                Vector2 move = Vector2.zero;
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) move.x -= 1f;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) move.x += 1f;
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) move.y -= 1f;
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) move.y += 1f;
                return Vector2.ClampMagnitude(move, 1f);
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Vector2.ClampMagnitude(new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")), 1f);
#else
            return Vector2.zero;
#endif
        }
    }

    public static Vector2 MouseDelta
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null) return Mouse.current.delta.ReadValue();
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
#else
            return Vector2.zero;
#endif
        }
    }

    public static bool JumpPressed { get { return KeyPressed(KeyCode.Space); } }
    public static bool ShiftHeld { get { return KeyHeld(KeyCode.LeftShift) || KeyHeld(KeyCode.RightShift); } }
    public static bool ControlHeld { get { return KeyHeld(KeyCode.LeftControl) || KeyHeld(KeyCode.RightControl); } }

    public static bool FirePressed
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(0);
#else
            return false;
#endif
        }
    }

    public static bool FireHeld
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null) return Mouse.current.leftButton.isPressed;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButton(0);
#else
            return false;
#endif
        }
    }

    public static bool AltFirePressed
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(1);
#else
            return false;
#endif
        }
    }

    static bool KeyPressed(KeyCode key)
    {
#if ENABLE_INPUT_SYSTEM
        KeyControl control = GetKey(key);
        if (control != null) return control.wasPressedThisFrame;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(key);
#else
        return false;
#endif
    }

    static bool KeyHeld(KeyCode key)
    {
#if ENABLE_INPUT_SYSTEM
        KeyControl control = GetKey(key);
        if (control != null) return control.isPressed;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKey(key);
#else
        return false;
#endif
    }

#if ENABLE_INPUT_SYSTEM
    static KeyControl GetKey(KeyCode key)
    {
        if (Keyboard.current == null) return null;
        switch (key)
        {
            case KeyCode.Space: return Keyboard.current.spaceKey;
            case KeyCode.LeftShift: return Keyboard.current.leftShiftKey;
            case KeyCode.RightShift: return Keyboard.current.rightShiftKey;
            case KeyCode.LeftControl: return Keyboard.current.leftCtrlKey;
            case KeyCode.RightControl: return Keyboard.current.rightCtrlKey;
            default: return null;
        }
    }
#endif
}
