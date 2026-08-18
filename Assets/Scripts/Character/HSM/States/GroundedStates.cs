using UnityEngine;
using AttackSkill.Combat;
using AttackSkill.Rouge;

namespace AttackSkill.Character.HSM
{
    /// <summary>
    /// 地面父状态：共享落地逻辑，子状态 Idle / Move / Sprint / Motorcycle。
    /// </summary>
    public class GroundedState : CharacterState
    {
        public readonly IdleState Idle;
        public readonly MoveState Move;
        public readonly SprintState Sprint;
        public readonly DodgeState Dodge;
        public readonly MotorcycleState Motorcycle;

        public GroundedState(CharacterContext ctx) : base("Grounded", ctx)
        {
            Idle = new IdleState(ctx);
            Move = new MoveState(ctx);
            Sprint = new SprintState(ctx);
            Dodge = new DodgeState(ctx);
            Motorcycle = new MotorcycleState(ctx);
        }

        public override void OnEnter()
        {
            Ctx.CanGlide = true;
            Ctx.SetAnimBool(CharacterAnimParams.IsGrounded, true);
            Ctx.SetAnimBool(CharacterAnimParams.IsGliding, false);
            Ctx.SetAnimBool(CharacterAnimParams.IsSwordFlying, false);
            Ctx.SetAnimBool(CharacterAnimParams.IsClimbing, false);
            Ctx.SetAnimBool(CharacterAnimParams.IsSwimming, false);

            if (Ctx.WasAirborne)
            {
                Ctx.SetAnimBool(CharacterAnimParams.IsFalling, false);
                Ctx.SetAnimTrigger(CharacterAnimParams.Land);
                Ctx.Audio?.PlayLand();
                Ctx.WasAirborne = false;
            }
        }

        public override void OnUpdate(float deltaTime)
        {
            if (Ctx.IsInWater)
            {
                GoTo(Ctx.Owner.States.Swim.Idle);
                return;
            }

            bool onBike = Machine != null && Machine.Current == Motorcycle;

            // 骑乘中保留 Motorcycle 状态（含空中跳跃），不切 Fall；跳跃由 MotorcycleState 处理
            if (onBike)
            {
                return;
            }

            // 真正离地（不含 coyote）且向下掉时才进 Fall，避免站立抖动
            if (!Ctx.Motor.IsGroundedRaw && Ctx.Motor.Velocity.y < -1f && !Ctx.Motor.IsGrounded)
            {
                GoTo(Ctx.Owner.States.Airborne.Fall);
                return;
            }

            if (Ctx.Input.DodgePressed && Ctx.CanDodge)
            {
                GoTo(Dodge);
                return;
            }

            if (Ctx.Input.JumpPressed)
            {
                GoTo(Ctx.Owner.States.Airborne.Jump);
                return;
            }

            if (Ctx.Input.InteractPressed && Ctx.IsNearClimbable)
            {
                GoTo(Ctx.Owner.States.Climb);
                return;
            }

            if (Ctx.Input.AttackPressed)
            {
                GoTo(Ctx.Owner.States.Combat.Attack);
                return;
            }

            if (Ctx.Input.SkillPressed)
            {
                var stats = CombatStats.Find(Ctx.Transform);
                if (stats == null || stats.IsSkillEReady)
                {
                    GoTo(Ctx.Owner.States.Combat.Skill);
                }

                return;
            }

            if (Ctx.Input.SkillRPressed)
            {
                var stats = CombatStats.Find(Ctx.Transform);
                if (stats == null || stats.IsSkillRReady)
                {
                    GoTo(Ctx.Owner.States.Combat.SkillR);
                }
            }
        }
    }

    public class IdleState : CharacterState
    {
        public IdleState(CharacterContext ctx) : base("Idle", ctx) { }

