using AttackSkill.UI;
using UnityEditor;
using UnityEngine;

namespace AttackSkill.Editor
{
    public static class PauseMenuPrefabMenu
    {
        const string PrefabPath = "Assets/Prefabs/UI/UI_PauseMenu_Dialog.prefab";

        [MenuItem("工具/UI/生成暂停菜单 Prefab", false, 65)]
        public static void CreatePauseMenuPrefab()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            {
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            }

            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/UI"))
            {
                AssetDatabase.CreateFolder("Assets/Prefabs", "UI");
            }

            var template = UIPauseMenuDialog.CreateRuntimeTemplate();
            template.SetActive(true);
            template.hideFlags = HideFlags.None;

            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            {
                AssetDatabase.DeleteAsset(PrefabPath);
            }

            PrefabUtility.SaveAsPrefabAsset(template, PrefabPath);
            Object.DestroyImmediate(template);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            EditorUtility.DisplayDialog(
                "UI",
                "已生成 UI_PauseMenu_Dialog.prefab。\n可再执行「刷新场景 UIManager 条目」。",
                "OK");
        }
    }
}
