using TMPro;
using UnityEngine;

namespace AttackSkill.UI.World
{
    /// <summary>伤害跳字：世界 HitPoint → 屏幕投影，上浮、遮挡隐藏。</summary>
    [DefaultExecutionOrder(1000)]
    public sealed class DamageNumberView : MonoBehaviour
    {
        [SerializeField] TextMeshProUGUI txtNumber;
        [SerializeField] float worldRiseSpeed = 1.35f;
        [SerializeField] float fadeStartNormalized = 0.4f;

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
            if (txtNumber == null)
            {
                txtNumber = GetComponentInChildren<TextMeshProUGUI>(true);
            }

            _rt = transform as RectTransform;
            _group = GetComponent<CanvasGroup>();
            if (_group == null)
            {
                _group = gameObject.AddComponent<CanvasGroup>();
            }
        }

        public void Play(
            float amount,
            Vector3 worldPosition,
            float lifetime,
            WorldUiService ui,
            Transform ignoreRoot,
            System.Action<DamageNumberView> onFinished)
        {
            if (txtNumber == null)
            {
                txtNumber = GetComponentInChildren<TextMeshProUGUI>(true);
            }

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

            ResolveStyle(amount, out _color, out _baseScale);
            int shown = Mathf.Max(1, Mathf.RoundToInt(amount));
            if (txtNumber != null)
            {
                txtNumber.text = shown.ToString();
                txtNumber.color = _color;
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

        static void ResolveStyle(float amount, out Color color, out float scale)
        {
            if (amount < 100f)
            {
                color = Color.white;
                scale = 0.9f;
            }
            else if (amount <= 10000f)
            {
                color = new Color(1f, 0.88f, 0.2f, 1f);
                scale = 1.2f;
            }
            else
            {
                color = new Color(1f, 0.25f, 0.2f, 1f);
                scale = 1.6f;
            }
        }
    }
}
