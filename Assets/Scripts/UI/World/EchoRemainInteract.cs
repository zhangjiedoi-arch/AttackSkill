using AttackSkill.Core;
using AttackSkill.Enemy;
using AttackSkill.Game;
using AttackSkill.Localization;
using AttackSkill.UI;
using UnityEngine;

namespace AttackSkill.UI.World
{
    /// <summary>
    /// 金透声骸靠近提示：≤1m 显示 ObtainRemains；F 键 Tip（loc: echo_obtain_wip），并压制滑翔。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class EchoRemainInteract : MonoBehaviour
    {
        public const string ObtainWipLocKey = "echo_obtain_wip";

        const float DefaultInteractRange = 1f;

        static int _nearCount;
        static int _nearFrame = -1;

        [SerializeField] float interactRange = DefaultInteractRange;

        EnemyAgent _agent;
        ObtainRemainsHud _hud;
        bool _active;
        bool _wasNear;

        /// <summary>本帧玩家是否在任一声骸交互范围内（供角色输入跳过滑翔）。</summary>
        public static bool ShouldPreferInteract
        {
            get
            {
                RefreshNearFrame();
                return _nearCount > 0;
            }
        }

        static void RefreshNearFrame()
        {
            int f = Time.frameCount;
            if (_nearFrame == f)
            {
                return;
            }

            _nearFrame = f;
            _nearCount = 0;
        }

        public void Activate(EnemyAgent agent)
        {
            _agent = agent;
            _active = agent != null;
            _wasNear = false;
            if (!_active)
            {
                return;
            }

            var ui = WorldUiService.EnsureExists();
            _hud = ui != null ? ui.AttachObtainRemains(agent) : null;
            enabled = true;
        }

        public void Deactivate()
        {
            if (_wasNear)
            {
                RefreshNearFrame();
                if (_nearCount > 0)
                {
                    _nearCount--;
                }

                _wasNear = false;
            }

            _active = false;
            if (_hud != null)
            {
                Destroy(_hud.gameObject);
                _hud = null;
            }

            enabled = false;
        }

        void OnDestroy()
        {
            Deactivate();
        }

        void Update()
        {
            if (!_active || _agent == null)
            {
                return;
            }

            RefreshNearFrame();

            Transform player = PlayerTargetLocator.GetActivePlayerTransform();
            bool near = false;
            if (player != null && !GameplayInputGate.IsBlocked)
            {
                float range = Mathf.Max(0.1f, interactRange);
                near = (player.position - _agent.transform.position).sqrMagnitude <= range * range;
            }

            if (near)
            {
                _nearCount++;
            }

            if (near != _wasNear)
            {
                _wasNear = near;
                _hud?.SetPromptVisible(near);
            }
            else if (near)
            {
                _hud?.SetPromptVisible(true);
            }

            if (!near)
            {
                return;
            }

            if (GameInput.GetKeyDown(KeyCode.F))
            {
                LocalizationService.EnsureInitialized();
                string tip = LocalizationService.Get(LocalizationTableType.UI, ObtainWipLocKey);
                UIManager.Instance?.ShowTip(tip, 1.8f);
            }
        }
    }
}
