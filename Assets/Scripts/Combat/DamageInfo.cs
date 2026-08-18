using UnityEngine;

namespace AttackSkill.Combat
{
    public struct DamageInfo
    {
        public float Amount;
        public Vector3 HitPoint;
        public Vector3 HitDirection;
        public float Knockback;
        public int ComboIndex;
        public GameObject Attacker;
        /// <summary>结算后是否暴击（DamageCalculator 写入）。</summary>
        public bool IsCritical;
        /// <summary>攻击方元素（DamageCalculator 写入；可被 OverrideAttackElement 覆盖）。</summary>
        public CombatElement AttackElement;
        /// <summary>为 true 时跳过暴击判定。</summary>
        public bool SkipCritical;
        /// <summary>为 true 时使用 <see cref="AttackElement"/>，不用攻击者自身元素。</summary>
        public bool OverrideAttackElement;

        public DamageInfo(float amount, Vector3 hitPoint, Vector3 hitDirection, float knockback, int comboIndex, GameObject attacker)
        {
            Amount = amount;
            HitPoint = hitPoint;
            HitDirection = hitDirection;
            Knockback = knockback;
            ComboIndex = comboIndex;
            Attacker = attacker;
            IsCritical = false;
            AttackElement = CombatElement.Light;
            SkipCritical = false;
            OverrideAttackElement = false;
        }
    }

    public interface IDamageable
    {
        bool IsAlive { get; }
        void TakeDamage(DamageInfo info);
    }
}
