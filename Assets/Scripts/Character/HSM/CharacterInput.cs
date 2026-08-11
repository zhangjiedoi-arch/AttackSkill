using UnityEngine;
using AttackSkill.Core;
using AttackSkill.Game;

namespace AttackSkill.Character.HSM
{
    /// <summary>
    /// 每帧输入快照（可替换为新 Input System）。
    /// </summary>
    public struct CharacterInput
    {
        public Vector2 Move;
        public bool JumpPressed;
        public bool JumpHeld;
        public bool SprintHeld;
        public bool GlidePressed;
        public bool AttackPressed;
        public bool SkillPressed;
        public bool InteractPressed;
        public bool DodgePressed;

        public bool HasMove => Move.sqrMagnitude > 0.01f;
    }

    public interface ICharacterInputSource
    {
        CharacterInput Read();
    }

    /// <summary>
    /// 默认：WASD、Space 跳、LeftShift 冲刺、F 滑翔、鼠标左键普攻、右键闪避、E 技能、R 攀爬。
    /// 经 <see cref="AttackSkill.Core.GameInput"/> 读取（Input System 后端）。
    /// </summary>
    public class LegacyCharacterInputSource : ICharacterInputSource
    {
        /// <summary>临时关闭 E 技能输入；改回 true 即恢复。</summary>
        public const bool SkillInputEnabled = true;

        /// <summary>临时关闭 R 攀爬交互（战斗技能键占用 R）；改回 true 即恢复。</summary>
        public const bool InteractInputEnabled = false;

        public CharacterInput Read()
        {
            if (GameplayInputGate.IsBlocked)
            {
                return default;
            }

            return new CharacterInput
            {
                Move = new Vector2(GameInput.GetAxisRaw("Horizontal"), GameInput.GetAxisRaw("Vertical")),
                JumpPressed = GameInput.GetButtonDown("Jump"),
                JumpHeld = GameInput.GetButton("Jump"),
                SprintHeld = GameInput.GetKey(KeyCode.LeftShift),
                GlidePressed = GameInput.GetKeyDown(KeyCode.F),
                AttackPressed = GameInput.GetMouseButtonDown(0),
                SkillPressed = SkillInputEnabled &&
                               (GameInput.GetKeyDown(KeyCode.E) || CombatSkillInput.TakePending()),
                InteractPressed = InteractInputEnabled && GameInput.GetKeyDown(KeyCode.R),
                DodgePressed = GameInput.GetMouseButtonDown(1)
            };
        }
    }

    /// <summary>HUD 按钮等非键盘路径请求释放 E 技能（下一帧输入读取时消费）。</summary>
    public static class CombatSkillInput
    {
        static bool _pending;

        public static void Request() => _pending = true;

        public static bool TakePending()
        {
            if (!_pending)
            {
                return false;
            }

            _pending = false;
            return true;
        }
    }

    /// <summary>残留体/禁用控制时使用：不响应任何输入。</summary>
    public class NullCharacterInputSource : ICharacterInputSource
    {
        public CharacterInput Read() => default;
    }
}
