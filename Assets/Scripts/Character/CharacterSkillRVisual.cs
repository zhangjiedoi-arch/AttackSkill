using UnityEngine;

namespace AttackSkill.Character
{
    /// <summary>
    /// R 技能表现入口（已停用 AoE 挂载）。
    /// AoE / 出伤改由 <see cref="AttackSkill.Combat.TimedHitProfile"/> phase「Skill_R」驱动。
    /// </summary>
    public static class CharacterSkillRVisual
    {
        public static void Begin(Transform characterRoot)
        {
            // no-op：避免与 TimedHit 特效叠播
        }

        public static void End(Transform characterRoot)
        {
            // no-op
        }
    }
}
