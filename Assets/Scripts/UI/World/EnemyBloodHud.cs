using AttackSkill.Combat;
using AttackSkill.Enemy;
using UnityEngine;
using UnityEngine.UI;

namespace AttackSkill.UI.World
{
    /// <summary>怪物血条：世界挂点 → 屏幕投影，遮挡隐藏。</summary>
    [DefaultExecutionOrder(1000)]
    public sealed class EnemyBloodHud : MonoBehaviour
    {
        [SerializeField] Text txtLv;
        [SerializeField] Image imgBlood;
        [SerializeField] CanvasGroup canvasGroup;
        [SerializeField] float visibleRange = 20f;
        [SerializeField] Vector3 worldOffset = new Vector3(0f, 2.6f, 0f);

        EnemyAgent _agent;
        Health _health;
        WorldUiService _ui;
        RectTransform _rt;
        bool _bound;
        float _rangeSq;

        public void Bind(EnemyAgent agent, WorldUiService ui = null)
        {
            UnbindHealth();
            AutoBindRefs();
            EnsureCanvasGroup();
            _rt = transform as RectTransform;

            _agent = agent;
            _health = agent != null ? agent.Health : null;
            _ui = ui != null ? ui : WorldUiService.Instance;
            _bound = _agent != null && _health != null;

            if (_ui != null)
            {
                visibleRange = Mathf.Max(0.5f, _ui.BloodVisibleRange);
                worldOffset = _ui.BloodWorldOffset;
                transform.SetParent(_ui.PoolRoot, false);
            }

            WorldUiScreen.PrepareOverlayItem(gameObject);
            _rangeSq = visibleRange * visibleRange;

            if (_health != null)
            {
                _health.HpChanged += OnHpChanged;
                _health.Died += OnDied;
            }

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            RefreshLevel();
            RefreshHp();
            SetVisible(false);
            SyncPoseAndVisibility();
        }

        void OnDestroy() => UnbindHealth();

        void UnbindHealth()
        {
            if (_health != null)
            {
                _health.HpChanged -= OnHpChanged;
                _health.Died -= OnDied;
                _health = null;
            }

            _agent = null;
            _bound = false;
        }

        void LateUpdate() => SyncPoseAndVisibility();

        void SyncPoseAndVisibility()
        {
            if (!_bound || _agent == null)
            {
                return;
            }

            if (_agent.IsDead || _health == null || !_health.IsAlive || _agent.IsHibernating)
            {
                SetVisible(false);
                return;
            }

            Transform player = PlayerTargetLocator.GetActivePlayerTransform();
            if (player == null)
            {
                SetVisible(false);
                return;
            }

            if ((_agent.transform.position - player.position).sqrMagnitude > _rangeSq)
            {
                SetVisible(false);
                return;
            }

            Vector3 worldPos = WorldUiScreen.ResolveEnemyHeadWorldPos(_agent, worldOffset);
            Camera cam = WorldUiScreen.ResolveRenderCamera(_ui != null ? _ui.WorldCamera : null);
            if (_rt == null)
            {
                _rt = transform as RectTransform;
            }

            if (!WorldUiScreen.TrySetOverlayFromWorld(_rt, cam, worldPos))
            {
                SetVisible(false);
                return;
            }

            bool occluded = _ui != null
                ? _ui.IsWorldPointOccluded(worldPos, _agent.transform, player)
                : WorldUiScreen.IsOccluded(cam, worldPos, ~0, _agent.transform, player);

            SetVisible(!occluded);
        }

        void SetVisible(bool visible)
        {
            EnsureCanvasGroup();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }
        }

        void OnHpChanged() => RefreshHp();

        void OnDied() => SetVisible(false);

        void RefreshLevel()
        {
            if (txtLv == null)
            {
                return;
            }

            int lv = 1;
            if (_agent != null && _agent.Definition != null)
            {
                lv = Mathf.Max(1, _agent.Definition.level);
            }

            txtLv.text = $"Lv.{lv}";
        }

        void RefreshHp()
        {
            if (imgBlood == null || _health == null)
            {
                return;
            }

            if (imgBlood.type != Image.Type.Filled)
            {
                imgBlood.type = Image.Type.Filled;
                imgBlood.fillMethod = Image.FillMethod.Horizontal;
                imgBlood.fillOrigin = (int)Image.OriginHorizontal.Left;
            }

            float max = Mathf.Max(0.01f, _health.MaxHp);
            imgBlood.fillAmount = Mathf.Clamp01(_health.CurrentHp / max);
        }

        void EnsureCanvasGroup()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        void AutoBindRefs()
        {
            if (txtLv == null)
            {
                var t = transform.Find("txtLv");
                if (t != null)
                {
                    txtLv = t.GetComponent<Text>();
                }

                if (txtLv == null)
                {
                    txtLv = GetComponentInChildren<Text>(true);
                }
            }

            if (imgBlood == null)
            {
                var t = transform.Find("imgBlood");
                if (t != null)
                {
                    imgBlood = t.GetComponent<Image>();
                }
            }
        }
    }
}
