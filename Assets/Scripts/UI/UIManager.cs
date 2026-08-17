using System.Collections.Generic;
using UnityEngine;
using AttackSkill.Core;

namespace AttackSkill.UI
{
    /// <summary>
    /// 单 Canvas 分层 UI：Panel 互斥，Dialog 可叠，Tip 独立层。
    /// </summary>
    [DefaultExecutionOrder(-150)]
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("Layers")]
        [SerializeField] RectTransform layerPanel;
        [SerializeField] RectTransform layerDialog;
        [SerializeField] RectTransform layerTip;

        [Header("Prefabs")]
        [SerializeField] List<UIPrefabEntry> entries = new List<UIPrefabEntry>();

        readonly Dictionary<UIId, UIPrefabEntry> _entryMap = new Dictionary<UIId, UIPrefabEntry>();
        readonly Dictionary<UIId, UIBase> _singletons = new Dictionary<UIId, UIBase>();
        readonly List<UIId> _openPanels = new List<UIId>();
        readonly List<UIId> _openDialogs = new List<UIId>();
        UIBase _tipInstance;

        public RectTransform LayerPanel => layerPanel;
        public RectTransform LayerDialog => layerDialog;
        public RectTransform LayerTip => layerTip;
        public IReadOnlyList<UIId> OpenPanels => _openPanels;
        public IReadOnlyList<UIId> OpenDialogs => _openDialogs;

        void Awake()
        {
            if (!SceneSingleton.ShouldKeep(this, Instance))
            {
                return;
            }

            Instance = this;
            // DDOL / EventSystem / 卸载 Flow 由 UIBootstrap 统一负责
            if (GetComponent<UIBootstrap>() == null)
            {
                gameObject.AddComponent<UIBootstrap>();
            }

            TryFillMissingEntriesInEditor();
            RebuildEntryMap();
        }

        /// <summary>编辑器 Play 时自动补齐新增界面 Prefab，避免场景未刷新条目。</summary>
        void TryFillMissingEntriesInEditor()
        {
#if UNITY_EDITOR
            EnsureEntry(UIId.ChangeScene, UILayer.Panel, "UI_ChangeScene_Panel");
            EnsureEntry(UIId.LogIn, UILayer.Dialog, "UI_LogIn_Dialog");
            EnsureEntry(UIId.ChooseGender, UILayer.Dialog, "UI_ChooseGender_Dialog");
            EnsureEntry(UIId.PauseMenu, UILayer.Dialog, "UI_PauseMenu_Dialog");
            EnsureEntry(UIId.SkillWheel, UILayer.Dialog, "BattleHUD/UI_SkillWheel_Dialog");
            EnsureEntry(UIId.SkillSelect, UILayer.Dialog, "BattleHUD/UI_SkillSelect_Panel");
            EnsureBattleHudEntries();
#endif
        }

        /// <summary>进游戏后打开战斗 HUD（编队 / 系统 / 战斗键）。</summary>
        public void OpenBattleHud()
        {
#if UNITY_EDITOR
            EnsureBattleHudEntries();
            RebuildEntryMap();
#endif
            Open(UIId.BattleParty);
            Open(UIId.BattleSystem);
            Open(UIId.BattleCombat);
            Open(UIId.BattleVitals);
        }

        /// <summary>兼容旧调用：打开完整战斗 HUD。</summary>
        public UIBase OpenBattlePartyHud()
        {
            OpenBattleHud();
            return _singletons.TryGetValue(UIId.BattleParty, out UIBase ui) ? ui : null;
        }

        /// <summary>无资源 Prefab 时注册运行时暂停菜单模板。</summary>
        public void EnsurePauseMenuRegistered()
        {
            if (_entryMap.TryGetValue(UIId.PauseMenu, out UIPrefabEntry existing) && existing != null && existing.prefab != null)
            {
                return;
            }

            if (entries == null)
            {
                entries = new List<UIPrefabEntry>();
            }

            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && entries[i].id == UIId.PauseMenu && entries[i].prefab != null)
                {
                    RebuildEntryMap();
                    return;
                }
            }

            var template = UIPauseMenuDialog.CreateRuntimeTemplate();
            template.hideFlags = HideFlags.HideAndDontSave;
            entries.Add(new UIPrefabEntry
            {
                id = UIId.PauseMenu,
                layer = UILayer.Dialog,
                prefab = template,
                singleton = true
            });
            RebuildEntryMap();
        }

