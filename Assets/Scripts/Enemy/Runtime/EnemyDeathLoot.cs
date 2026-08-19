using AttackSkill.Character;
using AttackSkill.Combat;
using AttackSkill.Rouge;
using UnityEngine;

namespace AttackSkill.Enemy
{
    /// <summary>敌人死亡掉落：治疗圈 + 经验球。</summary>
    public static class EnemyDeathLoot
    {
        public const float DefaultHealDropChance = 0.3f;

        public static void TryDropAll(EnemyAgent agent)
        {
            TryDropHealingCircle(agent);
            TryDropExpOrb(agent);
        }

        public static void TryDropHealingCircle(EnemyAgent agent)
        {
            if (agent == null)
            {
                return;
            }

            var settings = CharacterRuntimeSettings.Get();
            float chance = settings != null ? settings.enemyHealDropChance : DefaultHealDropChance;
            chance = Mathf.Clamp01(chance);
            if (Random.value > chance)
            {
                return;
            }

            GameObject circlePrefab = settings != null ? settings.healingCirclePrefab : null;
            GameObject auraPrefab = settings != null ? settings.healingAuraPrefab : null;
            if (circlePrefab == null)
            {
                Debug.LogWarning(
                    "[EnemyDeathLoot] healingCirclePrefab 未配置。请在 CharacterRuntimeSettings 指定 Prefabs/VFX/Healing circle。",
                    agent);
                return;
            }

            Vector3 pos = agent.transform.position;
            pos.y += 0.02f;
            float healRate = settings != null ? settings.healingCircleHealPerSecond : 100f;
            HealingCircleZone.Spawn(
                pos,
                circlePrefab,
                auraPrefab,
                radius: settings != null ? settings.healingCircleRadius : 3f,
                healPerSecond: healRate,
                lifetime: settings != null ? settings.healingCircleLifetime : 20f);
        }

        public static void TryDropExpOrb(EnemyAgent agent)
        {
            if (agent == null || !IsRougeLootSource(agent))
            {
                return;
            }

            RougeCatalog.EnsureLoaded();
            float chance = Mathf.Clamp01(RougeCatalog.ExpOrb.dropChance);
            if (Random.value > chance)
            {
                return;
            }

            Vector3 pos = agent.transform.position;

            var settings = CharacterRuntimeSettings.Get();
            GameObject prefab = settings != null ? settings.GetExpOrbPrefab() : null;
            if (prefab == null)
            {
                prefab = Resources.Load<GameObject>("Rouge/Exp");
            }

            if (prefab != null)
            {
                ExpOrbPickup.Spawn(pos, prefab);
                return;
            }

            // Prefab 全丢时仍生成可见球体，保证玩法不断
            ExpOrbPickup.SpawnFallback(pos);
            Debug.LogWarning(
                "[EnemyDeathLoot] expOrbPrefab 缺失，已用兜底球体。请配置 CharacterRuntimeSettings.expOrbPrefab 或 Resources/Rouge/Exp。",
                agent);
        }

        static bool IsRougeLootSource(EnemyAgent agent)
        {
            return agent.IsRougeEncounter || RouGeLikeFlowController.ContainsWorldPoint(agent.transform.position);
        }
    }
}
