using System.Collections.Generic;
using AttackSkill.Character;
using AttackSkill.Enemy;
using UnityEngine;

namespace AttackSkill.UI
{
    /// <summary>小地图头像：<c>Assets/Sprites/SmallMap/PlayerIcon|EnemyIcon</c>，按角色名/怪名匹配。</summary>
    public static class SmallMapIconCatalog
    {
        const string PlayerFolder = "Assets/Sprites/SmallMap/PlayerIcon";
        const string EnemyFolder = "Assets/Sprites/SmallMap/EnemyIcon";
        const string MarkerFolder = "Assets/Sprites/SmallMap/MarkerIcon";

        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>(32);

        public static Sprite ForPlayer(PartyPortraitId id)
        {
            switch (id)
            {
                case PartyPortraitId.WandererFemale:
                    return Load(PlayerFolder, "女漂泊者");
                case PartyPortraitId.WandererMale:
                    return Load(PlayerFolder, "男漂泊者");
                case PartyPortraitId.Qianxiao:
                    return Load(PlayerFolder, "千咲");
                case PartyPortraitId.Coletta:
                    return Load(PlayerFolder, "柯莱塔");
                default:
                    return Load(PlayerFolder, "女漂泊者");
            }
        }

        public static Sprite ForEnemy(EnemyDefinition def)
        {
            if (def == null)
            {
                return null;
            }

            Sprite s = TryLoadEnemy(def.displayName);
            if (s != null)
            {
                return s;
            }

            s = TryLoadEnemy(def.name);
            if (s != null)
            {
                return s;
            }

            if (!string.IsNullOrEmpty(def.displayName) && def.displayName.IndexOf("鳞人") >= 0)
            {
                return TryLoadEnemy("鳞人");
            }

            if (def.displayName == "朔雷之麟" || def.name == "朔雷之麟")
            {
                return TryLoadEnemy("朔雷之鳞");
            }

            return null;
        }

        public static Sprite ForMarker(string iconFile)
        {
            if (string.IsNullOrEmpty(iconFile))
            {
                return null;
            }

            return Load(MarkerFolder, iconFile);
        }

        static Sprite TryLoadEnemy(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return null;
            }

            return Load(EnemyFolder, fileName);
        }

        static Sprite Load(string folder, string fileName)
        {
            string key = folder + "/" + fileName;
            if (Cache.TryGetValue(key, out Sprite cached))
            {
                return cached;
            }

            Sprite sprite = null;
#if UNITY_EDITOR
            string path = $"{folder}/{fileName}.png";
            sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Object[] all = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] is Sprite sp)
                    {
                        sprite = sp;
                        break;
                    }
                }
            }
#endif
            if (sprite == null)
            {
                string resFolder = "EnemyIcon";
                if (folder.EndsWith("PlayerIcon"))
                {
                    resFolder = "PlayerIcon";
                }
                else if (folder.EndsWith("MarkerIcon"))
                {
                    resFolder = "MarkerIcon";
                }

                sprite = Resources.Load<Sprite>($"SmallMap/{resFolder}/{fileName}");
            }
            if (sprite != null)
            {
                Cache[key] = sprite;
            }

            return sprite;
        }
    }
}
