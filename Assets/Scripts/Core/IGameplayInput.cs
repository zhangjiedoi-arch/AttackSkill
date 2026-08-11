using UnityEngine;

namespace AttackSkill.Core
{
    /// <summary>
    /// 玩法输入抽象：先包 Legacy，后续可换成 Input System 实现而不改调用方。
    /// </summary>
    public interface IGameplayInput
    {
        bool GetKey(KeyCode key);
        bool GetKeyDown(KeyCode key);
        bool GetKeyUp(KeyCode key);
        bool GetButton(string buttonName);
        bool GetButtonDown(string buttonName);
        float GetAxis(string axisName);
        float GetAxisRaw(string axisName);
        bool GetMouseButton(int button);
        bool GetMouseButtonDown(int button);
        bool GetMouseButtonUp(int button);
        Vector3 MousePosition { get; }
    }

    /// <summary>Unity Legacy Input Manager 实现。</summary>
    public sealed class LegacyGameplayInput : IGameplayInput
    {
        public bool GetKey(KeyCode key) => Input.GetKey(key);
        public bool GetKeyDown(KeyCode key) => Input.GetKeyDown(key);
        public bool GetKeyUp(KeyCode key) => Input.GetKeyUp(key);
        public bool GetButton(string buttonName) => Input.GetButton(buttonName);
        public bool GetButtonDown(string buttonName) => Input.GetButtonDown(buttonName);
        public float GetAxis(string axisName) => Input.GetAxis(axisName);
        public float GetAxisRaw(string axisName) => Input.GetAxisRaw(axisName);
        public bool GetMouseButton(int button) => Input.GetMouseButton(button);
        public bool GetMouseButtonDown(int button) => Input.GetMouseButtonDown(button);
        public bool GetMouseButtonUp(int button) => Input.GetMouseButtonUp(button);
        public Vector3 MousePosition => Input.mousePosition;
    }

    /// <summary>全局入口：默认 Input System；可回退 Legacy（需 Active Input Handling = Both）。</summary>
    public static class GameInput
    {
        static IGameplayInput _backend = new InputSystemGameplayInput();

        public static IGameplayInput Backend
        {
            get => _backend;
            set => _backend = value ?? new InputSystemGameplayInput();
        }

        public static void UseInputSystem() => Backend = new InputSystemGameplayInput();

        public static void UseLegacy() => Backend = new LegacyGameplayInput();

        public static bool GetKey(KeyCode key) => Backend.GetKey(key);
        public static bool GetKeyDown(KeyCode key) => Backend.GetKeyDown(key);
        public static bool GetKeyUp(KeyCode key) => Backend.GetKeyUp(key);
        public static bool GetButton(string buttonName) => Backend.GetButton(buttonName);
        public static bool GetButtonDown(string buttonName) => Backend.GetButtonDown(buttonName);
        public static float GetAxis(string axisName) => Backend.GetAxis(axisName);
        public static float GetAxisRaw(string axisName) => Backend.GetAxisRaw(axisName);
        public static bool GetMouseButton(int button) => Backend.GetMouseButton(button);
        public static bool GetMouseButtonDown(int button) => Backend.GetMouseButtonDown(button);
        public static bool GetMouseButtonUp(int button) => Backend.GetMouseButtonUp(button);
        public static Vector3 MousePosition => Backend.MousePosition;
    }
}
