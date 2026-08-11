using System;
using UnityEngine;

namespace AttackSkill.Game
{
    /// <summary>全局暂停状态（ESC）。timeScale=0，输入侧应查询 IsPaused。</summary>
    public static class GamePause
    {
        static bool _paused;
        static float _timeScaleBeforePause = 1f;

        public static bool IsPaused => _paused;

        public static event Action<bool> PauseChanged;

        public static void Toggle()
        {
            SetPaused(!_paused);
        }

        public static void SetPaused(bool paused)
        {
            if (_paused == paused)
            {
                return;
            }

            _paused = paused;
            if (_paused)
            {
                _timeScaleBeforePause = Time.timeScale > 0f ? Time.timeScale : 1f;
                Time.timeScale = 0f;
            }
            else
            {
                Time.timeScale = _timeScaleBeforePause > 0f ? _timeScaleBeforePause : 1f;
            }

            PauseChanged?.Invoke(_paused);
        }

        /// <summary>退出 Play / 销毁控制器时务必调用，避免 Editor 卡在 timeScale=0。</summary>
        public static void ForceResume()
        {
            GameplayInputGate.ForceClear();

            if (!_paused && Time.timeScale > 0f)
            {
                return;
            }

            _paused = false;
            Time.timeScale = _timeScaleBeforePause > 0f ? _timeScaleBeforePause : 1f;
            PauseChanged?.Invoke(false);
        }
    }
}
