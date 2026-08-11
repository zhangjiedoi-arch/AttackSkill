using UnityEngine;

namespace AttackSkill.Combat
{
    /// <summary>一份技能动作的多段出伤表。</summary>
    [CreateAssetMenu(
        menuName = "AttackSkill/Combat/Skill Hit Profile",
        fileName = "SkillHitProfile")]
    public class SkillHitProfile : ScriptableObject
    {
        public SkillHitSegment[] segments =
        {
            new SkillHitSegment
            {
                id = "chest_r",
                socket = HitSocketId.Hit_Chest_R,
                shape = HitShapeType.Sphere,
                radius = 1f,
                damage = 28f,
                knockback = 1.2f,
                vfxLife = 2.5f,
            },
            new SkillHitSegment
            {
                id = "chest_l",
                socket = HitSocketId.Hit_Chest_L,
                shape = HitShapeType.Sphere,
                radius = 1f,
                damage = 28f,
                knockback = 1.2f,
                vfxLife = 2.5f,
            },
            new SkillHitSegment
            {
                id = "root_aoe",
                socket = HitSocketId.Hit_Root,
                shape = HitShapeType.Cylinder,
                radius = 2f,
                height = 0.5f,
                damage = 55f,
                knockback = 3f,
                vfxLife = 2.5f,
            },
        };

        public bool TryGetSegment(int index, out SkillHitSegment segment)
        {
            segment = null;
            if (segments == null || index < 0 || index >= segments.Length)
            {
                return false;
            }

            segment = segments[index];
            return segment != null;
        }

        public int FindIndexBySocket(HitSocketId socket)
        {
            if (segments == null)
            {
                return -1;
            }

            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i] != null && segments[i].socket == socket)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
