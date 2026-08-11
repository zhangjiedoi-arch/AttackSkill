using UnityEngine;

namespace AttackSkill.Character.HSM
{
    /// <summary>
    /// 战斗父状态：普攻连段 / 元素战技（时序简化）。
    /// </summary>
    public class CombatState : CharacterState
    {
        public readonly AttackState Attack;
        public readonly SkillState Skill;

        public CombatState(CharacterContext ctx) : base("Combat", ctx)
        {
            Attack = new AttackState(ctx);
            Skill = new SkillState(ctx);
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

        public override void OnExit()
        {
            // 离开普攻（含连段间隙会马上再 OnEnter 显示；进 Skill/Idle 则保持隐藏）
            Ctx.AttackHits?.SetWeaponVisible(false);
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

            // E 技能：只走 Animator 状态 skill（Trigger），不再播 Timeline
            _elapsed = 0f;
            _durationSynced = false;
            _duration = Mathf.Max(0.1f, Ctx.Settings.SkillDuration);

            if (Ctx.SkillPlayer != null && Ctx.SkillPlayer.IsPlaying)
            {
                Ctx.SkillPlayer.Stop();
            }

            // 允许动画 Event（Hit_Chest_* / Hit_Root）出伤
            if (Ctx.AttackHits != null)
            {
                Ctx.AttackHits.SuppressAnimHits = false;
            }

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
}
