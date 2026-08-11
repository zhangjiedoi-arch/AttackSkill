using UnityEngine;

namespace AttackSkill.Combat
{
    /// <summary>无 SO 资源时的运行时默认 Profile（与当前 E 技能 / 敌人攻击参数一致）。</summary>
    public static class SkillHitProfileDefaults
    {
        static SkillHitProfile _playerE;
        static SkillHitProfile _enemyBasic;

        public static SkillHitProfile PlayerE(
            GameObject snowHit = null,
            GameObject groundAoe = null,
            AudioClip fatKickR = null,
            AudioClip fatKickL = null,
            AudioClip rootLand = null)
        {
            if (_playerE == null)
            {
                _playerE = ScriptableObject.CreateInstance<SkillHitProfile>();
                _playerE.name = "SkillHit_Player_E_Runtime";
                _playerE.segments = new[]
                {
                    Make("chest_r", HitSocketId.Hit_Chest_R, HitShapeType.Sphere, 1f, 0f, 28f, 1.2f, snowHit, fatKickR),
                    Make("chest_l", HitSocketId.Hit_Chest_L, HitShapeType.Sphere, 1f, 0f, 28f, 1.2f, snowHit, fatKickL),
                    Make("root_aoe", HitSocketId.Hit_Root, HitShapeType.Cylinder, 2f, 0.5f, 55f, 3f, groundAoe, rootLand),
                };
            }
            else
            {
                ApplyVfxIfEmpty(_playerE, snowHit, groundAoe);
                ApplySfxIfEmpty(_playerE, fatKickR, fatKickL, rootLand);
            }

            return _playerE;
        }

        public static SkillHitProfile EnemyBasic(float damage = 12f, float knockback = 1.2f, float radius = 1.5f)
        {
            if (_enemyBasic == null)
            {
                _enemyBasic = ScriptableObject.CreateInstance<SkillHitProfile>();
                _enemyBasic.name = "SkillHit_Enemy_Basic_Runtime";
                _enemyBasic.segments = new[]
                {
                    Make(
                        "enemy_chest_r",
                        HitSocketId.Enemy_Hit_Chest_R,
                        HitShapeType.Sphere,
                        radius,
                        0f,
                        damage,
                        knockback,
                        null,
                        null),
                };
            }

            return _enemyBasic;
        }

        static void ApplyVfxIfEmpty(SkillHitProfile profile, GameObject snowHit, GameObject groundAoe)
        {
            if (profile?.segments == null)
            {
                return;
            }

            for (int i = 0; i < profile.segments.Length; i++)
            {
                var seg = profile.segments[i];
                if (seg == null || seg.vfxPrefab != null)
                {
                    continue;
                }

                if (seg.socket == HitSocketId.Hit_Root)
                {
                    seg.vfxPrefab = groundAoe;
                }
                else if (seg.socket == HitSocketId.Hit_Chest_R || seg.socket == HitSocketId.Hit_Chest_L)
                {
                    seg.vfxPrefab = snowHit;
                }
            }
        }

        static void ApplySfxIfEmpty(
            SkillHitProfile profile,
            AudioClip fatKickR,
            AudioClip fatKickL,
            AudioClip rootLand)
        {
            if (profile?.segments == null)
            {
                return;
            }

            for (int i = 0; i < profile.segments.Length; i++)
            {
                var seg = profile.segments[i];
                if (seg == null || seg.sfxClip != null)
                {
                    continue;
                }

                switch (seg.socket)
                {
                    case HitSocketId.Hit_Chest_R:
                        seg.sfxClip = fatKickR;
                        break;
                    case HitSocketId.Hit_Chest_L:
                        seg.sfxClip = fatKickL;
                        break;
                    case HitSocketId.Hit_Root:
                        seg.sfxClip = rootLand;
                        break;
                }
            }
        }

        static SkillHitSegment Make(
            string id,
            HitSocketId socket,
            HitShapeType shape,
            float radius,
            float height,
            float damage,
            float knockback,
            GameObject vfx,
            AudioClip sfx)
        {
            return new SkillHitSegment
            {
                id = id,
                socket = socket,
                shape = shape,
                radius = radius,
                height = height,
                damage = damage,
                knockback = knockback,
                vfxPrefab = vfx,
                vfxLife = 2.5f,
                sfxClip = sfx,
                sfxVolume = 1f,
            };
        }
    }
}
