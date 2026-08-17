using AttackSkill.Character;
using UnityEngine;

namespace AttackSkill.Combat
{
    /// <summary>角色战斗属性（Resources/Combat/Stats/Characters）。</summary>
    [CreateAssetMenu(
        menuName = "AttackSkill/Combat/Character Combat Stats",
        fileName = "CharacterCombatStats")]
    public class CharacterCombatStatsDefinition : ScriptableObject
    {
        public string displayName;
        public PartyPortraitId portraitId = PartyPortraitId.Unknown;
        public CombatStatBlock stats = CombatStatBlock.DefaultCharacter(CombatElement.Light);

        [Header("Skill Cooldown (seconds)")]
        [Min(0f)]
        [Tooltip("E 技能冷却；出场角色以此为准")]
        public float skillECooldown = 5f;
        [Min(0f)]
        [Tooltip("R 技能冷却；出场角色以此为准")]
        public float skillRCooldown = 10f;
    }
}
