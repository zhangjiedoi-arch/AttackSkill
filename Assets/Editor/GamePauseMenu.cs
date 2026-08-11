using AttackSkill.CameraSystem;
using AttackSkill.Game;
using UnityEditor;
using UnityEngine;

namespace AttackSkill.Editor
{
    public static class GamePauseMenu
    {
        [MenuItem("GameObject/AttackSkill/创建暂停控制器 GamePause", false, 15)]
        static void CreatePauseController()
        {
            var existing = Object.FindObjectOfType<GamePauseController>();
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                Debug.Log("场景里已有 GamePauseController。", existing);
                return;
            }

            var go = new GameObject("GamePause");
            var pause = go.AddComponent<GamePauseController>();
            var tpc = Object.FindObjectOfType<ThirdPersonCamera>();
            if (tpc != null)
            {
                var so = new SerializedObject(pause);
                so.FindProperty("thirdPersonCamera").objectReferenceValue = tpc;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            Undo.RegisterCreatedObjectUndo(go, "Create GamePause");
            Selection.activeGameObject = go;
            Debug.Log("已创建 GamePause：ESC 开/关暂停菜单；设置从暂停进入。");
        }
    }
}
