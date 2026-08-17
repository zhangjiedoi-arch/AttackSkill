using System;
using UnityEngine;

namespace AttackSkill.Combat
{
    /// <summary>战斗属性块（攻击 / 防御 / 暴击 / 生命 / 元素）。</summary>
    [Serializable]
    public struct CombatStatBlock
    {
        [Min(0f)] public float attack;
        [Min(0f)] public float defense;
        [Range(0f, 1f)] public float critRate;
        [Tooltip("暴击时伤害倍率，1.5 = 造成 150% 伤害。")]
        [Min(1f)] public float critDamage;
        [Min(1f)] public float maxHp;
        public CombatElement element;

        public static CombatStatBlock DefaultCharacter(CombatElement element) => new CombatStatBlock
        {
            attack = 100f,
            defense = 20f,
            critRate = 0.15f,
            critDamage = 1.5f,
            maxHp = 1000f,
            element = element,
        };

        public static CombatStatBlock DefaultEnemyThunder() => new CombatStatBlock
        {
            attack = 100f,
            defense = 10f,
            critRate = 0.05f,
            critDamage = 1.5f,
            maxHp = 80f,
            element = CombatElement.Thunder,
        };
    }
}