#if UNITY_EDITOR
        void EnsureBattleHudEntries()
        {
            EnsureEntry(
                UIId.BattleParty,
                UILayer.Panel,
                "BattleHUD/UI_BattleParty_Panel",
                stretchToParent: false,
                closesOtherPanels: false);
            EnsureEntry(
                UIId.BattleSystem,
                UILayer.Panel,
                "BattleHUD/UI_BattleSystem_Panel",
                stretchToParent: false,
                closesOtherPanels: false);
            EnsureEntry(
                UIId.BattleCombat,
                UILayer.Panel,
                "BattleHUD/UI_BattleCombat_Panel",
                stretchToParent: false,
                closesOtherPanels: false);
            EnsureEntry(
                UIId.BattleVitals,
                UILayer.Panel,
                "BattleHUD/UI_BattleVitals_Panel",
                stretchToParent: false,
                closesOtherPanels: false);
        }

        void EnsureEntry(
            UIId id,
            UILayer layer,
            string prefabName,
            bool stretchToParent = true,
            bool closesOtherPanels = true)
        {
            if (entries == null)
            {
                entries = new List<UIPrefabEntry>();
            }

            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && entries[i].id == id && entries[i].prefab != null)
                {
                    entries[i].stretchToParent = stretchToParent;
                    entries[i].closesOtherPanels = closesOtherPanels;
                    return;
                }
            }

            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                $"Assets/Prefabs/UI/{prefabName}.prefab");
            if (prefab == null)
            {
                Debug.LogWarning($"[UIManager] 未找到 Prefab：{prefabName}");
                return;
            }

            entries.Add(new UIPrefabEntry
            {
                id = id,
                layer = layer,
                prefab = prefab,
                singleton = true,
                stretchToParent = stretchToParent,
                closesOtherPanels = closesOtherPanels
            });
        }
