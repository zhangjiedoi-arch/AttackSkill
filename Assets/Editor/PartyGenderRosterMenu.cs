using AttackSkill.Character;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AttackSkill.Editor
{
    /// <summary>给 PartyController / CharacterRuntimeSettings 绑定性别阵容 Prefab。</summary>
    public static class PartyGenderRosterMenu
    {
        const string MalePath = "Assets/Prefabs/男漂泊者.prefab";
        const string FemalePath = "Assets/Prefabs/女漂泊者.prefab";
        const string QianxiaoPath = "Assets/Prefabs/千咲.prefab";
        const string ColettaPath = "Assets/Prefabs/柯莱塔.prefab";
        const string SettingsPath = "Assets/Resources/CharacterRuntimeSettings.asset";

        [MenuItem("工具/角色/绑定 Party 性别阵容 Prefab", false, 13)]
        static void BindPartyGenderRoster()
        {
            var male = AssetDatabase.LoadAssetAtPath<GameObject>(MalePath);
            var female = AssetDatabase.LoadAssetAtPath<GameObject>(FemalePath);
            var qianxiao = AssetDatabase.LoadAssetAtPath<GameObject>(QianxiaoPath);
            var coletta = AssetDatabase.LoadAssetAtPath<GameObject>(ColettaPath);

            if (male == null || female == null || qianxiao == null || coletta == null)
            {
                EditorUtility.DisplayDialog(
                    "性别阵容",
                    "缺少 Prefab：\n男漂泊者 / 女漂泊者 / 千咲 / 柯莱塔",
                    "确定");
                return;
            }

            WriteRuntimeSettings(male, female, qianxiao, coletta);

            var parties = Object.FindObjectsOfType<PartyController>(true);
            int partyCount = parties != null ? parties.Length : 0;
            for (int i = 0; i < partyCount; i++)
            {
                var so = new SerializedObject(parties[i]);
                so.FindProperty("applyGenderRoster").boolValue = true;
                so.FindProperty("maleWandererPrefab").objectReferenceValue = male;
                so.FindProperty("femaleWandererPrefab").objectReferenceValue = female;
                so.FindProperty("qianxiaoPrefab").objectReferenceValue = qianxiao;
                so.FindProperty("colettaPrefab").objectReferenceValue = coletta;

                var roster = so.FindProperty("characterPrefabs");
                roster.arraySize = 3;
                roster.GetArrayElementAtIndex(0).objectReferenceValue = male;
                roster.GetArrayElementAtIndex(1).objectReferenceValue = qianxiao;
                roster.GetArrayElementAtIndex(2).objectReferenceValue = coletta;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(parties[i]);
            }

            var scene = SceneManager.GetActiveScene();
            if (scene.IsValid() && scene.isLoaded && partyCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[PartyGenderRoster] Settings 已更新；PartyController × {partyCount}");
            EditorUtility.DisplayDialog(
                "性别阵容",
                $"已写入 CharacterRuntimeSettings，并绑定 {partyCount} 个 PartyController。",
                "确定");
        }

        static void WriteRuntimeSettings(
            GameObject male,
            GameObject female,
            GameObject qianxiao,
            GameObject coletta)
        {
            var settings = AssetDatabase.LoadAssetAtPath<CharacterRuntimeSettings>(SettingsPath);
            if (settings == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                {
                    AssetDatabase.CreateFolder("Assets", "Resources");
                }

                settings = ScriptableObject.CreateInstance<CharacterRuntimeSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }

            settings.maleWandererPrefab = male;
            settings.femaleWandererPrefab = female;
            settings.qianxiaoPrefab = qianxiao;
            settings.colettaPrefab = coletta;
            EditorUtility.SetDirty(settings);
        }
    }
}
