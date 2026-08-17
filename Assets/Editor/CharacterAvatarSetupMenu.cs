using System.Collections.Generic;
using System.Text;
using AttackSkill.Character;
using AttackSkill.Character.HSM;
using AttackSkill.Combat;
using UnityEditor;
using UnityEngine;

namespace AttackSkill.Editor
{
    /// <summary>
    /// Avatar / Actor 拆分工具：表现 Prefab 与运行时装配默认资源。
    /// </summary>
    public static class CharacterAvatarSetupMenu
    {
        const string MaleWandererPath = "Assets/Prefabs/男漂泊者.prefab";
        const string FemaleWandererPath = "Assets/Prefabs/女漂泊者.prefab";
        const string QianxiaoPath = "Assets/Prefabs/千咲.prefab";
        const string ColettaPath = "Assets/Prefabs/柯莱塔.prefab";
        const string ResourcesFolder = "Assets/Resources";
        const string SettingsAssetPath = ResourcesFolder + "/CharacterRuntimeSettings.asset";

        [MenuItem("工具/角色/绑定为 Avatar（仅表现）", false, 11)]
        [MenuItem("Assets/AttackSkill/绑定为 Avatar（仅表现）", false, 81)]
        [MenuItem("GameObject/AttackSkill/绑定为 Avatar（仅表现）", false, 17)]
        static void BindAsAvatar()
        {
            var targets = CollectTargets();
            if (targets.Count == 0)
            {
                EditorUtility.DisplayDialog("Avatar", "请选中角色 Prefab 或场景物体。", "确定");
                return;
            }

            var log = new StringBuilder();
            int ok = 0;
            for (int i = 0; i < targets.Count; i++)
            {
                if (SetupAvatarOnly(targets[i], stripGameplay: true, log))
                {
                    ok++;
                }
            }

            EnsureRuntimeSettingsAsset();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[AvatarSetup] {ok}/{targets.Count}\n{log}");
            EditorUtility.DisplayDialog(
                "Avatar",
                $"已处理 {ok}/{targets.Count}。\nPrefab 仅保留表现；玩法由 CharacterRuntimeAssembler 在生成时装配。",
                "确定");
        }

        [MenuItem("工具/角色/绑定为 Avatar（仅表现）", true)]
        [MenuItem("Assets/AttackSkill/绑定为 Avatar（仅表现）", true)]
        [MenuItem("GameObject/AttackSkill/绑定为 Avatar（仅表现）", true)]
        static bool BindAsAvatarValidate() => CollectTargets().Count > 0;

        [MenuItem("工具/角色/生成 CharacterRuntimeSettings", false, 12)]
        static void CreateRuntimeSettings()
        {
            var asset = EnsureRuntimeSettingsAsset();
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            Debug.Log($"已生成/更新 {SettingsAssetPath}");
        }

        static bool SetupAvatarOnly(GameObject target, bool stripGameplay, StringBuilder log)
        {
            string path = AssetDatabase.GetAssetPath(target);
            bool isPrefabAsset = !string.IsNullOrEmpty(path) && path.EndsWith(".prefab");
            GameObject root;
            if (isPrefabAsset)
            {
                root = PrefabUtility.LoadPrefabContents(path);
            }
            else
            {
                root = target;
                Undo.RegisterFullObjectHierarchyUndo(root, "Bind Character Avatar");
            }

            try
            {
                var report = new StringBuilder();
                report.AppendLine($"▶ Avatar {root.name}");

                if (stripGameplay)
                {
                    StripGameplayComponents(root, report);
                }

                var avatar = root.GetComponent<CharacterAvatar>();
                if (avatar == null)
                {
                    avatar = isPrefabAsset
                        ? root.AddComponent<CharacterAvatar>()
                        : Undo.AddComponent<CharacterAvatar>(root);
                    report.AppendLine("  + CharacterAvatar");
                }

                avatar.AutoBind();
                EditorUtility.SetDirty(avatar);

                var animator = avatar.Animator;
                if (animator != null)
                {
                    animator.applyRootMotion = false;
                    EditorUtility.SetDirty(animator);
                    report.AppendLine("  · applyRootMotion = false");
                }
                else
                {
                    report.AppendLine("  ! 未找到 Animator");
                }

                if (avatar.Weapon != null)
                {
                    report.AppendLine($"  · weapon ← {avatar.Weapon.name}");
                }
                else
                {
                    report.AppendLine("  ! 未找到武器挂点");
                }

                EditorUtility.SetDirty(root);
                if (isPrefabAsset)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    report.AppendLine($"  ✓ {path}");
                }

                log.Append(report);
                return true;
            }
            finally
            {
                if (isPrefabAsset && root != null)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        static void StripGameplayComponents(GameObject root, StringBuilder report)
        {
            // 从根上移除玩法组件；AttackHitRelay 可能在 Animator 子物体上
            RemoveComponent<GenshinLikeCharacter>(root, report);
            RemoveComponent<CharacterSkillPlayer>(root, report);
            RemoveComponent<CharacterAudio>(root, report);
            RemoveComponent<Health>(root, report);
            RemoveComponent<CharacterController>(root, report);
            RemoveComponent<AudioSource>(root, report);

            var relays = root.GetComponentsInChildren<AttackHitRelay>(true);
            for (int i = 0; i < relays.Length; i++)
            {
                if (relays[i] != null)
                {
                    Object.DestroyImmediate(relays[i], true);
                    report.AppendLine("  - AttackHitRelay");
                }
            }
        }

        static void RemoveComponent<T>(GameObject root, StringBuilder report) where T : Component
        {
            var c = root.GetComponent<T>();
            if (c == null)
            {
                return;
            }

            Object.DestroyImmediate(c, true);
            report.AppendLine($"  - {typeof(T).Name}");
        }

        static CharacterRuntimeSettings EnsureRuntimeSettingsAsset()
        {
            if (!AssetDatabase.IsValidFolder(ResourcesFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }

            var existing = AssetDatabase.LoadAssetAtPath<CharacterRuntimeSettings>(SettingsAssetPath);
            if (existing == null)
            {
                existing = ScriptableObject.CreateInstance<CharacterRuntimeSettings>();
                AssetDatabase.CreateAsset(existing, SettingsAssetPath);
            }

            if (existing.maleWandererPrefab == null)
            {
                existing.maleWandererPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MaleWandererPath);
            }

            if (existing.femaleWandererPrefab == null)
            {
                existing.femaleWandererPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FemaleWandererPath);
            }

            if (existing.qianxiaoPrefab == null)
            {
                existing.qianxiaoPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(QianxiaoPath);
            }

            if (existing.colettaPrefab == null)
            {
                existing.colettaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ColettaPath);
            }

            EditorUtility.SetDirty(existing);
            return existing;
        }

        static List<GameObject> CollectTargets()
        {
            var list = new List<GameObject>();
            var selected = Selection.objects;
            for (int i = 0; i < selected.Length; i++)
            {
                if (selected[i] is GameObject go && !list.Contains(go))
                {
                    list.Add(go);
                    continue;
                }

                string path = AssetDatabase.GetAssetPath(selected[i]);
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab"))
                {
                    continue;
                }

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null && !list.Contains(prefab))
                {
                    list.Add(prefab);
                }
            }

            return list;
        }
    }
}
