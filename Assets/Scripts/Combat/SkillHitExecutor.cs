using System;
using UnityEngine;

namespace AttackSkill.Combat
{
    [Flags]
    public enum SkillHitExecuteFlags
    {
        None = 0,
        Presentation = 1 << 0,
        Hit = 1 << 1,
        All = Presentation | Hit,
    }

    /// <summary>执行一段 <see cref="SkillHitSegment"/>：特效 + 形状检测 + HitResolver。</summary>
    public static class SkillHitExecutor
    {
        public struct Context
        {
            public Transform OwnerRoot;
            public GameObject Attacker;
            public LayerMask Mask;
            public HitResolveFlags Flags;
            public HitSession Session;
            public Collider[] Buffer;
            public Collider[] Scratch;
            public Vector3 PlanarForward;
            public int ComboIndex;
            public bool ClearSession;
            public bool DrawDebug;
            public UnityEngine.Object LogContext;
        }

        public static int Execute(in SkillHitSegment segment, in Context ctx)
        {
            return Execute(segment, ctx, SkillHitExecuteFlags.All);
        }

        public static int Execute(in SkillHitSegment segment, in Context ctx, SkillHitExecuteFlags flags)
        {
            if (segment == null || flags == SkillHitExecuteFlags.None)
            {
                return 0;
            }

            Transform root = ctx.OwnerRoot;
            if (root == null)
            {
                return 0;
            }

            Transform socket = HitSocketResolver.Resolve(root, segment.socket);
            bool needSocketForHit = (flags & SkillHitExecuteFlags.Hit) != 0 && segment.shape != HitShapeType.Fan;
            if (socket == null && needSocketForHit)
            {
                Debug.LogWarning(
                    $"[SkillHit] 缺少挂点 {segment.socket}（{HitSocketResolver.ToHierarchyName(segment.socket)}）。",
                    ctx.LogContext);
                return 0;
            }

            if ((flags & SkillHitExecuteFlags.Presentation) != 0)
            {
                SpawnVfx(segment, socket);
                PlaySfx(segment, root);
            }

            if ((flags & SkillHitExecuteFlags.Hit) == 0)
            {
                return 0;
            }

            switch (segment.shape)
            {
                case HitShapeType.Sphere:
                    return socket != null ? ExecuteSphere(segment, socket.position, ctx) : 0;
                case HitShapeType.Cylinder:
                    return socket != null ? ExecuteCylinder(segment, socket.position, ctx) : 0;
                case HitShapeType.Fan:
                    return ExecuteFan(segment, socket, ctx);
                default:
                    return 0;
            }
        }

        static void SpawnVfx(SkillHitSegment segment, Transform socket)
        {
            if (segment.vfxPrefab == null || socket == null)
            {
                return;
            }

            if (segment.parentVfxToSocket)
            {
                var fx = UnityEngine.Object.Instantiate(segment.vfxPrefab, socket, false);
                fx.transform.localPosition = Vector3.zero;
                fx.transform.localRotation = Quaternion.identity;
                UnityEngine.Object.Destroy(fx, Mathf.Max(0.1f, segment.vfxLife));
                return;
            }

            var pooled = VfxObjectPool.Spawn(segment.vfxPrefab, socket.position, socket.rotation);
            if (pooled != null)
            {
                VfxObjectPool.Despawn(pooled, Mathf.Max(0.1f, segment.vfxLife));
            }
        }

        static void PlaySfx(SkillHitSegment segment, Transform ownerRoot)
        {
            if (segment.sfxClip == null || ownerRoot == null)
            {
                return;
            }

            float volume = Mathf.Clamp01(segment.sfxVolume);
            var audio = ownerRoot.GetComponent<AttackSkill.Character.CharacterAudio>();
            if (audio == null)
            {
                audio = ownerRoot.GetComponentInChildren<AttackSkill.Character.CharacterAudio>(true);
            }

            if (audio != null)
            {
                audio.PlaySfx(segment.sfxClip, volume);
                return;
            }

            var source = ownerRoot.GetComponentInChildren<AudioSource>(true);
            if (source != null)
            {
                if (segment.sfxClip.loadState == AudioDataLoadState.Unloaded)
                {
                    segment.sfxClip.LoadAudioData();
                }

                source.PlayOneShot(segment.sfxClip, volume);
                return;
            }

            AudioSource.PlayClipAtPoint(segment.sfxClip, ownerRoot.position, volume);
        }

        static int ExecuteSphere(SkillHitSegment segment, Vector3 center, in Context ctx)
        {
            BeginSessionIfNeeded(ctx);
            EnsureBuffers(ctx, out Collider[] buffer);

            Vector3 forward = ctx.PlanarForward.sqrMagnitude > 0.0001f
                ? ctx.PlanarForward.normalized
                : Vector3.forward;
            GameObject attacker = ctx.Attacker != null ? ctx.Attacker : ctx.OwnerRoot.gameObject;

            int count = ShapeHitDetector.OverlapSphere(center, segment.radius, ctx.Mask, buffer);
            int applied = 0;
            for (int i = 0; i < count; i++)
            {
                Collider col = buffer[i];
                if (!ShapeHitDetector.TryPassSphereFilter(
                        col, center, segment.radius, out Vector3 hitPoint, out Vector3 toHit))
                {
                    continue;
                }

                if (!TryApply(col, hitPoint, toHit, forward, segment, ctx, attacker))
                {
                    continue;
                }

                applied++;
            }

            Log(ctx, $"sphere r={segment.radius} scanned={count} hit={applied} dmg={segment.damage}");
            return applied;
        }

