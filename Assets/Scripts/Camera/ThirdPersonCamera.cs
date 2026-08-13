using UnityEngine;
using AttackSkill.Character;
using AttackSkill.Combat;
using AttackSkill.Core;
using AttackSkill.Game;

namespace AttackSkill.CameraSystem
{
    /// <summary>
    /// 类原神风格第三人称轨道相机：跟随、鼠标环视、滚轮缩放、墙体防穿。
    /// YawTransform 供角色移动按相机朝向计算输入方向。
    /// </summary>
    [DisallowMultipleComponent]
    public class ThirdPersonCamera : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] Transform followTarget;
        [SerializeField] Vector3 pivotOffset = new Vector3(0f, 1.45f, 0f);

        [Header("Rig (可留空，运行时自动创建)")]
        [SerializeField] Transform yawPivot;
        [SerializeField] Transform pitchPivot;
        [SerializeField] Camera controlledCamera;

        [Header("Orbit")]
        [SerializeField] float defaultDistance = 5f;
        [SerializeField] float minDistance = 1.8f;
        [SerializeField] float maxDistance = 12f;
        [SerializeField] float minPitch = -25f;
        [SerializeField] float maxPitch = 70f;
        [SerializeField] float yaw = 0f;
        [SerializeField] float pitch = 18f;
        [SerializeField] float distance = 5f;

        [Header("Input")]
        [SerializeField] bool lockCursorOnPlay = true;
        [Tooltip("未锁鼠时是否需按住右键才环视；锁鼠时始终环视")]
        [SerializeField] bool rotateOnlyWhenRightMouse = false;
        [SerializeField] float mouseSensitivity = 2.5f;
        [SerializeField] float scrollSensitivity = 1.2f;
        [SerializeField] KeyCode unlockCursorKey = KeyCode.LeftAlt;

        [Header("Smoothing")]
        [Tooltip("已废弃：跟拍改为硬跟随（避免 FixedUpdate 移动 + LateUpdate SmoothDamp 抖动）。保留字段仅兼容旧序列化。")]
        [SerializeField] float positionSmoothTime = 0f;
        [SerializeField] float rotationSmoothTime = 0.06f;
        [SerializeField] float zoomSmoothTime = 0.12f;
        [Tooltip("防穿拉近后的回弹平滑；被墙挡住时仍立即拉近。")]
        [SerializeField] float collisionSmoothTime = 0.08f;

        [Header("Collision")]
        [SerializeField] bool enableCollision = true;
        [SerializeField] float collisionRadius = 0.25f;
        [SerializeField] float collisionPadding = 0.15f;
        [Tooltip("留空/Everything 时运行时排除 UI/特效/玩家")]
        [SerializeField] LayerMask collisionMask = ~0;

        [Header("Near Fade")]
        [Tooltip("相机过近时淡出跟随角色（避免贴脸挡画面）。")]
        [SerializeField] bool enableNearCharacterFade = true;
        [Tooltip("开始淡出的相机距离（世界单位）。")]
        [SerializeField] float fadeStartDistance = 1.55f;
        [Tooltip("完全隐藏的相机距离（世界单位）。")]
        [SerializeField] float fadeEndDistance = 0.55f;

        Vector3 _pivotVelocity;
        float _targetDistance;
        float _distanceVelocity;
        float _collisionDistance = -1f;
        float _collisionDistanceVelocity;
        float _targetYaw;
        float _targetPitch;
        float _yawVelocity;
        float _pitchVelocity;
        bool _cursorLocked;
        /// <summary>玩法期望的锁鼠状态；组件禁用/UI 临时解锁后可据此恢复。</summary>
        bool _desireCursorLock;
        CharacterCameraProximityFade _nearFade;
        Transform _nearFadeTarget;

        /// <summary>角色移动应参考的水平朝向（仅 Y 轴旋转）。</summary>
        public Transform YawTransform => yawPivot;

        public Camera ControlledCamera => controlledCamera;

        public Transform FollowTarget
        {
            get => followTarget;
            set => followTarget = value;
        }

        /// <summary>大招等演出期间关闭跟拍与鼠标环视，结束后再打开。</summary>
        public void SetGameplayControlEnabled(bool enabled)
        {
            this.enabled = enabled;
            if (enabled)
            {
                RestoreDesiredCursorLock();
            }
        }

        void Reset()
        {
            EnsureRigHierarchy();
        }

        void Awake()
        {
            EnsureRigHierarchy();
            if (collisionMask.value == ~0 || collisionMask.value == -1)
            {
                collisionMask = CombatLayers.DefaultCameraCollisionMask;
            }

            _targetDistance = distance > 0f ? distance : defaultDistance;
            distance = _targetDistance;
            _targetYaw = yaw;
            _targetPitch = pitch;
            _desireCursorLock = lockCursorOnPlay;
            GameServices.Register(this);
            GameplayInputGate.SoftBlockChanged += OnSoftBlockChanged;
        }

        void OnDestroy()
        {
            GameplayInputGate.SoftBlockChanged -= OnSoftBlockChanged;
            ClearNearFade();
            GameServices.Unregister(this);
        }

        void Start()
        {
            if (_desireCursorLock && !GameplayInputGate.IsBlocked)
            {
                SetCursorLocked(true);
            }
        }

        void OnEnable()
        {
            if (_desireCursorLock && !GameplayInputGate.IsBlocked)
            {
                SetCursorLocked(true);
            }
        }

        void OnDisable()
        {
            ApplyCursorState(locked: false);
            ClearNearFade();
        }

        void OnSoftBlockChanged(bool softBlocked)
        {
            if (!softBlocked && !GamePause.IsPaused)
            {
                RestoreDesiredCursorLock();
            }
        }

        void Update()
        {
            if (GameplayInputGate.IsBlocked)
            {
                return;
            }

            // 焦点丢失或 UI 改写 Cursor 后，按期望强制锁回
            EnforceDesiredCursorLock();
            HandleCursorToggle();
            HandleInput();
        }

        void LateUpdate()
        {
            if (controlledCamera == null || GamePause.IsPaused)
            {
                return;
            }

            if (followTarget == null)
            {
                ClearNearFade();
                return;
            }

            if (!GameplayInputGate.IsSoftBlocked)
            {
                UpdateOrbitAngles();
            }

            UpdatePivotAndCamera();
        }

        void EnforceDesiredCursorLock()
        {
            if (!_desireCursorLock)
            {
                return;
            }

            if (Cursor.lockState != CursorLockMode.Locked || Cursor.visible)
            {
                ApplyCursorState(locked: true);
                _cursorLocked = true;
            }
        }

        void HandleCursorToggle()
        {
            if (GameInput.GetKeyDown(unlockCursorKey))
            {
                bool next = !IsCursorEffectivelyLocked();
                _desireCursorLock = next;
                SetCursorLocked(next);
            }
        }

        void HandleInput()
        {
            bool locked = IsCursorEffectivelyLocked();
            bool canRotate = locked || !rotateOnlyWhenRightMouse || GameInput.GetMouseButton(1);
            if (canRotate)
            {
                // 直接读像素 delta，避免轴缩放过小导致「转不动」
                float mx = GameInput.GetAxisRaw("Mouse X");
                float my = GameInput.GetAxisRaw("Mouse Y");
                if (Mathf.Abs(mx) < 200f && Mathf.Abs(my) < 200f)
                {
                    _targetYaw += mx * mouseSensitivity;
                    _targetPitch -= my * mouseSensitivity;
                    _targetPitch = Mathf.Clamp(_targetPitch, minPitch, maxPitch);
                }
            }

            float scroll = GameInput.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.0001f)
            {
                _targetDistance -= scroll * scrollSensitivity;
                _targetDistance = Mathf.Clamp(_targetDistance, minDistance, maxDistance);
            }
        }

        bool IsCursorEffectivelyLocked()
        {
            return _cursorLocked || Cursor.lockState == CursorLockMode.Locked;
        }

        void UpdateOrbitAngles()
        {
            yaw = Mathf.SmoothDampAngle(yaw, _targetYaw, ref _yawVelocity, rotationSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
            pitch = Mathf.SmoothDampAngle(pitch, _targetPitch, ref _pitchVelocity, rotationSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
            distance = Mathf.SmoothDamp(distance, _targetDistance, ref _distanceVelocity, zoomSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
        }

        void UpdatePivotAndCamera()
        {
            // 角色位移在 FixedUpdate：pivot 必须 LateUpdate 硬跟随，禁止 SmoothDamp 滞后追赶
            Vector3 desiredPivot = followTarget.position + pivotOffset;
            _pivotVelocity = Vector3.zero;
            transform.position = desiredPivot;
            ApplyRigTransforms();
        }

        void ApplyRigTransforms()
        {
            if (yawPivot != null)
            {
                yawPivot.position = transform.position;
                yawPivot.rotation = Quaternion.Euler(0f, yaw, 0f);
            }

            if (pitchPivot != null)
            {
                pitchPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }

            if (controlledCamera == null)
            {
                return;
            }

            float finalDistance = ResolveSmoothedCameraDistance(distance);
            Transform camTransform = controlledCamera.transform;
            camTransform.localPosition = new Vector3(0f, 0f, -finalDistance);
            camTransform.localRotation = Quaternion.identity;
            UpdateNearCharacterFade(finalDistance);
        }

        float ResolveSmoothedCameraDistance(float desiredDistance)
        {
            float resolved = desiredDistance;
            if (enableCollision)
            {
                resolved = ResolveCollisionDistance(desiredDistance);
            }

            if (_collisionDistance < 0f)
            {
                _collisionDistance = resolved;
                _collisionDistanceVelocity = 0f;
                return resolved;
            }

            // 被墙挡住：立刻拉近，避免穿模；离开障碍：平滑推回，避免距离每帧跳变
            if (resolved < _collisionDistance - 0.001f)
            {
                _collisionDistance = resolved;
                _collisionDistanceVelocity = 0f;
                return resolved;
            }

            if (collisionSmoothTime <= 0.0001f)
            {
                _collisionDistance = resolved;
                return resolved;
            }

            _collisionDistance = Mathf.SmoothDamp(
                _collisionDistance,
                resolved,
                ref _collisionDistanceVelocity,
                collisionSmoothTime,
                Mathf.Infinity,
                Time.deltaTime);
            return _collisionDistance;
        }

        /// <summary>切人/读档后立刻贴齐目标，避免相机还在旧出生点。</summary>
        public void SnapToFollowTarget()
        {
            if (followTarget == null)
            {
                return;
            }

            _pivotVelocity = Vector3.zero;
            _yawVelocity = 0f;
            _pitchVelocity = 0f;
            _distanceVelocity = 0f;
            _collisionDistanceVelocity = 0f;
            _collisionDistance = -1f;
            yaw = _targetYaw;
            pitch = _targetPitch;
            distance = _targetDistance;
            transform.position = followTarget.position + pivotOffset;
            ApplyRigTransforms();
        }

        void UpdateNearCharacterFade(float cameraDistance)
        {
            if (!enableNearCharacterFade || followTarget == null)
            {
                ClearNearFade();
                return;
            }

            if (_nearFadeTarget != followTarget)
            {
                ClearNearFade();
                _nearFadeTarget = followTarget;
                _nearFade = followTarget.GetComponent<CharacterCameraProximityFade>();
                if (_nearFade == null)
                {
                    _nearFade = followTarget.gameObject.AddComponent<CharacterCameraProximityFade>();
                }
            }

            float start = Mathf.Max(fadeEndDistance + 0.05f, fadeStartDistance);
            float end = Mathf.Max(0.05f, fadeEndDistance);
            float visibility = Mathf.InverseLerp(end, start, cameraDistance);
            _nearFade.SetVisibility(visibility);
        }

        void ClearNearFade()
        {
            if (_nearFade != null)
            {
                _nearFade.RestoreFullVisibility();
            }

            _nearFade = null;
            _nearFadeTarget = null;
        }

        float ResolveCollisionDistance(float desiredDistance)
        {
            Vector3 origin = transform.position;
            Vector3 direction = -GetCameraForward();
            float maxDist = desiredDistance;

            if (Physics.SphereCast(
                    origin,
                    collisionRadius,
                    direction,
                    out RaycastHit hit,
                    maxDist,
                    collisionMask,
                    QueryTriggerInteraction.Ignore))
            {
                return Mathf.Max(minDistance, hit.distance - collisionPadding);
            }

            return desiredDistance;
        }

        Vector3 GetCameraForward()
        {
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            return rot * Vector3.forward;
        }

        public void SetCursorLocked(bool locked)
        {
            _cursorLocked = locked;
            if (locked)
            {
                _desireCursorLock = true;
            }

            ApplyCursorState(locked);
        }

        /// <summary>仅改 Cursor，不改期望锁鼠（用于轮盘等临时释放）。</summary>
        public void SetCursorLockedTemporary(bool locked)
        {
            _cursorLocked = locked;
            ApplyCursorState(locked);
        }

        /// <summary>按玩法期望恢复锁鼠（轮盘关闭、暂停结束、大招结束）。</summary>
        public void RestoreDesiredCursorLock()
        {
            if (GameplayInputGate.IsBlocked)
            {
                return;
            }

            if (_desireCursorLock)
            {
                SetCursorLocked(true);
            }
            else
            {
                SetCursorLocked(false);
            }
        }

        void ApplyCursorState(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void EnsureRigHierarchy()
        {
            if (yawPivot == null)
            {
                var yawGo = new GameObject("YawPivot");
                yawGo.transform.SetParent(transform, false);
                yawPivot = yawGo.transform;
            }

            if (pitchPivot == null)
            {
                var pitchGo = new GameObject("PitchPivot");
                pitchPivot = pitchGo.transform;
                pitchPivot.SetParent(yawPivot, false);
            }

            if (controlledCamera == null)
            {
                controlledCamera = GetComponentInChildren<Camera>();
                if (controlledCamera == null)
                {
                    var camGo = new GameObject("Main Camera");
                    camGo.tag = "MainCamera";
                    camGo.transform.SetParent(pitchPivot, false);
                    controlledCamera = camGo.AddComponent<Camera>();
                }
                else if (controlledCamera.transform.parent != pitchPivot)
                {
                    controlledCamera.transform.SetParent(pitchPivot, false);
                }
            }

            EnsureAudioListener(controlledCamera);
        }

        static void EnsureAudioListener(Camera cam)
        {
            if (cam == null)
            {
                return;
            }

            // 场景里已有 Camera 但没挂 Listener 时，所有音效都会静音
            if (cam.GetComponent<AudioListener>() == null)
            {
                cam.gameObject.AddComponent<AudioListener>();
            }

            // 只保留一个启用的 Listener，避免警告/抢听
            var listeners = Object.FindObjectsOfType<AudioListener>();
            for (int i = 0; i < listeners.Length; i++)
            {
                var l = listeners[i];
                if (l == null)
                {
                    continue;
                }

                l.enabled = l.gameObject == cam.gameObject;
            }
        }

        public static ThirdPersonCamera CreateRig(Transform target = null)
        {
            var rigGo = new GameObject("ThirdPersonCameraRig");
            var rig = rigGo.AddComponent<ThirdPersonCamera>();
            if (target != null)
            {
                rig.followTarget = target;
            }

            return rig;
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (followTarget == null)
            {
                return;
            }

            Gizmos.color = Color.yellow;
            Vector3 pivot = followTarget.position + pivotOffset;
            Gizmos.DrawWireSphere(pivot, 0.12f);

            if (Application.isPlaying)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(pivot, controlledCamera != null ? controlledCamera.transform.position : pivot);
            }
        }
#endif
    }
}
