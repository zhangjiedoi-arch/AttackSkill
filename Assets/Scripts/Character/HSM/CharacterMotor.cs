using UnityEngine;

namespace AttackSkill.Character.HSM
{
    [System.Serializable]
    public class CharacterMotorSettings
    {
        public float WalkSpeed = 3.5f;
        public float RunSpeed = 6f;
        public float SprintSpeed = 9f;
        public float RotateSpeed = 12f;
        public float JumpHeight = 1.6f;
        public float Gravity = -25f;
        public float FallGravityMultiplier = 1.4f;
        public float AirControl = 0.7f;
        public float GlideGravity = -3f;
        public float GlideSpeed = 7f;
        public float ClimbSpeed = 3f;
        public float SwimSpeed = 4f;
        public float SwimGravity = -1f;
        public float GroundStickForce = -2f;
        [Tooltip("离地后仍视为着地的宽限时间，防止 Idle/Fall 抖动。")]
        public float CoyoteTime = 0.12f;
        [Tooltip("着地探测额外下探距离。")]
        public float GroundProbeExtra = 0.12f;
        [Tooltip("单段普攻逻辑时长（秒）。应接近动画实际长度；若能读到 Animator 状态长度会优先用动画时长。")]
        public float AttackDuration = 1.0f;
        [Tooltip("可选：三段普攻各自时长，长度>=3 时优先使用；否则用 AttackDuration。")]
        public float[] AttackComboDurations = { 1.0f, 1.0f, 1.1f };
        public float SkillDuration = 2.5f;
        public int MaxAttackCombo = 3;
        [Tooltip("距离上次攻击超过该时间后，连段从第一刀重新开始。")]
        public float ComboResetTime = 1.2f;
        [Tooltip("闪避持续时间（秒），应接近闪避动画长度。")]
        public float DodgeDuration = 0.45f;
        [Tooltip("闪避水平速度。")]
        public float DodgeSpeed = 14f;
        [Tooltip("闪避冷却（秒）。")]
        public float DodgeCooldown = 0.55f;

        [Header("Wing Flight")]
        [Tooltip("翅膀/御剑基础飞行速度。")]
        public float WingFlightSpeed = 14f;
        [Tooltip("Shift 加速上升时的参考上升速度。")]
        public float WingAscendSpeed = 10f;
        [Tooltip("Shift 加速倍率（叠在基础速度上）。")]
        public float WingFlightBoostMultiplier = 1.35f;
        [Tooltip("无输入时的弱重力（接近悬浮）。")]
        public float WingFlightGravity = -1.5f;
        [Tooltip("飞行最低下落速度钳制。")]
        public float WingMinFallSpeed = -6f;
        [Tooltip("地面起飞初速度。")]
        public float WingTakeoffSpeed = 9f;
        [Tooltip("飞行气流：该速度以下按比例减弱。")]
        public float WingAirflowFullSpeed = 30f;
        [Tooltip("W 上升最大抬头角（度）。")]
        public float FlightPitchAscendDegrees = 45f;
        [Tooltip("S 下降最大低头角（度）。")]
        public float FlightPitchDescendDegrees = 45f;
        [Tooltip("A/D 最大侧倾角（度）。")]
        public float FlightBankDegrees = 45f;
        [Tooltip("侧倾参考转向速率（度/秒），越大越不易满倾。")]
        public float FlightBankTurnRateRef = 140f;
        [Tooltip("飞行俯仰/侧倾平滑速度。")]
        public float FlightTiltSmooth = 8f;

        [Header("Motorcycle")]
        [Tooltip("摩托最高前进速度。")]
        public float BikeMaxSpeed = 16f;
        [Tooltip("油门加速度。")]
        public float BikeAccel = 22f;
        [Tooltip("刹车/倒车减速度。")]
        public float BikeBrake = 28f;
        [Tooltip("松油门滑行减速。")]
        public float BikeCoastDecel = 10f;
        [Tooltip("低速时每秒转向角度。")]
        public float BikeSteerDegrees = 110f;
        [Tooltip("高速转向系数（0~1，越小弯越慢）。")]
        public float BikeHighSpeedSteerFactor = 2f;
        [Tooltip("摩托跳跃高度。")]
        public float BikeJumpHeight = 2f;
        [Tooltip("摩托跳跃冷却（秒），不限次数。")]
        public float BikeJumpCooldown = 1f;
        [Tooltip("骑乘时 CharacterController 世界高度（底贴地）。")]
        public float BikeControllerWorldHeight = 1.68f;
        [Tooltip("骑乘时 CharacterController 世界半径。")]
        public float BikeControllerWorldRadius = 0.6f;
        [Tooltip("骑乘时台阶高度（世界单位）。")]
        public float BikeControllerWorldStep = 0.25f;
        [Tooltip("骑乘时 CharacterController.center.y（本地）。")]
        public float BikeControllerCenterY = 0.5f;
    }

    /// <summary>
    /// 基于 CharacterController 的移动辅助。
    /// </summary>
    public class CharacterMotor
    {
        public CharacterController Controller { get; }
        public CharacterMotorSettings Settings { get; }
        public Vector3 Velocity;
        public Vector3 PlanarVelocity;

        /// <summary>带 coyote 的稳定着地判定，供状态机使用。</summary>
        public bool IsGrounded { get; private set; }

        /// <summary>本帧物理探测是否接触地面（不含 coyote）。</summary>
        public bool IsGroundedRaw { get; private set; }

        float _coyoteTimer;
        bool _forceUnground;
        float _forceUngroundTimer;

        public CharacterMotor(CharacterController controller, CharacterMotorSettings settings)
        {
            Controller = controller;
            Settings = settings;
            IsGrounded = true;
            IsGroundedRaw = true;
        }

