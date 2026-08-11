using UnityEngine;

namespace AttackSkill.Character.HSM
{
    /// <summary>
    /// 攀爬：贴墙上下左右移动（简化版）。
    /// </summary>
    public class ClimbState : CharacterState
    {
        public ClimbState(CharacterContext ctx) : base("Climb", ctx) { }

        public override void OnEnter()
        {
            Ctx.Motor.Velocity = Vector3.zero;
            Ctx.Motor.PlanarVelocity = Vector3.zero;
            Ctx.SetAnimBool(CharacterAnimParams.IsClimbing, true);
            Ctx.SetAnimBool(CharacterAnimParams.IsGrounded, false);
            Ctx.SetAnimBool(CharacterAnimParams.IsGliding, false);
            Ctx.SetAnimTrigger(CharacterAnimParams.Climb);
        }

        public override void OnExit()
        {
            Ctx.SetAnimBool(CharacterAnimParams.IsClimbing, false);
        }

        public override void OnUpdate(float deltaTime)
        {
            if (Ctx.Input.JumpPressed)
            {
                // 蹬墙跳离
                Vector3 push = Ctx.ClimbNormal.normalized * 4f + Vector3.up * 3f;
                Ctx.Motor.Velocity = push;
                Ctx.CanGlide = true;
                GoTo(Ctx.Owner.States.Airborne.Fall);
                return;
            }

            if (!Ctx.IsNearClimbable)
            {
                GoTo(Ctx.Owner.States.Airborne.Fall);
            }
        }

        public override void OnFixedUpdate(float deltaTime)
        {
            Vector2 m = Ctx.Input.Move;
            Vector3 up = Vector3.up * m.y;
            Vector3 right = Vector3.Cross(Vector3.up, Ctx.ClimbNormal).normalized * m.x;
            Vector3 climbVel = (up + right) * Ctx.Settings.ClimbSpeed;

            // 轻微贴墙
            climbVel += -Ctx.ClimbNormal.normalized * 1.5f;
            Ctx.Motor.Velocity = climbVel;
            Ctx.Motor.TickMove(deltaTime);
            Ctx.SetAnimFloat(CharacterAnimParams.Speed, new Vector2(m.x, m.y).magnitude * Ctx.Settings.ClimbSpeed);

            if (Ctx.ClimbNormal.sqrMagnitude > 0.01f)
            {
                Quaternion look = Quaternion.LookRotation(-Ctx.ClimbNormal, Vector3.up);
                Ctx.Transform.rotation = Quaternion.Slerp(Ctx.Transform.rotation, look, Ctx.Settings.RotateSpeed * deltaTime);
            }
        }
    }

    public class SwimState : CharacterState
    {
        public readonly SwimIdleState Idle;
        public readonly SwimMoveState Move;

        public SwimState(CharacterContext ctx) : base("Swim", ctx)
        {
            Idle = new SwimIdleState(ctx);
            Move = new SwimMoveState(ctx);
        }

        public override void OnEnter()
        {
            Ctx.Motor.Velocity.y = 0f;
            Ctx.SetAnimBool(CharacterAnimParams.IsSwimming, true);
            Ctx.SetAnimBool(CharacterAnimParams.IsGrounded, false);
            Ctx.SetAnimBool(CharacterAnimParams.IsGliding, false);
            Ctx.SetAnimBool(CharacterAnimParams.IsClimbing, false);
        }

        public override void OnExit()
        {
            Ctx.SetAnimBool(CharacterAnimParams.IsSwimming, false);
        }

        public override void OnUpdate(float deltaTime)
        {
            if (!Ctx.IsInWater)
            {
                GoTo(Ctx.Motor.IsGrounded
                    ? Ctx.Owner.States.Grounded.Idle
                    : (HState)Ctx.Owner.States.Airborne.Fall);
            }
        }
    }

    public class SwimIdleState : CharacterState
    {
        public SwimIdleState(CharacterContext ctx) : base("SwimIdle", ctx) { }

        public override void OnUpdate(float deltaTime)
        {
            if (Ctx.Input.HasMove)
            {
                GoTo(Ctx.Owner.States.Swim.Move);
            }
        }

        public override void OnFixedUpdate(float deltaTime)
        {
            Ctx.Motor.SetPlanarFromInput(Vector2.zero, Ctx.CameraYaw, 0f, deltaTime);
            Ctx.Motor.ApplyGravity(deltaTime, Ctx.Settings.SwimGravity);
            Ctx.Motor.Velocity.y = Mathf.Max(Ctx.Motor.Velocity.y, Ctx.Settings.SwimGravity);
            Ctx.Motor.TickMove(deltaTime);
            Ctx.SetAnimFloat(CharacterAnimParams.Speed, 0f);
        }
    }

    public class SwimMoveState : CharacterState
    {
        public SwimMoveState(CharacterContext ctx) : base("SwimMove", ctx) { }

        public override void OnUpdate(float deltaTime)
        {
            if (!Ctx.Input.HasMove)
            {
                GoTo(Ctx.Owner.States.Swim.Idle);
            }
        }

        public override void OnFixedUpdate(float deltaTime)
        {
            Ctx.Motor.SetPlanarFromInput(Ctx.Input.Move, Ctx.CameraYaw, Ctx.Settings.SwimSpeed, deltaTime);
            Ctx.Motor.FacePlanarVelocity(Ctx.Transform, deltaTime);
            float vertical = 0f;
            if (Ctx.Input.JumpHeld)
            {
                vertical = Ctx.Settings.SwimSpeed;
            }

            Ctx.Motor.Velocity.y = vertical;
            Ctx.Motor.TickMove(deltaTime);
            Ctx.SetAnimFloat(CharacterAnimParams.Speed, Ctx.Motor.PlanarVelocity.magnitude);
        }
    }
}
