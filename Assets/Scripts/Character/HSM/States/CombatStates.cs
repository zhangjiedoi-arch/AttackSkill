using UnityEngine;
using AttackSkill.Combat;

namespace AttackSkill.Character.HSM
{
    /// <summary>
    /// 战斗父状态：普攻连段 / 元素战技（时序简化）。
    /// </summary>
    public class CombatState : CharacterState
    {
        public readonly AttackState Attack;
        public readonly SkillState Skill;
        public readonly SkillRState SkillR;

        public CombatState(CharacterContext ctx) : base("Combat", ctx)
        {
            Attack = new AttackState(ctx);
            Skill = new SkillState(ctx);
            SkillR = new SkillRState(ctx);
        }

        public override void OnEnter()
        {
            Ctx.SetAnimBool(CharacterAnimParams.InCombatAction, true);
        }

        public override void OnExit()
        {
            Ctx.SetAnimBool(CharacterAnimParams.InCombatAction, false);
            Ctx.SetAnimInt(CharacterAnimParams.AttackCombo, -1);
            Ctx.AttackComboIndex = 0;
            Ctx.AttackHits?.EndCombat();
            Ctx.ResetAnimTrigger(CharacterAnimParams.Attack);
        }
    }

    public class AttackState : CharacterState
    {
        float _elapsed;
        float _duration;
        bool _comboQueued;
        bool _durationSynced;
        int _playingCombo;

        static readonly string[] AttackStateNames = { "attack1", "attack2", "attack3" };

        public AttackState(CharacterContext ctx) : base("Attack", ctx) { }

        public override void OnEnter()
        {
            // 连段索引由上一段结束时预写好；只有离开战斗后才会在 Return/Combat.OnExit 清零。
            // 不要用 ComboResetTime 在这里清零，否则第一段动画一长过重置时间，第二段会被打回 attack1。
            _playingCombo = Mathf.Clamp(Ctx.AttackComboIndex, 0, Ctx.Settings.MaxAttackCombo - 1);
            Ctx.LastAttackTime = Time.time;
            _elapsed = 0f;
            _comboQueued = false;
            _durationSynced = false;
            _duration = ResolveDurationFallback(_playingCombo);

            Ctx.Motor.PlanarVelocity *= 0.35f;
            Ctx.Motor.Velocity.x = Ctx.Motor.PlanarVelocity.x;
            Ctx.Motor.Velocity.z = Ctx.Motor.PlanarVelocity.z;

            Ctx.SetAnimBool(CharacterAnimParams.InCombatAction, true);
            Ctx.SetAnimInt(CharacterAnimParams.AttackCombo, _playingCombo);
            Ctx.AttackHits?.BeginSwing(_playingCombo);

            if (Ctx.Animator != null)
            {
                Ctx.ResetAnimTrigger(CharacterAnimParams.Attack);
                string stateName = AttackStateNames[Mathf.Clamp(_playingCombo, 0, AttackStateNames.Length - 1)];
                Ctx.Animator.CrossFadeInFixedTime(stateName, 0.08f, 0, 0f);
            }

            Ctx.SetAnimTrigger(CharacterAnimParams.Attack);

            // 预写下一段；若本段结束没有接招，ReturnToLocomotion 会清回 0
            Ctx.AttackComboIndex = _playingCombo + 1;
            if (Ctx.AttackComboIndex >= Ctx.Settings.MaxAttackCombo)
            {
                Ctx.AttackComboIndex = 0;
            }
        }

