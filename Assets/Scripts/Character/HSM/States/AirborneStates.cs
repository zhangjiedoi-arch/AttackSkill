using AttackSkill.Character.Exploration;
using UnityEngine;

namespace AttackSkill.Character.HSM
{
    /// <summary>
    /// 空中父状态：Jump / Fall / Glide / WingFlight / SwordFlight。
    /// </summary>
    public class AirborneState : CharacterState
    {
        public readonly JumpState Jump;
        public readonly FallState Fall;
        public readonly GlideState Glide;
        public readonly WingFlightState WingFlight;
        public readonly SwordFlightState SwordFlight;

        public AirborneState(CharacterContext ctx) : base("Airborne", ctx)
        {
            Jump = new JumpState(ctx);
            Fall = new FallState(ctx);
            Glide = new GlideState(ctx);
            WingFlight = new WingFlightState(ctx);
            SwordFlight = new SwordFlightState(ctx);
        }

        public override void OnEnter()
        {
            Ctx.WasAirborne = true;
            Ctx.SetAnimBool(CharacterAnimParams.IsGrounded, false);
            Ctx.SetAnimBool(CharacterAnimParams.IsClimbing, false);
            Ctx.SetAnimBool(CharacterAnimParams.IsSwimming, false);
        }

        public override void OnUpdate(float deltaTime)
        {
            if (Ctx.IsInWater)
            {
                GoTo(Ctx.Owner.States.Swim.Idle);
                return;
            }

            bool inFlightTool = Machine != null &&
                                (Machine.Current == WingFlight || Machine.Current == SwordFlight);

            // 用稳定着地接 Idle：普通下落 / 飞行中贴地都走 Grounded → Land（Jump_Land）
            if (Ctx.Motor.IsGroundedRaw && Ctx.Motor.Velocity.y <= 0.5f)
            {
                Ctx.Motor.Velocity.y = Ctx.Settings.GroundStickForce;
                GoTo(Ctx.Owner.States.Grounded.Idle);
                return;
            }

            // 翅膀 / 御剑空中：不攀爬、不放技能（贴地已在上方处理）
            if (inFlightTool)
            {
                return;
            }

            if (Ctx.Input.InteractPressed && Ctx.IsNearClimbable)
            {
                GoTo(Ctx.Owner.States.Climb);
                return;
            }

            // 空中也可放技能（简化：仅技能）
            if (Ctx.Input.SkillPressed)
            {
                GoTo(Ctx.Owner.States.Combat.Skill);
            }
        }
    }

    public class JumpState : CharacterState
    {
        public JumpState(CharacterContext ctx) : base("Jump", ctx) { }

        public override void OnEnter()
        {
            Ctx.Motor.Jump();
            Ctx.SetAnimBool(CharacterAnimParams.IsFalling, false);
            Ctx.SetAnimTrigger(CharacterAnimParams.Jump);
            Ctx.SetAnimBool(CharacterAnimParams.IsGliding, false);
            Ctx.Audio?.PlayJump();
        }

        public override void OnUpdate(float deltaTime)
        {
            if (Ctx.Motor.Velocity.y <= 0f)
            {
                GoTo(Ctx.Owner.States.Airborne.Fall);
                return;
            }

            TryEnterGlide();
        }

        public override void OnFixedUpdate(float deltaTime)
        {
            AirMove(deltaTime, Ctx.Settings.Gravity);
        }

        void TryEnterGlide()
        {
            if (Ctx.CanGlide && Ctx.Input.GlidePressed)
            {
                GoTo(Ctx.Owner.States.Airborne.Glide);
            }
        }

        void AirMove(float deltaTime, float gravity)
        {
            Ctx.Motor.SetPlanarFromInput(
                Ctx.Input.Move,
                Ctx.CameraYaw,
                Ctx.Settings.RunSpeed,
                deltaTime,
                Ctx.Settings.AirControl);
            Ctx.Motor.FacePlanarVelocity(Ctx.Transform, deltaTime);
            Ctx.Motor.ApplyGravity(deltaTime, gravity);
            Ctx.Motor.TickMove(deltaTime);
            Ctx.SetAnimFloat(CharacterAnimParams.Speed, Ctx.Motor.PlanarVelocity.magnitude);
            Ctx.SetAnimFloat(CharacterAnimParams.VerticalSpeed, Ctx.Motor.Velocity.y);
        }
    }

    public class FallState : CharacterState
    {
        public FallState(CharacterContext ctx) : base("Fall", ctx) { }

        public override void OnEnter()
        {
            Ctx.SetAnimBool(CharacterAnimParams.IsGliding, false);
            Ctx.SetAnimBool(CharacterAnimParams.IsFalling, true);
            Ctx.Audio?.BeginFallLoop();
        }

        public override void OnExit()
        {
            Ctx.SetAnimBool(CharacterAnimParams.IsFalling, false);
            Ctx.Audio?.EndFallLoop();
        }

        public override void OnUpdate(float deltaTime)
        {
            if (Ctx.CanGlide && Ctx.Input.GlidePressed)
            {
                GoTo(Ctx.Owner.States.Airborne.Glide);
            }
        }

