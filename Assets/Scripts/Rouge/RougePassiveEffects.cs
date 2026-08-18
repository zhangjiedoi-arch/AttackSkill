using AttackSkill.Character;
using AttackSkill.Character.HSM;
using AttackSkill.Combat;
using UnityEngine;

namespace AttackSkill.Rouge
{
    /// <summary>把被动数值接到战斗、机动与环绕武器。</summary>
    public static class RougePassiveEffects
    {
        public const float ExecuteHpThreshold = 0.25f;
        public const float MinSwitchCooldown = 0.1f;
        /// <summary>相对 1 级表内值：每升 1 级，攻/防/血 +10%。</summary>
        public const float LevelStatBonusPerLevel = 0.10f;

        public static event System.Action Changed;

        public static void NotifyChanged()
        {
            ApplyAbyssPactToActiveParty();
            if (PartyRougeProgress.GetStack(RougeOrbitWeaponDriver.FireBladeId) > 0 ||
                PartyRougeProgress.GetStack(RougeOrbitWeaponDriver.WindBladeId) > 0)
            {
                RougeOrbitWeaponDriver.Ensure();
            }

            if (RougeConstructDriver.HasAnyConstruct())
            {
                RougeConstructDriver.Ensure();
            }

            Changed?.Invoke();
        }

        public static void OnRunReset()
        {
            ApplyAbyssPactToActiveParty();
            if (RougeConstructDriver.Instance != null)
            {
                RougeConstructDriver.Instance.DespawnAll();
            }
        }

        /// <summary>1 级 = 100%；2 级 = 110%，以此类推。</summary>
        public static float LevelStatMul =>
            1f + LevelStatBonusPerLevel * Mathf.Max(0, PartyRougeProgress.Level - 1);

        public static float AttackDamageMul => 1f + PartyRougeProgress.SumMod("attackDamageMul");
        public static float AttackMul => 1f + PartyRougeProgress.SumMod("attackMul");
        public static float DamageTakenMul => Mathf.Max(0.05f, 1f + PartyRougeProgress.SumMod("damageTakenMul"));
        public static float MoveSpeedMul => 1f + PartyRougeProgress.SumMod("moveSpeedMul");
        public static float LifestealRatio => Mathf.Max(0f, PartyRougeProgress.SumMod("lifestealRatio"));
        public static float EngageRadius => CombatEngageUtility.DefaultSearchRadius;
        public static float HealCircleBonusPerSecond => 0f;
        public static float HealCircleDropChanceAdd => 0f;
        public static float MaxHpMul => 1f + PartyRougeProgress.SumMod("maxHpMul");
        public static float SkillEDamageMul => 1f + PartyRougeProgress.SumMod("skillEDamageMul");
        public static float ExecuteDamageMul => 1f + PartyRougeProgress.SumMod("executeDamageMul");

        public static float EffectiveMoveSpeedMul => Mathf.Max(0.2f, MoveSpeedMul);

        public static float GetSwitchCooldown(float baseCooldown)
        {
            return Mathf.Max(MinSwitchCooldown, baseCooldown);
        }

        public static void NotifyEnemyKilledByPlayer()
        {
        }

        public static bool TryTriggerSecondHeart(Health health)
        {
            return false;
        }

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

            var stats = CombatStats.Find(character);
            if (stats != null)
            {
                stats.RefreshHealth(refillHp: false);
                return;
            }

            var health = character.GetComponent<Health>();
            if (health == null)
            {
                return;
            }

            float baseMax = health.MaxHp;
            float targetMax = Mathf.Max(1f, baseMax * MaxHpMul);
            float ratio = health.MaxHp > 0.01f ? health.CurrentHp / health.MaxHp : 1f;
            health.Configure(targetMax, destroyWhenDead: false);
            health.SetCurrentHp(targetMax * ratio);
        }
    }
}
