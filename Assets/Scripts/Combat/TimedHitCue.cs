using UnityEngine;

namespace AttackSkill.Combat
{
    /// <summary>
    /// 相对当前攻击动画 <c>normalizedTime</c>（0~1，通常取未循环的小数部分）的触发方式。
    /// </summary>
    public enum TimedHitFireMode
    {
        /// <summary>越过 <see cref="TimedHitCue.startNormalized"/> 时触发一次。</summary>
        AtTime = 0,

        /// <summary>
        /// 落在 [start, end] 内视为窗口激活。
        /// 出伤默认进窗触发一次；勾选 <see cref="TimedHitCue.continuousHitSampling"/> 则窗内每帧可采样。
        /// </summary>
        Window = 1,
    }

    /// <summary>
    /// 一段按 normalizedTime 驱动的特效 / 出伤 cue。
    /// 形状与数值复用 <see cref="SkillHitSegment"/>，便于日后接 <see cref="SkillHitExecutor"/>。
    /// </summary>
    [System.Serializable]
    public class TimedHitCue
    {
        [Tooltip("调试用备注，如 slash_vfx / hit_main")]
        public string id = "cue";

        public TimedHitFireMode fireMode = TimedHitFireMode.AtTime;

        [Tooltip("AtTime：触发点；Window：窗口起点（含）")]
        [Range(0f, 1f)]
        public float startNormalized = 0.35f;

        [Tooltip("仅 Window：窗口终点（含）。AtTime 忽略。")]
        [Range(0f, 1f)]
        public float endNormalized = 0.45f;

        [Header("Actions")]
        [Tooltip("到点播放 segment.vfxPrefab / sfx")]
        public bool playPresentation = true;

        [Tooltip("到点做形状检测出伤")]
        public bool dealHit = true;

        [Tooltip("Window 模式下：true=窗内每帧可出伤采样；false=仅进入窗口时出伤一次")]
        public bool continuousHitSampling;

        [Header("Payload")]
        public SkillHitSegment segment = new SkillHitSegment
        {
            id = "hit",
            socket = HitSocketId.HitOrigin,
            shape = HitShapeType.Fan,
            radius = 2.2f,
            fanAngle = 90f,
            minHitDistance = 0.15f,
            hitHeight = 0.9f,
            damage = 100f,
            knockback = 1.5f,
            vfxLife = 1.2f,
        };

        /// <summary>规范化窗口：保证 end &gt;= start。</summary>
        public void GetNormalizedRange(out float start, out float end)
        {
            start = Mathf.Clamp01(startNormalized);
            end = fireMode == TimedHitFireMode.Window
                ? Mathf.Clamp01(Mathf.Max(endNormalized, start))
                : start;
        }

        /// <summary>
        /// 从上一采样到当前采样是否应触发「瞬时」逻辑（AtTime 越过点，或 Window 首次进入）。
        /// <paramref name="prevNormalized"/> / <paramref name="currNormalized"/> 建议已处理为同一片段内的 0~1（勿用跨循环的 raw）。
        /// </summary>
        public bool CrossedInstant(float prevNormalized, float currNormalized)
        {
            GetNormalizedRange(out float start, out float end);

            if (fireMode == TimedHitFireMode.AtTime)
            {
                return prevNormalized < start && currNormalized >= start;
            }

            bool wasInside = prevNormalized >= start && prevNormalized <= end;
            bool isInside = currNormalized >= start && currNormalized <= end;
            return !wasInside && isInside;
        }

        /// <summary>当前 normalized 是否落在 Window 内（AtTime 恒为 false）。</summary>
        public bool IsInsideWindow(float normalized)
        {
            if (fireMode != TimedHitFireMode.Window)
            {
                return false;
            }

            GetNormalizedRange(out float start, out float end);
            return normalized >= start && normalized <= end;
        }

        /// <summary>本帧是否应做命中采样（含连续窗）。</summary>
        public bool ShouldSampleHit(float prevNormalized, float currNormalized)
        {
            if (!dealHit)
            {
                return false;
            }

            if (fireMode == TimedHitFireMode.AtTime)
            {
                return CrossedInstant(prevNormalized, currNormalized);
            }

            if (continuousHitSampling)
            {
                return IsInsideWindow(currNormalized);
            }

            return CrossedInstant(prevNormalized, currNormalized);
        }

        /// <summary>本帧是否应播表现（VFX/SFX）。表现始终一次性，避免窗内每帧刷特效。</summary>
        public bool ShouldPlayPresentation(float prevNormalized, float currNormalized)
        {
            return playPresentation && CrossedInstant(prevNormalized, currNormalized);
        }
    }

    /// <summary>一次攻击动画（如普攻连段某一刀）的全部 timed cues。</summary>
    [System.Serializable]
    public class TimedAttackPhase
    {
        [Tooltip("调试名，如 attack1")]
        public string id = "phase";

        [Tooltip("与 HSM AttackComboIndex / BeginSwing 对齐：0/1/2")]
        public int comboIndex;

        [Tooltip("可选：Animator 状态短名，供运行时校验（如 attack1 或 Armature|Sword_Regular_A）。空=不校验")]
        public string animatorStateName;

        public TimedHitCue[] cues =
        {
            new TimedHitCue
            {
                id = "hit",
                fireMode = TimedHitFireMode.AtTime,
                startNormalized = 0.35f,
                playPresentation = true,
                dealHit = true,
            },
        };

        public bool MatchesCombo(int index) => comboIndex == index;

        public bool MatchesAnimatorState(AnimatorStateInfo info)
        {
            if (string.IsNullOrEmpty(animatorStateName))
            {
                return true;
            }

            return info.IsName(animatorStateName);
        }
    }
}
