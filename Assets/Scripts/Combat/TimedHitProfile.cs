using System;
using System.Collections.Generic;
using UnityEngine;

namespace AttackSkill.Combat
{
    /// <summary>
    /// 按动画 <c>normalizedTime</c> 驱动出伤/特效的配置表（不依赖 Animation Event）。
    /// 运行时：每帧用上一帧与当前帧的 normalized 对 <see cref="TimedHitCue"/> 做越过/进窗判定即可。
    /// </summary>
    [CreateAssetMenu(
        menuName = "AttackSkill/Combat/Timed Hit Profile",
        fileName = "TimedHitProfile")]
    public class TimedHitProfile : ScriptableObject
    {
        [Tooltip("通常一段连招一 phase；技能也可多 phase 共用本 SO")]
        public TimedAttackPhase[] phases =
        {
            CreateDefaultPhase(0, "attack1", 0.32f, 100f, 2.0f, 80f, 1.2f),
            CreateDefaultPhase(1, "attack2", 0.34f, 120f, 2.3f, 100f, 1.6f),
            CreateDefaultPhase(2, "attack3", 0.36f, 150f, 2.6f, 120f, 2.4f),
        };

        public bool TryGetPhaseByCombo(int comboIndex, out TimedAttackPhase phase)
        {
            phase = null;
            if (phases == null)
            {
                return false;
            }

            for (int i = 0; i < phases.Length; i++)
            {
                TimedAttackPhase p = phases[i];
                if (p != null && p.MatchesCombo(comboIndex))
                {
                    phase = p;
                    return true;
                }
            }

            return false;
        }

        public bool TryGetPhaseById(string phaseId, out TimedAttackPhase phase)
        {
            phase = null;
            if (phases == null || string.IsNullOrEmpty(phaseId))
            {
                return false;
            }

            for (int i = 0; i < phases.Length; i++)
            {
                TimedAttackPhase p = phases[i];
                if (p != null && string.Equals(p.id, phaseId, StringComparison.Ordinal))
                {
                    phase = p;
                    return true;
                }
            }

            return false;
        }

        public TimedAttackPhase GetPhaseByComboOrNull(int comboIndex)
        {
            return TryGetPhaseByCombo(comboIndex, out TimedAttackPhase phase) ? phase : null;
        }

        /// <summary>
        /// 收集本帧应播表现的 cues（一次性越过判定）。
        /// </summary>
        public void CollectPresentationCues(
            int comboIndex,
            float prevNormalized,
            float currNormalized,
            List<TimedHitCue> results)
        {
            if (results == null || !TryGetPhaseByCombo(comboIndex, out TimedAttackPhase phase) || phase.cues == null)
            {
                return;
            }

            for (int i = 0; i < phase.cues.Length; i++)
            {
                TimedHitCue cue = phase.cues[i];
                if (cue != null && cue.ShouldPlayPresentation(prevNormalized, currNormalized))
                {
                    results.Add(cue);
                }
            }
        }

        /// <summary>
        /// 收集本帧应做命中采样的 cues。
        /// </summary>
        public void CollectHitSampleCues(
            int comboIndex,
            float prevNormalized,
            float currNormalized,
            List<TimedHitCue> results)
        {
            if (results == null || !TryGetPhaseByCombo(comboIndex, out TimedAttackPhase phase) || phase.cues == null)
            {
                return;
            }

            for (int i = 0; i < phase.cues.Length; i++)
            {
                TimedHitCue cue = phase.cues[i];
                if (cue != null && cue.ShouldSampleHit(prevNormalized, currNormalized))
                {
                    results.Add(cue);
                }
            }
        }

        static TimedAttackPhase CreateDefaultPhase(
            int comboIndex,
            string id,
            float hitNormalized,
            float damage,
            float radius,
            float fanAngle,
            float knockback)
        {
            // 刀光略早于出伤，便于手感对齐
            float vfxNormalized = Mathf.Max(0f, hitNormalized - 0.04f);

            return new TimedAttackPhase
            {
                id = id,
                comboIndex = comboIndex,
                animatorStateName = id,
                cues = new[]
                {
                    new TimedHitCue
                    {
                        id = "slash_vfx",
                        fireMode = TimedHitFireMode.AtTime,
                        startNormalized = vfxNormalized,
                        playPresentation = true,
                        dealHit = false,
                        segment = new SkillHitSegment
                        {
                            id = "slash_vfx",
                            socket = HitSocketId.Weapon,
                            shape = HitShapeType.Fan,
                            radius = radius,
                            fanAngle = fanAngle,
                            minHitDistance = 0.15f,
                            hitHeight = 0.9f,
                            damage = 0f,
                            knockback = 0f,
                            vfxLife = 1.2f,
                        },
                    },
                    new TimedHitCue
                    {
                        id = "hit",
                        fireMode = TimedHitFireMode.AtTime,
                        startNormalized = hitNormalized,
                        playPresentation = false,
                        dealHit = true,
                        segment = new SkillHitSegment
                        {
                            id = "hit",
                            socket = HitSocketId.HitOrigin,
                            shape = HitShapeType.Fan,
                            radius = radius,
                            fanAngle = fanAngle,
                            minHitDistance = 0.15f,
                            hitHeight = 0.9f,
                            damage = damage,
                            knockback = knockback,
                            vfxLife = 1.2f,
                        },
                    },
                },
            };
        }
    }
}
