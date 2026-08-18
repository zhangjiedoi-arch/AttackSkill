using System;
using UnityEngine;

namespace AttackSkill.Combat
{
    [Flags]
    public enum HitResolveFlags
    {
        None = 0,
        /// <summary>目标必须 IsAlive。</summary>
        RequireAliveTarget = 1 << 0,
        /// <summary>攻击者若为已死亡的玩家角色则拒绝（残留大招仍可打时角色未标死）。</summary>
        SkipDeadAttacker = 1 << 1,
        /// <summary>跳过 OwnerRoot 自身层级。</summary>
        SkipOwnerHierarchy = 1 << 2,
        /// <summary>跳过带 GenshinLikeCharacter 的目标（防友伤）。</summary>
        SkipFriendlyPlayerCharacters = 1 << 3,
        /// <summary>仅当前 Active 玩家可受伤（敌人打人）；诱敌之树等生成物也可受击。</summary>
        ActivePlayerOnly = 1 << 4,
    }

    public enum HitRejectReason : byte
    {
        None = 0,
        NullTarget,
        DeadTarget,
        DeadAttacker,
        OwnerSelf,
        FriendlyPlayerCharacter,
        NotActivePlayer,
        AlreadyHitInSession,
    }

    /// <summary>一次候选命中：Detector 负责形状，Resolver 负责规则与结算。</summary>
    public struct HitRequest
    {
        public DamageInfo Damage;
        public IDamageable Target;
        public Component TargetHint;
        public Transform OwnerRoot;
        public HitResolveFlags Flags;
        public HitSession Session;
        public GameObject HitVfxPrefab;
        public float HitVfxLife;

        public static HitRequest Create(
            in DamageInfo damage,
            IDamageable target,
            Component targetHint,
            HitResolveFlags flags,
            HitSession session = null,
            Transform ownerRoot = null,
            GameObject hitVfxPrefab = null,
            float hitVfxLife = 1.2f)
        {
            return new HitRequest
            {
                Damage = damage,
                Target = target,
                TargetHint = targetHint,
                OwnerRoot = ownerRoot,
                Flags = flags,
                Session = session,
                HitVfxPrefab = hitVfxPrefab,
                HitVfxLife = hitVfxLife,
            };
        }
    }
}
