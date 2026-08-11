using System;
using UnityEngine;

namespace AttackSkill.Game
{
    /// <summary>
    /// 玩法输入总闸：ESC 暂停或技能轮盘等软阻塞时，角色/相机应停止接收输入。
    /// 软阻塞可额外将 timeScale 置 0，且不设置 <see cref="GamePause.IsPaused"/>。
    /// </summary>
    public static class GameplayInputGate
    {
        static int _softBlocks;
        static float _timeScaleBeforeSoft = 1f;
        static bool _softFrozenTime;

        /// <summary>软阻塞数量变化时触发（参数：当前是否仍软阻塞）。</summary>
        public static event Action<bool> SoftBlockChanged;

        public static bool IsSoftBlocked => _softBlocks > 0;

        public static bool IsBlocked => GamePause.IsPaused || _softBlocks > 0;

        public static void PushSoftBlock(bool freezeTime = true)
        {
            bool was = _softBlocks > 0;
            if (_softBlocks == 0 && freezeTime && !GamePause.IsPaused)
            {
                _timeScaleBeforeSoft = Time.timeScale > 0f ? Time.timeScale : 1f;
                Time.timeScale = 0f;
                _softFrozenTime = true;
            }

            _softBlocks++;
            if (!was)
            {
                SoftBlockChanged?.Invoke(true);
            }
        }

        public static void PopSoftBlock()
        {
            if (_softBlocks <= 0)
            {
                return;
            }

            _softBlocks--;
            if (_softBlocks == 0)
            {
                if (_softFrozenTime)
                {
                    _softFrozenTime = false;
                    if (!GamePause.IsPaused)
                    {
                        Time.timeScale = _timeScaleBeforeSoft > 0f ? _timeScaleBeforeSoft : 1f;
                    }
                }

                SoftBlockChanged?.Invoke(false);
            }
        }

        /// <summary>退出 Play 时复位，避免 timeScale 卡在 0。</summary>
        public static void ForceClear()
        {
            bool was = _softBlocks > 0;
            _softBlocks = 0;
            if (_softFrozenTime && !GamePause.IsPaused)
            {
                Time.timeScale = _timeScaleBeforeSoft > 0f ? _timeScaleBeforeSoft : 1f;
            }

            _softFrozenTime = false;
            if (was)
            {
                SoftBlockChanged?.Invoke(false);
            }
        }
    }
}
