using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using AttackSkill.CameraSystem;
using AttackSkill.Combat;
using AttackSkill.Core;

namespace AttackSkill.Character
{
    [Serializable]
    public class SkillHitWindow
    {
        [Tooltip("Timeline 时间（秒）到达后触发一次扇形伤害")]
        public float time = 0.85f;
        public float damage = 45f;
        public float hitRadius = 4.5f;
        [Range(1f, 360f)] public float fanAngle = 180f;
        public float knockback = 3.2f;
        public float minHitDistance = 0.2f;
        public float hitHeight = 0.9f;
        public bool spawnSlashVfx = true;
    }

    /// <summary>
    /// 大招 Timeline 临时播放器。
    /// 轨道约定（按名字绑定）：
    /// - SwingAnimation (Animation) → 角色 Animator
    /// - SwingAudio (Audio) → AudioSource
    /// - SkillCamera (Animation) → 技能相机 Animator
    /// - SkillCamera (Activation) → 技能相机 GameObject
    /// - Circle (Activation) → Freeze circle 特效
    /// 伤害：Inspector 配置 SkillHitWindow → AttackHitRelay.PerformFanHit → HitResolver。
    /// 或动画 Event 调 AttackHitRelay（与窗口互斥时用 SuppressAnimHits）。
    /// </summary>
    public class CharacterSkillPlayer : MonoBehaviour
    {
        const string TrackSwingAnimation = "SwingAnimation";
        const string TrackSwingAudio = "SwingAudio";
        const string TrackSkillCamera = "SkillCamera";
        const string TrackCircle = "Circle";

        [Header("Prefabs")]
        [SerializeField] GameObject skillTimelinePrefab;
        [SerializeField] GameObject skillCameraPrefab;
        [SerializeField] GameObject circleVfxPrefab;

        [Header("Bindings")]
        [SerializeField] Animator characterAnimator;
        [SerializeField] AudioSource skillAudioSource;
        [SerializeField] ThirdPersonCamera thirdPersonCamera;
        [SerializeField] bool autoFindRefs = true;

        [Header("Placement")]
        [Tooltip("Timeline 根节点对齐角色")]
        [SerializeField] bool alignTimelineToCharacter = true;
        [Tooltip("技能相机作为角色子物体")]
        [SerializeField] bool parentSkillCameraToCharacter = true;
        [Tooltip("Circle 特效挂在角色根下（脚下法阵）")]
        [SerializeField] bool parentCircleToCharacter = true;
        [Tooltip("角色动画轨使用 Apply Scene Offsets，避免跳到世界原点")]
        [SerializeField] bool useSceneOffsetsForCharacter = true;
        [Tooltip("播完后把 Animator 子物体的位移烘回 CharacterController 根节点")]
        [SerializeField] bool bakeAnimatorMotionToRoot = true;

        [Header("Camera Takeover")]
        [Tooltip("为 false 时不抢第三人称相机（切人残留体用）")]
        [SerializeField] bool allowCameraTakeover = true;

        [Header("Skill Damage")]
        [Tooltip("按 Timeline 时间触发的扇形伤害窗口（不依赖 Signal）")]
        [SerializeField] SkillHitWindow[] hitWindows =
        {
            new SkillHitWindow { time = 0.9f, damage = 45f, hitRadius = 4.5f, fanAngle = 180f, knockback = 3.2f }
        };

        GameObject _runtimeRoot;
        GameObject _runtimeCamera;
        GameObject _runtimeCircle;
        PlayableDirector _director;
        AttackHitRelay _hitRelay;
        bool[] _hitWindowFired;
        bool _playing;
        bool _animatorRootMotionWasEnabled;
        Camera _tpcCamera;
        AudioListener _tpcListener;
        CharacterController _characterController;
        Vector3 _animatorLocalPos;
        Quaternion _animatorLocalRot;
        Vector3 _playStartRootPos;
        Quaternion _playStartRootRot;

        public bool IsPlaying => _playing;
        public bool AllowCameraTakeover
        {
            get => allowCameraTakeover;
            set => allowCameraTakeover = value;
        }

        public event Action PlayStarted;
        public event Action PlayFinished;

        /// <summary>运行时装配：注入 Animator / Timeline Prefab / 相机。</summary>
        public void ConfigureRuntime(
            Animator animator,
            GameObject timelinePrefab,
            GameObject cameraPrefab,
            GameObject circlePrefab,
            ThirdPersonCamera tpc)
        {
            if (animator != null)
            {
                characterAnimator = animator;
            }

            if (timelinePrefab != null)
            {
                skillTimelinePrefab = timelinePrefab;
            }

            if (cameraPrefab != null)
            {
                skillCameraPrefab = cameraPrefab;
            }

            if (circlePrefab != null)
            {
                circleVfxPrefab = circlePrefab;
            }

            if (tpc != null)
            {
                thirdPersonCamera = tpc;
            }
        }

