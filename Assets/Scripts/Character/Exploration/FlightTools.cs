using AttackSkill.Character.HSM;
using UnityEngine;

namespace AttackSkill.Character.Exploration
{
    /// <summary>
    /// 翅膀 / 御剑共用空中移动：
    /// W/S 俯仰飞升/俯冲，A/D 左右斜飞，Shift 加速上升；空格不再上升。
    /// </summary>
    public static class AerialExplorationFlightMotion
    {
        const float VelocityAccel = 30f;

        public static void Tick(
            in ExplorationToolContext ctx,
            float deltaTime,
            WingFlightAirflowVfx airflow,
            FlightVisualTilt tilt)
        {
            if (ctx.Motor == null || ctx.Settings == null || ctx.Transform == null)
            {
                return;
            }

            var settings = ctx.Settings;
            const float tiltMax = 45f;
            float pitchMax = tiltMax;
            float bankMax = tiltMax;
            float pitchInput = Mathf.Clamp(ctx.Input.Move.y, -1f, 1f);
            float bankInput = Mathf.Clamp(ctx.Input.Move.x, -1f, 1f);
            bool shiftUp = ctx.Input.SprintHeld;

            float targetPitch = -pitchInput * pitchMax;
            float targetBank = -bankInput * bankMax;

            Transform yaw = ctx.CameraYaw != null ? ctx.CameraYaw : ctx.Transform;
            Vector3 camFwd = Vector3.ProjectOnPlane(yaw.forward, Vector3.up);
            Vector3 camRight = Vector3.ProjectOnPlane(yaw.right, Vector3.up);
            if (camFwd.sqrMagnitude < 0.0001f)
            {
                camFwd = Vector3.forward;
            }
            else
            {
                camFwd.Normalize();
            }

            if (camRight.sqrMagnitude < 0.0001f)
            {
                camRight = Vector3.right;
            }
            else
            {
                camRight.Normalize();
            }

            Vector3 wish = Vector3.zero;

            // W/S：按倾斜角向前 + 上/下
            if (Mathf.Abs(pitchInput) > 0.01f)
            {
                float pitchRad = pitchInput * pitchMax * Mathf.Deg2Rad;
                float horiz = Mathf.Cos(pitchRad);
                float vert = Mathf.Sin(pitchRad);
                wish += camFwd * horiz;
                wish += Vector3.up * vert;
            }

            // A/D：左右斜向（侧移 + 略向前）
            if (Mathf.Abs(bankInput) > 0.01f)
            {
                float bankRad = Mathf.Abs(bankInput) * bankMax * Mathf.Deg2Rad;
                float side = Mathf.Cos(bankRad * 0.35f);
                wish += camRight * (bankInput * side);
                wish += camFwd * (0.4f * Mathf.Abs(bankInput));
            }

            // Shift：加速向上
            if (shiftUp)
            {
                wish += Vector3.up;
            }

            float speed = settings.WingFlightSpeed;
            if (shiftUp)
            {
                speed *= Mathf.Max(1f, settings.WingFlightBoostMultiplier);
                speed = Mathf.Max(speed, settings.WingAscendSpeed);
            }

            if (wish.sqrMagnitude > 0.01f)
            {
                wish.Normalize();
                Vector3 targetVel = wish * speed;
                ctx.Motor.Velocity = Vector3.MoveTowards(
                    ctx.Motor.Velocity,
                    targetVel,
                    VelocityAccel * deltaTime);
                ctx.Motor.PlanarVelocity = new Vector3(ctx.Motor.Velocity.x, 0f, ctx.Motor.Velocity.z);
            }
            else
            {
                ctx.Motor.PlanarVelocity = Vector3.MoveTowards(
                    ctx.Motor.PlanarVelocity,
                    Vector3.zero,
                    settings.WingFlightSpeed * 1.5f * deltaTime);
                ctx.Motor.Velocity.x = ctx.Motor.PlanarVelocity.x;
                ctx.Motor.Velocity.z = ctx.Motor.PlanarVelocity.z;
                ctx.Motor.ApplyGravity(deltaTime, settings.WingFlightGravity);
                ctx.Motor.Velocity.y = Mathf.Max(ctx.Motor.Velocity.y, settings.WingMinFallSpeed);
            }

            Vector3 face = new Vector3(ctx.Motor.Velocity.x, 0f, ctx.Motor.Velocity.z);
            if (face.sqrMagnitude > 0.05f)
            {
                Quaternion target = Quaternion.LookRotation(face.normalized, Vector3.up);
                ctx.Transform.rotation = Quaternion.Slerp(
                    ctx.Transform.rotation,
                    target,
                    settings.RotateSpeed * deltaTime);
            }
            else
            {
                Quaternion target = Quaternion.LookRotation(camFwd, Vector3.up);
                ctx.Transform.rotation = Quaternion.Slerp(
                    ctx.Transform.rotation,
                    target,
                    settings.RotateSpeed * 0.6f * deltaTime);
            }

            ctx.Motor.TickMove(deltaTime);
            float planarSpeed = ctx.Motor.PlanarVelocity.magnitude;
            ctx.SetAnimFloat(CharacterAnimParams.Speed, planarSpeed);
            ctx.SetAnimFloat(CharacterAnimParams.VerticalSpeed, ctx.Motor.Velocity.y);

            float fullSpeed = Mathf.Max(0.01f, settings.WingAirflowFullSpeed);
            float strength = Mathf.Clamp01(ctx.Motor.Velocity.magnitude / fullSpeed);
            if (shiftUp || pitchInput > 0.2f)
            {
                strength = Mathf.Max(strength, 0.55f);
            }

            airflow?.SetStrength(strength);
            tilt?.Tick(targetPitch, targetBank, settings, deltaTime);
        }

