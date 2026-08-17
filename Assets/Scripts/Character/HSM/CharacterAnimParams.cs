using UnityEngine;

namespace AttackSkill.Character.HSM
{
    /// <summary>
    /// 角色 Animator 参数名哈希，避免运行时反复 StringToHash。
    /// </summary>
    public static class CharacterAnimParams
    {
        public static readonly int Speed = Animator.StringToHash("Speed");
        public static readonly int VerticalSpeed = Animator.StringToHash("VerticalSpeed");
        public static readonly int IsGrounded = Animator.StringToHash("IsGrounded");
        /// <summary>HSM Fall 状态：播 Jump_Loop 下落循环。</summary>
        public static readonly int IsFalling = Animator.StringToHash("IsFalling");
        public static readonly int IsSprinting = Animator.StringToHash("IsSprinting");
        public static readonly int IsGliding = Animator.StringToHash("IsGliding");
        public static readonly int IsSwordFlying = Animator.StringToHash("IsSwordFlying");
        public static readonly int IsRidingMotorcycle = Animator.StringToHash("IsRidingMotorcycle");
        public static readonly int IsClimbing = Animator.StringToHash("IsClimbing");
        public static readonly int IsSwimming = Animator.StringToHash("IsSwimming");
        public static readonly int IsDodging = Animator.StringToHash("IsDodging");
        public static readonly int InCombatAction = Animator.StringToHash("InCombatAction");
        public static readonly int AttackCombo = Animator.StringToHash("AttackCombo");
        public static readonly int DodgeX = Animator.StringToHash("DodgeX");
        public static readonly int DodgeZ = Animator.StringToHash("DodgeZ");

        public static readonly int Jump = Animator.StringToHash("Jump");
        /// <summary>落地瞬间：播 Jump_Land。</summary>
        public static readonly int Land = Animator.StringToHash("Land");
        public static readonly int Glide = Animator.StringToHash("Glide");
        public static readonly int Climb = Animator.StringToHash("Climb");
        public static readonly int Attack = Animator.StringToHash("Attack");
        public static readonly int Skill = Animator.StringToHash("Skill");
        /// <summary>R 技能 Trigger（状态名建议 skill_r / SkillR）。</summary>
        public static readonly int SkillR = Animator.StringToHash("SkillR");
        public static readonly int Dodge = Animator.StringToHash("Dodge");
    }
}
