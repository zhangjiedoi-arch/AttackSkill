using AttackSkill.Character.HSM;
using UnityEngine;

namespace AttackSkill.Character.Exploration
{
    /// <summary>摩托骑行逻辑（从 HSM MotorcycleState 迁出）。</summary>
    public sealed class MotorcycleTool : IExplorationTool
    {
        const int MaxAirJumps = 2;

        float _speed;
        bool _jumpQueued;
        int _jumpsUsed;
        bool _active;

        public ExplorationToolKind Kind => ExplorationToolKind.Motorcycle;
        public bool IsActive => _active;
        public bool BlocksSkillWheelWhenActive => true;
        public float RideSpeed => _speed;
        public int AirJumpsUsed => _jumpsUsed;
        public int MaxAirJumpsAllowed => MaxAirJumps;

        public void SetRideSpeed(float speed)
        {
            _speed = speed;
        }

        /// <summary>切人续态：恢复车速与空中已用跳跃次数。</summary>
        public void RestoreRideState(float rideSpeed, int airJumpsUsed)
        {
            _speed = rideSpeed;
            _jumpsUsed = Mathf.Clamp(airJumpsUsed, 0, MaxAirJumps);
            _jumpQueued = false;
        }

        public bool CanActivate(in ExplorationToolContext ctx, ExplorationToolDefinition definition)
        {
            if (ctx.Owner == null || ctx.Motor == null)
            {
                return false;
            }

            if ((definition == null || definition.RequiresGroundToActivate) && !ctx.Motor.IsGroundedRaw)
            {
                return false;
            }

            return true;
        }

        public void Activate(in ExplorationToolContext ctx, ExplorationToolDefinition definition)
        {
            _active = true;
            _speed = ctx.Motor != null
                ? Mathf.Max(0f, ctx.Motor.PlanarVelocity.magnitude)
                : 0f;
            _jumpQueued = false;
            _jumpsUsed = 0;

            ctx.SetAnimBool(CharacterAnimParams.IsSprinting, false);
            ctx.SetAnimBool(CharacterAnimParams.IsGliding, false);
            ctx.SetAnimBool(CharacterAnimParams.IsSwordFlying, false);
            ctx.SetAnimBool(CharacterAnimParams.IsRidingMotorcycle, true);
            ctx.SetAnimFloat(CharacterAnimParams.Speed, _speed);

            if (ctx.Transform != null)
            {
                CharacterToolAttach.ShowMotorcycle(ctx.Transform);
            }

            ctx.Audio?.BeginMotorcycle();
        }

        public void Deactivate(in ExplorationToolContext ctx)
        {
            _active = false;
            _speed = 0f;
            _jumpQueued = false;
            _jumpsUsed = 0;

            if (ctx.Motor != null)
            {
                ctx.Motor.PlanarVelocity = Vector3.zero;
            }

            ctx.SetAnimBool(CharacterAnimParams.IsRidingMotorcycle, false);

            if (ctx.Transform != null)
            {
                CharacterToolAttach.HideMotorcycle(ctx.Transform);
            }

            ctx.Audio?.EndMotorcycle();
        }

        public void OnUpdate(in ExplorationToolContext ctx, float deltaTime)
        {
            if (!_active || ctx.Character == null)
            {
                return;
            }

            if (ctx.Input.JumpPressed && _jumpsUsed < MaxAirJumps)
            {
                _jumpQueued = true;
            }

            ctx.Audio?.TickMotorcycle(ctx.Input.Move.y);
        }

        public void OnFixedUpdate(in ExplorationToolContext ctx, float deltaTime)
        {
            if (!_active || ctx.Motor == null || ctx.Settings == null || ctx.Transform == null)
            {
                return;
            }

            var settings = ctx.Settings;
            var motor = ctx.Motor;

            if (_jumpQueued && _jumpsUsed < MaxAirJumps)
            {
                _jumpQueued = false;
                _jumpsUsed++;
                motor.Jump(settings.BikeJumpHeight, 0.35f);
                ctx.SetAnimTrigger(CharacterAnimParams.Jump);
                ctx.SetAnimBool(CharacterAnimParams.IsGrounded, false);
                ctx.Audio?.PlayMotorcycleJump();
            }
            else
            {
                _jumpQueued = false;
            }

            bool airborne = !motor.IsGroundedRaw;
            if (!airborne)
            {
                _jumpsUsed = 0;
            }

            float throttle = ctx.Input.Move.y;
            float steer = ctx.Input.Move.x;
            float control = airborne ? settings.AirControl : 1f;

            if (throttle > 0.05f)
            {
                _speed = Mathf.MoveTowards(
                    _speed,
                    settings.BikeMaxSpeed * throttle,
                    settings.BikeAccel * control * deltaTime);
            }
            else if (throttle < -0.05f)
            {
                float reverseMax = settings.BikeMaxSpeed * 0.35f;
                _speed = Mathf.MoveTowards(
                    _speed,
                    -reverseMax,
                    settings.BikeBrake * control * deltaTime);
            }
            else if (!airborne)
            {
                _speed = Mathf.MoveTowards(_speed, 0f, settings.BikeCoastDecel * deltaTime);
            }

            float speedAbs = Mathf.Abs(_speed);
            float steerFactor = Mathf.Lerp(
                1f,
                settings.BikeHighSpeedSteerFactor,
                Mathf.Clamp01(speedAbs / settings.BikeMaxSpeed));
            if (speedAbs > 0.2f && Mathf.Abs(steer) > 0.01f)
            {
                float yawDelta = steer * settings.BikeSteerDegrees * steerFactor * control * deltaTime;
                if (_speed < 0f)
                {
                    yawDelta = -yawDelta;
                }

                ctx.Transform.Rotate(0f, yawDelta, 0f, Space.World);
            }

            Vector3 planar = ctx.Transform.forward * _speed;
            motor.PlanarVelocity = planar;
            motor.Velocity.x = planar.x;
            motor.Velocity.z = planar.z;

            float gravity = airborne && motor.Velocity.y < 0f
                ? settings.Gravity * settings.FallGravityMultiplier
                : settings.Gravity;
            motor.ApplyGravity(deltaTime, gravity);
            motor.TickMove(deltaTime);

            ctx.SetAnimBool(CharacterAnimParams.IsGrounded, motor.IsGrounded);
            ctx.SetAnimFloat(CharacterAnimParams.Speed, speedAbs);
            ctx.SetAnimFloat(CharacterAnimParams.VerticalSpeed, motor.Velocity.y);
        }
    }
}