        public override void OnEnter()
        {
            Ctx.Motor.PlanarVelocity = Vector3.zero;
            Ctx.SetAnimFloat(CharacterAnimParams.Speed, 0f);
            Ctx.SetAnimBool(CharacterAnimParams.IsSprinting, false);
        }

        public override void OnUpdate(float deltaTime)
        {
            if (Ctx.Input.HasMove)
            {
                GoTo(Ctx.Input.SprintHeld ? Ctx.Owner.States.Grounded.Sprint : Ctx.Owner.States.Grounded.Move);
            }
        }

        public override void OnFixedUpdate(float deltaTime)
        {
            Ctx.Motor.SetPlanarFromInput(Vector2.zero, Ctx.CameraYaw, 0f, deltaTime);
            Ctx.Motor.ApplyGravity(deltaTime, Ctx.Settings.Gravity);
            Ctx.Motor.TickMove(deltaTime);
        }
    }

    public class MoveState : CharacterState
    {
        public MoveState(CharacterContext ctx) : base("Move", ctx) { }

        public override void OnEnter()
        {
            Ctx.SetAnimBool(CharacterAnimParams.IsSprinting, false);
            Ctx.Audio?.ResetFootstepTimer();
        }

        public override void OnUpdate(float deltaTime)
        {
            if (!Ctx.Input.HasMove)
            {
                GoTo(Ctx.Owner.States.Grounded.Idle);
                return;
            }

            if (Ctx.Input.SprintHeld)
            {
                GoTo(Ctx.Owner.States.Grounded.Sprint);
            }

            Ctx.Audio?.TickFootsteps(deltaTime, false);
        }

        public override void OnFixedUpdate(float deltaTime)
        {
            float speed = Ctx.Input.Move.magnitude > 0.7f ? Ctx.Settings.RunSpeed : Ctx.Settings.WalkSpeed;
            speed *= RougePassiveEffects.EffectiveMoveSpeedMul;
            Ctx.Motor.SetPlanarFromInput(Ctx.Input.Move, Ctx.CameraYaw, speed, deltaTime);
            Ctx.Motor.FacePlanarVelocity(Ctx.Transform, deltaTime);
            Ctx.Motor.ApplyGravity(deltaTime, Ctx.Settings.Gravity);
            Ctx.Motor.TickMove(deltaTime);
            Ctx.SetAnimFloat(CharacterAnimParams.Speed, Ctx.Motor.PlanarVelocity.magnitude);
        }
    }

    public class SprintState : CharacterState
    {
        public SprintState(CharacterContext ctx) : base("Sprint", ctx) { }

        public override void OnEnter()
        {
            Ctx.SetAnimBool(CharacterAnimParams.IsSprinting, true);
            Ctx.Audio?.ResetFootstepTimer();
        }

        public override void OnExit()
        {
            Ctx.SetAnimBool(CharacterAnimParams.IsSprinting, false);
        }

        public override void OnUpdate(float deltaTime)
        {
            if (!Ctx.Input.HasMove)
            {
                GoTo(Ctx.Owner.States.Grounded.Idle);
                return;
            }

            if (!Ctx.Input.SprintHeld)
            {
                GoTo(Ctx.Owner.States.Grounded.Move);
            }

            Ctx.Audio?.TickFootsteps(deltaTime, true);
        }

        public override void OnFixedUpdate(float deltaTime)
        {
            float speed = Ctx.Settings.SprintSpeed * RougePassiveEffects.EffectiveMoveSpeedMul;
            Ctx.Motor.SetPlanarFromInput(Ctx.Input.Move, Ctx.CameraYaw, speed, deltaTime);
            Ctx.Motor.FacePlanarVelocity(Ctx.Transform, deltaTime);
            Ctx.Motor.ApplyGravity(deltaTime, Ctx.Settings.Gravity);
            Ctx.Motor.TickMove(deltaTime);
            Ctx.SetAnimFloat(CharacterAnimParams.Speed, Ctx.Motor.PlanarVelocity.magnitude);
        }
    }
}