        /// <summary>切人残留时调用：立刻交回第三人称相机，Timeline 继续播。</summary>
        public void ReleaseGameplayCamera()
        {
            allowCameraTakeover = false;
            if (_runtimeCamera != null && _runtimeCamera.activeSelf)
            {
                _runtimeCamera.SetActive(false);
            }

            RestoreCamera();
        }

        void Awake()
        {
            TryAssignDefaultPrefabs();
            ResolveRefs();
        }

        void TryAssignDefaultPrefabs()
        {
            var settings = CharacterRuntimeSettings.Get();
            if (settings == null)
            {
                return;
            }

            if (skillTimelinePrefab == null)
            {
                skillTimelinePrefab = settings.skillTimelinePrefab;
            }

            if (skillCameraPrefab == null)
            {
                skillCameraPrefab = settings.skillCameraPrefab;
            }

            if (circleVfxPrefab == null)
            {
                circleVfxPrefab = settings.circleVfxPrefab;
            }
        }

        void OnDestroy()
        {
            DestroyRuntimeInstances();
        }

        public bool Play()
        {
            if (_playing)
            {
                return false;
            }

            ResolveRefs();
            if (skillTimelinePrefab == null)
            {
                Debug.LogError("[CharacterSkillPlayer] 未指定 skillTimelinePrefab。", this);
                return false;
            }

            if (characterAnimator == null)
            {
                Debug.LogError("[CharacterSkillPlayer] 未找到角色 Animator。", this);
                return false;
            }

            if (!EnsureRuntimeRoot())
            {
                CleanupRuntime(false);
                return false;
            }

            EnsureAudioSource();
            EnsureSkillCamera();
            EnsureCircleVfx();
            if (!BindTracksByName(_director))
            {
                CleanupRuntime(false);
                return false;
            }

            CachePoseBeforePlay();
            if (allowCameraTakeover)
            {
                TakeOverCamera();
            }

            _director.stopped -= OnDirectorStopped;
            _director.stopped += OnDirectorStopped;
            _director.time = 0d;
            _director.Evaluate();
            RestoreRootIfTeleported(0.05f);
            ResetHitWindows();
            ResolveHitRelay();
            SetAnimHitSuppressed(HasSkillHitWindows());
            _director.Play();

            _playing = true;
            PlayStarted?.Invoke();
            return true;
        }

        void LateUpdate()
        {
            if (!_playing)
            {
                return;
            }

            // 残留体：禁止技能相机再被 Activation Track 打开
            if (!allowCameraTakeover && _runtimeCamera != null && _runtimeCamera.activeSelf)
            {
                _runtimeCamera.SetActive(false);
                if (_tpcCamera != null)
                {
                    _tpcCamera.enabled = true;
                }

                if (_tpcListener != null)
                {
                    _tpcListener.enabled = true;
                }

                if (thirdPersonCamera != null)
                {
                    thirdPersonCamera.SetGameplayControlEnabled(true);
                }
            }

            TickSkillHitWindows();

            if (characterAnimator == null)
            {
                return;
            }

            if (bakeAnimatorMotionToRoot && characterAnimator.transform != transform)
            {
                TransferAnimatorDeltaToRoot();
            }
        }

        void ResetHitWindows()
        {
            int n = hitWindows != null ? hitWindows.Length : 0;
            if (_hitWindowFired == null || _hitWindowFired.Length != n)
            {
                _hitWindowFired = n > 0 ? new bool[n] : Array.Empty<bool>();
            }
            else
            {
                for (int i = 0; i < n; i++)
                {
                    _hitWindowFired[i] = false;
                }
            }
        }

        void ResolveHitRelay()
        {
            if (_hitRelay != null)
            {
                return;
            }

            if (characterAnimator != null)
            {
                _hitRelay = characterAnimator.GetComponent<AttackHitRelay>();
            }

            if (_hitRelay == null)
            {
                _hitRelay = GetComponentInChildren<AttackHitRelay>();
            }

            if (_hitRelay == null)
            {
                _hitRelay = GetComponent<AttackHitRelay>();
            }
        }