        static int ExecuteCylinder(SkillHitSegment segment, Vector3 bottom, in Context ctx)
        {
            BeginSessionIfNeeded(ctx);
            EnsureBuffers(ctx, out Collider[] buffer);
            Collider[] scratch = EnsureScratch(ctx);

            Vector3 forward = ctx.PlanarForward.sqrMagnitude > 0.0001f
                ? ctx.PlanarForward.normalized
                : Vector3.forward;
            GameObject attacker = ctx.Attacker != null ? ctx.Attacker : ctx.OwnerRoot.gameObject;

            int count = ShapeHitDetector.OverlapCylinder(
                bottom,
                segment.radius,
                segment.height,
                ctx.Mask,
                buffer,
                scratch);

            int applied = 0;
            for (int i = 0; i < count; i++)
            {
                Collider col = buffer[i];
                if (!ShapeHitDetector.TryPassCylinderFilter(
                        col,
                        bottom,
                        segment.radius,
                        segment.height,
                        out Vector3 hitPoint,
                        out Vector3 planarToHit))
                {
                    continue;
                }

                if (!TryApply(col, hitPoint, planarToHit, forward, segment, ctx, attacker))
                {
                    continue;
                }

                applied++;
            }

            Log(ctx, $"cylinder r={segment.radius} h={segment.height} scanned={count} hit={applied} dmg={segment.damage}");
            return applied;
        }

        static int ExecuteFan(SkillHitSegment segment, Transform socket, in Context ctx)
        {
            BeginSessionIfNeeded(ctx);
            EnsureBuffers(ctx, out Collider[] buffer);

            Transform root = ctx.OwnerRoot;
            Vector3 forward = ctx.PlanarForward.sqrMagnitude > 0.0001f
                ? ctx.PlanarForward.normalized
                : Vector3.forward;
            GameObject attacker = ctx.Attacker != null ? ctx.Attacker : root.gameObject;

            Vector3 origin;
            if (socket != null)
            {
                origin = socket.position;
                origin.y = root.position.y + segment.hitHeight;
            }
            else
            {
                origin = root.position + Vector3.up * segment.hitHeight;
            }

            int count = FanHitDetector.Overlap(
                origin,
                forward,
                segment.radius,
                segment.fanAngle,
                segment.minHitDistance,
                ctx.Mask,
                buffer);

            int applied = 0;
            for (int i = 0; i < count; i++)
            {
                Collider col = buffer[i];
                if (!FanHitDetector.TryPassFanFilter(
                        col,
                        origin,
                        forward,
                        segment.radius,
                        segment.fanAngle,
                        segment.minHitDistance,
                        out Vector3 hitPoint,
                        out Vector3 planarToHit))
                {
                    continue;
                }

                if (!TryApply(col, hitPoint, planarToHit, forward, segment, ctx, attacker))
                {
                    continue;
                }

                applied++;
            }

            Log(ctx, $"fan a={segment.fanAngle} r={segment.radius} scanned={count} hit={applied} dmg={segment.damage}");
            return applied;
        }

        static bool TryApply(
            Collider col,
            Vector3 hitPoint,
            Vector3 toHit,
            Vector3 forward,
            SkillHitSegment segment,
            in Context ctx,
            GameObject attacker)
        {
            IDamageable damageable = FanHitDetector.ResolveDamageable(col);
            if (damageable == null)
            {
                return false;
            }

            Vector3 dir = toHit.sqrMagnitude > 0.0001f ? toHit.normalized : forward;
            var info = new DamageInfo(
                segment.damage,
                hitPoint,
                dir,
                segment.knockback,
                ctx.ComboIndex,
                attacker);

            return HitResolver.TryApply(HitRequest.Create(
                info,
                damageable,
                col,
                ctx.Flags,
                ctx.Session,
                ctx.OwnerRoot));
        }

        static void BeginSessionIfNeeded(in Context ctx)
        {
            if (ctx.ClearSession && ctx.Session != null)
            {
                ctx.Session.Begin();
            }
        }

        static void EnsureBuffers(in Context ctx, out Collider[] buffer)
        {
            buffer = ctx.Buffer;
            if (buffer == null || buffer.Length == 0)
            {
                buffer = new Collider[24];
            }
        }

        static Collider[] EnsureScratch(in Context ctx)
        {
            if (ctx.Scratch != null && ctx.Scratch.Length > 0)
            {
                return ctx.Scratch;
            }

            return new Collider[24];
        }

        static void Log(in Context ctx, string message)
        {
            if (!ctx.DrawDebug)
            {
                return;
            }

            Debug.Log($"[SkillHit] {message}", ctx.LogContext);
        }
    }
}
