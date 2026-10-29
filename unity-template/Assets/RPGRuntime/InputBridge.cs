using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RPGWorkshop
{
    // 同时兼容新旧两套输入系统：工程设置里选哪个都能跑
    public static class InputBridge
    {
        public static bool ClickDown(out Vector2 screenPos)
        {
#if ENABLE_INPUT_SYSTEM
            var m = Mouse.current;
            if (m != null && m.leftButton.wasPressedThisFrame) { screenPos = m.position.ReadValue(); return true; }
            var t = Touchscreen.current;
            if (t != null && t.primaryTouch.press.wasPressedThisFrame) { screenPos = t.primaryTouch.position.ReadValue(); return true; }
#else
            if (Input.GetMouseButtonDown(0)) { screenPos = Input.mousePosition; return true; }
#endif
            screenPos = default;
            return false;
        }

        public static Vector2 Move()
        {
            var v = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k == null) return v;
            if (k.wKey.isPressed || k.upArrowKey.isPressed) v.y += 1;
            if (k.sKey.isPressed || k.downArrowKey.isPressed) v.y -= 1;
            if (k.aKey.isPressed || k.leftArrowKey.isPressed) v.x -= 1;
            if (k.dKey.isPressed || k.rightArrowKey.isPressed) v.x += 1;
#else
            v = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#endif
            return v.sqrMagnitude > 1 ? v.normalized : v;
        }

        public static bool ConfirmDown()
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            return k != null && (k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame);
#else
            return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return);
#endif
        }
    }
}
