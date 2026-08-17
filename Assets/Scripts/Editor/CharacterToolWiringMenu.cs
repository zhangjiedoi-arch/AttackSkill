#if UNITY_EDITOR
using AttackSkill.Character;
using UnityEditor;
using UnityEngine;

namespace AttackSkill.EditorTools
{
    /// <summary>
    /// 把 Prefabs/Tools 接到 RuntimeSettings，并给角色 Avatar 填序列化挂点。
    /// </summary>
    public static class CharacterToolWiringMenu
    {
        const string SettingsPath = "Assets/Resources/CharacterRuntimeSettings.asset";
        const string MotorcyclePath = "Assets/Prefabs/Tools/摩托.prefab";
        const string SwordPath = "Assets/Prefabs/Tools/脆刃.prefab";
        const string WingsPath = "Assets/Prefabs/Tools/哥伦比亚的翅膀.prefab";
        const string SkillRAoePath = "Assets/Prefabs/VFX/AoE slash orange.prefab";

        [MenuItem("AttackSkill/Character/Assign Tool Prefabs To Runtime Settings")]
        public static void AssignToolPrefabs()
        {
            var settings = AssetDatabase.LoadAssetAtPath<CharacterRuntimeSettings>(SettingsPath);
            if (settings == null)
            {
                Debug.LogError($"[CharacterToolWiring] 未找到 {SettingsPath}");
                return;
            }

            settings.motorcyclePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MotorcyclePath);
            settings.swordPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SwordPath);
            settings.wingsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WingsPath);
            settings.skillRAoeVfxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SkillRAoePath);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            Debug.Log(
                $"[CharacterToolWiring] RuntimeSettings Tools → " +
                $"moto={(settings.motorcyclePrefab != null ? settings.motorcyclePrefab.name : "null")}, " +
                $"sword={(settings.swordPrefab != null ? settings.swordPrefab.name : "null")}, " +
                $"wings={(settings.wingsPrefab != null ? settings.wingsPrefab.name : "null")}, " +
                $"skillRAoe={(settings.skillRAoeVfxPrefab != null ? settings.skillRAoeVfxPrefab.name : "null")}");
            Selection.activeObject = settings;
        }

        [MenuItem("AttackSkill/Character/Bind Tool Sockets On Player Prefabs")]
        public static void BindToolSockets()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Player" });
            int bound = 0;
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var avatars = root.GetComponentsInChildren<CharacterAvatar>(true);
                    bool dirty = false;
                    for (int a = 0; a < avatars.Length; a++)
                    {
                        var avatar = avatars[a];
                        if (avatar == null)
                        {
                            continue;
                        }

                        avatar.AutoBind();
                        EditorUtility.SetDirty(avatar);
                        dirty = true;
                        bound++;
                    }

                    if (dirty)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[CharacterToolWiring] 已为 {bound} 个 CharacterAvatar 绑定 Tool Sockets。");
        }

        [MenuItem("AttackSkill/Character/Delete Resources/Tools Copies")]
        public static void DeleteResourcesToolsCopies()
        {
            const string folder = "Assets/Resources/Tools";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                Debug.Log("[CharacterToolWiring] Resources/Tools 不存在，无需删除。");
                return;
            }

            if (!AssetDatabase.DeleteAsset(folder))
            {
                // 逐个删
                string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { folder });
                for (int i = 0; i < guids.Length; i++)
                {
                    AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guids[i]));
                }

                AssetDatabase.DeleteAsset(folder);
            }

            AssetDatabase.Refresh();
            Debug.Log("[CharacterToolWiring] 已删除 Resources/Tools 副本，请只用 Prefabs/Tools + RuntimeSettings。");
        }

        [MenuItem("AttackSkill/Character/Wire Tools (Assign + Bind + Delete Copies)")]
        public static void WireAll()
        {
            AssignToolPrefabs();
            BindToolSockets();
            DeleteResourcesToolsCopies();
        }
    }
}
#endif
