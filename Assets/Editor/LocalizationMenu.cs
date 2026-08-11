using AttackSkill.Localization;
using UnityEditor;
using UnityEngine;

namespace AttackSkill.Editor
{
    public static class LocalizationMenu
    {
        [MenuItem("GameObject/AttackSkill/创建多语言控制器 Localization", false, 17)]
        static void CreateBootstrap()
        {
            var existing = Object.FindObjectOfType<LocalizationBootstrap>();
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                Debug.Log("场景里已有 LocalizationBootstrap。", existing);
                return;
            }

            var go = new GameObject("Localization");
            go.AddComponent<LocalizationBootstrap>();
            Undo.RegisterCreatedObjectUndo(go, "Create Localization");
            Selection.activeGameObject = go;
            Debug.Log(
                "已创建 LocalizationBootstrap：Awake 载 Bundle，F8 切语言。\n" +
                "文案请用「工具/多语言/从 Excel 导出 JSON」。");
        }
    }
}