        public override void OnFixedUpdate(float deltaTime)
        {
            float g = Ctx.Settings.Gravity * Ctx.Settings.FallGravityMultiplier;
            Ctx.Motor.SetPlanarFromInput(
                Ctx.Input.Move,
                Ctx.CameraYaw,
                Ctx.Settings.RunSpeed,
                deltaTime,
                Ctx.Settings.AirControl);
            Ctx.Motor.FacePlanarVelocity(Ctx.Transform, deltaTime);
            Ctx.Motor.ApplyGravity(deltaTime, g);
            Ctx.Motor.TickMove(deltaTime);
            Ctx.SetAnimFloat(CharacterAnimParams.VerticalSpeed, Ctx.Motor.Velocity.y);
        }
    }

    public class GlideState : CharacterState
    {
        public GlideState(CharacterContext ctx) : base("Glide", ctx) { }

        public override void OnEnter()
        {
            Ctx.Motor.Velocity.y = Mathf.Min(Ctx.Motor.Velocity.y, 0f);
            Ctx.SetAnimBool(CharacterAnimParams.IsGliding, true);
            Ctx.SetAnimTrigger(CharacterAnimParams.Glide);
        }

        public override void OnExit()
        {
            Ctx.SetAnimBool(CharacterAnimParams.IsGliding, false);
        }

        public override void OnUpdate(float deltaTime)
        {
            // 再按 F 取消滑翔
            if (Ctx.Input.GlidePressed)
            {
                GoTo(Ctx.Owner.States.Airborne.Fall);
            }
        }

        public override void OnFixedUpdate(float deltaTime)
        {
            Ctx.Motor.SetPlanarFromInput(
                Ctx.Input.Move,
                Ctx.CameraYaw,
                Ctx.Settings.GlideSpeed,
                deltaTime,
                1f);
            Ctx.Motor.FacePlanarVelocity(Ctx.Transform, deltaTime);
            Ctx.Motor.ApplyGravity(deltaTime, Ctx.Settings.GlideGravity);
            // 限制下落速度
            Ctx.Motor.Velocity.y = Mathf.Max(Ctx.Motor.Velocity.y, Ctx.Settings.GlideGravity);
            Ctx.Motor.TickMove(deltaTime);
            Ctx.SetAnimFloat(CharacterAnimParams.VerticalSpeed, Ctx.Motor.Velocity.y);
        }
    }

    /// <summary>
    /// 翅膀飞行薄壳：转发 <see cref="WingFlightTool"/>。
    /// </summary>
    public class WingFlightState : CharacterState
    {
        public WingFlightState(CharacterContext ctx) : base("WingFlight", ctx)
        {
        }

        WingFlightTool Tool =>
            Ctx.Owner != null && Ctx.Owner.ExplorationTools != null
                ? Ctx.Owner.ExplorationTools.WingFlight
                : null;

        ExplorationToolContext ToolCtx =>
            Ctx.Owner != null && Ctx.Owner.ExplorationTools != null
                ? Ctx.Owner.ExplorationTools.BuildContext()
                : new ExplorationToolContext(Ctx.Owner, Ctx);

        public override void OnEnter()
        {
            var tools = Ctx.Owner != null ? Ctx.Owner.ExplorationTools : null;
            var def = tools != null ? tools.ConsumePendingDefinition() : null;
            var tool = Tool;
            if (tool == null)
            {
                GoTo(Ctx.Owner.States.Airborne.Fall);
                return;
            }

            tool.Activate(ToolCtx, def);
        }

        public override void OnExit()
        {
            Tool?.Deactivate(ToolCtx);
        }

        public override void OnUpdate(float deltaTime)
        {
            if (AerialExplorationFlightMotion.WantsExitByAttack(ToolCtx))
            {
                Ctx.Owner?.TryToggleExplorationTool(Tool, null);
                return;
            }

            Tool?.OnUpdate(ToolCtx, deltaTime);
        }

        public override void OnFixedUpdate(float deltaTime)
        {
            Tool?.OnFixedUpdate(ToolCtx, deltaTime);
        }
    }

    /// <summary>
    /// 御剑飞行薄壳：转发 <see cref="SwordFlightTool"/>。
    /// </summary>
    public class SwordFlightState : CharacterState
    {
        public SwordFlightState(CharacterContext ctx) : base("SwordFlight", ctx)
        {
        }

        SwordFlightTool Tool =>
            Ctx.Owner != null && Ctx.Owner.ExplorationTools != null
                ? Ctx.Owner.ExplorationTools.SwordFlight
                : null;

        ExplorationToolContext ToolCtx =>
            Ctx.Owner != null && Ctx.Owner.ExplorationTools != null
                ? Ctx.Owner.ExplorationTools.BuildContext()
                : new ExplorationToolContext(Ctx.Owner, Ctx);

        public override void OnEnter()
        {
            var tools = Ctx.Owner != null ? Ctx.Owner.ExplorationTools : null;
            var def = tools != null ? tools.ConsumePendingDefinition() : null;
            var tool = Tool;
            if (tool == null)
            {
                GoTo(Ctx.Owner.States.Airborne.Fall);
                return;
            }

            tool.Activate(ToolCtx, def);
        }

        public override void OnExit()
        {
            Tool?.Deactivate(ToolCtx);
        }

        public override void OnUpdate(float deltaTime)
        {
            if (AerialExplorationFlightMotion.WantsExitByAttack(ToolCtx))
            {
                Ctx.Owner?.TryToggleExplorationTool(Tool, null);
                return;
            }

            Tool?.OnUpdate(ToolCtx, deltaTime);
        }

        public override void OnFixedUpdate(float deltaTime)
        {
            Tool?.OnFixedUpdate(ToolCtx, deltaTime);
        }
    }
}
