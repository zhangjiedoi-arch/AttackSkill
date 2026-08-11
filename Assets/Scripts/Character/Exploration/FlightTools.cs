using AttackSkill.Character.HSM;
using UnityEngine;

namespace AttackSkill.Character.Exploration
{
    /// <summary>翅膀 / 御剑共用空中移动与气流强度。</summary>
    public static class AerialExplorationFlightMotion
    {
        public static void Tick(in ExplorationToolContext ctx, float deltaTime, WingFlightAirflowVfx airflow)
        {
            if (ctx.Motor == null || ctx.Settings == null || ctx.Transform == null)
            {
                return;
            }

            var settings = ctx.Settings;
            float speed = settings.WingFlightSpeed;
            ctx.Motor.SetPlanarFromInput(
                ctx.Input.Move,
                ctx.CameraYaw,
                speed,
                deltaTime,
                1f);
            ctx.Motor.FacePlanarVelocity(ctx.Transform, deltaTime);

            if (ctx.Input.JumpHeld)
            {
                ctx.Motor.Velocity.y = Mathf.MoveTowards(
                    ctx.Motor.Velocity.y,
                    settings.WingAscendSpeed,
                    settings.WingAscendSpeed * 3f * deltaTime);
            }
            else
            {
                ctx.Motor.ApplyGravity(deltaTime, settings.WingFlightGravity);
                ctx.Motor.Velocity.y = Mathf.Max(ctx.Motor.Velocity.y, settings.WingMinFallSpeed);
            }

            ctx.Motor.TickMove(deltaTime);
            float planarSpeed = ctx.Motor.PlanarVelocity.magnitude;
            ctx.SetAnimFloat(CharacterAnimParams.Speed, planarSpeed);
            ctx.SetAnimFloat(CharacterAnimParams.VerticalSpeed, ctx.Motor.Velocity.y);

            float fullSpeed = Mathf.Max(0.01f, settings.WingAirflowFullSpeed);
            float strength = Mathf.Clamp01(planarSpeed / fullSpeed);
            if (ctx.Input.JumpHeld)
            {
                strength = Mathf.Max(strength, 0.55f);
            }

            airflow?.SetStrength(strength);
        }
    }

    /// <summary>翅膀飞行：挂点 + glide 动画 + 气流 + 空中移动。</summary>
    public sealed class WingFlightTool : IExplorationTool
    {
        bool _active;
        WingFlightAirflowVfx _airflow;

        public ExplorationToolKind Kind => ExplorationToolKind.WingFlight;
        public bool IsActive => _active;
        public bool BlocksSkillWheelWhenActive => true;

        public bool CanActivate(in ExplorationToolContext ctx, ExplorationToolDefinition definition)
        {
            if (ctx.Owner == null || ctx.Motor == null)
            {
                return false;
            }

            if (definition != null && definition.RequiresGroundToActivate && !ctx.Motor.IsGroundedRaw)
            {
                return false;
            }

            return true;
        }

        public void Activate(in ExplorationToolContext ctx, ExplorationToolDefinition definition)
        {
            _active = true;
            ctx.SetAnimBool(CharacterAnimParams.IsFalling, false);
            ctx.SetAnimBool(CharacterAnimParams.IsSwordFlying, false);
            ctx.SetAnimBool(CharacterAnimParams.IsRidingMotorcycle, false);
            ctx.SetAnimBool(CharacterAnimParams.IsGliding, true);
            ctx.SetAnimTrigger(CharacterAnimParams.Glide);
            ctx.Motor?.ForceUnground(0.25f);

            if (ctx.Transform != null)
            {
                CharacterToolAttach.ShowWings(ctx.Transform);
                Transform wingsSocket = CharacterToolAttach.GetWingsSocket(ctx.Transform);
                _airflow = WingFlightAirflowVfx.Ensure(ctx.Transform);
                _airflow?.ShowForWingFlight(ctx.Transform, wingsSocket);
            }

            bool playTakeOff = ctx.Owner?.ExplorationTools == null ||
                               !ctx.Owner.ExplorationTools.SuppressEnterSfx;
            ctx.Audio?.BeginWingFlight(playTakeOff);
        }

