using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using AttackSkill.Core;
using AttackSkill.Localization;

namespace AttackSkill.UI
{
    /// <summary>
    /// 设置列表：手动纵向排布；语言可改；性别在 OpenScene 可改，进游戏后只读。
    /// </summary>
    public partial class UISettingDialog : UIBase
    {
        const float SectionSpacing = 10f;

        static readonly GameLocale[] DropdownLocales = LocalizationService.ActiveLocales;

        RectTransform _content;
        bool _laidOut;
        bool _suppressLocaleCallback;
        bool _suppressGenderCallback;

        public override void OnOpen(object args)
        {
            BindClose(btnExit);
            BindClose(btnClose);

            _content = srlSetting != null ? srlSetting.content : null;

            RebuildContentHeight(force: true);
            BindJump(btnSound, palSound);
            BindJump(btnPortrait, palPortrait);
            BindJump(btnLanguage, palLanguage);
            BindJump(btnGender, palGender);
            BindLanguageDropdown();
            BindGenderDropdown();
        }

        public override void OnClose()
        {
            if (dropTextSelection != null)
            {
                dropTextSelection.onValueChanged.RemoveListener(OnLanguageDropdownChanged);
            }

            if (dropGenderSelection != null)
            {
                dropGenderSelection.onValueChanged.RemoveListener(OnGenderDropdownChanged);
            }
        }

        void BindLanguageDropdown()
        {
            if (dropTextSelection == null)
            {
                Debug.LogWarning("[UISetting] dropTextSelection 未绑定。", this);
                return;
            }

            LocalizationService.EnsureInitialized();
            dropTextSelection.onValueChanged.RemoveListener(OnLanguageDropdownChanged);

            dropTextSelection.ClearOptions();
            var options = new System.Collections.Generic.List<Dropdown.OptionData>(DropdownLocales.Length);
            for (int i = 0; i < DropdownLocales.Length; i++)
            {
                options.Add(new Dropdown.OptionData(LocaleOptionLabel(DropdownLocales[i])));
            }

            dropTextSelection.AddOptions(options);

            _suppressLocaleCallback = true;
            dropTextSelection.value = LocaleToIndex(LocalizationService.CurrentLocale);
            dropTextSelection.RefreshShownValue();
            _suppressLocaleCallback = false;

            dropTextSelection.onValueChanged.AddListener(OnLanguageDropdownChanged);
        }

        void OnLanguageDropdownChanged(int index)
        {
            if (_suppressLocaleCallback)
            {
                return;
            }

            if (index < 0 || index >= DropdownLocales.Length)
            {
                return;
            }

            LocalizationService.SetLocale(DropdownLocales[index]);
            RefreshDropdownLabels();
            UIManager.Instance?.ShowTip(LocalizationService.LocaleDisplayName(DropdownLocales[index]));
        }

        void BindGenderDropdown()
        {
            if (dropGenderSelection == null)
            {
                Debug.LogWarning("[UISetting] dropGenderSelection 未绑定。", this);
                return;
            }

            dropGenderSelection.onValueChanged.RemoveListener(OnGenderDropdownChanged);

            bool editable = IsGenderEditableInCurrentContext();
            if (editable && LocalAccountStore.IsGenderLocked)
            {
                LocalAccountStore.UnlockGender();
            }

            OpenSceneGender gender = LocalAccountStore.HasGender
                ? LocalAccountStore.Gender
                : OpenSceneGender.Female;

            _suppressGenderCallback = true;
            SetGenderDropdownValue(dropGenderSelection, gender);
            _suppressGenderCallback = false;

            dropGenderSelection.interactable = editable;
            if (editable)
            {
                dropGenderSelection.onValueChanged.AddListener(OnGenderDropdownChanged);
            }
        }