        public void Move(Vector3 worldDelta, float deltaTime)
        {
            Controller.Move(worldDelta);
            RefreshGrounded(deltaTime);
        }

        public void SetPlanarFromInput(Vector2 moveInput, Transform cameraYaw, float speed, float deltaTime, float control = 1f)
        {
            Vector3 desired = Vector3.zero;
            if (moveInput.sqrMagnitude > 0.01f)
            {
                Vector3 forward = cameraYaw != null ? Vector3.ProjectOnPlane(cameraYaw.forward, Vector3.up).normalized : Vector3.forward;
                Vector3 right = cameraYaw != null ? Vector3.ProjectOnPlane(cameraYaw.right, Vector3.up).normalized : Vector3.right;
                desired = (forward * moveInput.y + right * moveInput.x).normalized * speed;
            }

            PlanarVelocity = Vector3.Lerp(PlanarVelocity, desired, Mathf.Clamp01(control));
            Velocity.x = PlanarVelocity.x;
            Velocity.z = PlanarVelocity.z;
        }

        public void FacePlanarVelocity(Transform body, float deltaTime)
        {
            Vector3 flat = new Vector3(PlanarVelocity.x, 0f, PlanarVelocity.z);
            if (flat.sqrMagnitude < 0.001f)
            {
                return;
            }

            Quaternion target = Quaternion.LookRotation(flat.normalized, Vector3.up);
            body.rotation = Quaternion.Slerp(body.rotation, target, Settings.RotateSpeed * deltaTime);
        }

        public void ApplyGravity(float deltaTime, float gravity)
        {
            if (IsGroundedRaw && !_forceUnground && Velocity.y <= 0f)
            {
                // 贴地：固定下压力，本帧不再累加重力，避免 Velocity.y 越来越负
                Velocity.y = Settings.GroundStickForce;
                return;
            }

            Velocity.y += gravity * deltaTime;
        }

        public void Jump()
        {
            Jump(Settings.JumpHeight, 0.2f);
        }

        public void Jump(float jumpHeight, float forceUngroundDuration)
        {
            // 起跳后短暂强制离地，避免同一帧又被判定着地
            ForceUnground(forceUngroundDuration);
            float height = Mathf.Max(0.05f, jumpHeight);
            Velocity.y = Mathf.Sqrt(height * -2f * Settings.Gravity);
            IsGrounded = false;
            IsGroundedRaw = false;
            _coyoteTimer = 0f;
        }

        public void ForceUnground(float duration)
        {
            _forceUnground = true;
            _forceUngroundTimer = duration;
            IsGrounded = false;
            IsGroundedRaw = false;
            _coyoteTimer = 0f;
        }

        /// <summary>传送后清速度，避免沿用旧动量把新实例顶飞。</summary>
        public void ResetMotion()
        {
            Velocity = Vector3.zero;
            PlanarVelocity = Vector3.zero;
            _forceUnground = false;
            _forceUngroundTimer = 0f;
            _coyoteTimer = Settings != null ? Settings.CoyoteTime : 0.12f;
            IsGrounded = true;
            IsGroundedRaw = Controller != null && Controller.enabled && Controller.isGrounded;
        }

        /// <summary>切人继承动量；飞行/摩托续态时用。</summary>
        public void ApplyMotion(Vector3 velocity, Vector3 planarVelocity, float forceUngroundSeconds = 0f)
        {
            Velocity = velocity;
            PlanarVelocity = planarVelocity;
            if (forceUngroundSeconds > 0f)
            {
                ForceUnground(forceUngroundSeconds);
            }
            else
            {
                RefreshGrounded(0f);
            }
        }

        public void TickMove(float deltaTime)
        {
            // 水平/垂直拆开 Move：高速贴地时合成位移容易被 CC 吃掉向上分量
            Vector3 planar = new Vector3(Velocity.x, 0f, Velocity.z) * deltaTime;
            Vector3 vertical = new Vector3(0f, Velocity.y, 0f) * deltaTime;
            if (planar.sqrMagnitude > 0f)
            {
                Controller.Move(planar);
            }

            if (vertical.sqrMagnitude > 0f || _forceUnground)
            {
                Controller.Move(vertical);
            }

            RefreshGrounded(deltaTime);

            // 离地保护在本帧探测之后再衰减，避免最后一帧又被判回地面
            if (_forceUnground)
            {
                _forceUngroundTimer -= deltaTime;
                if (_forceUngroundTimer <= 0f)
                {
                    _forceUnground = false;
                }
            }
        }

        void RefreshGrounded(float deltaTime)
        {
            if (_forceUnground)
            {
                IsGroundedRaw = false;
                IsGrounded = false;
                _coyoteTimer = 0f;
                return;
            }

            IsGroundedRaw = Controller.isGrounded || ProbeGround();

            if (IsGroundedRaw)
            {
                IsGrounded = true;
                _coyoteTimer = Settings.CoyoteTime;
            }
            else
            {
                _coyoteTimer -= deltaTime;
                IsGrounded = _coyoteTimer > 0f;
            }
        }

        bool ProbeGround()
        {
            // CharacterController 底部中心略上移，再向下打一小段 SphereCast
            float radius = Mathf.Max(0.05f, Controller.radius * 0.9f);
            float skin = Controller.skinWidth + 0.01f;
            Vector3 origin = Controller.transform.TransformPoint(Controller.center);
            origin.y = Controller.bounds.min.y + radius + skin;

            float distance = Settings.GroundProbeExtra + skin + 0.02f;
            return Physics.SphereCast(
                origin,
                radius,
                Vector3.down,
                out _,
                distance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
        }
    }
}