        public override void OnUpdate(float deltaTime)
        {
            // 普攻可被右键闪避取消
            if (Ctx.Input.DodgePressed && Ctx.CanDodge)
            {
                GoTo(Ctx.Owner.States.Grounded.Dodge);
                return;
            }

            TrySyncDurationFromAnimator();

            // 攻击过程中按左键只缓存，本段完整结束后再衔接下一段
            if (Ctx.Input.AttackPressed)
            {
                _comboQueued = true;
            }

            _elapsed += deltaTime;
            if (_elapsed < _duration)
            {
                return;
            }

            if (_comboQueued && _playingCombo < Ctx.Settings.MaxAttackCombo - 1)
            {
                // 还有下一段可接
                Machine.ChangeState(Ctx.Owner.States.Combat.Attack, allowReenter: true);
                return;
            }

            // 第三段结束，或没有缓存输入
            ReturnToLocomotion();
        }

        public override void OnFixedUpdate(float deltaTime)
        {
            Ctx.Motor.ApplyGravity(deltaTime, Ctx.Settings.Gravity);
            Ctx.Motor.TickMove(deltaTime);
        }

        void TrySyncDurationFromAnimator()
        {
            if (_durationSynced || Ctx.Animator == null)
            {
                return;
            }

            string stateName = AttackStateNames[Mathf.Clamp(_playingCombo, 0, AttackStateNames.Length - 1)];
            var info = Ctx.Animator.GetCurrentAnimatorStateInfo(0);
            if (!info.IsName(stateName))
            {
                var next = Ctx.Animator.GetNextAnimatorStateInfo(0);
                if (next.IsName(stateName) && next.length > 0.05f)
                {
                    _duration = next.length / Mathf.Max(0.01f, next.speed);
                    _durationSynced = true;
                }

                return;
            }

            if (info.length > 0.05f)
            {
                _duration = info.length / Mathf.Max(0.01f, info.speed);
                _durationSynced = true;
            }
        }

        float ResolveDurationFallback(int comboIndex)
        {
            var arr = Ctx.Settings.AttackComboDurations;
            if (arr != null && comboIndex >= 0 && comboIndex < arr.Length && arr[comboIndex] > 0.05f)
            {
                return arr[comboIndex];
            }

            return Mathf.Max(0.2f, Ctx.Settings.AttackDuration);
        }

        void ReturnToLocomotion()
        {
            Ctx.AttackComboIndex = 0;

            if (Ctx.Motor.IsGrounded)
            {
                GoTo(Ctx.Input.HasMove
                    ? (Ctx.Input.SprintHeld ? Ctx.Owner.States.Grounded.Sprint : (HState)Ctx.Owner.States.Grounded.Move)
                    : Ctx.Owner.States.Grounded.Idle);
            }
            else
            {
                GoTo(Ctx.Owner.States.Airborne.Fall);
            }
        }
    }

    public class SkillState : CharacterState
    {
        const string SkillAnimStateName = "skill";

        float _elapsed;
        float _duration;
        bool _durationSynced;

        public SkillState(CharacterContext ctx) : base("Skill", ctx) { }

        public override void OnEnter()
        {
            Ctx.Motor.PlanarVelocity = Vector3.zero;
            Ctx.Motor.Velocity.x = 0f;
            Ctx.Motor.Velocity.z = 0f;

            // E 技能：只走 Animator 状态 skill（Trigger），出伤由 TimedHitProfile phase「skill」
            _elapsed = 0f;
            _durationSynced = false;
            _duration = Mathf.Max(0.1f, Ctx.Settings.SkillDuration);

            if (Ctx.SkillPlayer != null && Ctx.SkillPlayer.IsPlaying)
            {
                Ctx.SkillPlayer.Stop();
            }

            Ctx.AttackHits?.BeginTimedPhase("skill");
            Ctx.AttackHits?.SetWeaponVisible(false);
            CombatStats.Find(Ctx.Transform)?.BeginSkillECooldown();

            Ctx.SetAnimBool(CharacterAnimParams.InCombatAction, true);
            if (Ctx.Animator != null)
            {
                // 只用 Trigger → Any State 进 skill，避免再 CrossFade 导致状态重入、事件丢帧
                Ctx.ResetAnimTrigger(CharacterAnimParams.Skill);
                Ctx.SetAnimTrigger(CharacterAnimParams.Skill);
            }
            else
            {
                Ctx.SetAnimTrigger(CharacterAnimParams.Skill);
            }
        }