#endif

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void SetLayers(RectTransform panel, RectTransform dialog, RectTransform tip)
        {
            layerPanel = panel;
            layerDialog = dialog;
            layerTip = tip;
        }

        public void SetEntries(List<UIPrefabEntry> list)
        {
            entries = list ?? new List<UIPrefabEntry>();
            RebuildEntryMap();
        }

        void RebuildEntryMap()
        {
            _entryMap.Clear();
            if (entries == null)
            {
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e == null || e.id == UIId.None || e.prefab == null)
                {
                    continue;
                }

                _entryMap[e.id] = e;
            }
        }

        public bool IsOpen(UIId id)
        {
            if (id == UIId.CommonTip)
            {
                return _tipInstance != null && _tipInstance.IsOpen;
            }

            if (_singletons.TryGetValue(id, out UIBase ui))
            {
                return ui != null && ui.IsOpen;
            }

            return _openPanels.Contains(id) || _openDialogs.Contains(id);
        }

        /// <summary>取已注册 Prefab 资源（不实例化），供读档解析轮盘图标等。</summary>
        public bool TryGetPrefab(UIId id, out GameObject prefab)
        {
            prefab = null;
            if (_entryMap.TryGetValue(id, out UIPrefabEntry entry) && entry != null && entry.prefab != null)
            {
                prefab = entry.prefab;
                return true;
            }

            return false;
        }

        public UIBase Open(UIId id, object args = null)
        {
            if (!_entryMap.TryGetValue(id, out UIPrefabEntry entry) || entry.prefab == null)
            {
                Debug.LogError($"[UIManager] 未注册 Prefab：{id}");
                return null;
            }

            switch (entry.layer)
            {
                case UILayer.Panel:
                    return OpenPanelInternal(entry, args);
                case UILayer.Tip:
                    return OpenTipInternal(entry, args);
                default:
                    return OpenDialogInternal(entry, args);
            }
        }

        public UIBase OpenPanel(UIId id, object args = null) => Open(id, args);

        public UIBase OpenDialog(UIId id, object args = null) => Open(id, args);

        public void OpenSure(string title, string tip, System.Action onSure, System.Action onCancel = null)
        {
            Open(UIId.CommonSure, new CommonSureArgs
            {
                title = title,
                tip = tip,
                onSure = onSure,
                onCancel = onCancel
            });
        }

        public void ShowTip(string tip, float duration = 1.5f)
        {
            Open(UIId.CommonTip, new CommonTipArgs { tip = tip, duration = duration });
        }

        public void Close(UIId id)
        {
            if (id == UIId.CommonTip)
            {
                if (_tipInstance != null)
                {
                    CloseInstance(_tipInstance);
                }

                return;
            }

            if (_singletons.TryGetValue(id, out UIBase ui) && ui != null)
            {
                CloseInstance(ui);
            }
        }

        public void CloseTopDialog()
        {
            if (_openDialogs.Count == 0)
            {
                return;
            }

            Close(_openDialogs[_openDialogs.Count - 1]);
        }

        public void CloseAllPanels()
        {
            for (int i = _openPanels.Count - 1; i >= 0; i--)
            {
                Close(_openPanels[i]);
            }
        }

        public void CloseAllDialogs()
        {
            for (int i = _openDialogs.Count - 1; i >= 0; i--)
            {
                Close(_openDialogs[i]);
            }

            if (_tipInstance != null && _tipInstance.IsOpen)
            {
                CloseInstance(_tipInstance);
            }
        }

        public void CloseAll()
        {
            CloseAllDialogs();
            CloseAllPanels();
        }

        UIBase OpenPanelInternal(UIPrefabEntry entry, object args)
        {
            // 全屏 Panel 互斥；战斗 HUD 等可设 closesOtherPanels=false 并存
            if (entry.closesOtherPanels)
            {
                for (int i = _openPanels.Count - 1; i >= 0; i--)
                {
                    if (_openPanels[i] != entry.id)
                    {
                        Close(_openPanels[i]);
                    }
                }
            }

            var ui = GetOrCreate(entry, layerPanel);
            if (ui == null)
            {
                return null;
            }

            ShowInstance(ui, args);
            if (!_openPanels.Contains(entry.id))
            {
                _openPanels.Add(entry.id);
            }

            return ui;
        }

        UIBase OpenDialogInternal(UIPrefabEntry entry, object args)
        {
            var ui = GetOrCreate(entry, layerDialog);
            if (ui == null)
            {
                return null;
            }

            ShowInstance(ui, args);
            ui.transform.SetAsLastSibling();
            _openDialogs.Remove(entry.id);
            _openDialogs.Add(entry.id);
            return ui;
        }

        UIBase OpenTipInternal(UIPrefabEntry entry, object args)
        {
            var ui = GetOrCreate(entry, layerTip);
            if (ui == null)
            {
                return null;
            }

            _tipInstance = ui;
            ShowInstance(ui, args);
            ui.transform.SetAsLastSibling();
            return ui;
        }

        UIBase GetOrCreate(UIPrefabEntry entry, RectTransform parent)
        {
            if (parent == null)
            {
                Debug.LogError("[UIManager] Layer 未绑定。");
                return null;
            }

            if (entry.singleton && _singletons.TryGetValue(entry.id, out UIBase cached) && cached != null)
            {
                return cached;
            }

            var go = Instantiate(entry.prefab, parent, false);
            go.name = entry.prefab.name;
            if (entry.stretchToParent)
            {
                StretchFull(go.transform as RectTransform);
            }

            System.Type expectedType = ResolveFallbackType(entry.id);
            var ui = go.GetComponent(expectedType) as UIBase;
            if (ui == null)
            {
                Debug.LogError(
                    $"[UIManager] Prefab「{entry.prefab.name}」缺少期望脚本 {expectedType.Name}。" +
                    "请用「工具/UI/挂接 View 脚本」修复 Prefab，禁止运行时拆脚本重挂。",
                    entry.prefab);
                Destroy(go);
                return null;
            }

            ui.BindMeta(entry.id, entry.layer);
            if (entry.singleton)
            {
                _singletons[entry.id] = ui;
            }

            return ui;
        }

        static System.Type ResolveFallbackType(UIId id)
        {
            switch (id)
            {
                case UIId.OpenScene1: return typeof(UIOpenScene1Panel);
                case UIId.OpenScene2: return typeof(UIOpenScene2Panel);
                case UIId.OpenScene3: return typeof(UIOpenScene3Panel);
                case UIId.OpenScene4: return typeof(UIOpenScene4Panel);
                case UIId.ChangeScene: return typeof(UIChangeScenePanel);
                case UIId.AgeRem: return typeof(UIAgeRemDialog);
                case UIId.Setting: return typeof(UISettingDialog);
                case UIId.Tools: return typeof(UIToolsDialog);
                case UIId.JoinGame: return typeof(UIJoinGameDialog);
                case UIId.CommonSure: return typeof(UICommonSureDialog);
                case UIId.CommonTip: return typeof(UICommonTipDialog);
                case UIId.LogIn: return typeof(UILogInDialog);
                case UIId.ChooseGender: return typeof(UIChooseGenderDialog);
                case UIId.PauseMenu: return typeof(UIPauseMenuDialog);
                case UIId.SkillWheel: return typeof(UISkillWheelDialog);
                case UIId.SkillSelect: return typeof(UISkillSelectPanel);
                case UIId.BattleParty: return typeof(UIBattlePartyPanel);
                case UIId.BattleSystem: return typeof(UIBattleSystemPanel);
                case UIId.BattleCombat: return typeof(UIBattleCombatPanel);
                case UIId.BattleVitals: return typeof(UIBattleVitalsPanel);
                default: return typeof(UIGenericView);
            }
        }

        void ShowInstance(UIBase ui, object args)
        {
            if (!ui.gameObject.activeSelf)
            {
                ui.gameObject.SetActive(true);
            }

            ui.OnOpen(args);
        }

        void CloseInstance(UIBase ui)
        {
            if (ui == null)
            {
                return;
            }

            ui.OnClose();
            ui.gameObject.SetActive(false);

            _openPanels.Remove(ui.Id);
            _openDialogs.Remove(ui.Id);
        }

        static void StretchFull(RectTransform rt)
        {
            if (rt == null)
            {
                return;
            }

            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
        }
    }
}
