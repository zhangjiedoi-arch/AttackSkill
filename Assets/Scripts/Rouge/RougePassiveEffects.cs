using AttackSkill.Character;
using AttackSkill.Character.HSM;
using AttackSkill.Combat;
using UnityEngine;

namespace AttackSkill.Rouge
{
    /// <summary>把被动数值接到战斗/治疗/贴身等系统。</summary>
    public static class RougePassiveEffects
    {
        public static event System.Action Changed;

        public static void NotifyChanged()
        {
            ApplyAbyssPactToActiveParty();
            Changed?.Invoke();
        }

        public static float AttackDamageMul => 1f + PartyRougeProgress.SumMod("attackDamageMul");
        public static float AttackMul => 1f + PartyRougeProgress.SumMod("attackMul");
        public static float DamageTakenMul => Mathf.Max(0.05f, 1f + PartyRougeProgress.SumMod("damageTakenMul"));
        public static float MoveSpeedMul => 1f + PartyRougeProgress.SumMod("moveSpeedMul");
        public static float LifestealRatio => Mathf.Max(0f, PartyRougeProgress.SumMod("lifestealRatio"));
        public static float EngageRadius =>
            CombatEngageUtility.DefaultSearchRadius + PartyRougeProgress.SumMod("engageRadiusAdd");
        public static float HealCircleBonusPerSecond =>
            Mathf.Max(0f, PartyRougeProgress.SumMod("healCircleBonusPerSecond"));
        public static float HealCircleDropChanceAdd =>
            Mathf.Max(0f, PartyRougeProgress.SumMod("healCircleDropChanceAdd"));
        public static float MaxHpMul => 1f + PartyRougeProgress.SumMod("maxHpMul");

        public static void ApplyAbyssPactToActiveParty()
        {
            var party = PartyController.Instance;
            if (party == null || party.Active == null)
            {
                return;
            }

            ApplyMaxHpMul(party.Active);
        }

        public static void ApplyMaxHpMul(GenshinLikeCharacter character)
        {
            if (character == null)
            {
                return;
            }

            var health = character.GetComponent<Health>();
            var stats = CombatStats.Find(character);
            if (health == null || stats == null)
            {
                return;
            }

            float baseMax = stats.MaxHp;
            if (baseMax <= 1f)
            {
                baseMax = health.MaxHp;
            }

            float targetMax = Mathf.Max(1f, baseMax * MaxHpMul);
            float ratio = health.MaxHp > 0.01f ? health.CurrentHp / health.MaxHp : 1f;
            health.Configure(targetMax, destroyWhenDead: false);
            health.SetCurrentHp(targetMax * ratio);
        }
    }
}
