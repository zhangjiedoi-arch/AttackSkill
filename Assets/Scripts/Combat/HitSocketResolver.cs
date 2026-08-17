using AttackSkill.Character;
using UnityEngine;

namespace AttackSkill.Combat
{
    /// <summary>挂点 ID → Transform（Prefab 实体 + 命名约定）。</summary>
    public static class HitSocketResolver
    {
        public static Transform Resolve(Transform root, HitSocketId id)
        {
            if (root == null || id == HitSocketId.None)
            {
                return null;
            }

            var avatar = root.GetComponent<CharacterAvatar>();
            if (avatar == null)
            {
                avatar = root.GetComponentInChildren<CharacterAvatar>(true);
            }

            if (avatar != null)
            {
                if (avatar.Hits == null ||
                    avatar.Hits.ChestR == null ||
                    avatar.Hits.ChestL == null ||
                    avatar.Hits.Root == null ||
                    avatar.SkillR == null ||
                    avatar.SkillR.RHitRoot == null)
                {
                    avatar.AutoBind();
                }

                switch (id)
                {
                    case HitSocketId.Hit_Chest_R:
                        if (avatar.Hits?.ChestR != null)
                        {
                            return avatar.Hits.ChestR;
                        }

                        break;
                    case HitSocketId.Hit_Chest_L:
                        if (avatar.Hits?.ChestL != null)
                        {
                            return avatar.Hits.ChestL;
                        }

                        break;
                    case HitSocketId.Hit_Root:
                        if (avatar.Hits?.Root != null)
                        {
                            return avatar.Hits.Root;
                        }

                        break;
                    case HitSocketId.R_Hit_Root:
                        if (avatar.SkillR?.RHitRoot != null)
                        {
                            return avatar.SkillR.RHitRoot;
                        }

                        break;
                    case HitSocketId.Weapon:
                        if (avatar.Weapon != null)
                        {
                            return avatar.Weapon;
                        }

                        break;
                    case HitSocketId.HitOrigin:
                        return avatar.HitOrigin;
                }
            }

            return FindChildExact(root, ToHierarchyName(id));
        }

        public static string ToHierarchyName(HitSocketId id)
        {
            switch (id)
            {
                case HitSocketId.Hit_Chest_R:
                    return CharacterAvatar.HitChestRName;
                case HitSocketId.Hit_Chest_L:
                    return CharacterAvatar.HitChestLName;
                case HitSocketId.Hit_Root:
                    return CharacterAvatar.HitRootName;
                case HitSocketId.R_Hit_Root:
                    return CharacterAvatar.SkillRHitRootName;
                case HitSocketId.Enemy_Hit_Chest_R:
                    return "Enemy_Hit_Chest_R";
                case HitSocketId.Weapon:
                    return CharacterAvatar.SwordSocketName;
                default:
                    return id.ToString();
            }
        }

        static Transform FindChildExact(Transform root, string exactName)
        {
            if (root == null || string.IsNullOrEmpty(exactName))
            {
                return null;
            }

            var all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == exactName)
                {
                    return all[i];
                }
            }

            return null;
        }
    }
}
