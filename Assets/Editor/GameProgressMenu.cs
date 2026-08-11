using AttackSkill.Game;
using UnityEditor;
using UnityEngine;

namespace AttackSkill.Editor
{
    public static class GameProgressMenu
    {
        [MenuItem("GameObject/AttackSkill/创建进度控制器 GameProgress", false, 16)]
        static void CreateProgressController()
        {
            var existing = Object.FindObjectOfType<GameProgressController>();
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                Debug.Log("场景里已有 GameProgressController。", existing);
                return;
            }

            var go = new GameObject("GameProgress");
            go.AddComponent<GameProgressController>();
            Undo.RegisterCreatedObjectUndo(go, "Create GameProgress");
            Selection.activeGameObject = go;
            Debug.Log(
                "已创建 GameProgress：进游戏读档→加载场景→复位角色。\n" +
                $"存档路径：{GameSaveService.SavePath}\n" +
                "F5 快速存档 / F6 删档 / 退出自动存。");
        }
    }
}
