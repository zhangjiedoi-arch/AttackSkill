using UnityEngine;

namespace AttackSkill.Combat
{
    /// <summary>怪物战斗属性（Resources/Combat/Stats/Enemies）。</summary>
    [CreateAssetMenu(
        menuName = "AttackSkill/Combat/Enemy Combat Stats",
        fileName = "EnemyCombatStats")]
    public class EnemyCombatStatsDefinition : ScriptableObject
    {
        public string displayName = "Wild Enemy";
        public CombatStatBlock stats = CombatStatBlock.DefaultEnemyThunder();
    }
}
