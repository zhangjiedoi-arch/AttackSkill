using AttackSkill.Character.HSM;
using AttackSkill.Rouge;
using UnityEngine;

namespace AttackSkill.Combat
{
    /// <summary>
    /// 伤害结算：技能倍率% × 攻击 → 防御减免 → 元素加伤/同属免伤 → 暴击 → 肉鸽被动。
    /// 出伤表里的 damage 字段视为技能倍率（100 = 100% 攻击力）。
    /// </summary>
    public static class DamageCalculator
    {
        public const float ElementOffenseBonus = 0.20f;
        public const float SameElementResist = 0.20f;
        public const float DefenseConstant = 100f;

        public static DamageInfo Resolve(
            in DamageInfo raw,
            CombatStats attacker,
            CombatStats defender)
        {
            DamageInfo result = raw;
            float skillPower = Mathf.Max(0f, raw.Amount);
            float atk = attacker != null ? Mathf.Max(0f, attacker.Attack) : skillPower;
            float def = defender != null ? Mathf.Max(0f, defender.Defense) : 0f;
            CombatElement atkElement = attacker != null ? attacker.Element : CombatElement.Light;
            CombatElement defElement = defender != null ? defender.Element : CombatElement.Light;

            // skillPower 为倍率百分比：10 → 10% ATK
            float skillRatio = skillPower * 0.01f;
            float damage = atk * skillRatio;

            // 无属性组件时退化：仍用表内数值当绝对伤害
            if (attacker == null)
            {
                damage = skillPower;
            }

            float defenseMul = DefenseConstant / (DefenseConstant + def);
            damage *= defenseMul;

            // 自带元素造成伤害：+20%
            damage *= 1f + ElementOffenseBonus;

            // 同属性基础免伤：-20%
            if (atkElement == defElement)
            {
                damage *= 1f - SameElementResist;
            }

            bool isCrit = false;
            float critRate = attacker != null ? attacker.CritRate : 0f;
            float critDamage = attacker != null ? attacker.CritDamage : 1.5f;
            if (critRate > 0f && Random.value < critRate)
            {
                isCrit = true;
                damage *= Mathf.Max(1f, critDamage);
            }

            // 玩家出伤：深渊契约攻击倍率；普攻再叠锋刃
            if (IsPlayerAttacker(raw.Attacker))
            {
                damage *= RougePassiveEffects.AttackMul;
                if (IsBasicAttack(raw.Attacker))
                {
                    damage *= RougePassiveEffects.AttackDamageMul;
                }
            }

            result.Amount = Mathf.Max(1f, damage);
            result.IsCritical = isCrit;
            result.AttackElement = atkElement;
            return result;
        }

        static bool IsPlayerAttacker(GameObject attacker)
        {
            if (attacker == null)
            {
                return false;
            }

            return attacker.GetComponentInParent<GenshinLikeCharacter>() != null;
        }

        static bool IsBasicAttack(GameObject attacker)
        {
            if (attacker == null)
            {
                return false;
            }

            var relay = attacker.GetComponentInParent<AttackHitRelay>();
            if (relay == null)
            {
                relay = attacker.GetComponentInChildren<AttackHitRelay>();
            }

            return relay != null && relay.IsBasicAttackActive;
        }
    }
}
