using AttackSkill.Core;
using AttackSkill.Localization;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AttackSkill.UI
{
    /// <summary>所有面板/弹窗根脚本基类。</summary>
    public abstract class UIBase : MonoBehaviour
    {
        public UIId Id { get; private set; }
        public UILayer Layer { get; private set; }
        public bool IsOpen => gameObject.activeSelf;

        internal void BindMeta(UIId id, UILayer layer)
        {
            Id = id;
            Layer = layer;
        }

        public virtual void OnOpen(object args) { }

        public virtual void OnClose() { }

        /// <summary>绑定按钮点击（先清空再挂）。</summary>
        protected static void BindClick(Button button, UnityAction action)
        {
            if (button == null || action == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        /// <summary>批量绑定同一点击回调。</summary>
        protected static void BindClick(Button[] buttons, UnityAction action)
        {
            if (buttons == null || action == null)
            {
                return;
            }

            for (int i = 0; i < buttons.Length; i++)
            {
                BindClick(buttons[i], action);
            }
        }

        /// <summary>点击后关闭当前界面。</summary>
        protected void BindClose(Button button)
        {
            BindClick(button, CloseSelf);
        }

        protected void BindClose(Button[] buttons)
        {
            BindClick(buttons, CloseSelf);
        }

        /// <summary>把子层级所有 Button 绑成关闭（占位弹窗用）。</summary>
        protected void BindCloseAllButtons()
        {
            BindClose(GetComponentsInChildren<Button>(true));
        }

        protected void CloseSelf()
        {
            UIManager.Instance?.Close(Id);
        }

        protected static OpenSceneFlowController FindOpenSceneFlow()
        {
            return GameServices.OpenSceneFlow;
        }

        protected static void SetInteractable(Selectable selectable, bool value)
        {
            if (selectable != null)
            {
                selectable.interactable = value;
            }
        }

        /// <summary>Slider + 百分比 Text 进度（0~1）。</summary>
        protected static void ApplyProgress01(Slider slider, Text percentLabel, float value01)
        {
            value01 = Mathf.Clamp01(value01);
            if (slider != null)
            {
                slider.value = value01;
            }

            if (percentLabel != null)
            {
                percentLabel.text = $"{Mathf.RoundToInt(value01 * 100f)}%";
            }
        }

        /// <summary>UI 表取文案。</summary>
        protected static string L(string key) =>
            LocalizationService.Get(LocalizationTableType.UI, key);

        /// <summary>UI 表 Format 拼接。</summary>
        protected static string LF(string key, params object[] args) =>
            LocalizationService.Format(LocalizationTableType.UI, key, args);

        /// <summary>Common 表取文案。</summary>
        protected static string LC(string key) =>
            LocalizationService.Get(LocalizationTableType.Common, key);

        /// <summary>Common 表 Format 拼接。</summary>
        protected static string LCF(string key, params object[] args) =>
            LocalizationService.Format(LocalizationTableType.Common, key, args);

        /// <summary>确保性别下拉有「男/女」两项（文案来自语言表 key：male / female）。</summary>
        protected static void EnsureGenderDropdownOptions(Dropdown dropdown)
        {
            if (dropdown == null)
            {
                return;
            }

            LocalizationService.EnsureInitialized();
            string male = L("male");
            string female = L("female");

            if (dropdown.options == null || dropdown.options.Count != 2)
            {
                dropdown.ClearOptions();
                dropdown.AddOptions(new System.Collections.Generic.List<string> { male, female });
            }
            else
            {
                dropdown.options[0].text = male;
                dropdown.options[1].text = female;
            }

            dropdown.RefreshShownValue();
        }

        protected static void SetGenderDropdownValue(Dropdown dropdown, OpenSceneGender gender)
        {
            if (dropdown == null)
            {
                return;
            }

            EnsureGenderDropdownOptions(dropdown);
            dropdown.value = GenderUi.ToDropdownIndex(gender);
            dropdown.RefreshShownValue();
        }

        /// <summary>
        /// 按名称查找子节点组件。新界面请用生成绑定字段，避免魔法字符串。
        /// </summary>
        [System.Obsolete("改用「工具/UI/生成绑定代码」产生的 SerializeField 绑定")]
        protected static T FindChild<T>(Transform root, string name) where T : Component
        {
            if (root == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            var all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name == name)
                {
                    return all[i].GetComponent<T>();
                }
            }

            return null;
        }

        [System.Obsolete("改用生成绑定字段")]
        protected static GameObject FindChildGo(Transform root, string name)
        {
#pragma warning disable CS0618
            var t = FindChild<Transform>(root, name);
#pragma warning restore CS0618
            return t != null ? t.gameObject : null;
        }
    }

    /// <summary>开场性别 ↔ 下拉索引（男=0，女=1）。</summary>
    public static class GenderUi
    {
        public static int ToDropdownIndex(OpenSceneGender gender)
        {
            return gender == OpenSceneGender.Male ? 0 : 1;
        }

        public static OpenSceneGender FromDropdownIndex(int index)
        {
            return index == 0 ? OpenSceneGender.Male : OpenSceneGender.Female;
        }
    }
}
