using System;
using UnityEngine;
using AttackSkill.CameraSystem;
using AttackSkill.Combat;
using AttackSkill.Character;
using AttackSkill.Character.Exploration;
using AttackSkill.Core;

namespace AttackSkill.Character.HSM
{
    public enum CharacterControlMode
    {
        Active,
        Residual,
        Disabled
    }

    /// <summary>
    /// 类原神基础角色控制器（HSM）。
    /// 支持 Active / Residual（切人后继续放技能再消失）。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class GenshinLikeCharacter : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] Transform cameraYaw;
        [SerializeField] bool autoFindThirdPersonCamera = true;
        [SerializeField] Animator animator;
        [SerializeField] CharacterAvatar avatar;

        [Header("Motor")]
        [SerializeField] CharacterMotorSettings settings = new CharacterMotorSettings();

        [Header("Probe")]
        [SerializeField] float climbProbeDistance = 0.7f;
        [SerializeField] float climbProbeHeight = 1.2f;
        [SerializeField] LayerMask climbMask = ~0;
        [SerializeField] string waterTag = "Water";
        [SerializeField] bool drawDebugState = false;

        CharacterController _controller;
        Health _health;
        HStateMachine _fsm;
        CharacterContext _ctx;
        CharacterMotor _motor;
        CharacterControlMode _mode = CharacterControlMode.Active;
        CharacterSkillPlayer _skillPlayer;
        CharacterExplorationTools _explorationTools;
        bool _residualFinishRaised;
        bool _dead;

        public CharacterStateTree States { get; private set; }
        public string CurrentStatePath => _fsm != null ? _fsm.CurrentPath : string.Empty;
        public CharacterControlMode ControlMode => _mode;
        public CharacterSkillPlayer SkillPlayer => _skillPlayer;
        public CharacterContext Context => _ctx;
        public CharacterExplorationTools ExplorationTools => _explorationTools;
        public Health Health => _health;
        public CharacterAvatar Avatar => avatar;
        public bool IsActive => _mode == CharacterControlMode.Active;
        public bool IsResidual => _mode == CharacterControlMode.Residual;
        public bool IsDead => _dead || (_health != null && !_health.IsAlive);
        public bool IsWingFlying =>
            _fsm != null &&
            States != null &&
            _fsm.Current == States.Airborne.WingFlight;

        public bool IsSwordFlying =>
            _fsm != null &&
            States != null &&
            _fsm.Current == States.Airborne.SwordFlight;

        public bool IsRidingMotorcycle =>
            _fsm != null &&
            States != null &&
            _fsm.Current == States.Grounded.Motorcycle;

        /// <summary>装备翅膀工具后按 T：起飞 / 退出飞行（glide）。</summary>
        public bool TryToggleWingFlight() =>
            TryToggleExplorationTool(
                _explorationTools != null ? _explorationTools.WingFlight : null,
                FindDefaultDefinition(ExplorationToolKind.WingFlight));

        /// <summary>装备御剑工具后按 T：起飞 / 退出御剑。</summary>
        public bool TryToggleSwordFlight() =>
            TryToggleExplorationTool(
                _explorationTools != null ? _explorationTools.SwordFlight : null,
                FindDefaultDefinition(ExplorationToolKind.SwordFlight));

        /// <summary>装备摩托工具后按 T：上车 / 下车（接地由 Definition.RequiresGroundToActivate 决定）。</summary>
        public bool TryToggleMotorcycle(ExplorationToolDefinition definition = null) =>
            TryToggleExplorationTool(
                _explorationTools != null ? _explorationTools.Motorcycle : null,
                definition != null ? definition : FindDefaultDefinition(ExplorationToolKind.Motorcycle));

        /// <summary>统一工具切换：互斥、门闩、进/出对应 HSM 薄壳。</summary>
        public bool TryToggleExplorationTool(IExplorationTool tool, ExplorationToolDefinition definition)
        {
            if (tool == null || !CanToggleToolLocomotion())
            {
                return false;
            }

            if (tool.IsActive)
            {
                return ExitExplorationTool(tool.Kind);
            }

            if (HasConflictingExplorationTool(tool.Kind))
            {
                return false;
            }

            var ctx = _explorationTools != null
                ? _explorationTools.BuildContext()
                : new ExplorationToolContext(this, _ctx);

            if (!tool.CanActivate(ctx, definition))
            {
                return false;
            }

            if (_explorationTools != null)
            {
                _explorationTools.SetPendingDefinition(definition);
            }

            return EnterExplorationTool(tool.Kind);
        }

