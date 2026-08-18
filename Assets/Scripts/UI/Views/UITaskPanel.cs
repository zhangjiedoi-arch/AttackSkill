using AttackSkill.Enemy;
using AttackSkill.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace AttackSkill.UI
{
    /// <summary>战斗 HUD 任务描述：区外营地 / 区内深渊，背景高度随文案适配。</summary>
    public partial class UITaskPanel : UIBase
    {
        const string CampKey = "task_camp";
        const string AbyssKey = "task_abyss";
        const float BgExtraHeight = 20f;

        bool _lastInRouge;
        bool _hasLast;

        public override void OnOpen(object args)
        {
            CacheBindings();
            RefreshTask(force: true);
        }

        void OnEnable()
        {
            LocalizationService.LocaleChanged -= OnLocaleChanged;
            LocalizationService.LocaleChanged += OnLocaleChanged;
            CacheBindings();
            RefreshTask(force: true);
        }

        void OnDisable()
        {
            LocalizationService.LocaleChanged -= OnLocaleChanged;
        }

        void Update()
        {
            RefreshTask(force: false);
        }

        void OnLocaleChanged(GameLocale _)
        {
            RefreshTask(force: true);
        }

        void RefreshTask(bool force)
        {
            bool inRouge = IsInRougeArea();
            if (!force && _hasLast && inRouge == _lastInRouge)
            {
                return;
            }

            _hasLast = true;
            _lastInRouge = inRouge;
            ApplyText(inRouge ? AbyssKey : CampKey);
        }

        static bool IsInRougeArea()
        {
            var flow = RouGeLikeFlowController.Instance;
            return flow != null && (flow.HasTeleported || flow.IsPlayerInArea);
        }

        void ApplyText(string key)
        {
            if (txtDesc == null)
            {
                return;
            }

            txtDesc.text = L(key);
            Canvas.ForceUpdateCanvases();
            FitBackgroundToText();
        }

        void FitBackgroundToText()
        {
            if (txtDesc == null)
            {
                return;
            }

            RectTransform textRt = txtDesc.rectTransform;
            float width = textRt.rect.width;
            if (width < 1f)
            {
                width = Mathf.Max(1f, textRt.sizeDelta.x);
            }

            TextGenerationSettings settings = txtDesc.GetGenerationSettings(new Vector2(width, 0f));
            float textH = txtDesc.cachedTextGeneratorForLayout.GetPreferredHeight(txtDesc.text, settings);
            if (txtDesc.pixelsPerUnit > 0.01f)
            {
                textH /= txtDesc.pixelsPerUnit;
            }

            textH = Mathf.Max(txtDesc.fontSize, textH);
            textRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, textH);

            float bgH = textH + BgExtraHeight;
            if (imgBg != null)
            {
                imgBg.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bgH);
            }

            var root = transform as RectTransform;
            if (root != null)
            {
                root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bgH);
            }
        }

        void CacheBindings()
        {
            if (imgBg == null)
            {
                imgBg = FindChildComponent<Image>("imgBg");
            }

            if (txtDesc == null)
            {
                txtDesc = FindChildComponent<Text>("txtDesc");
            }
        }

        T FindChildComponent<T>(string nodeName) where T : Component
        {
            var all = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == nodeName)
                {
                    return all[i].GetComponent<T>();
                }
            }

            return null;
        }
    }
}
