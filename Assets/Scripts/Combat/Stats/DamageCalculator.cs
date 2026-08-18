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
            return Resolve(raw, attacker, defender, defenderHealth: null);
        }

        public static DamageInfo Resolve(
            in DamageInfo raw,
            CombatStats attacker,
            CombatStats defender,
            Health defenderHealth)
        {
            DamageInfo result = raw;
            float skillPower = Mathf.Max(0f, raw.Amount);
            float atk = attacker != null ? Mathf.Max(0f, attacker.Attack) : skillPower;
            float def = defender != null ? Mathf.Max(0f, defender.Defense) : 0f;
            CombatElement atkElement = raw.OverrideAttackElement
                ? raw.AttackElement
                : (attacker != null ? attacker.Element : CombatElement.Light);
            CombatElement defElement = defender != null ? defender.Element : CombatElement.Light;

            float skillRatio = skillPower * 0.01f;
            float damage = atk * skillRatio;

            if (attacker == null)
            {
                damage = skillPower;
            }

            float defenseMul = DefenseConstant / (DefenseConstant + def);
            damage *= defenseMul;
            damage *= 1f + ElementOffenseBonus;

            if (atkElement == defElement)
            {
                damage *= 1f - SameElementResist;
            }

            bool isCrit = false;
            if (!raw.SkipCritical)
            {
                float critRate = attacker != null ? attacker.CritRate : 0f;
                float critDamage = attacker != null ? attacker.CritDamage : 1.5f;
                if (critRate > 0f && Random.value < critRate)
                {
                    isCrit = true;
                    damage *= Mathf.Max(1f, critDamage);
                }
            }

            if (IsPlayerAttacker(raw.Attacker))
            {
                damage *= RougePassiveEffects.AttackMul;

                var relay = FindRelay(raw.Attacker);
                if (relay != null && relay.IsBasicAttackActive)
                {
                    damage *= RougePassiveEffects.AttackDamageMul;
                }

                if (relay != null && relay.IsSkillEActive)
                {
                    damage *= RougePassiveEffects.SkillEDamageMul;
                }

                Health hp = defenderHealth;
                if (hp == null && defender != null)
                {
                    hp = defender.GetComponent<Health>();
                }

                if (hp != null &&
                    hp.MaxHp > 0.01f &&
                    hp.CurrentHp / hp.MaxHp < RougePassiveEffects.ExecuteHpThreshold)
                {
                    damage *= RougePassiveEffects.ExecuteDamageMul;
                }
            }

            result.Amount = Mathf.Max(1f, damage);
            result.IsCritical = isCrit;
            result.AttackElement = atkElement;
            return result;
        }

        static bool IsPlayerAttacker(GameObject attacker)
        {
            return attacker != null &&
                   attacker.GetComponentInParent<GenshinLikeCharacter>() != null;
        }

        static AttackHitRelay FindRelay(GameObject attacker)
        {
            if (attacker == null)
            {
                return null;
            }

            var relay = attacker.GetComponentInParent<AttackHitRelay>();
            return relay != null ? relay : attacker.GetComponentInChildren<AttackHitRelay>();
        }
    }
}
