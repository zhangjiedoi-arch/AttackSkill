using UnityEngine;

namespace AttackSkill.Character.HSM
{
    /// <summary>
    /// 地面闪避：右键触发，短位移，期间锁输入。
    /// Animator：Trigger Dodge / Bool IsDodging / Float DodgeX、DodgeZ（相机水平相对方向）。
    /// </summary>
    public class DodgeState : CharacterState
    {
        float _timer;
        Vector3 _dodgeDir;

        public DodgeState(CharacterContext ctx) : base("Dodge", ctx) { }

        public override void OnEnter()
        {
            Ctx.LastDodgeTime = Time.time;
            _timer = Mathf.Max(0.05f, Ctx.Settings.DodgeDuration);
            _dodgeDir = ResolveDodgeDirection();

            // 相机空间水平方向 → 供 2D Blend Tree（Freeform Directional）
            Vector3 camForward = Ctx.CameraYaw != null
                ? Vector3.ProjectOnPlane(Ctx.CameraYaw.forward, Vector3.up).normalized
                : Vector3.forward;
            Vector3 camRight = Ctx.CameraYaw != null
                ? Vector3.ProjectOnPlane(Ctx.CameraYaw.right, Vector3.up).normalized
                : Vector3.right;
            float dodgeX = Vector3.Dot(_dodgeDir, camRight);
            float dodgeZ = Vector3.Dot(_dodgeDir, camForward);

            Ctx.SetAnimBool(CharacterAnimParams.IsDodging, true);
            Ctx.SetAnimFloat(CharacterAnimParams.DodgeX, dodgeX);
            Ctx.SetAnimFloat(CharacterAnimParams.DodgeZ, dodgeZ);
            Ctx.SetAnimTrigger(CharacterAnimParams.Dodge);

            Ctx.AttackHits?.SetWeaponVisible(false);
            Ctx.SetAnimBool(CharacterAnimParams.InCombatAction, false);
            Ctx.SetAnimInt(CharacterAnimParams.AttackCombo, -1);

            Ctx.Motor.PlanarVelocity = _dodgeDir * Ctx.Settings.DodgeSpeed;
            Ctx.Motor.Velocity.x = Ctx.Motor.PlanarVelocity.x;
            Ctx.Motor.Velocity.z = Ctx.Motor.PlanarVelocity.z;
            Ctx.Motor.Velocity.y = Mathf.Min(Ctx.Motor.Velocity.y, 0f);
        }

        public override void OnExit()
        {
            Ctx.SetAnimBool(CharacterAnimParams.IsDodging, false);
            Ctx.ResetAnimTrigger(CharacterAnimParams.Dodge);

            Ctx.Motor.PlanarVelocity *= 0.35f;
            Ctx.Motor.Velocity.x = Ctx.Motor.PlanarVelocity.x;
            Ctx.Motor.Velocity.z = Ctx.Motor.PlanarVelocity.z;
        }

        public override void OnUpdate(float deltaTime)
        {
            _timer -= deltaTime;
            if (_timer > 0f)
            {
                return;
            }

            if (Ctx.Input.HasMove)
            {
                GoTo(Ctx.Input.SprintHeld ? Ctx.Owner.States.Grounded.Sprint : (HState)Ctx.Owner.States.Grounded.Move);
            }
            else
            {
                GoTo(Ctx.Owner.States.Grounded.Idle);
            }
        }

        public override void OnFixedUpdate(float deltaTime)
        {
            Ctx.Motor.PlanarVelocity = _dodgeDir * Ctx.Settings.DodgeSpeed;
            Ctx.Motor.Velocity.x = Ctx.Motor.PlanarVelocity.x;
            Ctx.Motor.Velocity.z = Ctx.Motor.PlanarVelocity.z;
            Ctx.Motor.FacePlanarVelocity(Ctx.Transform, deltaTime);
            Ctx.Motor.ApplyGravity(deltaTime, Ctx.Settings.Gravity);
            Ctx.Motor.TickMove(deltaTime);
            Ctx.SetAnimFloat(CharacterAnimParams.Speed, Ctx.Motor.PlanarVelocity.magnitude);
        }

        Vector3 ResolveDodgeDirection()
        {
            Vector3 forward = Ctx.CameraYaw != null
                ? Vector3.ProjectOnPlane(Ctx.CameraYaw.forward, Vector3.up).normalized
                : Vector3.forward;
            Vector3 right = Ctx.CameraYaw != null
                ? Vector3.ProjectOnPlane(Ctx.CameraYaw.right, Vector3.up).normalized
                : Vector3.right;

            if (Ctx.Input.HasMove)
            {
                Vector3 dir = (forward * Ctx.Input.Move.y + right * Ctx.Input.Move.x);
                if (dir.sqrMagnitude > 0.001f)
                {
                    return dir.normalized;
                }
            }

            // 无方向输入：向角色背后闪（后撤）
            Vector3 back = -Vector3.ProjectOnPlane(Ctx.Transform.forward, Vector3.up);
            if (back.sqrMagnitude < 0.001f)
            {
                back = -forward;
            }

            return back.normalized;
        }
    }
}
