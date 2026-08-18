using System.Collections.Generic;
using AttackSkill.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AttackSkill.Editor
{
    public static class UIRootMenu
    {
        const string PrefabFolder = "Assets/Prefabs/UI";

        [MenuItem("GameObject/AttackSkill/创建 UIRoot（Canvas分层+UIManager）", false, 18)]
        public static void CreateUIRoot()
        {
            var existing = Object.FindObjectOfType<UIManager>();
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                EditorUtility.DisplayDialog("UIRoot", "场景里已有 UIManager。", "OK");
                return;
            }

            var root = new GameObject("UIRoot");
            Undo.RegisterCreatedObjectUndo(root, "Create UIRoot");

            var canvasGo = new GameObject("Canvas");
            canvasGo.transform.SetParent(root.transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var layerPanel = CreateLayer(canvasGo.transform, "Layer_Panel");
            var layerDialog = CreateLayer(canvasGo.transform, "Layer_Dialog");
            var layerTip = CreateLayer(canvasGo.transform, "Layer_Tip");

            if (Object.FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.transform.SetParent(root.transform, false);
                es.AddComponent<EventSystem>();
                es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }

            var manager = root.AddComponent<UIManager>();
            root.AddComponent<UIBootstrap>();
            var so = new SerializedObject(manager);
            so.FindProperty("layerPanel").objectReferenceValue = layerPanel;
            so.FindProperty("layerDialog").objectReferenceValue = layerDialog;
            so.FindProperty("layerTip").objectReferenceValue = layerTip;

            // 只注册 Prefab 引用，不在创建时改 Prefab 资产（避免 Missing Script / 保存崩溃）
            var list = BuildEntries(attachViews: false);
            var entriesProp = so.FindProperty("entries");
            entriesProp.ClearArray();
            for (int i = 0; i < list.Count; i++)
            {
                entriesProp.InsertArrayElementAtIndex(i);
                var elem = entriesProp.GetArrayElementAtIndex(i);
                var e = list[i];
                elem.FindPropertyRelative("id").intValue = (int)e.id;
                elem.FindPropertyRelative("layer").intValue = (int)e.layer;
                elem.FindPropertyRelative("prefab").objectReferenceValue = e.prefab;
                elem.FindPropertyRelative("singleton").boolValue = e.singleton;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            root.AddComponent<OpenSceneFlowController>();
            Selection.activeGameObject = root;
            Debug.Log(
                $"已创建 UIRoot，注册 {list.Count} 个界面。\n" +
                "View 脚本由运行时自动挂载；如需写进 Prefab 请用「工具/UI/安全挂载 View 到 Prefab」。");
        }

        [MenuItem("工具/UI/给场景 UIRoot 挂 UIBootstrap", false, 64)]
        public static void AttachUIBootstrap()
        {
            var managers = Object.FindObjectsOfType<UIManager>(true);
            if (managers == null || managers.Length == 0)
            {
                EditorUtility.DisplayDialog("UI", "场景中没有 UIManager。", "OK");
                return;
            }

            int n = 0;
            for (int i = 0; i < managers.Length; i++)
            {
                var go = managers[i].gameObject;
                if (go.GetComponent<UIBootstrap>() == null)
                {
                    Undo.AddComponent<UIBootstrap>(go);
                    n++;
                }
            }

            EditorUtility.DisplayDialog("UI", $"已为 {n} 个 UIRoot 添加 UIBootstrap。", "OK");
        }

        [MenuItem("工具/UI/安全挂载 View 到 Prefab", false, 60)]
        public static void AttachViewsSafely()
        {
            int changed = 0;
            changed += AttachOne("UI_OpenScene1_Panel", typeof(UIOpenScene1Panel)) ? 1 : 0;
            changed += AttachOne("UI_OpenScene2_Panel", typeof(UIOpenScene2Panel)) ? 1 : 0;
            changed += AttachOne("UI_OpenScene3_Panel", typeof(UIOpenScene3Panel)) ? 1 : 0;
            changed += AttachOne("UI_OpenScene4_Panel", typeof(UIOpenScene4Panel)) ? 1 : 0;
            changed += AttachOne("UI_ChangeScene_Panel", typeof(UIChangeScenePanel)) ? 1 : 0;
            changed += AttachOne("UI_AgeRem_Dialog", typeof(UIAgeRemDialog)) ? 1 : 0;
            changed += AttachOne("UI_Setting_Dialog", typeof(UISettingDialog)) ? 1 : 0;
            changed += AttachOne("UI_Tools_Dialog", typeof(UIToolsDialog)) ? 1 : 0;
            changed += AttachOne("UI_JoinGame_Dialog", typeof(UIJoinGameDialog)) ? 1 : 0;
            changed += AttachOne("UI_CommonSure_Dialog", typeof(UICommonSureDialog)) ? 1 : 0;
            changed += AttachOne("UI_CommonTip_Dialog", typeof(UICommonTipDialog)) ? 1 : 0;
            changed += AttachOne("UI_LogIn_Dialog", typeof(UILogInDialog)) ? 1 : 0;
            changed += AttachOne("UI_ChooseGender_Dialog", typeof(UIChooseGenderDialog)) ? 1 : 0;
            changed += AttachOne("UI_PauseMenu_Dialog", typeof(UIPauseMenuDialog)) ? 1 : 0;
            changed += AttachOne("UI_GameOver_Dialog", typeof(UIGameOverDialog)) ? 1 : 0;
            changed += AttachOne("BattleHUD/UI_SkillWheel_Dialog", typeof(UISkillWheelDialog)) ? 1 : 0;
            changed += AttachOne("BattleHUD/UI_BattleParty_Panel", typeof(UIBattlePartyPanel)) ? 1 : 0;
            changed += AttachOne("BattleHUD/UI_BattleSystem_Panel", typeof(UIBattleSystemPanel)) ? 1 : 0;
            changed += AttachOne("BattleHUD/UI_BattleCombat_Panel", typeof(UIBattleCombatPanel)) ? 1 : 0;
            changed += AttachOne("BattleHUD/UI_BattleVitals_Panel", typeof(UIBattleVitalsPanel)) ? 1 : 0;
            changed += AttachOne("BattleHUD/UI_Task_Panel", typeof(UITaskPanel)) ? 1 : 0;
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("UI", $"安全挂载完成，更新 {changed} 个 Prefab。", "OK");
        }

        [MenuItem("工具/UI/刷新场景 UIManager 条目", false, 62)]
        public static void RefreshSceneUiManagerEntries()
        {
            var managers = Object.FindObjectsOfType<UIManager>();
            if (managers == null || managers.Length == 0)
            {
                EditorUtility.DisplayDialog("UI", "场景中没有 UIManager。", "OK");
                return;
            }

            var list = BuildEntries(attachViews: false);
            for (int m = 0; m < managers.Length; m++)
            {
                var manager = managers[m];
                Undo.RecordObject(manager, "Refresh UIManager Entries");
                manager.SetEntries(list);
                EditorUtility.SetDirty(manager);
            }

            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("UI", $"已刷新 {managers.Length} 个 UIManager，共 {list.Count} 条。\n请保存场景。", "OK");
        }

        [MenuItem("工具/UI/清理本地登录存档", false, 63)]
        public static void ClearLocalLogin()
        {
            LocalAccountStore.ClearAll();
            EditorUtility.DisplayDialog("UI", "已清理本地账号/性别/锁定状态。", "OK");
        }

        [MenuItem("工具/UI/清理 Prefab Missing Script", false, 61)]
        public static void CleanupMissingScripts()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder });
            int removed = 0;
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    int before = CountMissing(root);
                    if (before <= 0)
                    {
                        continue;
                    }

                    var transforms = root.GetComponentsInChildren<Transform>(true);
                    for (int t = 0; t < transforms.Length; t++)
                    {
                        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transforms[t].gameObject);
                    }

                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    removed += before;
                    Debug.Log($"[UI] 清理 Missing Script ×{before} → {path}");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("UI", $"已清理 Missing Script（共 {removed} 处）。", "OK");
        }

        static int CountMissing(GameObject root)
        {
            int n = 0;
            var behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            // Missing scripts don't deserialize as MonoBehaviour instances well;
            // use GameObjectUtility
            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                n += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transforms[i].gameObject);
            }

            return n;
        }

        static bool AttachOne(string prefabName, System.Type viewType)
        {
            string path = $"{PrefabFolder}/{prefabName}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"未找到 {path}");
                return false;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root);
                var existing = root.GetComponents<UIBase>();
                bool hasTarget = false;
                for (int i = 0; i < existing.Length; i++)
                {
                    if (existing[i] != null && existing[i].GetType() == viewType)
                    {
                        hasTarget = true;
                    }
                    else if (existing[i] != null)
                    {
                        Object.DestroyImmediate(existing[i]);
                    }
                }

                if (!hasTarget)
                {
                    root.AddComponent(viewType);
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[UI] 已安全挂载 {viewType.Name} → {path}");
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static RectTransform CreateLayer(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        static List<UIPrefabEntry> BuildEntries(bool attachViews)
        {
            var list = new List<UIPrefabEntry>();
            TryAdd(list, UIId.OpenScene1, UILayer.Panel, "UI_OpenScene1_Panel", typeof(UIOpenScene1Panel), attachViews);
            TryAdd(list, UIId.OpenScene2, UILayer.Panel, "UI_OpenScene2_Panel", typeof(UIOpenScene2Panel), attachViews);
            TryAdd(list, UIId.OpenScene3, UILayer.Panel, "UI_OpenScene3_Panel", typeof(UIOpenScene3Panel), attachViews);
            TryAdd(list, UIId.OpenScene4, UILayer.Panel, "UI_OpenScene4_Panel", typeof(UIOpenScene4Panel), attachViews);
            TryAdd(list, UIId.ChangeScene, UILayer.Panel, "UI_ChangeScene_Panel", typeof(UIChangeScenePanel), attachViews);
            TryAdd(list, UIId.AgeRem, UILayer.Dialog, "UI_AgeRem_Dialog", typeof(UIAgeRemDialog), attachViews);
            TryAdd(list, UIId.Setting, UILayer.Dialog, "UI_Setting_Dialog", typeof(UISettingDialog), attachViews);
            TryAdd(list, UIId.Tools, UILayer.Dialog, "UI_Tools_Dialog", typeof(UIToolsDialog), attachViews);
            TryAdd(list, UIId.JoinGame, UILayer.Dialog, "UI_JoinGame_Dialog", typeof(UIJoinGameDialog), attachViews);
            TryAdd(list, UIId.CommonSure, UILayer.Dialog, "UI_CommonSure_Dialog", typeof(UICommonSureDialog), attachViews);
            TryAdd(list, UIId.CommonTip, UILayer.Tip, "UI_CommonTip_Dialog", typeof(UICommonTipDialog), attachViews);
            TryAdd(list, UIId.LogIn, UILayer.Dialog, "UI_LogIn_Dialog", typeof(UILogInDialog), attachViews);
            TryAdd(list, UIId.ChooseGender, UILayer.Dialog, "UI_ChooseGender_Dialog", typeof(UIChooseGenderDialog), attachViews);
            TryAdd(list, UIId.PauseMenu, UILayer.Dialog, "UI_PauseMenu_Dialog", typeof(UIPauseMenuDialog), attachViews);
            TryAdd(list, UIId.GameOver, UILayer.Dialog, "UI_GameOver_Dialog", typeof(UIGameOverDialog), attachViews);
            TryAdd(list, UIId.SkillWheel, UILayer.Dialog, "BattleHUD/UI_SkillWheel_Dialog", typeof(UISkillWheelDialog), attachViews);
            TryAdd(
                list,
                UIId.BattleParty,
                UILayer.Panel,
                "BattleHUD/UI_BattleParty_Panel",
                typeof(UIBattlePartyPanel),
                attachViews,
                stretchToParent: false,
                closesOtherPanels: false);
            TryAdd(
                list,
                UIId.BattleSystem,
                UILayer.Panel,
                "BattleHUD/UI_BattleSystem_Panel",
                typeof(UIBattleSystemPanel),
                attachViews,
                stretchToParent: false,
                closesOtherPanels: false);
            TryAdd(
                list,
                UIId.BattleCombat,
                UILayer.Panel,
                "BattleHUD/UI_BattleCombat_Panel",
                typeof(UIBattleCombatPanel),
                attachViews,
                stretchToParent: false,
                closesOtherPanels: false);
            TryAdd(
                list,
                UIId.BattleVitals,
                UILayer.Panel,
                "BattleHUD/UI_BattleVitals_Panel",
                typeof(UIBattleVitalsPanel),
                attachViews,
                stretchToParent: false,
                closesOtherPanels: false);
            TryAdd(
                list,
                UIId.BattleTask,
                UILayer.Panel,
                "BattleHUD/UI_Task_Panel",
                typeof(UITaskPanel),
                attachViews,
                stretchToParent: false,
                closesOtherPanels: false);
            return list;
        }

        static void TryAdd(
            List<UIPrefabEntry> list,
            UIId id,
            UILayer layer,
            string prefabName,
            System.Type viewType,
            bool attachViews,
            bool stretchToParent = true,
            bool closesOtherPanels = true)
        {
            string path = $"{PrefabFolder}/{prefabName}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"[UIRoot] 未找到 Prefab：{path}");
                return;
            }

            if (attachViews)
            {
                AttachOne(prefabName, viewType);
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }

            list.Add(new UIPrefabEntry
            {
                id = id,
                layer = layer,
                prefab = prefab,
                singleton = true,
                stretchToParent = stretchToParent,
                closesOtherPanels = closesOtherPanels
            });
        }
    }
}
