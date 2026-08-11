using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace AttackSkill.Editor.UISpecChecker
{
    /// <summary>
    /// UI 拼接规范检查核心逻辑。
    /// </summary>
    public static class UISpecCheckService
    {
        public class Options
        {
            public bool CheckSizeEven = true;
            public bool CheckNineSlice = true;
            public bool CheckMissing = true;
            public bool CheckNaming = true;

            /// <summary>九宫中心区域在某一方向上的平铺次数上限。</summary>
            public float MaxNineSliceTiles = 8f;

            /// <summary>宽高判定偶数时允许的浮点误差。</summary>
            public float SizeEpsilon = 0.01f;

            /// <summary>是否跳过纯布局空节点（无 Image/Text 等）的命名检查。</summary>
            public bool SkipUnprefixedEmptyNodes = true;
        }

        public static List<UISpecCheckIssue> CheckRoots(IEnumerable<GameObject> roots, Options options)
        {
            var issues = new List<UISpecCheckIssue>();
            if (roots == null || options == null)
            {
                return issues;
            }

            var visited = new HashSet<int>();
            foreach (var root in roots)
            {
                if (root == null)
                {
                    continue;
                }

                CheckHierarchy(root, root.name, options, issues, visited);
            }

            return issues;
        }

        private static void CheckHierarchy(
            GameObject go,
            string path,
            Options options,
            List<UISpecCheckIssue> issues,
            HashSet<int> visited)
        {
            int id = go.GetInstanceID();
            if (!visited.Add(id))
            {
                return;
            }

            if (options.CheckMissing)
            {
                CheckMissingOnGameObject(go, path, issues);
            }

            if (options.CheckSizeEven)
            {
                CheckEvenSize(go, path, options, issues);
            }

            if (options.CheckNineSlice)
            {
                CheckNineSliceTiling(go, path, options, issues);
            }

            if (options.CheckNaming)
            {
                CheckNaming(go, path, options, issues);
            }

            var t = go.transform;
            for (int i = 0; i < t.childCount; i++)
            {
                var child = t.GetChild(i).gameObject;
                CheckHierarchy(child, path + "/" + child.name, options, issues, visited);
            }
        }

        #region Size

        private static void CheckEvenSize(GameObject go, string path, Options options, List<UISpecCheckIssue> issues)
        {
            var rect = go.GetComponent<RectTransform>();
            if (rect == null)
            {
                return;
            }

            float w = rect.rect.width;
            float h = rect.rect.height;

            // 忽略接近 0 的节点（常见于未展开布局）
            if (Mathf.Abs(w) < options.SizeEpsilon && Mathf.Abs(h) < options.SizeEpsilon)
            {
                return;
            }

            bool widthOk = IsEvenDimension(w, options.SizeEpsilon, out int widthInt, out bool widthIntegral);
            bool heightOk = IsEvenDimension(h, options.SizeEpsilon, out int heightInt, out bool heightIntegral);

            if (widthOk && heightOk)
            {
                return;
            }

            var sb = new StringBuilder();
            sb.Append($"界面尺寸应为偶数。当前宽={FormatSize(w)}, 高={FormatSize(h)}。");
            if (!widthIntegral || !heightIntegral)
            {
                sb.Append(" 存在非整数尺寸，可能导致拼接缝隙。");
            }
            else
            {
                if (!widthOk)
                {
                    sb.Append($" 宽度 {widthInt} 为奇数。");
                }

                if (!heightOk)
                {
                    sb.Append($" 高度 {heightInt} 为奇数。");
                }
            }

            issues.Add(new UISpecCheckIssue(
                UISpecIssueSeverity.Warning,
                UISpecIssueCategory.SizeEven,
                path,
                sb.ToString(),
                go,
                rect));
        }

        private static bool IsEvenDimension(float value, float epsilon, out int rounded, out bool isIntegral)
        {
            rounded = Mathf.RoundToInt(value);
            isIntegral = Mathf.Abs(value - rounded) <= epsilon;
            if (!isIntegral)
            {
                return false;
            }

            return rounded % 2 == 0;
        }

        private static string FormatSize(float v)
        {
            return Mathf.Approximately(v, Mathf.Round(v)) ? ((int)Mathf.Round(v)).ToString() : v.ToString("0.###");
        }

        #endregion

        #region Nine Slice

        private static void CheckNineSliceTiling(GameObject go, string path, Options options, List<UISpecCheckIssue> issues)
        {
            var image = go.GetComponent<Image>();
            if (image == null || image.sprite == null)
            {
                return;
            }

            if (image.type != Image.Type.Sliced && image.type != Image.Type.Tiled)
            {
                return;
            }

            var rect = image.rectTransform;
            if (rect == null)
            {
                return;
            }

            Sprite sprite = image.sprite;
            Vector4 border = sprite.border; // x=left, y=bottom, z=right, w=top
            Vector2 spriteSize = sprite.rect.size;
            Vector2 uiSize = rect.rect.size;

            float centerW = spriteSize.x - border.x - border.z;
            float centerH = spriteSize.y - border.y - border.w;

            float tilesX = 1f;
            float tilesY = 1f;

            if (image.type == Image.Type.Sliced)
            {
                float innerW = Mathf.Max(0f, uiSize.x - border.x - border.z);
                float innerH = Mathf.Max(0f, uiSize.y - border.y - border.w);

                if (centerW > 0.01f)
                {
                    tilesX = innerW / centerW;
                }

                if (centerH > 0.01f)
                {
                    tilesY = innerH / centerH;
                }
            }
            else // Tiled
            {
                if (spriteSize.x > 0.01f)
                {
                    tilesX = uiSize.x / spriteSize.x;
                }

                if (spriteSize.y > 0.01f)
                {
                    tilesY = uiSize.y / spriteSize.y;
                }
            }

            float maxTiles = Mathf.Max(tilesX, tilesY);
            if (maxTiles <= options.MaxNineSliceTiles)
            {
                return;
            }

            string typeName = image.type == Image.Type.Sliced ? "九宫(Sliced)" : "平铺(Tiled)";
            issues.Add(new UISpecCheckIssue(
                UISpecIssueSeverity.Warning,
                UISpecIssueCategory.NineSliceTile,
                path,
                $"{typeName} 平铺次数过高：X≈{tilesX:0.##}，Y≈{tilesY:0.##}（上限 {options.MaxNineSliceTiles}）。" +
                $"精灵 {spriteSize.x:0}x{spriteSize.y:0}，边框 L{border.x:0} B{border.y:0} R{border.z:0} T{border.w:0}，" +
                $"UI {uiSize.x:0.##}x{uiSize.y:0.##}。建议加大九宫中心区域或改用更大底图，避免过度平铺影响性能。",
                go,
                image));
        }

        #endregion

        #region Missing

        private static void CheckMissingOnGameObject(GameObject go, string path, List<UISpecCheckIssue> issues)
        {
            int missingScriptCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
            if (missingScriptCount > 0)
            {
                issues.Add(new UISpecCheckIssue(
                    UISpecIssueSeverity.Error,
                    UISpecIssueCategory.Missing,
                    path,
                    $"存在 {missingScriptCount} 个 Missing Script。",
                    go));
            }

            var components = go.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                var comp = components[i];
                if (comp == null)
                {
                    // Missing Script 已在上方统计
                    continue;
                }

                CheckMissingReferences(comp, path, issues);
            }
        }

        private static void CheckMissingReferences(Component comp, string path, List<UISpecCheckIssue> issues)
        {
            var so = new SerializedObject(comp);
            var prop = so.GetIterator();
            bool enterChildren = true;
            while (prop.NextVisible(enterChildren))
            {
                enterChildren = true;
                if (prop.propertyType != SerializedPropertyType.ObjectReference)
                {
                    continue;
                }

                // objectReferenceValue 为 null，但 InstanceID 非 0，说明引用丢失
                if (prop.objectReferenceValue == null && prop.objectReferenceInstanceIDValue != 0)
                {
                    issues.Add(new UISpecCheckIssue(
                        UISpecIssueSeverity.Error,
                        UISpecIssueCategory.Missing,
                        path,
                        $"组件 {comp.GetType().Name} 字段「{GetNicePropertyPath(prop)}」引用丢失 (Missing)。",
                        comp.gameObject,
                        comp));
                }
            }
        }

        private static string GetNicePropertyPath(SerializedProperty prop)
        {
            string p = prop.propertyPath;
            if (string.IsNullOrEmpty(p))
            {
                return prop.displayName;
            }

            // 常见 UI 字段友好名
            if (p == "m_Sprite")
            {
                return "Sprite";
            }

            if (p == "m_Font" || p == "m_fontAsset")
            {
                return "Font";
            }

            if (p == "m_Material" || p == "m_SharedMaterial")
            {
                return "Material";
            }

            return prop.displayName;
        }

        #endregion

        #region Naming

        private static void CheckNaming(GameObject go, string path, Options options, List<UISpecCheckIssue> issues)
        {
            if (!UISpecNamingRules.TryGetExpectedPrefixes(go, out string[] prefixes, out string hint))
            {
                return;
            }

            if (options.SkipUnprefixedEmptyNodes && prefixes == null)
            {
                return;
            }

            if (UISpecNamingRules.IsNameValid(go.name, prefixes, out string detail))
            {
                return;
            }

            issues.Add(new UISpecCheckIssue(
                UISpecIssueSeverity.Warning,
                UISpecIssueCategory.Naming,
                path,
                $"命名不符合规范（{hint}）：{detail}。当前名称「{go.name}」。",
                go));
        }

        #endregion
    }
}
