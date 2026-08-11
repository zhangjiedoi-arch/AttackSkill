using System;
using AttackSkill.Character.HSM;
using AttackSkill.Enemy;
using UnityEngine;

namespace AttackSkill.Combat
{
    /// <summary>
    /// 统一命中结算：过滤、去重、<see cref="IDamageable.TakeDamage"/>、可选命中特效。
    /// Detector（扇形 / Trigger）只负责找出候选碰撞体。
    /// </summary>
    public static class HitResolver
    {
        public const HitResolveFlags DefaultPlayerOffense =
            HitResolveFlags.RequireAliveTarget |
            HitResolveFlags.SkipDeadAttacker |
            HitResolveFlags.SkipOwnerHierarchy |
            HitResolveFlags.SkipFriendlyPlayerCharacters;

        public const HitResolveFlags DefaultEnemyOffense =
            HitResolveFlags.RequireAliveTarget |
            HitResolveFlags.ActivePlayerOnly;

        /// <summary>成功结算后触发（在 TakeDamage 之后）。</summary>
        public static event Action<DamageInfo, IDamageable> Applied;

        public static bool TryApply(in HitRequest request)
        {
            return TryApply(request, out _);
        }

        public static bool TryApply(in HitRequest request, out HitRejectReason reject)
        {
            reject = HitRejectReason.None;

            if (request.Target == null)
            {
                reject = HitRejectReason.NullTarget;
                return false;
            }

            HitResolveFlags flags = request.Flags;
            if ((flags & HitResolveFlags.RequireAliveTarget) != 0 && !request.Target.IsAlive)
            {
                reject = HitRejectReason.DeadTarget;
                return false;
            }

            if ((flags & HitResolveFlags.SkipDeadAttacker) != 0 &&
                IsDeadPlayerAttacker(request.Damage.Attacker))
            {
                reject = HitRejectReason.DeadAttacker;
                return false;
            }

            Component hint = request.TargetHint;
            Transform hintTf = hint != null ? hint.transform : null;

            if ((flags & HitResolveFlags.SkipOwnerHierarchy) != 0 &&
                request.OwnerRoot != null &&
                hintTf != null &&
                (hintTf == request.OwnerRoot || hintTf.IsChildOf(request.OwnerRoot)))
            {
                reject = HitRejectReason.OwnerSelf;
                return false;
            }

            if ((flags & HitResolveFlags.SkipFriendlyPlayerCharacters) != 0 &&
                hint != null &&
                hint.GetComponentInParent<GenshinLikeCharacter>() != null)
            {
                reject = HitRejectReason.FriendlyPlayerCharacter;
                return false;
            }

            if ((flags & HitResolveFlags.ActivePlayerOnly) != 0 &&
                (hint == null || !PlayerTargetLocator.IsActivePlayer(hint)))
            {
                reject = HitRejectReason.NotActivePlayer;
                return false;
            }

            if (request.Session != null)
            {
                int id = ResolveDedupId(hintTf, request.Target);
                if (!request.Session.TryRegister(id))
                {
                    reject = HitRejectReason.AlreadyHitInSession;
                    return false;
                }
            }

            request.Target.TakeDamage(request.Damage);
            SpawnHitVfx(request);
            Applied?.Invoke(request.Damage, request.Target);
            return true;
        }

        public static int ResolveDedupId(Transform hintTf, IDamageable target)
        {
            if (hintTf != null)
            {
                return hintTf.root.GetInstanceID();
            }

            if (target is Component c && c != null)
            {
                return c.transform.root.GetInstanceID();
            }

            return target.GetHashCode();
        }

        static bool IsDeadPlayerAttacker(GameObject attacker)
        {
            if (attacker == null)
            {
                return false;
            }

            var character = attacker.GetComponentInParent<GenshinLikeCharacter>();
            if (character == null)
            {
                character = attacker.GetComponent<GenshinLikeCharacter>();
            }

            return character != null && character.IsDead;
        }

        static void SpawnHitVfx(in HitRequest request)
        {
            if (request.HitVfxPrefab == null)
            {
                return;
            }

            var vfx = UnityEngine.Object.Instantiate(
                request.HitVfxPrefab,
                request.Damage.HitPoint,
                Quaternion.identity);
            UnityEngine.Object.Destroy(vfx, Mathf.Max(0.05f, request.HitVfxLife));
        }
    }
}
