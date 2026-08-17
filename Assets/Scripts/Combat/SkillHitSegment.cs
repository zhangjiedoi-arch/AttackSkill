using UnityEngine;

namespace AttackSkill.Combat
{
    public enum HitSocketId
    {
        None = 0,
        Hit_Chest_R,
        Hit_Chest_L,
        Hit_Root,
        Weapon,
        HitOrigin,
        Enemy_Hit_Chest_R,
        /// <summary>R 技能 AoE 挂点（节点名 R_Hit_Root）。</summary>
        R_Hit_Root,
    }

    public enum HitShapeType
    {
        Sphere = 0,
        Cylinder = 1,
        Fan = 2,
    }

    /// <summary>技能/攻击的一段出伤配置：挂点 + 形状 + 伤害 + 特效。</summary>
    [System.Serializable]
    public class SkillHitSegment
    {
        [Tooltip("调试用备注")]
        public string id;

        public HitSocketId socket = HitSocketId.Hit_Chest_R;
        public HitShapeType shape = HitShapeType.Sphere;

        [Header("Shape")]
        public float radius = 1f;
        [Tooltip("Cylinder：从挂点向上的高度")]
        public float height = 0.5f;
        [Tooltip("Fan：水平全角（度）")]
        [Range(1f, 360f)]
        public float fanAngle = 90f;
        [Tooltip("Fan：内圈忽略距离")]
        public float minHitDistance = 0.15f;
        [Tooltip("Fan：相对角色脚底高度（无挂点时）")]
        public float hitHeight = 0.9f;

        [Header("Damage")]
        [Tooltip("技能倍率%（100 = 100% 攻击力）。最终伤害由角色 CombatStats.Attack 经 DamageCalculator 结算。")]
        public float damage = 100f;
        public float knockback = 1.2f;

        [Header("VFX")]
        public GameObject vfxPrefab;
        public float vfxLife = 2.5f;
        [Tooltip("true=挂在挂点下跟随；false=世界坐标解绑（默认，配合对象池）")]
        public bool parentVfxToSocket;

        [Header("SFX")]
        [Tooltip("出伤帧音效，如 FatKick_R / FatKick_L / Hit_Root_Land")]
        public AudioClip sfxClip;
        [Range(0f, 1f)]
        public float sfxVolume = 1f;
    }
}
