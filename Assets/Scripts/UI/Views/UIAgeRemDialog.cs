using UnityEngine;
using UnityEngine.UI;
using AttackSkill.Localization;

namespace AttackSkill.UI
{
    /// <summary>适龄提醒：修复 Viewport 后按文案高度撑开 Content，保证 txtTip 可见可滚。</summary>
    public partial class UIAgeRemDialog : UIBase
    {
        public override void OnOpen(object args)
        {
            BindClose(btnExit);
            BindClose(btnSure);
            EnsureViewportFilled();
            Canvas.ForceUpdateCanvases();
            RefreshScrollContent();
        }

        void EnsureViewportFilled()
        {
            if (viewport == null)
            {
                return;
            }

            bool degenerate =
                Mathf.Approximately(viewport.anchorMin.x, viewport.anchorMax.x) &&
                Mathf.Approximately(viewport.anchorMin.y, viewport.anchorMax.y) &&
                viewport.rect.width < 1f;

            if (!degenerate && viewport.rect.width >= 1f)
            {
                return;
            }

            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.pivot = new Vector2(0.5f, 0.5f);
            viewport.anchoredPosition = new Vector2(-12.5f, 0f);
            viewport.sizeDelta = new Vector2(-25f, 0f);
        }

        void RefreshScrollContent()
        {
            if (txtTip == null || content == null || item == null)
            {
                return;
            }

            var loc = txtTip.GetComponent<LocalizedText>();
            loc?.Apply();

            if (string.IsNullOrEmpty(txtTip.text))
            {
                Debug.LogWarning("[UIAgeRem] txtTip 文案为空，检查 LocalizedText key/表类型。", txtTip);
            }

            txtTip.horizontalOverflow = HorizontalWrapMode.Wrap;
            txtTip.verticalOverflow = VerticalWrapMode.Overflow;
            txtTip.color = new Color(0f, 0f, 0f, 1f);

            Canvas.ForceUpdateCanvases();

            float width = viewport != null ? viewport.rect.width : 0f;
            if (width < 10f)
            {
                width = content.rect.width;
            }

            if (width < 10f)
            {
                width = 1400f;
            }

            var tipRt = txtTip.rectTransform;
            tipRt.anchorMin = new Vector2(0f, 1f);
            tipRt.anchorMax = new Vector2(1f, 1f);
            tipRt.pivot = new Vector2(0.5f, 1f);
            tipRt.offsetMin = new Vector2(0f, tipRt.offsetMin.y);
            tipRt.offsetMax = new Vector2(0f, tipRt.offsetMax.y);

            float height = Mathf.Max(100f, UIScrollHelper.PreferredTextHeight(txtTip, width));
            tipRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

            item.anchorMin = new Vector2(0f, 1f);
            item.anchorMax = new Vector2(1f, 1f);
            item.pivot = new Vector2(0.5f, 1f);
            item.anchoredPosition = Vector2.zero;
            item.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

            if (scrollView != null)
            {
                scrollView.horizontal = false;
                scrollView.vertical = true;
                scrollView.verticalNormalizedPosition = 1f;
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }
    }
}
