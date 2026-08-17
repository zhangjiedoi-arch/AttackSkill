using AttackSkill.Character;
using UnityEngine;

namespace AttackSkill.Combat
{
    /// <summary>
    /// 从 Resources 加载角色/怪物属性表。
    /// 角色：Combat/Stats/Characters/{PortraitId}
    /// 怪物默认：Combat/Stats/Enemies/Enemy_Thunder
    /// </summary>
    public static class CombatStatsCatalog
    {
        public const string CharacterFolder = "Combat/Stats/Characters";
        public const string EnemyFolder = "Combat/Stats/Enemies";
        public const string DefaultEnemyThunderPath = EnemyFolder + "/Enemy_Thunder";

        public static CharacterCombatStatsDefinition LoadCharacter(PartyPortraitId portraitId)
        {
            if (portraitId == PartyPortraitId.Unknown)
            {
                return null;
            }

            return Resources.Load<CharacterCombatStatsDefinition>($"{CharacterFolder}/{portraitId}");
        }

        public static EnemyCombatStatsDefinition LoadDefaultEnemyThunder()
        {
            return Resources.Load<EnemyCombatStatsDefinition>(DefaultEnemyThunderPath);
        }

        public static EnemyCombatStatsDefinition ResolveEnemy(EnemyCombatStatsDefinition overrideStats)
        {
            if (overrideStats != null)
            {
                return overrideStats;
            }

            return LoadDefaultEnemyThunder();
        }
    }
}
