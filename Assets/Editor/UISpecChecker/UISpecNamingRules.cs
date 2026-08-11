using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AttackSkill.Editor.UISpecChecker
{
    /// <summary>
    /// UI 节点命名规范：前缀 + 名称驼峰（如 btnAdd、imgIcon、txtTitle）。
    /// </summary>
    public static class UISpecNamingRules
    {
        private static readonly Regex CamelAfterPrefix =
            new Regex(@"^[a-z]+[A-Z][A-Za-z0-9]*$", RegexOptions.Compiled);

        private static readonly Regex ValidName =
            new Regex(@"^[a-z][a-zA-Z0-9]*$", RegexOptions.Compiled);

        /// <summary>
        /// 组件类型 -> 允许的命名前缀（按优先级）。
        /// </summary>
        private static readonly Dictionary<Type, string[]> TypePrefixes =
            new Dictionary<Type, string[]>
            {
                { typeof(Button), new[] { "btn" } },
                { typeof(Toggle), new[] { "tog" } },
                { typeof(Slider), new[] { "sld" } },
                { typeof(Scrollbar), new[] { "scb" } },
                { typeof(ScrollRect), new[] { "scr" } },
                { typeof(Dropdown), new[] { "ddl", "dro" } },
                { typeof(TMP_Dropdown), new[] { "ddl", "dro" } },
                { typeof(InputField), new[] { "inp", "input" } },
                { typeof(TMP_InputField), new[] { "inp", "input" } },
                { typeof(Text), new[] { "txt", "lbl" } },
                { typeof(TextMeshProUGUI), new[] { "txt", "lbl" } },
                { typeof(TMP_Text), new[] { "txt", "lbl" } },
                { typeof(RawImage), new[] { "rimg", "raw" } },
                { typeof(Image), new[] { "img" } },
                { typeof(Mask), new[] { "mask" } },
                { typeof(RectMask2D), new[] { "mask" } },
                { typeof(CanvasGroup), new[] { "cg" } },
                { typeof(HorizontalLayoutGroup), new[] { "hlg" } },
                { typeof(VerticalLayoutGroup), new[] { "vlg" } },
                { typeof(GridLayoutGroup), new[] { "grid" } },
                { typeof(ContentSizeFitter), new[] { "csf" } },
                { typeof(LayoutElement), new[] { "le" } },
            };

        /// <summary>
        /// 根据节点上的关键组件，推导期望前缀。
        /// </summary>
        public static bool TryGetExpectedPrefixes(GameObject go, out string[] prefixes, out string componentHint)
        {
            prefixes = null;
            componentHint = null;
            if (go == null)
            {
                return false;
            }

            // 按业务语义优先级匹配，避免 Image+Button 同时存在时误报 img
            Type[] priority =
            {
                typeof(Button),
                typeof(Toggle),
                typeof(Slider),
                typeof(Scrollbar),
                typeof(ScrollRect),
                typeof(Dropdown),
                typeof(TMP_Dropdown),
                typeof(InputField),
                typeof(TMP_InputField),
                typeof(TextMeshProUGUI),
                typeof(TMP_Text),
                typeof(Text),
                typeof(RawImage),
                typeof(Image),
                typeof(Mask),
                typeof(RectMask2D),
                typeof(CanvasGroup),
                typeof(HorizontalLayoutGroup),
                typeof(VerticalLayoutGroup),
                typeof(GridLayoutGroup),
            };

            foreach (var type in priority)
            {
                if (go.GetComponent(type) == null)
                {
                    continue;
                }

                if (!TypePrefixes.TryGetValue(type, out prefixes))
                {
                    continue;
                }

                componentHint = type.Name;
                return true;
            }

            return false;
        }

        public static bool IsNameValid(string objectName, string[] expectedPrefixes, out string detail)
        {
            detail = null;
            if (string.IsNullOrEmpty(objectName))
            {
                detail = "名称为空";
                return false;
            }

            if (!ValidName.IsMatch(objectName))
            {
                detail = "应使用小写开头的驼峰命名（仅字母数字），例如 btnAdd、imgIcon";
                return false;
            }

            foreach (var prefix in expectedPrefixes)
            {
                if (!objectName.StartsWith(prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                if (objectName.Length == prefix.Length)
                {
                    detail = $"前缀「{prefix}」后需跟名称，例如 {prefix}Add";
                    return false;
                }

                // 前缀后第一个字符应为大写，形成驼峰：btnAdd
                char next = objectName[prefix.Length];
                if (!char.IsUpper(next))
                {
                    detail = $"前缀「{prefix}」后应为大写字母开头的驼峰名称，例如 {prefix}{char.ToUpper(next)}{objectName.Substring(prefix.Length + 1)}";
                    return false;
                }

                if (!CamelAfterPrefix.IsMatch(objectName) && !StartsWithPrefixAndCamel(objectName, prefix))
                {
                    detail = $"命名不符合「前缀+驼峰」规范，期望类似 {prefix}Name";
                    return false;
                }

                return true;
            }

            detail = $"期望前缀：{string.Join(" / ", expectedPrefixes)}（例如 {expectedPrefixes[0]}Name）";
            return false;
        }

        private static bool StartsWithPrefixAndCamel(string name, string prefix)
        {
            if (name.Length <= prefix.Length)
            {
                return false;
            }

            if (!name.StartsWith(prefix, StringComparison.Ordinal))
            {
                return false;
            }

            return char.IsUpper(name[prefix.Length]);
        }
    }
}
