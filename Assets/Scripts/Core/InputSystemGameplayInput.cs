using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace AttackSkill.Core
{
    /// <summary>
    /// 用新 Input System 填平 <see cref="IGameplayInput"/>（键码/轴名语义）。
    /// 便于在不改调用方的前提下从 Legacy 切过来；后续可再迁到 InputAction。
    /// </summary>
    public sealed class InputSystemGameplayInput : IGameplayInput
    {
        /// <summary>把像素级 mouse.delta 近似成旧 Input Manager 的 Mouse X/Y。</summary>
        const float MouseAxisScale = 0.15f;

        /// <summary>Windows 上通常一格滚轮 ≈ 120；旧 ScrollWheel 约 ±0.1。</summary>
        const float ScrollWheelScale = 1f / 120f;

        static readonly Dictionary<KeyCode, Key> KeyMap = BuildKeyMap();

        public bool GetKey(KeyCode key)
        {
            var control = ResolveKey(key);
            return control != null && control.isPressed;
        }

        public bool GetKeyDown(KeyCode key)
        {
            var control = ResolveKey(key);
            return control != null && control.wasPressedThisFrame;
        }

        public bool GetKeyUp(KeyCode key)
        {
            var control = ResolveKey(key);
            return control != null && control.wasReleasedThisFrame;
        }

        public bool GetButton(string buttonName)
        {
            if (IsJump(buttonName))
            {
                return GetKey(KeyCode.Space) || (Gamepad.current != null && Gamepad.current.buttonSouth.isPressed);
            }

            if (IsSubmit(buttonName))
            {
                return GetKey(KeyCode.Return) || GetKey(KeyCode.KeypadEnter) ||
                       (Gamepad.current != null && Gamepad.current.buttonSouth.isPressed);
            }

            if (IsCancel(buttonName))
            {
                return GetKey(KeyCode.Escape) ||
                       (Gamepad.current != null && Gamepad.current.buttonEast.isPressed);
            }

            return false;
        }

        public bool GetButtonDown(string buttonName)
        {
            if (IsJump(buttonName))
            {
                return GetKeyDown(KeyCode.Space) ||
                       (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);
            }

            if (IsSubmit(buttonName))
            {
                return GetKeyDown(KeyCode.Return) || GetKeyDown(KeyCode.KeypadEnter) ||
                       (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);
            }

            if (IsCancel(buttonName))
            {
                return GetKeyDown(KeyCode.Escape) ||
                       (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);
            }

            return false;
        }

        public float GetAxis(string axisName) => GetAxisRaw(axisName);

        public float GetAxisRaw(string axisName)
        {
            if (string.IsNullOrEmpty(axisName))
            {
                return 0f;
            }

            switch (axisName)
            {
                case "Horizontal":
                    return ReadCompositeAxis(
                        positive: GetKey(KeyCode.D) || GetKey(KeyCode.RightArrow),
                        negative: GetKey(KeyCode.A) || GetKey(KeyCode.LeftArrow),
                        stick: Gamepad.current != null ? Gamepad.current.leftStick.x.ReadValue() : 0f);
                case "Vertical":
                    return ReadCompositeAxis(
                        positive: GetKey(KeyCode.W) || GetKey(KeyCode.UpArrow),
                        negative: GetKey(KeyCode.S) || GetKey(KeyCode.DownArrow),
                        stick: Gamepad.current != null ? Gamepad.current.leftStick.y.ReadValue() : 0f);
                case "Mouse X":
                    return Mouse.current != null ? Mouse.current.delta.x.ReadValue() * MouseAxisScale : 0f;
                case "Mouse Y":
                    return Mouse.current != null ? Mouse.current.delta.y.ReadValue() * MouseAxisScale : 0f;
                case "Mouse ScrollWheel":
                    return Mouse.current != null
                        ? Mouse.current.scroll.y.ReadValue() * ScrollWheelScale
                        : 0f;
                default:
                    return 0f;
            }
        }

        public bool GetMouseButton(int button)
        {
            var control = ResolveMouseButton(button);
            return control != null && control.isPressed;
        }

        public bool GetMouseButtonDown(int button)
        {
            var control = ResolveMouseButton(button);
            return control != null && control.wasPressedThisFrame;
        }

        public bool GetMouseButtonUp(int button)
        {
            var control = ResolveMouseButton(button);
            return control != null && control.wasReleasedThisFrame;
        }

        public Vector3 MousePosition
        {
            get
            {
                if (Mouse.current == null)
                {
                    return Vector3.zero;
                }

                Vector2 p = Mouse.current.position.ReadValue();
                return new Vector3(p.x, p.y, 0f);
            }
        }

        static float ReadCompositeAxis(bool positive, bool negative, float stick)
        {
            float keyboard = 0f;
            if (positive)
            {
                keyboard += 1f;
            }

            if (negative)
            {
                keyboard -= 1f;
            }

            if (Mathf.Abs(keyboard) > 0.01f)
            {
                return keyboard;
            }

            return stick;
        }

        static bool IsJump(string name) =>
            string.Equals(name, "Jump", System.StringComparison.OrdinalIgnoreCase);

        static bool IsSubmit(string name) =>
            string.Equals(name, "Submit", System.StringComparison.OrdinalIgnoreCase);

        static bool IsCancel(string name) =>
            string.Equals(name, "Cancel", System.StringComparison.OrdinalIgnoreCase);

        static ButtonControl ResolveMouseButton(int button)
        {
            var mouse = Mouse.current;
            if (mouse == null)
            {
                return null;
            }

            switch (button)
            {
                case 0: return mouse.leftButton;
                case 1: return mouse.rightButton;
                case 2: return mouse.middleButton;
                case 3: return mouse.forwardButton;
                case 4: return mouse.backButton;
                default: return null;
            }
        }

        static ButtonControl ResolveKey(KeyCode keyCode)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return null;
            }

            if (!KeyMap.TryGetValue(keyCode, out Key key) || key == Key.None)
            {
                return null;
            }

            return keyboard[key];
        }

        static Dictionary<KeyCode, Key> BuildKeyMap()
        {
            var map = new Dictionary<KeyCode, Key>(128)
            {
                { KeyCode.None, Key.None },
                { KeyCode.Space, Key.Space },
                { KeyCode.Return, Key.Enter },
                { KeyCode.KeypadEnter, Key.NumpadEnter },
                { KeyCode.Escape, Key.Escape },
                { KeyCode.Tab, Key.Tab },
                { KeyCode.Backspace, Key.Backspace },
                { KeyCode.Delete, Key.Delete },
                { KeyCode.Insert, Key.Insert },
                { KeyCode.Home, Key.Home },
                { KeyCode.End, Key.End },
                { KeyCode.PageUp, Key.PageUp },
                { KeyCode.PageDown, Key.PageDown },
                { KeyCode.LeftShift, Key.LeftShift },
                { KeyCode.RightShift, Key.RightShift },
                { KeyCode.LeftControl, Key.LeftCtrl },
                { KeyCode.RightControl, Key.RightCtrl },
                { KeyCode.LeftAlt, Key.LeftAlt },
                { KeyCode.RightAlt, Key.RightAlt },
                { KeyCode.LeftCommand, Key.LeftCommand },
                { KeyCode.RightCommand, Key.RightCommand },
                { KeyCode.CapsLock, Key.CapsLock },
                { KeyCode.LeftArrow, Key.LeftArrow },
                { KeyCode.RightArrow, Key.RightArrow },
                { KeyCode.UpArrow, Key.UpArrow },
                { KeyCode.DownArrow, Key.DownArrow },
                { KeyCode.Comma, Key.Comma },
                { KeyCode.Period, Key.Period },
                { KeyCode.Slash, Key.Slash },
                { KeyCode.Semicolon, Key.Semicolon },
                { KeyCode.Quote, Key.Quote },
                { KeyCode.LeftBracket, Key.LeftBracket },
                { KeyCode.RightBracket, Key.RightBracket },
                { KeyCode.Backslash, Key.Backslash },
                { KeyCode.Minus, Key.Minus },
                { KeyCode.Equals, Key.Equals },
                { KeyCode.BackQuote, Key.Backquote },
                { KeyCode.F1, Key.F1 },
                { KeyCode.F2, Key.F2 },
                { KeyCode.F3, Key.F3 },
                { KeyCode.F4, Key.F4 },
                { KeyCode.F5, Key.F5 },
                { KeyCode.F6, Key.F6 },
                { KeyCode.F7, Key.F7 },
                { KeyCode.F8, Key.F8 },
                { KeyCode.F9, Key.F9 },
                { KeyCode.F10, Key.F10 },
                { KeyCode.F11, Key.F11 },
                { KeyCode.F12, Key.F12 },
                { KeyCode.Alpha0, Key.Digit0 },
                { KeyCode.Alpha1, Key.Digit1 },
                { KeyCode.Alpha2, Key.Digit2 },
                { KeyCode.Alpha3, Key.Digit3 },
                { KeyCode.Alpha4, Key.Digit4 },
                { KeyCode.Alpha5, Key.Digit5 },
                { KeyCode.Alpha6, Key.Digit6 },
                { KeyCode.Alpha7, Key.Digit7 },
                { KeyCode.Alpha8, Key.Digit8 },
                { KeyCode.Alpha9, Key.Digit9 },
                { KeyCode.Keypad0, Key.Numpad0 },
                { KeyCode.Keypad1, Key.Numpad1 },
                { KeyCode.Keypad2, Key.Numpad2 },
                { KeyCode.Keypad3, Key.Numpad3 },
                { KeyCode.Keypad4, Key.Numpad4 },
                { KeyCode.Keypad5, Key.Numpad5 },
                { KeyCode.Keypad6, Key.Numpad6 },
                { KeyCode.Keypad7, Key.Numpad7 },
                { KeyCode.Keypad8, Key.Numpad8 },
                { KeyCode.Keypad9, Key.Numpad9 },
                { KeyCode.KeypadPeriod, Key.NumpadPeriod },
                { KeyCode.KeypadDivide, Key.NumpadDivide },
                { KeyCode.KeypadMultiply, Key.NumpadMultiply },
                { KeyCode.KeypadMinus, Key.NumpadMinus },
                { KeyCode.KeypadPlus, Key.NumpadPlus },
            };

            for (char c = 'A'; c <= 'Z'; c++)
            {
                var keyCode = (KeyCode)System.Enum.Parse(typeof(KeyCode), c.ToString());
                var key = (Key)System.Enum.Parse(typeof(Key), c.ToString());
                map[keyCode] = key;
            }

            return map;
        }
    }
}
