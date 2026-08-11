using UnityEngine;
using UnityEngine.UI;

namespace AttackSkill.UI
{
    public static class UIScrollHelper
    {
        /// <summary>把 child 滚到 ScrollRect 视口顶部附近。</summary>
        public static void ScrollToChild(ScrollRect scroll, RectTransform child)
        {
            if (scroll == null || child == null || scroll.content == null)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);

            RectTransform content = scroll.content;
            RectTransform viewport = scroll.viewport != null
                ? scroll.viewport
                : scroll.transform as RectTransform;

            float contentH = content.rect.height;
            float viewH = viewport != null ? viewport.rect.height : 0f;
            float scrollable = contentH - viewH;
            if (scrollable <= 1f)
            {
                scroll.verticalNormalizedPosition = 1f;
                return;
            }

            // content 本地空间：顶部 yMax → 子项顶部
            Vector3 childTopWorld = child.TransformPoint(new Vector3(0f, child.rect.yMax, 0f));
            float childTopLocal = content.InverseTransformPoint(childTopWorld).y;
            float offsetFromTop = content.rect.yMax - childTopLocal;
            float normalized = 1f - Mathf.Clamp01(offsetFromTop / scrollable);
            scroll.verticalNormalizedPosition = normalized;
        }

        public static void EnsureVerticalList(RectTransform container, float spacing = 8f)
        {
            if (container == null)
            {
                return;
            }

            var layout = container.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
            {
                layout = container.gameObject.AddComponent<VerticalLayoutGroup>();
            }

            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(0, 0, 0, 0);

            var fitter = container.GetComponent<ContentSizeFitter>();
            if (fitter == null)
            {
                fitter = container.gameObject.AddComponent<ContentSizeFitter>();
            }

            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        public static float PreferredTextHeight(Text text, float width)
        {
            if (text == null)
            {
                return 0f;
            }

            TextGenerator generator = new TextGenerator();
            var settings = text.GetGenerationSettings(new Vector2(width, 0f));
            settings.horizontalOverflow = HorizontalWrapMode.Wrap;
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            float h = generator.GetPreferredHeight(text.text, settings);
            return Mathf.Ceil(h / text.pixelsPerUnit) + 2f;
        }

        public static float PreferredTextWidth(Text text)
        {
            if (text == null)
            {
                return 0f;
            }

            TextGenerator generator = new TextGenerator();
            var settings = text.GetGenerationSettings(Vector2.zero);
            settings.horizontalOverflow = HorizontalWrapMode.Overflow;
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            float w = generator.GetPreferredWidth(text.text, settings);
            return Mathf.Ceil(w / text.pixelsPerUnit);
        }
    }
}
