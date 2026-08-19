using AttackSkill.Character;
using AttackSkill.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace AttackSkill.UI
{
    /// <summary>肉鸽战斗 3 分钟获救倒计时；归零弹出救援结算。剩余时间写入 rougeRun 存档。</summary>
    public class UIBattleTimePanel : UIBase
    {
        public const float DurationSeconds = 3f * 60f;
        public const float WarnBelowSeconds = 60f;

        static readonly Color DefaultColor = Color.white;
        static readonly Color WarnColor = Color.red;

        /// <summary>运行中镜像；未开计时时为 -1。</summary>
        static float _trackedRemaining = -1f;

        [SerializeField] Text txtTime;

        float _remaining;
        bool _running;
        bool _finished;
        Color _baseColor = DefaultColor;

        /// <summary>存档用：当前剩余秒；未在跑则 -1。</summary>
        public static float CaptureRemainingSeconds() => _trackedRemaining;

        public static void ClearTrackedRemaining()
        {
            _trackedRemaining = -1f;
        }

        /// <param name="remainingSeconds">
        /// &lt;0：从满时长开始；≥0：从该剩余续跑（读档）。
        /// </param>
        public static void BeginRougeTimer(float remainingSeconds = -1f)
        {
            var ui = UIManager.Instance;
            if (ui == null)
            {
                return;
            }

            float start = remainingSeconds < 0f
                ? DurationSeconds
                : Mathf.Clamp(remainingSeconds, 0f, DurationSeconds);
            _trackedRemaining = start;
            ui.Open(UIId.BattleTime, start);
        }

        public static void EndRougeTimer()
        {
            var ui = UIManager.Instance;
            if (ui == null)
            {
                ClearTrackedRemaining();
                return;
            }

            if (ui.IsOpen(UIId.BattleTime))
            {
                ui.Close(UIId.BattleTime);
            }

            ClearTrackedRemaining();
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
            _trackedRemaining = 0f;
            PartyController.Instance?.ShowRescueGameOver();
            EndRougeTimer();
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