        public override void OnExit()
        {
            Ctx.AttackHits?.EndTimedPhase();
            Ctx.SetAnimBool(CharacterAnimParams.InCombatAction, false);
            Ctx.ResetAnimTrigger(CharacterAnimParams.Skill);
        }

        public override void OnUpdate(float deltaTime)
        {
            TrySyncDurationFromAnimator();

            _elapsed += deltaTime;
            if (_elapsed < _duration)
            {
                return;
            }

            ReturnFromSkill();
        }

        public override void OnFixedUpdate(float deltaTime)
        {
            float g = Ctx.Motor.IsGrounded
                ? Ctx.Settings.Gravity
                : Ctx.Settings.Gravity * Ctx.Settings.FallGravityMultiplier;
            Ctx.Motor.PlanarVelocity = Vector3.zero;
            Ctx.Motor.Velocity.x = 0f;
            Ctx.Motor.Velocity.z = 0f;
            Ctx.Motor.ApplyGravity(deltaTime, g);
            Ctx.Motor.TickMove(deltaTime);
        }

        void TrySyncDurationFromAnimator()
        {
            if (_durationSynced || Ctx.Animator == null)
            {
                return;
            }

            var info = Ctx.Animator.GetCurrentAnimatorStateInfo(0);
            if (!info.IsName(SkillAnimStateName))
            {
                var next = Ctx.Animator.GetNextAnimatorStateInfo(0);
                if (next.IsName(SkillAnimStateName) && next.length > 0.05f)
                {
                    _duration = next.length;
                    _durationSynced = true;
                }

                return;
            }

            if (info.length > 0.05f)
            {
                _duration = info.length;
                _durationSynced = true;
            }
        }

        void ReturnFromSkill()
        {
            if (Ctx.IsInWater)
            {
                GoTo(Ctx.Owner.States.Swim.Idle);
            }
            else if (Ctx.Motor.IsGrounded)
            {
                GoTo(Ctx.Owner.States.Grounded.Idle);
            }
            else
            {
                GoTo(Ctx.Owner.States.Airborne.Fall);
            }
        }
    }

    /// <summary>
    /// R 技能：Trigger SkillR；AoE/出伤由 TimedHit「Skill_R」驱动。
    /// 以 Animator 播完为准再退状态。
    /// </summary>
    public class SkillRState : CharacterState
    {
        static readonly string[] SkillRAnimStateNames =
        {
            "skill_r", "SkillR", "skillr", "Skill_R", "RSkill", "r_skill"
        };

        float _elapsed;
        float _fallbackDuration;
        bool _enteredSkillRAnim;

        public SkillRState(CharacterContext ctx) : base("SkillR", ctx) { }

        public override void OnEnter()
        {
            Ctx.Motor.PlanarVelocity = Vector3.zero;
            Ctx.Motor.Velocity.x = 0f;
            Ctx.Motor.Velocity.z = 0f;

            _elapsed = 0f;
            _enteredSkillRAnim = false;
            _fallbackDuration = Mathf.Max(0.5f, Ctx.Settings != null ? Ctx.Settings.SkillRDuration : 2.5f);

            if (Ctx.SkillPlayer != null && Ctx.SkillPlayer.IsPlaying)
            {
                Ctx.SkillPlayer.Stop();
            }

            // AoE / 出伤由 TimedHitProfile「Skill_R」驱动，不再走 SkillRVisual
            Ctx.AttackHits?.BeginTimedPhase("Skill_R");
            Ctx.AttackHits?.SetWeaponVisible(false);
            CombatStats.Find(Ctx.Transform)?.BeginSkillRCooldown();

            Ctx.SetAnimBool(CharacterAnimParams.InCombatAction, true);
            Ctx.ResetAnimTrigger(CharacterAnimParams.SkillR);
            Ctx.SetAnimTrigger(CharacterAnimParams.SkillR);
        }