        bool HasConflictingExplorationTool(ExplorationToolKind activating)
        {
            var tools = _explorationTools;
            bool wingBusy = IsWingFlying || (tools != null && tools.WingFlight.IsActive);
            bool swordBusy = IsSwordFlying || (tools != null && tools.SwordFlight.IsActive);
            bool bikeBusy = IsRidingMotorcycle || (tools != null && tools.Motorcycle.IsActive);

            if (activating != ExplorationToolKind.WingFlight && wingBusy)
            {
                return true;
            }

            if (activating != ExplorationToolKind.SwordFlight && swordBusy)
            {
                return true;
            }

            if (activating != ExplorationToolKind.Motorcycle && bikeBusy)
            {
                return true;
            }

            return false;
        }

        bool EnterExplorationTool(ExplorationToolKind kind, bool applyFlightTakeoff = true)
        {
            switch (kind)
            {
                case ExplorationToolKind.WingFlight:
                    if (applyFlightTakeoff)
                    {
                        ApplyFlightTakeoff();
                    }
                    else
                    {
                        _motor?.ForceUnground(0.25f);
                    }

                    _fsm.ChangeState(States.Airborne.WingFlight);
                    return true;
                case ExplorationToolKind.SwordFlight:
                    if (applyFlightTakeoff)
                    {
                        ApplyFlightTakeoff();
                    }
                    else
                    {
                        _motor?.ForceUnground(0.25f);
                    }

                    _fsm.ChangeState(States.Airborne.SwordFlight);
                    return true;
                case ExplorationToolKind.Motorcycle:
                    _fsm.ChangeState(States.Grounded.Motorcycle);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>切人前采集飞行/御剑/摩托状态与动量。</summary>
        public ExplorationLocomotionSnapshot CaptureExplorationLocomotion()
        {
            var snap = new ExplorationLocomotionSnapshot();
            if (_dead || _motor == null)
            {
                return snap;
            }

            snap.Velocity = _motor.Velocity;
            snap.PlanarVelocity = _motor.PlanarVelocity;

            var tools = _explorationTools;
            if (IsWingFlying || (tools != null && tools.WingFlight.IsActive))
            {
                snap.Kind = ExplorationToolKind.WingFlight;
            }
            else if (IsSwordFlying || (tools != null && tools.SwordFlight.IsActive))
            {
                snap.Kind = ExplorationToolKind.SwordFlight;
            }
            else if (IsRidingMotorcycle || (tools != null && tools.Motorcycle.IsActive))
            {
                snap.Kind = ExplorationToolKind.Motorcycle;
                if (tools != null)
                {
                    snap.MotorcycleRideSpeed = tools.Motorcycle.RideSpeed;
                    snap.MotorcycleJumpCooldownRemaining = tools.Motorcycle.JumpCooldownRemaining;
                }
                else
                {
                    snap.MotorcycleRideSpeed = snap.PlanarVelocity.magnitude;
                }
            }

            return snap;
        }

        /// <summary>切人后恢复探索载具/飞行（跳过起飞冲量与接地门闩）。</summary>
        public void ResumeExplorationLocomotion(in ExplorationLocomotionSnapshot snap)
        {
            if (!snap.HasTool || _dead || _fsm == null || States == null)
            {
                return;
            }

            bool alreadyInTool = IsInExplorationTool(snap.Kind);
            if (!alreadyInTool)
            {
                if (_explorationTools != null)
                {
                    _explorationTools.SetPendingDefinition(FindDefaultDefinition(snap.Kind));
                    _explorationTools.SuppressEnterSfx = true;
                }

                EnterExplorationTool(snap.Kind, applyFlightTakeoff: false);
                if (_explorationTools != null)
                {
                    _explorationTools.SuppressEnterSfx = false;
                }
            }
            else if (_explorationTools != null)
            {
                _explorationTools.ConsumePendingDefinition();
            }

            if (_motor != null)
            {
                float unground = 0.25f;
                if (snap.Kind == ExplorationToolKind.Motorcycle)
                {
                    // 贴地骑行不抬离；空中连跳/下落时保持离地
                    unground = Mathf.Abs(snap.Velocity.y) > 0.35f ? 0.2f : 0f;
                }

                _motor.ApplyMotion(snap.Velocity, snap.PlanarVelocity, unground);
            }

            if (snap.Kind == ExplorationToolKind.Motorcycle && _explorationTools != null)
            {
                _explorationTools.Motorcycle.RestoreRideState(
                    snap.MotorcycleRideSpeed,
                    snap.MotorcycleJumpCooldownRemaining);
                _ctx?.SetAnimFloat(CharacterAnimParams.Speed, Mathf.Abs(snap.MotorcycleRideSpeed));
            }
        }

        bool IsInExplorationTool(ExplorationToolKind kind)
        {
            switch (kind)
            {
                case ExplorationToolKind.WingFlight:
                    return IsWingFlying || (_explorationTools != null && _explorationTools.WingFlight.IsActive);
                case ExplorationToolKind.SwordFlight:
                    return IsSwordFlying || (_explorationTools != null && _explorationTools.SwordFlight.IsActive);
                case ExplorationToolKind.Motorcycle:
                    return IsRidingMotorcycle || (_explorationTools != null && _explorationTools.Motorcycle.IsActive);
                default:
                    return false;
            }
        }

        /// <summary>把探索工具交给新角色前，卸掉本角色挂点与 Tool 态（不整角色禁用）。</summary>
        public void HandoffClearExplorationTools()
        {
            if (_explorationTools != null)
            {
                _explorationTools.SuppressEnterSfx = true;
            }

            ForceStopExplorationTools();

            if (_explorationTools != null)
            {
                _explorationTools.SuppressEnterSfx = false;
            }
        }

        bool ExitExplorationTool(ExplorationToolKind kind)
        {
            switch (kind)
            {
                case ExplorationToolKind.WingFlight:
                case ExplorationToolKind.SwordFlight:
                    // 空中退出 → Fall（IsFalling / Jump_Loop）；贴地退出 → Grounded（Land / Jump_Land）
                    if (_motor != null && _motor.IsGroundedRaw && _motor.Velocity.y <= 0.5f)
                    {
                        _motor.Velocity.y = settings.GroundStickForce;
                        _fsm.ChangeState(States.Grounded.Idle);
                    }
                    else
                    {
                        _fsm.ChangeState(States.Airborne.Fall);
                    }

                    return true;
                case ExplorationToolKind.Motorcycle:
                    _fsm.ChangeState(States.Grounded.Idle);
                    return true;
                default:
                    return false;
            }
        }

        static ExplorationToolDefinition FindDefaultDefinition(ExplorationToolKind kind)
        {
            int slot = ExplorationToolService.FindSlot(kind);
            return slot >= 0 ? ExplorationToolCatalog.Get().GetSlot(slot) : null;
        }

        bool CanToggleToolLocomotion()
        {
            if (_dead || _mode != CharacterControlMode.Active || _fsm == null || States == null)
            {
                return false;
            }

            if (_skillPlayer != null && _skillPlayer.IsPlaying)
            {
                return false;
            }

            return true;
        }

        void ApplyFlightTakeoff()
        {
            if (_motor == null || settings == null)
            {
                return;
            }

            if (_motor.IsGroundedRaw)
            {
                _motor.Velocity.y = settings.WingTakeoffSpeed;
                _motor.ForceUnground(0.3f);
            }
            else
            {
                _motor.Velocity.y = Mathf.Max(_motor.Velocity.y, settings.WingTakeoffSpeed * 0.35f);
                _motor.ForceUnground(0.2f);
            }
        }

        /// <summary>由 RuntimeAssembler 在 AddComponent 前/后注入表现引用。</summary>
        public void BindPresentation(CharacterAvatar presentation, Animator anim = null)
        {
            if (presentation != null)
            {
                avatar = presentation;
                if (anim == null)
                {
                    anim = presentation.Animator;
                }
            }

            if (anim != null)
            {
                animator = anim;
            }
        }

        /// <summary>仍处于 Skill 状态（切人残留等）。</summary>
        public bool IsLingeringSkill
        {
            get
            {
                if (_dead)
                {
                    return false;
                }

                return !string.IsNullOrEmpty(CurrentStatePath) &&
                       CurrentStatePath.IndexOf("Skill", StringComparison.Ordinal) >= 0;
            }
        }

        /// <summary>残留体技能结束、可以回收时触发。</summary>
        public event Action<GenshinLikeCharacter> ResidualFinished;

        /// <summary>生命归零、进入死亡锁定时触发。</summary>
        public event Action<GenshinLikeCharacter> Died;

        void Awake()
        {
            _controller = GetComponent<CharacterController>();
            SanitizeCharacterController(_controller);
            _motor = new CharacterMotor(_controller, settings);

            int playerLayer = CombatLayers.PlayerLayer;
            if (playerLayer >= 0 && gameObject.layer != playerLayer)
            {
                CombatLayers.ApplyLayerRecursively(gameObject, playerLayer);
            }

            PlayerHurtbox.Ensure(gameObject);

            if (cameraYaw == null && autoFindThirdPersonCamera)
            {
                var tpc = GameServices.ResolveCamera();
                if (tpc != null && tpc.YawTransform != null)
                {
                    cameraYaw = tpc.YawTransform;
                }
            }

            if (avatar == null)
            {
                avatar = GetComponent<CharacterAvatar>();
                if (avatar == null)
                {
                    avatar = GetComponentInChildren<CharacterAvatar>(true);
                }
            }

            if (animator == null && avatar != null)
            {
                animator = avatar.Animator;
            }

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            if (animator != null)
            {
                animator.SetInteger(CharacterAnimParams.AttackCombo, -1);
                animator.SetBool(CharacterAnimParams.InCombatAction, false);
                animator.ResetTrigger(CharacterAnimParams.Attack);
                animator.ResetTrigger(CharacterAnimParams.Skill);
                animator.ResetTrigger(CharacterAnimParams.Jump);
                animator.ResetTrigger(CharacterAnimParams.Land);
                animator.ResetTrigger(CharacterAnimParams.Dodge);
                animator.SetBool(CharacterAnimParams.IsDodging, false);
            }

            AttackHitRelay hitRelay = null;
            if (animator != null)
            {
                hitRelay = animator.GetComponent<AttackHitRelay>();
                if (hitRelay == null)
                {
                    hitRelay = animator.gameObject.AddComponent<AttackHitRelay>();
                }
            }
            else
            {
                hitRelay = GetComponent<AttackHitRelay>();
                if (hitRelay == null)
                {
                    hitRelay = gameObject.AddComponent<AttackHitRelay>();
                }
            }

            if (hitRelay != null && hitRelay.TimedHitProfile == null)
            {
                var settings = CharacterRuntimeSettings.Get();
                if (settings != null)
                {
                    PartyPortraitId portrait = PartyPortraitId.WandererFemale;
                    if (avatar != null && !string.IsNullOrEmpty(avatar.DisplayName))
                    {
                        // 与 Assembler 命名约定一致的粗匹配
                        string n = avatar.DisplayName;
                        if (n.IndexOf("千咲", System.StringComparison.Ordinal) >= 0 ||
                            n.IndexOf("Qianxiao", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            portrait = PartyPortraitId.Qianxiao;
                        }
                        else if (n.IndexOf("柯莱塔", System.StringComparison.Ordinal) >= 0 ||
                                 n.IndexOf("Coletta", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            portrait = PartyPortraitId.Coletta;
                        }
                        else if (n.IndexOf("男", System.StringComparison.Ordinal) >= 0 ||
                                 n.IndexOf("Male", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            portrait = PartyPortraitId.WandererMale;
                        }
                    }

                    TimedHitProfile timed = settings.GetTimedHitProfile(portrait);
                    if (timed != null)
                    {
                        hitRelay.SetTimedHitProfile(timed);
                    }
                }
            }

            var audio = GetComponent<CharacterAudio>();
            if (audio == null)
            {
                audio = gameObject.AddComponent<CharacterAudio>();
            }

            // 玩家受击（野外敌人 Hitbox）+ 无敌帧/硬直
            _health = GetComponent<Health>();
            if (_health == null)
            {
                _health = gameObject.AddComponent<Health>();
            }

            // CombatStats 已按角色表/肉鸽等级写入生命时，不要再盖成 20000
            if (GetComponent<CombatStats>() == null)
            {
                _health.Configure(20000f, destroyWhenDead: false);
            }

            _health.ConfigureDefense(enableIFrames: true, iFrames: 0.5f, stun: 0.18f, enableHitStun: true);
            _health.Died += OnHealthDied;

            _skillPlayer = GetComponent<CharacterSkillPlayer>();
            if (_skillPlayer == null)
            {
                _skillPlayer = gameObject.AddComponent<CharacterSkillPlayer>();
            }

            _ctx = new CharacterContext
            {
                Transform = transform,
                CameraYaw = cameraYaw != null ? cameraYaw : Camera.main != null ? Camera.main.transform : transform,
                Animator = animator,
                Motor = _motor,
                Settings = settings,
                InputSource = new LegacyCharacterInputSource(),
                AttackHits = hitRelay,
                Audio = audio,
                SkillPlayer = _skillPlayer,
                Owner = this
            };

            _explorationTools = new CharacterExplorationTools(this);

            _fsm = new HStateMachine();
            States = new CharacterStateTree(_ctx, _fsm);
            _fsm.Start(States.Grounded.Idle);
        }

        void Start()
        {
            // 再确保一次：装配顺序/其它 Awake 改过 CC 尺寸时同步受击盒
            PlayerHurtbox.Ensure(gameObject);
        }

        void OnDestroy()
        {
            if (_health != null)
            {
                _health.Died -= OnHealthDied;
            }
        }

        /// <summary>
        /// 根节点有缩放（如 MMD 0.08）时，按世界尺寸校正 CC，避免 Step Offset 报错。
        /// </summary>
        static void SanitizeCharacterController(CharacterController cc)
        {
            if (cc == null)
            {
                return;
            }

            Vector3 lossy = cc.transform.lossyScale;
            float sx = Mathf.Max(0.0001f, Mathf.Abs(lossy.x));
            float sy = Mathf.Max(0.0001f, Mathf.Abs(lossy.y));
            float sz = Mathf.Max(0.0001f, Mathf.Abs(lossy.z));
            float sRadius = Mathf.Max(sx, sz);

            float scaledH = cc.height * sy;
            float scaledR = cc.radius * sRadius;
            float maxStep = scaledH + scaledR * 2f;

            // 世界高度明显过小：按 1.8/0.35 反算本地尺寸
            if (scaledH < 1.0f || scaledR < 0.12f)
            {
                const float worldHeight = 1.8f;
                const float worldRadius = 0.35f;
                cc.height = worldHeight / sy;
                cc.radius = worldRadius / sRadius;
                cc.center = new Vector3(cc.center.x, (worldHeight * 0.5f) / sy, cc.center.z);
                scaledH = worldHeight;
                scaledR = worldRadius;
                maxStep = scaledH + scaledR * 2f;
            }

            if (cc.stepOffset > maxStep)
            {
                cc.stepOffset = Mathf.Max(0f, maxStep * 0.5f);
            }
        }

        void OnHealthDied()
        {
            EnterDead();
        }

        void EnterDead()
        {
            if (_dead)
            {
                return;
            }

            // 先卸探索工具（摩托/飞行），再冻结 FSM，避免残留挂点与 _active 脏状态
            ForceStopExplorationTools();

            _dead = true;
            _mode = CharacterControlMode.Disabled;
            drawDebugState = false;

            if (_ctx != null)
            {
                _ctx.InputSource = new NullCharacterInputSource();
            }

            if (_skillPlayer != null)
            {
                if (_skillPlayer.IsPlaying)
                {
                    _skillPlayer.Stop();
                }
                else
                {
                    _skillPlayer.ReleaseGameplayCamera();
                }
            }

            if (_controller != null)
            {
                _controller.enabled = false;
            }

            if (animator != null)
            {
                animator.SetBool(CharacterAnimParams.InCombatAction, false);
                animator.SetBool(CharacterAnimParams.IsDodging, false);
                animator.SetInteger(CharacterAnimParams.AttackCombo, -1);
            }

            Died?.Invoke(this);
        }

        void Update()
        {
            if (_dead || _mode == CharacterControlMode.Disabled)
            {
                return;
            }

            _ctx.RefreshInput();
            if (_mode == CharacterControlMode.Active && _health != null && _health.IsHitStunned)
            {
                ApplyHitStunInputLock();
            }

            if (_mode == CharacterControlMode.Active)
            {
                ProbeEnvironment();
            }

            _fsm.Update(Time.deltaTime);

            if (_mode == CharacterControlMode.Residual && !_residualFinishRaised && !IsLingeringSkill)
            {
                _residualFinishRaised = true;
                ResidualFinished?.Invoke(this);
            }
        }

        void FixedUpdate()
        {
            if (_dead || _mode == CharacterControlMode.Disabled)
            {
                return;
            }

            _fsm.FixedUpdate(Time.fixedDeltaTime);
        }

        /// <summary>
        /// 安全传送：先关 CharacterController 再改 Transform，避免启用态下位姿被冲掉。
        /// </summary>
        /// <param name="resetMotion">false 时保留速度（切人确认坐标用）。</param>
        public void TeleportTo(Vector3 worldPos, Quaternion worldRot, bool resetMotion = true)
        {
            if (_controller == null)
            {
                _controller = GetComponent<CharacterController>();
            }

            Vector3 keepVelocity = default;
            Vector3 keepPlanar = default;
            if (!resetMotion && _motor != null)
            {
                keepVelocity = _motor.Velocity;
                keepPlanar = _motor.PlanarVelocity;
            }

            bool ccWasEnabled = false;
            if (_controller != null)
            {
                ccWasEnabled = _controller.enabled;
                _controller.enabled = false;
            }

            transform.SetPositionAndRotation(worldPos, worldRot);
            Physics.SyncTransforms();
            if (resetMotion)
            {
                _motor?.ResetMotion();
            }

            if (_controller != null)
            {
                _controller.enabled = ccWasEnabled;
                if (ccWasEnabled)
                {
                    Physics.SyncTransforms();
                    // 启用后若被碰撞挤开，再强制写回一次
                    if ((transform.position - worldPos).sqrMagnitude > 0.0001f)
                    {
                        _controller.enabled = false;
                        transform.SetPositionAndRotation(worldPos, worldRot);
                        Physics.SyncTransforms();
                        _controller.enabled = true;
                        Physics.SyncTransforms();
                    }

                    if (resetMotion)
                    {
                        _motor?.ResetMotion();
                    }
                }
            }

            if (!resetMotion && _motor != null)
            {
                _motor.ApplyMotion(keepVelocity, keepPlanar);
            }
        }

        /// <summary>成为当前操控角色。</summary>
        /// <param name="resetMotion">续态切人传 false，避免清零后再 Resume。</param>
        public void BecomeActive(
            ThirdPersonCamera camera = null,
            Transform yawOverride = null,
            bool resetMotion = true)
        {
            if (_dead)
            {
                return;
            }

            _mode = CharacterControlMode.Active;
            _residualFinishRaised = false;
            enabled = true;

            if (_controller != null)
            {
                _controller.enabled = true;
                Physics.SyncTransforms();
            }

            if (resetMotion)
            {
                _motor?.ResetMotion();
            }

            if (_skillPlayer != null)
            {
                _skillPlayer.AllowCameraTakeover = true;
            }

            _ctx.InputSource = new LegacyCharacterInputSource();

            if (camera != null)
            {
                camera.FollowTarget = transform;
                if (camera.YawTransform != null)
                {
                    cameraYaw = camera.YawTransform;
                    _ctx.CameraYaw = cameraYaw;
                }

                camera.SetGameplayControlEnabled(true);
                camera.SnapToFollowTarget();
                camera.RestoreDesiredCursorLock();
            }

            if (yawOverride != null)
            {
                cameraYaw = yawOverride;
                _ctx.CameraYaw = yawOverride;
            }
            else if (cameraYaw != null)
            {
                _ctx.CameraYaw = cameraYaw;
            }
        }

        /// <summary>
        /// 切人残留：不吃输入、不抢相机，技能/Timeline 继续，结束后抛 ResidualFinished。
        /// </summary>
        public void BecomeResidual()
        {
            _mode = CharacterControlMode.Residual;
            _residualFinishRaised = false;
            drawDebugState = false;
            _ctx.InputSource = new NullCharacterInputSource();

            if (_skillPlayer != null)
            {
                _skillPlayer.ReleaseGameplayCamera();
            }

            // 技能 Timeline 期间本就不依赖 CC 位移；关掉避免顶开新角色
            if (_controller != null)
            {
                _controller.enabled = false;
            }

            // 已经不在技能中则马上可回收
            if (!IsLingeringSkill)
            {
                _residualFinishRaised = true;
                ResidualFinished?.Invoke(this);
            }
        }

        public void SetDisabled()
        {
            ForceStopExplorationTools();
            _mode = CharacterControlMode.Disabled;
            _ctx.InputSource = new NullCharacterInputSource();
            if (_controller != null)
            {
                _controller.enabled = false;
            }
        }

        /// <summary>死亡 / 禁用前强制退出探索工具，清理挂点与 Tool.IsActive。</summary>
        void ForceStopExplorationTools()
        {
            var tools = _explorationTools;
            bool bikeBusy = IsRidingMotorcycle || (tools != null && tools.Motorcycle.IsActive);
            bool flightBusy = IsWingFlying || IsSwordFlying ||
                              (tools != null && (tools.WingFlight.IsActive || tools.SwordFlight.IsActive));

            if (_fsm != null && States != null)
            {
                if (bikeBusy)
                {
                    _fsm.ChangeState(States.Grounded.Idle);
                }
                else if (flightBusy)
                {
                    _fsm.ChangeState(States.Airborne.Fall);
                }
            }

            if (tools == null)
            {
                return;
            }

            var ctx = tools.BuildContext();
            if (tools.Motorcycle.IsActive)
            {
                tools.Motorcycle.Deactivate(ctx);
            }

            if (tools.WingFlight.IsActive)
            {
                tools.WingFlight.Deactivate(ctx);
            }

            if (tools.SwordFlight.IsActive)
            {
                tools.SwordFlight.Deactivate(ctx);
            }
        }

        void ApplyHitStunInputLock()
        {
            var input = _ctx.Input;
            input.Move = Vector2.zero;
            input.JumpPressed = false;
            input.JumpHeld = false;
            input.SprintHeld = false;
            input.GlidePressed = false;
            input.AttackPressed = false;
            input.SkillPressed = false;
            input.SkillRPressed = false;
            input.InteractPressed = false;
            input.DodgePressed = false;
            _ctx.Input = input;

            if (_motor != null)
            {
                _motor.PlanarVelocity *= 0.35f;
                _motor.Velocity.x = _motor.PlanarVelocity.x;
                _motor.Velocity.z = _motor.PlanarVelocity.z;
            }
        }

        void ProbeEnvironment()
        {
            Vector3 origin = transform.position + Vector3.up * climbProbeHeight;
            if (Physics.Raycast(origin, transform.forward, out RaycastHit hit, climbProbeDistance, climbMask, QueryTriggerInteraction.Ignore))
            {
                float angle = Vector3.Angle(hit.normal, Vector3.up);
                _ctx.IsNearClimbable = angle > 60f && angle < 120f;
                _ctx.ClimbNormal = hit.normal;
            }
            else
            {
                _ctx.IsNearClimbable = false;
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag(waterTag))
            {
                _ctx.IsInWater = true;
            }
        }

        void OnTriggerStay(Collider other)
        {
            if (other.CompareTag(waterTag))
            {
                _ctx.IsInWater = true;
            }
        }

        void OnTriggerExit(Collider other)
        {
            if (other.CompareTag(waterTag))
            {
                _ctx.IsInWater = false;
            }
        }

        void OnGUI()
        {
            if (!drawDebugState || _mode != CharacterControlMode.Active)
            {
                return;
            }

            GUI.Label(new Rect(12, 12, 800, 24), $"HSM: {CurrentStatePath}");
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Vector3 origin = transform.position + Vector3.up * climbProbeHeight;
            Gizmos.DrawLine(origin, origin + transform.forward * climbProbeDistance);
        }
#endif
    }
}
