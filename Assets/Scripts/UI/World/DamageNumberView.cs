using AttackSkill.Combat;
using TMPro;
using UnityEngine;

namespace AttackSkill.UI.World
{
    /// <summary>伤害跳字：世界 HitPoint → 屏幕投影；按元素着色，暴击橙黄且字号×2。</summary>
    [DefaultExecutionOrder(1000)]
    public sealed class DamageNumberView : MonoBehaviour
    {
        static readonly Color CritColor = new Color(1f, 0.72f, 0.12f, 1f);      // 橙黄
        static readonly Color LightColor = new Color(1f, 0.92f, 0.2f, 1f);     // 黄
        static readonly Color DarkColor = new Color(0.12f, 0.12f, 0.14f, 1f);   // 黑
        static readonly Color ThunderColor = new Color(0.72f, 0.35f, 1f, 1f);   // 紫
        static readonly Color IceColor = new Color(0.75f, 0.92f, 1f, 1f);       // 蓝白
        static readonly Color FireColor = new Color(1f, 0.28f, 0.18f, 1f);      // 红

        [SerializeField] TextMeshProUGUI txtNumber;
        [SerializeField] float worldRiseSpeed = 1.35f;
        [SerializeField] float fadeStartNormalized = 0.4f;
        [SerializeField] float normalFontSize = 36f;

        Color _color;
        float _baseScale = 1f;
        float _life;
        float _elapsed;
        Vector3 _worldPos;
        Transform _ignoreRoot;
        RectTransform _rt;
        WorldUiService _ui;
        CanvasGroup _group;
        bool _playing;
        System.Action<DamageNumberView> _onFinished;

        public bool IsPlaying => _playing;

        void Awake()
        {
            CacheRefs();
            if (txtNumber != null && normalFontSize < 1f)
            {
                normalFontSize = txtNumber.fontSize;
            }
        }

        public void Play(
            float amount,
            Vector3 worldPosition,
            float lifetime,
            WorldUiService ui,
            Transform ignoreRoot,
            System.Action<DamageNumberView> onFinished,
            bool isCritical = false,
            CombatElement element = CombatElement.Light)
        {
            CacheRefs();
            if (_group == null)
            {
                _group = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
            }

            _ui = ui;
            _ignoreRoot = ignoreRoot;
            if (ui != null)
            {
                transform.SetParent(ui.PoolRoot, false);
            }

            _rt = transform as RectTransform;
            WorldUiScreen.PrepareOverlayItem(gameObject);

            ResolveStyle(isCritical, element, out _color, out _baseScale, out float fontSize);
            int shown = Mathf.Max(1, Mathf.RoundToInt(amount));
            if (txtNumber != null)
            {
                txtNumber.text = shown.ToString();
                txtNumber.color = _color;
                txtNumber.fontSize = fontSize;
                txtNumber.ForceMeshUpdate();
            }

            _worldPos = worldPosition + new Vector3(
                Random.Range(-0.12f, 0.12f),
                0f,
                Random.Range(-0.08f, 0.08f));

            _life = Mathf.Max(0.2f, lifetime);
            _elapsed = 0f;
            _onFinished = onFinished;
            _playing = true;
            gameObject.SetActive(true);
            if (_group != null)
            {
                _group.alpha = 1f;
            }

            Camera cam = WorldUiScreen.ResolveRenderCamera(ui != null ? ui.WorldCamera : null);
            WorldUiScreen.TrySetOverlayFromWorld(_rt, cam, _worldPos, _baseScale);
        }

        public void StopAndHide()
        {
            _playing = false;
            _onFinished = null;
            gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (!_playing)
            {
                return;
            }

            float dt = Time.deltaTime;
            _elapsed += dt;
            _worldPos += Vector3.up * (worldRiseSpeed * dt);

            Camera cam = WorldUiScreen.ResolveRenderCamera(_ui != null ? _ui.WorldCamera : null);
            float t = _elapsed / _life;
            float punch = t < 0.12f
                ? Mathf.Lerp(0.75f, 1.15f, t / 0.12f)
                : Mathf.Lerp(1.15f, 1f, Mathf.InverseLerp(0.12f, 0.3f, t));

            bool onScreen = WorldUiScreen.TrySetOverlayFromWorld(_rt, cam, _worldPos, _baseScale * punch);

            Transform player = AttackSkill.Enemy.PlayerTargetLocator.GetActivePlayerTransform();
            bool occluded = _ui != null
                ? _ui.IsWorldPointOccluded(_worldPos, _ignoreRoot, player)
                : WorldUiScreen.IsOccluded(cam, _worldPos, ~0, _ignoreRoot, player);

            float alpha = 1f;
            if (t >= fadeStartNormalized)
            {
                alpha = Mathf.Lerp(1f, 0f, Mathf.InverseLerp(fadeStartNormalized, 1f, t));
            }

            if (occluded || !onScreen)
            {
                alpha = 0f;
            }

            if (_group != null)
            {
                _group.alpha = alpha;
            }
            else if (txtNumber != null)
            {
                Color c = _color;
                c.a = alpha;
                txtNumber.color = c;
            }

            if (_elapsed >= _life)
            {
                var cb = _onFinished;
                StopAndHide();
                cb?.Invoke(this);
            }
        }

        void CacheRefs()
        {
            if (txtNumber == null)
            {
                txtNumber = GetComponentInChildren<TextMeshProUGUI>(true);
            }

            _rt = transform as RectTransform;
            if (_group == null)
            {
                _group = GetComponent<CanvasGroup>();
            }
        }

        void ResolveStyle(
            bool isCritical,
            CombatElement element,
            out Color color,
            out float scale,
            out float fontSize)
        {
            float baseFont = normalFontSize > 1f
                ? normalFontSize
                : (txtNumber != null ? txtNumber.fontSize : 36f);
            if (baseFont < 1f)
            {
                baseFont = 36f;
            }

            if (isCritical)
            {
                color = CritColor;
                scale = 1.35f;
                fontSize = baseFont * 2f;
                return;
            }

            color = ColorForElement(element);
            scale = 1f;
            fontSize = baseFont;
        }

        public static Color ColorForElement(CombatElement element)
        {
            switch (element)
            {
                case CombatElement.Dark:
                    return DarkColor;
                case CombatElement.Thunder:
                    return ThunderColor;
                case CombatElement.Ice:
                    return IceColor;
                case CombatElement.Fire:
                    return FireColor;
                default:
                    return LightColor;
            }
        }
    }
}