        void TickSkillHitWindows()
        {
            if (_director == null || hitWindows == null || _hitWindowFired == null)
            {
                return;
            }

            double t = _director.time;
            for (int i = 0; i < hitWindows.Length; i++)
            {
                if (_hitWindowFired[i] || hitWindows[i] == null)
                {
                    continue;
                }

                if (t + 0.0001d < hitWindows[i].time)
                {
                    continue;
                }

                _hitWindowFired[i] = true;
                FireSkillHitWindow(hitWindows[i]);
            }
        }

        bool HasSkillHitWindows()
        {
            return hitWindows != null && hitWindows.Length > 0;
        }

        void SetAnimHitSuppressed(bool suppressed)
        {
            ResolveHitRelay();
            if (_hitRelay != null)
            {
                _hitRelay.SuppressAnimHits = suppressed;
            }
        }

        void FireSkillHitWindow(SkillHitWindow window)
        {
            ResolveHitRelay();
            if (_hitRelay == null)
            {
                Debug.LogWarning("[CharacterSkillPlayer] 无 AttackHitRelay，技能伤害窗口跳过。", this);
                return;
            }

            if (window.spawnSlashVfx)
            {
                _hitRelay.PlaySlashVfx(2);
            }

            _hitRelay.PerformFanHit(
                window.damage,
                window.hitRadius,
                window.fanAngle,
                window.knockback,
                window.minHitDistance,
                window.hitHeight,
                comboIndex: 2,
                clearHitIds: true);
        }

        public void Stop()
        {
            if (!_playing)
            {
                CleanupRuntime(true);
                return;
            }

            if (_director != null)
            {
                _director.stopped -= OnDirectorStopped;
                if (_director.state == PlayState.Playing)
                {
                    _director.Stop();
                }
            }

            FinishPlayback();
        }

        void OnDirectorStopped(PlayableDirector director)
        {
            if (director != _director)
            {
                return;
            }

            director.stopped -= OnDirectorStopped;
            FinishPlayback();
        }

        void FinishPlayback()
        {
            if (!_playing)
            {
                CleanupRuntime(true);
                return;
            }

            _playing = false;
            CleanupRuntime(true);
            PlayFinished?.Invoke();
        }

        void ResolveRefs()
        {
            if (!autoFindRefs)
            {
                return;
            }

            if (characterAnimator == null)
            {
                characterAnimator = GetComponentInChildren<Animator>();
            }

            if (thirdPersonCamera == null)
            {
                thirdPersonCamera = GameServices.ResolveCamera();
            }

            if (skillAudioSource == null)
            {
                skillAudioSource = GetComponent<AudioSource>();
            }
        }

        void EnsureAudioSource()
        {
            if (skillAudioSource != null)
            {
                return;
            }

            skillAudioSource = _runtimeRoot.GetComponent<AudioSource>();
            if (skillAudioSource == null)
            {
                skillAudioSource = _runtimeRoot.AddComponent<AudioSource>();
            }

            skillAudioSource.playOnAwake = false;
            skillAudioSource.spatialBlend = 0f;
        }

        bool EnsureRuntimeRoot()
        {
            if (_runtimeRoot == null)
            {
                _runtimeRoot = Instantiate(skillTimelinePrefab);
                _runtimeRoot.name = skillTimelinePrefab.name + "_Runtime";
            }

            _runtimeRoot.SetActive(true);
            if (alignTimelineToCharacter)
            {
                _runtimeRoot.transform.SetPositionAndRotation(transform.position, transform.rotation);
            }

            _director = _runtimeRoot.GetComponent<PlayableDirector>();
            if (_director == null)
            {
                Debug.LogError("[CharacterSkillPlayer] Timeline Prefab 上缺少 PlayableDirector。", _runtimeRoot);
                return false;
            }

            _director.playOnAwake = false;
            _director.extrapolationMode = DirectorWrapMode.None;
            if (_director.state == PlayState.Playing)
            {
                _director.Stop();
            }

            return true;
        }

        void EnsureSkillCamera()
        {
            if (skillCameraPrefab == null)
            {
                Debug.LogWarning("[CharacterSkillPlayer] 未指定 skillCameraPrefab。", this);
                return;
            }

            Transform parent = parentSkillCameraToCharacter ? transform : _runtimeRoot.transform;
            if (_runtimeCamera == null)
            {
                _runtimeCamera = Instantiate(skillCameraPrefab, parent);
                _runtimeCamera.name = skillCameraPrefab.name + "_Runtime";
            }
            else if (_runtimeCamera.transform.parent != parent)
            {
                _runtimeCamera.transform.SetParent(parent, false);
            }

            _runtimeCamera.transform.localPosition = skillCameraPrefab.transform.localPosition;
            _runtimeCamera.transform.localRotation = skillCameraPrefab.transform.localRotation;
            _runtimeCamera.transform.localScale = Vector3.one;
            _runtimeCamera.SetActive(false);
        }

