using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace AttackSkill.Editor
{
    public static class InputSystemMigrateMenu
    {
        [MenuItem("工具/输入/场景 EventSystem 切到 Input System UI", false, 80)]
        public static void UpgradeOpenScenesEventSystems()
        {
            int upgraded = 0;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                {
                    continue;
                }

                var roots = scene.GetRootGameObjects();
                for (int r = 0; r < roots.Length; r++)
                {
                    var systems = roots[r].GetComponentsInChildren<EventSystem>(true);
                    for (int s = 0; s < systems.Length; s++)
                    {
                        if (Upgrade(systems[s].gameObject))
                        {
                            upgraded++;
                            EditorSceneManager.MarkSceneDirty(scene);
                        }
                    }
                }
            }

            EditorUtility.DisplayDialog(
                "Input System",
                upgraded > 0
                    ? $"已升级 {upgraded} 个 EventSystem。请保存场景。"
                    : "打开的场景里没有需要升级的 EventSystem。",
                "OK");
        }

        static bool Upgrade(GameObject go)
        {
            bool changed = false;
            var legacy = go.GetComponent<StandaloneInputModule>();
            if (legacy != null)
            {
                Undo.DestroyObjectImmediate(legacy);
                changed = true;
            }

            if (go.GetComponent<InputSystemUIInputModule>() == null)
            {
                Undo.AddComponent<InputSystemUIInputModule>(go);
                changed = true;
            }

            return changed;
        }
    }
}
