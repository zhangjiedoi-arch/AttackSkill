using AttackSkill.Enemy;
using UnityEngine;

namespace AttackSkill.UI.World
{
    /// <summary>声骸获取提示 UI：世界挂点投影到 Screen Overlay。</summary>
    [DefaultExecutionOrder(1000)]
    public sealed class ObtainRemainsHud : MonoBehaviour
    {
        [SerializeField] CanvasGroup canvasGroup;
        [SerializeField] Vector3 worldOffset = new Vector3(0f, 1.15f, 0f);

        EnemyAgent _agent;
        WorldUiService _ui;
        RectTransform _rt;
        bool _bound;
        bool _promptVisible;

        public void Bind(EnemyAgent agent, WorldUiService ui = null)
        {
            _agent = agent;
            _ui = ui != null ? ui : WorldUiService.Instance;
            _rt = transform as RectTransform;
            _bound = _agent != null;

            if (_ui != null)
            {
                transform.SetParent(_ui.PoolRoot, false);
            }

            WorldUiScreen.PrepareOverlayItem(gameObject);
            EnsureCanvasGroup();

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            SetPromptVisible(false);
        }

        public void SetPromptVisible(bool visible)
        {
            _promptVisible = visible;
            EnsureCanvasGroup();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }
        }

        void LateUpdate()
        {
            if (!_bound || _agent == null || !_promptVisible)
            {
                return;
            }

            if (_rt == null)
            {
                _rt = transform as RectTransform;
            }

            Vector3 worldPos = _agent.transform.position + worldOffset;
            CharacterController cc = _agent.Controller;
            if (cc != null)
            {
                worldPos = _agent.transform.TransformPoint(cc.center) + Vector3.up * (cc.height * 0.15f);
            }

            Camera cam = WorldUiScreen.ResolveRenderCamera(_ui != null ? _ui.WorldCamera : null);
            if (!WorldUiScreen.TrySetOverlayFromWorld(_rt, cam, worldPos))
            {
                SetPromptVisible(false);
            }
        }

        void OnDestroy()
        {
            _bound = false;
            _agent = null;
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
    }
}