        void EnsureCircleVfx()
        {
            if (circleVfxPrefab == null)
            {
                Debug.LogWarning("[CharacterSkillPlayer] 未指定 circleVfxPrefab（Freeze circle）。", this);
                return;
            }

            Transform parent = parentCircleToCharacter ? transform : _runtimeRoot.transform;
            if (_runtimeCircle == null)
            {
                _runtimeCircle = Instantiate(circleVfxPrefab, parent);
                _runtimeCircle.name = circleVfxPrefab.name + "_Runtime";
            }
            else if (_runtimeCircle.transform.parent != parent)
            {
                _runtimeCircle.transform.SetParent(parent, false);
            }

            _runtimeCircle.transform.localPosition = Vector3.zero;
            _runtimeCircle.transform.localRotation = Quaternion.identity;
            _runtimeCircle.transform.localScale = Vector3.one;
            // 交给 Circle Activation Track 打开
            _runtimeCircle.SetActive(false);
        }

        bool BindTracksByName(PlayableDirector director)
        {
            var timeline = director.playableAsset as TimelineAsset;
            if (timeline == null)
            {
                Debug.LogError("[CharacterSkillPlayer] PlayableAsset 不是 TimelineAsset。", director);
                return false;
            }

            bool boundSwingAnim = false;
            Animator camAnimator = _runtimeCamera != null ? _runtimeCamera.GetComponent<Animator>() : null;

            foreach (var track in timeline.GetOutputTracks())
            {
                if (track == null || track.muted)
                {
                    continue;
                }

                string trackName = track.name;

                if (track is AnimationTrack animTrack)
                {
                    if (trackName == TrackSwingAnimation)
                    {
                        if (useSceneOffsetsForCharacter)
                        {
                            animTrack.trackOffset = TrackOffset.ApplySceneOffsets;
                        }

                        director.SetGenericBinding(track, characterAnimator);
                        boundSwingAnim = true;
                    }
                    else if (trackName == TrackSkillCamera)
                    {
                        if (camAnimator == null)
                        {
                            Debug.LogWarning("[CharacterSkillPlayer] SkillCamera 动画轨：技能相机缺少 Animator。", this);
                            continue;
                        }

                        animTrack.trackOffset = TrackOffset.ApplySceneOffsets;
                        director.SetGenericBinding(track, camAnimator);
                    }
                    else
                    {
                        Debug.LogWarning($"[CharacterSkillPlayer] 未识别的 Animation 轨：{trackName}", this);
                    }
                }
                else if (track is AudioTrack)
                {
                    if (trackName == TrackSwingAudio || string.IsNullOrEmpty(trackName))
                    {
                        director.SetGenericBinding(track, skillAudioSource);
                    }
                }
                else if (track is ActivationTrack)
                {
                    if (trackName == TrackSkillCamera)
                    {
                        if (_runtimeCamera == null)
                        {
                            Debug.LogWarning("[CharacterSkillPlayer] SkillCamera Activation：未生成技能相机。", this);
                            continue;
                        }

                        director.SetGenericBinding(track, _runtimeCamera);
                    }
                    else if (trackName == TrackCircle)
                    {
                        if (_runtimeCircle == null)
                        {
                            Debug.LogWarning("[CharacterSkillPlayer] Circle Activation：未生成 Freeze circle。", this);
                            continue;
                        }

                        director.SetGenericBinding(track, _runtimeCircle);
                    }
                    else
                    {
                        Debug.LogWarning($"[CharacterSkillPlayer] 未识别的 Activation 轨：{trackName}", this);
                    }
                }
            }

            if (!boundSwingAnim)
            {
                Debug.LogError($"[CharacterSkillPlayer] 未找到名为 {TrackSwingAnimation} 的 Animation 轨。", director);
            }

            return boundSwingAnim;
        }

        void CachePoseBeforePlay()
        {
            _characterController = GetComponent<CharacterController>();
            _playStartRootPos = transform.position;
            _playStartRootRot = transform.rotation;

            if (characterAnimator != null)
            {
                _animatorLocalPos = characterAnimator.transform.localPosition;
                _animatorLocalRot = characterAnimator.transform.localRotation;
                _animatorRootMotionWasEnabled = characterAnimator.applyRootMotion;
                characterAnimator.applyRootMotion = false;
            }
        }

