using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AttackSkill.Character
{
    /// <summary>
    /// 角色常用音效：脚步 / 跳跃 / 落地 / 挥砍 + 探索工具（飞行/御剑/摩托）循环与点缀。
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class CharacterAudio : MonoBehaviour
    {
        [Header("Clips")]
        [SerializeField] AudioClip[] footsteps = new AudioClip[4];
        [SerializeField] AudioClip jump;
        [SerializeField] AudioClip land;
        [Tooltip("优先于 land：普通落地（Jump_Land）")]
        [SerializeField] AudioClip jumpLand;
        [Tooltip("下落过程循环（Jump_Loop），Fall 状态播放")]
        [SerializeField] AudioClip jumpLoop;
        [SerializeField] AudioClip swinging;
        [SerializeField] AudioClip largeSwordSwing;

        [Header("Exploration — Flight")]
        [SerializeField] AudioClip flyingLoop;
        [SerializeField] AudioClip flyingTakeOff;
        [SerializeField] AudioClip flyingGoUp;
        [SerializeField] AudioClip flyingDown;

        [Header("Exploration — Sword Flight")]
        [SerializeField] AudioClip swordFlyingLoop;
        [SerializeField] AudioClip swordFlyingGoOn;

        [Header("Exploration — Motorcycle")]
        [SerializeField] AudioClip motorcycleLoop;
        [SerializeField] AudioClip motorcycleDownSpeed;
        [SerializeField] AudioClip motorcycleGoOn;
        [SerializeField] AudioClip motorcycleJump;

        [Header("Footsteps")]
        [SerializeField] float walkStepInterval = 0.45f;
        [SerializeField] float sprintStepInterval = 0.32f;
        [SerializeField, Range(0f, 1f)] float footstepVolume = 0.55f;
        [SerializeField, Range(0f, 1f)] float jumpVolume = 0.7f;
        [SerializeField, Range(0f, 1f)] float landVolume = 0.75f;
        [SerializeField, Range(0f, 1f)] float jumpFallLoopVolume = 0.35f;
        [SerializeField, Range(0f, 1f)] float swingVolume = 0.85f;

        [Header("Exploration Volumes")]
        [SerializeField, Range(0f, 1f)] float explorationOneShotVolume = 0.8f;
        [SerializeField, Range(0f, 1f)] float explorationLoopVolume = 0.45f;

        AudioSource _source;
        AudioSource _loopSource;
        AudioSource _fallLoopSource;
        float _stepTimer;
        int _lastFootIndex = -1;

        AudioClip _currentLoopClip;
        bool _jumpHeldPrev;
        bool _throttlePrev;

        public AudioSource Source => _source;

        void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 1f;
            _source.rolloffMode = AudioRolloffMode.Linear;
            _source.maxDistance = 22f;

            EnsureLoopSource();
            EnsureFallLoopSource();
            TryAutoAssignClips();
            ApplyClipsFromRuntimeSettings();
            if (HasMissingClips())
            {
                Debug.LogWarning(
                    "[CharacterAudio] 音效 Clip 未配齐。请在 Prefab 上序列化引用或填 CharacterRuntimeSettings。",
                    this);
            }
        }

        void OnDisable()
        {
            StopExplorationLoop();
            EndFallLoop();
        }

        void EnsureLoopSource()
        {
            if (_loopSource != null)
            {
                return;
            }

            var go = new GameObject("ExplorationLoopAudio");
            go.transform.SetParent(transform, false);
            _loopSource = go.AddComponent<AudioSource>();
            _loopSource.playOnAwake = false;
            _loopSource.loop = true;
            _loopSource.spatialBlend = 1f;
            _loopSource.rolloffMode = AudioRolloffMode.Linear;
            _loopSource.maxDistance = 28f;
            _loopSource.volume = explorationLoopVolume;
        }

        void EnsureFallLoopSource()
        {
            if (_fallLoopSource != null)
            {
                return;
            }

            var go = new GameObject("JumpFallLoopAudio");
            go.transform.SetParent(transform, false);
            _fallLoopSource = go.AddComponent<AudioSource>();
            _fallLoopSource.playOnAwake = false;
            _fallLoopSource.loop = true;
            _fallLoopSource.spatialBlend = 1f;
            _fallLoopSource.rolloffMode = AudioRolloffMode.Linear;
            _fallLoopSource.maxDistance = 22f;
            _fallLoopSource.volume = jumpFallLoopVolume;
        }

        public void ResetFootstepTimer()
        {
            _stepTimer = 0f;
        }

        public void TickFootsteps(float deltaTime, bool sprinting)
        {
            _stepTimer += deltaTime;
            float interval = sprinting ? sprintStepInterval : walkStepInterval;
            if (_stepTimer < interval)
            {
                return;
            }

            _stepTimer = 0f;
            PlayFootstep();
        }

        public void PlayFootstep()
        {
            if (footsteps == null || footsteps.Length == 0)
            {
                return;
            }

            int index = Random.Range(0, footsteps.Length);
            if (footsteps.Length > 1 && index == _lastFootIndex)
            {
                index = (index + 1) % footsteps.Length;
            }

            _lastFootIndex = index;
            PlayOneShot(footsteps[index], footstepVolume);
        }

        public void PlayJump()
        {
            PlayOneShot(jump, jumpVolume);
        }

        /// <summary>进入 <c>Fall</c>：下落风声循环。</summary>
        public void BeginFallLoop()
        {
            EnsureFallLoopSource();
            if (jumpLoop == null || _fallLoopSource == null)
            {
                return;
            }

            if (jumpLoop.loadState == AudioDataLoadState.Unloaded)
            {
                jumpLoop.LoadAudioData();
            }

            if (_fallLoopSource.clip == jumpLoop && _fallLoopSource.isPlaying)
            {
                _fallLoopSource.volume = jumpFallLoopVolume;
                return;
            }

            _fallLoopSource.clip = jumpLoop;
            _fallLoopSource.mute = false;
            _fallLoopSource.volume = jumpFallLoopVolume;
            _fallLoopSource.loop = true;
            _fallLoopSource.Play();
        }

        /// <summary>离开 <c>Fall</c>（落地 / 滑翔 / 飞行等）。</summary>
        public void EndFallLoop()
        {
            if (_fallLoopSource == null)
            {
                return;
            }

            if (_fallLoopSource.isPlaying)
            {
                _fallLoopSource.Stop();
            }

            _fallLoopSource.clip = null;
        }

        public void PlayLand()
        {
            EndFallLoop();
            AudioClip clip = jumpLand != null ? jumpLand : land;
            PlayOneShot(clip, landVolume);
        }

        /// <summary>0 = swinging，1 = large-sword-swing（其它值按重挥处理）。</summary>
        public void PlaySwing(int swingType)
        {
            AudioClip clip = swingType <= 0 ? swinging : largeSwordSwing;
            PlayOneShot(clip, swingVolume);
        }

        /// <summary>技能/出伤帧等一次性音效（由 SkillHitSegment.sfxClip 驱动）。</summary>
        public void PlaySfx(AudioClip clip, float volume = 1f)
        {
            PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        // ----- 翅膀飞行 -----

        public void BeginWingFlight(bool playTakeOff = true)
        {
            _jumpHeldPrev = false;
            if (playTakeOff)
            {
                PlayOneShot(flyingTakeOff, explorationOneShotVolume);
            }

            PlayExplorationLoop(flyingLoop);
        }

        public void EndWingFlight(bool playLand = true)
        {
            StopExplorationLoop();
            if (playLand)
            {
                PlayOneShot(flyingDown, explorationOneShotVolume);
            }
        }

        public void TickWingFlight(bool jumpHeld)
        {
            if (jumpHeld && !_jumpHeldPrev)
            {
                PlayOneShot(flyingGoUp, explorationOneShotVolume);
            }

            _jumpHeldPrev = jumpHeld;
        }

        // ----- 御剑 -----

        public void BeginSwordFlight(bool playTakeOff = true)
        {
            _jumpHeldPrev = false;
            if (playTakeOff)
            {
                PlayOneShot(flyingTakeOff, explorationOneShotVolume);
            }

            PlayExplorationLoop(swordFlyingLoop);
        }

        public void EndSwordFlight(bool playLand = true)
        {
            StopExplorationLoop();
            if (playLand)
            {
                PlayOneShot(flyingDown, explorationOneShotVolume);
            }
        }

        public void TickSwordFlight(bool jumpHeld)
        {
            if (jumpHeld && !_jumpHeldPrev)
            {
                PlayOneShot(swordFlyingGoOn, explorationOneShotVolume);
            }

            _jumpHeldPrev = jumpHeld;
        }

        // ----- 摩托 -----

        public void BeginMotorcycle()
        {
            _throttlePrev = false;
            PlayExplorationLoop(motorcycleDownSpeed != null ? motorcycleDownSpeed : motorcycleLoop);
        }

        public void EndMotorcycle()
        {
            StopExplorationLoop();
        }

        public void PlayMotorcycleJump()
        {
            PlayOneShot(motorcycleJump != null ? motorcycleJump : jump, explorationOneShotVolume);
        }

        /// <summary>
        /// throttleAbs&gt;0.05 用 Motorcycle 循环并在按下油门时播加速；
        /// 否则用 MotorcycleDownSpeed（匀速/滑行）。
        /// </summary>
        public void TickMotorcycle(float throttle)
        {
            bool throttleOn = Mathf.Abs(throttle) > 0.05f;
            if (throttleOn && !_throttlePrev)
            {
                PlayOneShot(motorcycleGoOn, explorationOneShotVolume);
            }

            AudioClip want = throttleOn ? motorcycleLoop : motorcycleDownSpeed;
            if (want == null)
            {
                want = motorcycleLoop != null ? motorcycleLoop : motorcycleDownSpeed;
            }

            PlayExplorationLoop(want);
            _throttlePrev = throttleOn;
        }

        public void StopExplorationLoop()
        {
            _currentLoopClip = null;
            _jumpHeldPrev = false;
            _throttlePrev = false;
            if (_loopSource != null && _loopSource.isPlaying)
            {
                _loopSource.Stop();
            }

            if (_loopSource != null)
            {
                _loopSource.clip = null;
            }
        }

        void PlayOneShot(AudioClip clip, float volume)
        {
            if (clip == null || _source == null)
            {
                return;
            }

            if (clip.loadState == AudioDataLoadState.Unloaded)
            {
                clip.LoadAudioData();
            }

            _source.mute = false;
            _source.PlayOneShot(clip, volume);
        }

        void PlayExplorationLoop(AudioClip clip)
        {
            EnsureLoopSource();
            if (clip == null || _loopSource == null)
            {
                return;
            }

            if (clip.loadState == AudioDataLoadState.Unloaded)
            {
                clip.LoadAudioData();
            }

            if (_currentLoopClip == clip && _loopSource.isPlaying)
            {
                _loopSource.volume = explorationLoopVolume;
                return;
            }

            _currentLoopClip = clip;
            _loopSource.clip = clip;
            _loopSource.mute = false;
            _loopSource.volume = explorationLoopVolume;
            _loopSource.loop = true;
            _loopSource.Play();
        }

        void ApplyClipsFromRuntimeSettings()
        {
            var settings = CharacterRuntimeSettings.Get();
            if (settings == null)
            {
                return;
            }

            if (flyingLoop == null) flyingLoop = settings.flyingLoop;
            if (flyingTakeOff == null) flyingTakeOff = settings.flyingTakeOff;
            if (flyingGoUp == null) flyingGoUp = settings.flyingGoUp;
            if (flyingDown == null) flyingDown = settings.flyingDown;
            if (swordFlyingLoop == null) swordFlyingLoop = settings.swordFlyingLoop;
            if (swordFlyingGoOn == null) swordFlyingGoOn = settings.swordFlyingGoOn;
            if (motorcycleLoop == null) motorcycleLoop = settings.motorcycleLoop;
            if (motorcycleDownSpeed == null) motorcycleDownSpeed = settings.motorcycleDownSpeed;
            if (motorcycleGoOn == null) motorcycleGoOn = settings.motorcycleGoOn;
            if (motorcycleJump == null) motorcycleJump = settings.motorcycleJump;
            if (jumpLand == null) jumpLand = settings.jumpLand;
            if (jumpLoop == null) jumpLoop = settings.jumpLoop;
        }

        void TryAutoAssignClips()
        {
#if UNITY_EDITOR
            if (HasMissingClips())
            {
                AssignFromAudioFolder();
            }
            else
            {
                EnsureOptionalJumpClips();
            }
#endif
        }

        bool HasMissingClips()
        {
            if (jump == null || land == null || swinging == null || largeSwordSwing == null)
            {
                return true;
            }

            if (footsteps == null || footsteps.Length < 4)
            {
                return true;
            }

            for (int i = 0; i < 4; i++)
            {
                if (footsteps[i] == null)
                {
                    return true;
                }
            }

            if (flyingLoop == null || flyingTakeOff == null || flyingGoUp == null || flyingDown == null)
            {
                return true;
            }

            if (swordFlyingLoop == null || swordFlyingGoOn == null)
            {
                return true;
            }

            if (motorcycleLoop == null || motorcycleDownSpeed == null ||
                motorcycleGoOn == null || motorcycleJump == null)
            {
                return true;
            }

            return false;
        }

#if UNITY_EDITOR
        void Reset()
        {
            AssignFromAudioFolder();
        }

        void OnValidate()
        {
            if (HasMissingClips())
            {
                AssignFromAudioFolder();
            }
            else
            {
                EnsureOptionalJumpClips();
            }
        }

        void EnsureOptionalJumpClips()
        {
            if (jumpLand == null)
            {
                jumpLand = LoadClip("Assets/Audio/Jump_Land.wav");
            }

            if (jumpLoop == null)
            {
                jumpLoop = LoadClip("Assets/Audio/Jump_Loop.wav");
            }
        }

        void AssignFromAudioFolder()
        {
            if (footsteps == null || footsteps.Length != 4)
            {
                footsteps = new AudioClip[4];
            }

            footsteps[0] = LoadClip("Assets/Audio/Footstep01.wav");
            footsteps[1] = LoadClip("Assets/Audio/Footstep02.wav");
            footsteps[2] = LoadClip("Assets/Audio/Footstep03.wav");
            footsteps[3] = LoadClip("Assets/Audio/Footstep04.wav");
            jump = LoadClip("Assets/Audio/Jump.wav");
            land = LoadClip("Assets/Audio/Land.wav");
            EnsureOptionalJumpClips();
            swinging = LoadClip("Assets/Audio/swinging.wav");
            largeSwordSwing = LoadClip("Assets/Audio/large-sword-swing.wav");

            flyingLoop = LoadClip("Assets/Audio/Flying.wav");
            flyingTakeOff = LoadClip("Assets/Audio/FlyingTakeOff.wav");
            flyingGoUp = LoadClip("Assets/Audio/FlyingGoUp.wav");
            flyingDown = LoadClip("Assets/Audio/FlyingDown.wav");

            swordFlyingLoop = LoadClip("Assets/Audio/SwordFlying.wav");
            swordFlyingGoOn = LoadClip("Assets/Audio/SwordFlyingGoOn.wav");

            motorcycleLoop = LoadClip("Assets/Audio/Motorcycle.wav");
            motorcycleDownSpeed = LoadClip("Assets/Audio/MotorcycleDownSpeed.wav");
            motorcycleGoOn = LoadClip("Assets/Audio/MotorcycleGoOn.wav");
            motorcycleJump = LoadClip("Assets/Audio/MotorcycleJump.wav");
        }

        static AudioClip LoadClip(string path)
        {
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
#endif
    }
}
