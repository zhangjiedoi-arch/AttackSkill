using System;
using UnityEngine;
using UnityEngine.UI;
using AttackSkill.Localization;

namespace AttackSkill.UI
{
    public class UIPauseMenuDialogArgs
    {
        public Action onContinue;
        public Action onOpenSettings;
        public Action onQuit;
    }

    /// <summary>
    /// 独立暂停菜单：继续 / 设置 / 退出。文案统一走 LocalizedText。
    /// </summary>
    public class UIPauseMenuDialog : UIBase
    {
        [Header("UI Bindings")]
        [SerializeField] Button btnContinue;
        [SerializeField] Button btnSettings;
        [SerializeField] Button btnQuit;
        [SerializeField] Text txtTitle;

        UIPauseMenuDialogArgs _args;
        bool _built;

        public override void OnOpen(object args)
        {
            EnsureBuilt();
            BindLocalizedTexts();
            _args = args as UIPauseMenuDialogArgs;

            BindClick(btnContinue, () =>
            {
                var cb = _args?.onContinue;
                CloseSelf();
                cb?.Invoke();
            });
            BindClick(btnSettings, () => _args?.onOpenSettings?.Invoke());
            BindClick(btnQuit, () =>
            {
                var cb = _args?.onQuit;
                if (cb != null)
                {
                    cb.Invoke();
                    return;
                }

                QuitGame();
            });
        }

        void BindLocalizedTexts()
        {
            LocalizationService.EnsureInitialized();
            LocalizedText.EnsureOn(txtTitle, "pause_menu_title");

            Text continueLabel = btnContinue != null ? btnContinue.GetComponentInChildren<Text>(true) : null;
            Text settingsLabel = btnSettings != null ? btnSettings.GetComponentInChildren<Text>(true) : null;
            Text quitLabel = btnQuit != null ? btnQuit.GetComponentInChildren<Text>(true) : null;

            LocalizedText.EnsureOn(continueLabel, "pause_continue");
            LocalizedText.EnsureOn(settingsLabel, "setting");
            LocalizedText.EnsureOn(quitLabel, "pause_quit_game");
        }

        /// <summary>供 UIManager 在无资源 Prefab 时注册运行时模板。</summary>
        public static GameObject CreateRuntimeTemplate()
        {
            var root = new GameObject("UI_PauseMenu_Dialog", typeof(RectTransform));
            var view = root.AddComponent<UIPauseMenuDialog>();
            view.EnsureBuilt();
            root.SetActive(false);
            return root;
        }

        void EnsureBuilt()
        {
            if (_built && btnContinue != null)
            {
                return;
            }

            if (btnContinue != null && btnSettings != null && btnQuit != null)
            {
                _built = true;
                return;
            }

            var rt = transform as RectTransform;
            if (rt != null)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            var dim = CreateChild("Dim", transform);
            Stretch(dim);
            var dimImg = dim.gameObject.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.55f);
            dimImg.raycastTarget = true;

            var panel = CreateChild("Panel", transform);
            var panelRt = panel;
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(420f, 360f);
            panelRt.anchoredPosition = Vector2.zero;
            var panelImg = panel.gameObject.AddComponent<Image>();
            panelImg.color = new Color(0.12f, 0.14f, 0.18f, 0.96f);

            txtTitle = CreateLabel(panel, "txtTitle", 36, new Vector2(0f, 120f), new Vector2(360f, 48f));
            btnContinue = CreateButton(panel, "btnContinue", new Vector2(0f, 40f));
            btnSettings = CreateButton(panel, "btnSettings", new Vector2(0f, -40f));
            btnQuit = CreateButton(panel, "btnQuit", new Vector2(0f, -120f));

            _built = true;
        }

        static RectTransform CreateChild(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static Text CreateLabel(Transform parent, string name, int size, Vector2 pos, Vector2 sizeDelta)
        {
            var rt = CreateChild(name, parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = sizeDelta;
            var label = rt.gameObject.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (label.font == null)
            {
                label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            label.text = string.Empty;
            label.fontSize = size;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;
            return label;
        }

        static Button CreateButton(Transform parent, string name, Vector2 pos)
        {
            var rt = CreateChild(name, parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(280f, 56f);

            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0.22f, 0.45f, 0.75f, 1f);

            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;

            CreateLabel(rt, "Label", 28, Vector2.zero, new Vector2(260f, 48f));
            return btn;
        }

        static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