        public override void OnExit()
        {
            Ctx.AttackHits?.EndTimedPhase();
            Ctx.SetAnimBool(CharacterAnimParams.InCombatAction, false);
            Ctx.ResetAnimTrigger(CharacterAnimParams.SkillR);
        }

        public override void OnUpdate(float deltaTime)
        {
            _elapsed += deltaTime;

            if (ShouldFinishSkillR())
            {
                ReturnFromSkill();
            }
        }

        public override void OnFixedUpdate(float deltaTime)
        {
            float g = Ctx.Motor.IsGrounded
                ? Ctx.Settings.Gravity
                : Ctx.Settings.Gravity * Ctx.Settings.FallGravityMultiplier;
            Ctx.Motor.PlanarVelocity = Vector3.zero;
            Ctx.Motor.Velocity.x = 0f;
            Ctx.Motor.Velocity.z = 0f;
            Ctx.Motor.ApplyGravity(deltaTime, g);
            Ctx.Motor.TickMove(deltaTime);
        }

        bool ShouldFinishSkillR()
        {
            if (Ctx.Animator == null)
            {
                return _elapsed >= _fallbackDuration;
            }

            bool inSkillR = IsPlayingSkillR(out AnimatorStateInfo info, out bool inTransition);
            if (inSkillR)
            {
                _enteredSkillRAnim = true;
                float clipLen = info.length / Mathf.Max(0.01f, info.speed);
                if (clipLen > _fallbackDuration)
                {
                    _fallbackDuration = clipLen + 0.15f;
                }

                // 仍在 SkillR：等本段接近播完且不在过渡中
                if (!inTransition && info.normalizedTime >= 0.98f)
                {
                    return true;
                }

                return false;
            }

            // 已经进过 SkillR，又离开该状态 → 动画结束
            if (_enteredSkillRAnim)
            {
                return true;
            }

            // Trigger 尚未切入：等到回退时长再结束（防卡死）
            return _elapsed >= _fallbackDuration;
        }

        bool IsPlayingSkillR(out AnimatorStateInfo info, out bool inTransition)
        {
            inTransition = Ctx.Animator.IsInTransition(0);
            info = Ctx.Animator.GetCurrentAnimatorStateInfo(0);
            if (MatchesSkillR(info) || MatchesSkillRClips(Ctx.Animator, 0))
            {
                return true;
            }

            if (inTransition)
            {
                info = Ctx.Animator.GetNextAnimatorStateInfo(0);
                if (MatchesSkillR(info))
                {
                    return true;
                }
            }

            return false;
        }

        static bool MatchesSkillR(AnimatorStateInfo info)
        {
            for (int i = 0; i < SkillRAnimStateNames.Length; i++)
            {
                if (info.IsName(SkillRAnimStateNames[i]))
                {
                    return true;
                }
            }

            return false;
        }

        static bool MatchesSkillRClips(Animator animator, int layer)
        {
            int count = animator.GetCurrentAnimatorClipInfoCount(layer);
            if (count <= 0)
            {
                return false;
            }

            var clips = animator.GetCurrentAnimatorClipInfo(layer);
            for (int i = 0; i < clips.Length; i++)
            {
                AnimationClip clip = clips[i].clip;
                if (clip == null || string.IsNullOrEmpty(clip.name))
                {
                    continue;
                }

                string n = clip.name;
                if (n.IndexOf("SkillR", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("skill_r", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("Skill_R", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        void ReturnFromSkill()
        {
            if (Ctx.IsInWater)
            {
                GoTo(Ctx.Owner.States.Swim.Idle);
            }
            else if (Ctx.Motor.IsGrounded)
            {
                GoTo(Ctx.Owner.States.Grounded.Idle);
            }
            else
            {
                GoTo(Ctx.Owner.States.Airborne.Fall);
            }
        }
    }
}
