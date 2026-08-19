using AttackSkill.Combat;
using UnityEngine;

namespace AttackSkill.Enemy
{
    [CreateAssetMenu(menuName = "AttackSkill/Enemy/Enemy Definition", fileName = "EnemyDefinition")]
    public class EnemyDefinition : ScriptableObject
    {
        public string displayName = "Wild Enemy";
        public GameObject prefab;

        [Header("Stats")]
        [Tooltip("可选：覆盖 Resources/Combat/Stats/Enemies 默认雷属性表；空则用 Enemy_Thunder")]
        public EnemyCombatStatsDefinition combatStats;
        [Tooltip("1 级表内生命；运行时再乘玩家肉鸽等级系数（每级 +10%）")]
        public float maxHp = 80f;
        [Tooltip("头顶血条显示等级")]
        public int level = 1;
        public float moveSpeed = 3.5f;
        public float turnSpeed = 8f;

        [Header("Sense")]
        [Tooltip("发现/追击玩家的视觉距离（米）")]
        public float sightRange = 20f;
        [Range(10f, 180f)] public float sightAngle = 90f;
        public float hearRange = 20f;
        [Tooltip("视线遮挡检测层；默认 Everything")]
        public LayerMask losMask = ~0;

        [Header("Combat")]
        public float attackRange = 1.8f;
        [Tooltip("与目标距离超过此值则脱战回家")]
        public float disengageRange = 22f;
        [Tooltip("离出生点超过此值则强制勒回（leash）。建议 ≥ disengageRange，否则会先被出生点拉开")]
        public float returnHomeRange = 28f;
        [Tooltip("技能倍率%（100=100% ATK）。1 级表内值；ATK 会随玩家肉鸽等级 +10%/级")]
        public float attackDamage = 12f;
        public float attackKnockback = 1.2f;
        public float attackWindup = 0.35f;
        public float attackActive = 0.15f;
        public float attackRecovery = 0.45f;
        public float attackCooldown = 1.1f;
        public float hitRadius = 1.1f;
        public float hitForwardOffset = 0.9f;
        [Tooltip("动画 Event 出伤表；空则用运行时默认（Enemy_Hit_Chest_R 球）")]
        public SkillHitProfile skillHitProfile;

        [Header("AI")]
        public float alertDuration = 0.4f;
        public float loseTargetTime = 2.5f;

        [Header("Death Visual")]
        [Tooltip("死亡后变成金色声骸的概率；其余走飘散溶解")]
        [Range(0f, 1f)] public float echoChance = 0.35f;
        [Tooltip("调试强制结局；正式玩法用 Random")]
        public EnemyDeathForceMode deathForceMode = EnemyDeathForceMode.Random;
        [Tooltip("飘散溶解时长（秒）")]
        public float dissolveDuration = 1.55f;
        [Tooltip("溶解过程上浮高度")]
        public float dissolveRiseDistance = 0.55f;
        [Tooltip("可选金尘粒子；空则只做 Shader 溶解")]
        public GameObject dissolveDustVfx;
        [Tooltip("声骸（金透）残留多久后销毁（秒）")]
        public float echoCorpseLifetime = 20f;
    }
}