        public static FlightVisualTilt EnsureTilt(in ExplorationToolContext ctx)
        {
            CharacterAvatar avatar = ctx.Owner != null ? ctx.Owner.Avatar : null;
            return FlightVisualTilt.Ensure(ctx.Transform, avatar);
        }

        public static void ResetTilt(FlightVisualTilt tilt, CharacterMotorSettings settings)
        {
            tilt?.ResetTilt(immediate: true, settings, 0f);
        }

        public static bool WantsExitByAttack(in ExplorationToolContext ctx) =>
            ctx.Character != null && ctx.Input.AttackPressed;
    }

    /// <summary>翅膀飞行：挂点 + glide 动画 + 气流 + 空中移动。</summary>
    public sealed class WingFlightTool : IExplorationTool
    {
        bool _active;
        WingFlightAirflowVfx _airflow;
        FlightVisualTilt _tilt;

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

            _tilt = AerialExplorationFlightMotion.EnsureTilt(ctx);

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
            AerialExplorationFlightMotion.ResetTilt(_tilt, ctx.Settings);
            _tilt = null;

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

            ctx.Audio?.TickWingFlight(ctx.Input.SprintHeld);
        }

        public void OnFixedUpdate(in ExplorationToolContext ctx, float deltaTime)
        {
            if (!_active)
            {
                return;
            }

            AerialExplorationFlightMotion.Tick(ctx, deltaTime, _airflow, _tilt);
        }
    }

    /// <summary>御剑飞行：挂脆刃 + 气流 + 共用空中移动。</summary>
    public sealed class SwordFlightTool : IExplorationTool
    {
        bool _active;
        WingFlightAirflowVfx _airflow;
        FlightVisualTilt _tilt;

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

            _tilt = AerialExplorationFlightMotion.EnsureTilt(ctx);

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
            AerialExplorationFlightMotion.ResetTilt(_tilt, ctx.Settings);
            _tilt = null;

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

            ctx.Audio?.TickSwordFlight(ctx.Input.SprintHeld);
        }

        public void OnFixedUpdate(in ExplorationToolContext ctx, float deltaTime)
        {
            if (!_active)
            {
                return;
            }

            AerialExplorationFlightMotion.Tick(ctx, deltaTime, _airflow, _tilt);
        }
    }
}
