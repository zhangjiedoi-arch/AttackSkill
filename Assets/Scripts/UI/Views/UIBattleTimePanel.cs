using AttackSkill.Character;
using AttackSkill.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace AttackSkill.UI
{
    /// <summary>
    /// 肉鸽获救倒计时。存档语义：
    /// -1 = 未开表；≥0 = 剩余秒（0 = 已归零/结算，勿回填满时长）。
    /// </summary>
    public class UIBattleTimePanel : UIBase
    {
        public const float DurationSeconds = 3f * 60f;
        public const float WarnBelowSeconds = 60f;

        static readonly Color DefaultColor = Color.white;
        static readonly Color WarnColor = Color.red;

        /// <summary>-1 未开表；≥0 运行中或已结束（0）。</summary>
        static float _trackedRemaining = -1f;

        /// <summary>UI 未就绪时暂存，Boot 后再 Open。</summary>
        static float _pendingOpenRemaining = float.NaN;

        [SerializeField] Text txtTime;

        float _remaining;
        bool _running;
        bool _finished;
        Color _baseColor = DefaultColor;

        /// <summary>存档用：-1 未开表；≥0 含已结束的 0。</summary>
        public static float CaptureRemainingSeconds() => _trackedRemaining;

        public static bool HasActiveOrPendingTimer =>
            _trackedRemaining >= 0f || !float.IsNaN(_pendingOpenRemaining);

        public static void ClearTrackedRemaining()
        {
            _trackedRemaining = -1f;
            _pendingOpenRemaining = float.NaN;
        }

        /// <summary>结算/归零：保持 0，关闭面板但不清成 -1。</summary>
        public static void MarkExpiredAndClose()
        {
            _trackedRemaining = 0f;
            _pendingOpenRemaining = float.NaN;
            var ui = UIManager.Instance;
            if (ui != null && ui.IsOpen(UIId.BattleTime))
            {
                ui.Close(UIId.BattleTime);
            }
        }

        /// <param name="remainingSeconds">&lt;0 满时长；≥0 续跑（含 0 立刻结算）。</param>
        public static void BeginRougeTimer(float remainingSeconds = -1f)
        {
            float start = remainingSeconds < 0f
                ? DurationSeconds
                : Mathf.Clamp(remainingSeconds, 0f, DurationSeconds);

            // 无 UI 也先记镜像，避免存档回填满时长
            _trackedRemaining = start;
            _pendingOpenRemaining = start;

            var ui = UIManager.Instance;
            if (ui == null)
            {
                Debug.LogWarning("[BattleTime] UIManager 未就绪，已记录剩余秒，待 Boot 补开面板。");
                return;
            }

            FlushPendingOpen(ui);
        }

        /// <summary>回海滩等：关面板并清成未开表。</summary>
        public static void EndRougeTimer()
        {
            ClearTrackedRemaining();
            var ui = UIManager.Instance;
            if (ui != null && ui.IsOpen(UIId.BattleTime))
            {
                ui.Close(UIId.BattleTime);
            }
        }

        /// <summary>GameProgress Boot 末调用：补开因 UI 未就绪而挂起的倒计时。</summary>
        public static void TryOpenPendingAfterBoot()
        {
            if (float.IsNaN(_pendingOpenRemaining) && _trackedRemaining < 0f)
            {
                return;
            }

            var ui = UIManager.Instance;
            if (ui == null)
            {
                return;
            }

            FlushPendingOpen(ui);
        }

        static void FlushPendingOpen(UIManager ui)
        {
            float start = !float.IsNaN(_pendingOpenRemaining)
                ? _pendingOpenRemaining
                : _trackedRemaining;
            if (start < 0f)
            {
                return;
            }

            _pendingOpenRemaining = float.NaN;
            _trackedRemaining = start;

            if (ui.IsOpen(UIId.BattleTime))
            {
                // 已打开则刷新 args：关再开
                ui.Close(UIId.BattleTime);
            }

            ui.Open(UIId.BattleTime, start);
        }

        public override void OnOpen(object args)
        {
            EnsureBound();
            LocalizationService.LocaleChanged -= OnLocaleChanged;
            LocalizationService.LocaleChanged += OnLocaleChanged;

            if (args is float f)
            {
                _remaining = Mathf.Clamp(f, 0f, DurationSeconds);
            }
            else if (_trackedRemaining >= 0f)
            {
                _remaining = Mathf.Clamp(_trackedRemaining, 0f, DurationSeconds);
            }
            else
            {
                _remaining = DurationSeconds;
            }

            _trackedRemaining = _remaining;
            _pendingOpenRemaining = float.NaN;
            _running = true;
            _finished = false;
            RefreshText();

            if (_remaining <= 0f)
            {
                FinishRescue();
            }
        }

        public override void OnClose()
        {
            LocalizationService.LocaleChanged -= OnLocaleChanged;
            _running = false;
        }

        void OnDestroy()
        {
            LocalizationService.LocaleChanged -= OnLocaleChanged;
        }

        void Update()
        {
            if (!_running || _finished)
            {
                return;
            }

            _remaining -= Time.deltaTime;
            if (_remaining < 0f)
            {
                _remaining = 0f;
            }

            _trackedRemaining = _remaining;
            RefreshText();

            if (_remaining <= 0f)
            {
                FinishRescue();
            }
        }

        void OnLocaleChanged(GameLocale _)
        {
            RefreshText();
        }

        void FinishRescue()
        {
            if (_finished)
            {
                return;
            }

            _finished = true;
            _running = false;
            MarkExpiredAndClose();
            PartyController.Instance?.ShowRescueGameOver();
        }

        void RefreshText()
        {
            if (txtTime == null)
            {
                return;
            }

            int total = Mathf.Max(0, Mathf.CeilToInt(_remaining));
            int minutes = total / 60;
            int seconds = total % 60;
            string clock = $"{minutes:00}:{seconds:00}";
            txtTime.text = LocalizationService.Format(
                LocalizationTableType.UI,
                "battle_time_rescue",
                clock);

            bool warn = _remaining > 0f && _remaining < WarnBelowSeconds;
            txtTime.color = warn ? WarnColor : _baseColor;
        }

        void EnsureBound()
        {
            if (txtTime == null)
            {
                txtTime = FindChildComponent<Text>(transform, "txtTime");
            }

            if (txtTime != null)
            {
                _baseColor = txtTime.color;
                if (_baseColor.a < 0.01f)
                {
                    _baseColor = DefaultColor;
                }
            }
        }

        static T FindChildComponent<T>(Transform root, string name) where T : Component
        {
            if (root == null)
            {
                return null;
            }

            var all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == name)
                {
                    return all[i].GetComponent<T>();
                }
            }

            return null;
        }
    }
}
