using UnityEngine;

namespace AttackSkill.Combat
{
    /// <summary>
    /// 小队共享：T（探索工具）/ Q 冷却。
    /// E / R 冷却挂在当前角色 <see cref="CombatStats"/> 上。
    /// </summary>
    public static class PartySkillCooldown
    {
        public const float ToolTCooldown = 3f;
        public const float SkillQCooldown = 5f;

        static float _tReadyAt;
        static float _qReadyAt;

        public static bool IsTReady => Time.time >= _tReadyAt;
        public static bool IsQReady => Time.time >= _qReadyAt;

        public static float TRemaining => Mathf.Max(0f, _tReadyAt - Time.time);
        public static float QRemaining => Mathf.Max(0f, _qReadyAt - Time.time);

        /// <summary>fillAmount：0=刚进 CD，1=可用。</summary>
        public static float TFillAmount =>
            ToolTCooldown <= 0.01f ? 1f : Mathf.Clamp01(1f - TRemaining / ToolTCooldown);

        public static float QFillAmount =>
            SkillQCooldown <= 0.01f ? 1f : Mathf.Clamp01(1f - QRemaining / SkillQCooldown);

        public static void BeginT()
        {
            _tReadyAt = Time.time + ToolTCooldown;
        }

        public static void BeginQ()
        {
            _qReadyAt = Time.time + SkillQCooldown;
        }

        public static void ResetAll()
        {
            _tReadyAt = 0f;
            _qReadyAt = 0f;
        }
    }
}