        void OnGenderDropdownChanged(int index)
        {
            if (_suppressGenderCallback)
            {
                return;
            }

            OpenSceneGender gender = GenderUi.FromDropdownIndex(index);
            if (!LocalAccountStore.SaveGender(gender))
            {
                return;
            }

            GameServices.OpenSceneFlow?.SetSelectedGender(gender);
            UIManager.Instance?.ShowTip(
                LocalizationService.Get(
                    LocalizationTableType.UI,
                    gender == OpenSceneGender.Male ? "male" : "female"));
        }

        /// <summary>OpenScene（有 Flow）可改；GameScene 等局内只读。</summary>
        static bool IsGenderEditableInCurrentContext()
        {
            if (GameServices.OpenSceneFlow != null)
            {
                return true;
            }

            string scene = SceneManager.GetActiveScene().name;
            return !string.IsNullOrEmpty(scene) &&
                   scene.IndexOf("OpenScene", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        void RefreshDropdownLabels()
        {
            if (dropTextSelection != null)
            {
                for (int i = 0; i < dropTextSelection.options.Count && i < DropdownLocales.Length; i++)
                {
                    dropTextSelection.options[i].text = LocaleOptionLabel(DropdownLocales[i]);
                }

                dropTextSelection.RefreshShownValue();
            }

            // 性别下拉也跟语言刷新（key: male / female）
            if (dropGenderSelection != null)
            {
                int keep = dropGenderSelection.value;
                EnsureGenderDropdownOptions(dropGenderSelection);
                dropGenderSelection.value = keep;
                dropGenderSelection.RefreshShownValue();
            }
        }

        static string LocaleOptionLabel(GameLocale locale)
        {
            return LocalizationService.LocaleDisplayName(locale);
        }

        static int LocaleToIndex(GameLocale locale)
        {
            for (int i = 0; i < DropdownLocales.Length; i++)
            {
                if (DropdownLocales[i] == locale)
                {
                    return i;
                }
            }

            return 0;
        }

        void RebuildContentHeight(bool force)
        {
            if (item == null || _content == null)
            {
                return;
            }

            if (_laidOut && !force)
            {
                return;
            }

            StripRuntimeLayout(item);
            StripRuntimeLayout(_content);

            float y = 0f;
            for (int i = 0; i < item.childCount; i++)
            {
                var child = item.GetChild(i) as RectTransform;
                if (child == null || !child.gameObject.activeSelf)
                {
                    continue;
                }

                float h = child.sizeDelta.y;
                if (h < 1f)
                {
                    h = child.rect.height;
                }

                if (h < 1f)
                {
                    h = 50f;
                }

                child.anchorMin = new Vector2(0f, 1f);
                child.anchorMax = new Vector2(1f, 1f);
                child.pivot = new Vector2(0.5f, 1f);
                child.anchoredPosition = new Vector2(0f, -y);
                child.sizeDelta = new Vector2(0f, h);
                y += h + SectionSpacing;
            }

            float total = Mathf.Max(100f, y - SectionSpacing);
            item.anchorMin = new Vector2(0f, 1f);
            item.anchorMax = new Vector2(1f, 1f);
            item.pivot = new Vector2(0.5f, 1f);
            item.anchoredPosition = Vector2.zero;
            item.sizeDelta = new Vector2(0f, total);

            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.anchoredPosition = Vector2.zero;
            _content.sizeDelta = new Vector2(0f, total);

            Canvas.ForceUpdateCanvases();
            if (srlSetting != null)
            {
                srlSetting.verticalNormalizedPosition = 1f;
            }

            _laidOut = true;
        }

        static void StripRuntimeLayout(RectTransform rt)
        {
            if (rt == null)
            {
                return;
            }

            var vlg = rt.GetComponent<VerticalLayoutGroup>();
            if (vlg != null)
            {
                Object.Destroy(vlg);
            }

            var csf = rt.GetComponent<ContentSizeFitter>();
            if (csf != null)
            {
                Object.Destroy(csf);
            }
        }

        void BindJump(Button btn, RectTransform section)
        {
            BindClick(btn, () =>
            {
                RebuildContentHeight(force: false);
                UIScrollHelper.ScrollToChild(srlSetting, section);
            });
        }
    }
}
