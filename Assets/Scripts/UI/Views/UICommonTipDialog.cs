using UnityEngine;
using UnityEngine.UI;

namespace AttackSkill.UI
{
    /// <summary>Tip：imgBg 宽度 = 文本优选宽度 + 60。</summary>
    public partial class UICommonTipDialog : UIBase
    {
        const float ExtraWidth = 60f;

        RectTransform _imgBgRt;
        RectTransform _root;
        float _hideAt;

        public override void OnOpen(object args)
        {
            _root = transform as RectTransform;
            _imgBgRt = imgBg != null ? imgBg.rectTransform : null;

            var tipArgs = args as CommonTipArgs;
            string text = tipArgs?.tip ?? args as string ?? string.Empty;
            if (txtTip != null)
            {
                txtTip.text = text;
                txtTip.horizontalOverflow = HorizontalWrapMode.Overflow;
                txtTip.verticalOverflow = VerticalWrapMode.Overflow;
                txtTip.alignment = TextAnchor.MiddleCenter;
            }

            FitBackgroundToText();

            float duration = tipArgs != null ? Mathf.Max(0.2f, tipArgs.duration) : 1.5f;
            _hideAt = Time.unscaledTime + duration;
        }

        void FitBackgroundToText()
        {
            if (txtTip == null)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            float textW = Mathf.Max(40f, UIScrollHelper.PreferredTextWidth(txtTip));
            float textH = Mathf.Max(txtTip.rectTransform.sizeDelta.y, txtTip.preferredHeight);
            float bgW = textW + ExtraWidth;

            txtTip.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, textW);
            if (_imgBgRt != null)
            {
                _imgBgRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, bgW);
                if (_imgBgRt.sizeDelta.y < textH)
                {
                    _imgBgRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, textH);
                }
            }

            if (_root != null)
            {
                _root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(_root.sizeDelta.x, bgW));
            }
        }

        void Update()
        {
            if (!IsOpen)
            {
                return;
            }

            if (Time.unscaledTime >= _hideAt)
            {
                UIManager.Instance?.Close(UIId.CommonTip);
            }
        }
    }
}