        public void Deactivate(in ExplorationToolContext ctx)
        {
            _active = false;
            ctx.SetAnimBool(CharacterAnimParams.IsGliding, false);

            if (ctx.Transform != null)
            {
                CharacterToolAttach.HideWings(ctx.Transform);
            }

            _airflow?.Hide();
            _airflow = null;

            // 空中退出交给 Fall（Jump_Loop）；仅贴地取消时播飞行落地音
            bool grounded = ctx.Motor != null && ctx.Motor.IsGroundedRaw;
            bool playLand = grounded &&
                            (ctx.Owner?.ExplorationTools == null ||
                             !ctx.Owner.ExplorationTools.SuppressEnterSfx);
            ctx.Audio?.EndWingFlight(playLand);
        }

        public void OnUpdate(in ExplorationToolContext ctx, float deltaTime)
        {
            if (!_active)
            {
                return;
            }

            ctx.Audio?.TickWingFlight(ctx.Input.JumpHeld);
        }

        public void OnFixedUpdate(in ExplorationToolContext ctx, float deltaTime)
        {
            if (!_active)
            {
                return;
            }

            AerialExplorationFlightMotion.Tick(ctx, deltaTime, _airflow);
        }
    }

    /// <summary>御剑飞行：挂脆刃 + 气流 + 共用空中移动。</summary>
    public sealed class SwordFlightTool : IExplorationTool
    {
        bool _active;
        WingFlightAirflowVfx _airflow;

        public ExplorationToolKind Kind => ExplorationToolKind.SwordFlight;
        public bool IsActive => _active;
        public bool BlocksSkillWheelWhenActive => true;

        public bool CanActivate(in ExplorationToolContext ctx, ExplorationToolDefinition definition)
        {
            if (ctx.Owner == null || ctx.Motor == null)
            {
                return false;
            }

            if (definition != null && definition.RequiresGroundToActivate && !ctx.Motor.IsGroundedRaw)
            {
                return false;
            }

            return true;
        }

        public void Activate(in ExplorationToolContext ctx, ExplorationToolDefinition definition)
        {
            _active = true;
            ctx.SetAnimBool(CharacterAnimParams.IsFalling, false);
            ctx.SetAnimBool(CharacterAnimParams.IsGliding, false);
            ctx.SetAnimBool(CharacterAnimParams.IsRidingMotorcycle, false);
            ctx.SetAnimBool(CharacterAnimParams.IsSwordFlying, true);
            ctx.Motor?.ForceUnground(0.25f);

            Transform attachRoot = ctx.Owner != null ? ctx.Owner.transform : ctx.Transform;
            if (attachRoot != null)
            {
                CharacterToolAttach.ShowSword(attachRoot);
            }

            if (ctx.Transform != null)
            {
                Transform socketRoot = attachRoot != null ? attachRoot : ctx.Transform;
                Transform swordSocket = CharacterToolAttach.GetSwordSocket(socketRoot);
                _airflow = WingFlightAirflowVfx.Ensure(ctx.Transform);
                _airflow?.ShowForSwordFlight(ctx.Transform, swordSocket);
            }

            bool playTakeOff = ctx.Owner?.ExplorationTools == null ||
                               !ctx.Owner.ExplorationTools.SuppressEnterSfx;
            ctx.Audio?.BeginSwordFlight(playTakeOff);
        }

        public void Deactivate(in ExplorationToolContext ctx)
        {
            _active = false;
            ctx.SetAnimBool(CharacterAnimParams.IsSwordFlying, false);

            Transform attachRoot = ctx.Owner != null ? ctx.Owner.transform : ctx.Transform;
            if (attachRoot != null)
            {
                CharacterToolAttach.HideSword(attachRoot);
            }

            _airflow?.Hide();
            _airflow = null;

            bool grounded = ctx.Motor != null && ctx.Motor.IsGroundedRaw;
            bool playLand = grounded &&
                            (ctx.Owner?.ExplorationTools == null ||
                             !ctx.Owner.ExplorationTools.SuppressEnterSfx);
            ctx.Audio?.EndSwordFlight(playLand);
        }

        public void OnUpdate(in ExplorationToolContext ctx, float deltaTime)
        {
            if (!_active)
            {
                return;
            }

            ctx.Audio?.TickSwordFlight(ctx.Input.JumpHeld);
        }

        public void OnFixedUpdate(in ExplorationToolContext ctx, float deltaTime)
        {
            if (!_active)
            {
                return;
            }

            AerialExplorationFlightMotion.Tick(ctx, deltaTime, _airflow);
        }
    }
}