        void RestoreRootIfTeleported(float threshold)
        {
            if (Vector3.Distance(transform.position, _playStartRootPos) <= threshold)
            {
                return;
            }

            Debug.LogWarning(
                $"[CharacterSkillPlayer] 检测到开播瞬移 {transform.position} <- {_playStartRootPos}，已拉回。",
                this);
            SetCharacterPosition(_playStartRootPos, _playStartRootRot);
            RestoreAnimatorLocalPose();
        }

        void TransferAnimatorDeltaToRoot()
        {
            Transform animT = characterAnimator.transform;
            Vector3 worldDelta = animT.position - transform.TransformPoint(_animatorLocalPos);
            worldDelta.y = 0f;
            if (worldDelta.sqrMagnitude < 0.000001f)
            {
                if ((animT.localPosition - _animatorLocalPos).sqrMagnitude > 0.000001f)
                {
                    animT.localPosition = _animatorLocalPos;
                }

                return;
            }

            SetCharacterPosition(transform.position + worldDelta, transform.rotation);
            animT.localPosition = _animatorLocalPos;
            animT.localRotation = _animatorLocalRot;
        }

        void SetCharacterPosition(Vector3 position, Quaternion rotation)
        {
            bool ccWasEnabled = false;
            if (_characterController != null)
            {
                ccWasEnabled = _characterController.enabled;
                _characterController.enabled = false;
            }

            transform.SetPositionAndRotation(position, rotation);

            if (_characterController != null)
            {
                _characterController.enabled = ccWasEnabled;
            }
        }

        void RestoreAnimatorLocalPose()
        {
            if (characterAnimator == null || characterAnimator.transform == transform)
            {
                return;
            }

            characterAnimator.transform.localPosition = _animatorLocalPos;
            characterAnimator.transform.localRotation = _animatorLocalRot;
        }

        void TakeOverCamera()
        {
            if (!allowCameraTakeover || thirdPersonCamera == null)
            {
                return;
            }

            thirdPersonCamera.SetGameplayControlEnabled(false);

            _tpcCamera = thirdPersonCamera.ControlledCamera;
            if (_tpcCamera != null)
            {
                _tpcCamera.enabled = false;
                _tpcListener = _tpcCamera.GetComponent<AudioListener>();
                if (_tpcListener != null)
                {
                    _tpcListener.enabled = false;
                }
            }
        }

        void RestoreCamera()
        {
            if (thirdPersonCamera != null)
            {
                // 大招结束务必恢复跟拍与锁鼠，避免「鼠标不转」
                thirdPersonCamera.SetGameplayControlEnabled(true);
                thirdPersonCamera.RestoreDesiredCursorLock();
            }

            if (_tpcCamera != null)
            {
                _tpcCamera.enabled = true;
            }

            if (_tpcListener != null)
            {
                _tpcListener.enabled = true;
            }

            _tpcCamera = null;
            _tpcListener = null;
        }

        void CleanupRuntime(bool restoreCamera)
        {
            if (_director != null)
            {
                _director.stopped -= OnDirectorStopped;
                if (_director.state == PlayState.Playing)
                {
                    _director.Stop();
                }
            }

            SetAnimHitSuppressed(false);

            // 池化：停用并保留实例，避免每次大招 Instantiate/Destroy
            if (_runtimeCircle != null)
            {
                _runtimeCircle.SetActive(false);
            }

            if (_runtimeCamera != null)
            {
                _runtimeCamera.SetActive(false);
            }

            if (_runtimeRoot != null)
            {
                _runtimeRoot.SetActive(false);
            }

            if (bakeAnimatorMotionToRoot)
            {
                TransferAnimatorDeltaToRoot();
                RestoreAnimatorLocalPose();
            }

            if (characterAnimator != null)
            {
                characterAnimator.applyRootMotion = _animatorRootMotionWasEnabled;
            }

            if (restoreCamera)
            {
                RestoreCamera();
            }

            _playing = false;
        }

        void DestroyRuntimeInstances()
        {
            if (_director != null)
            {
                _director.stopped -= OnDirectorStopped;
                _director = null;
            }

            if (_runtimeCircle != null)
            {
                Destroy(_runtimeCircle);
                _runtimeCircle = null;
            }

            if (_runtimeCamera != null)
            {
                Destroy(_runtimeCamera);
                _runtimeCamera = null;
            }

            if (_runtimeRoot != null)
            {
                Destroy(_runtimeRoot);
                _runtimeRoot = null;
            }

            _playing = false;
        }
    }
}
