using UnityEngine;
using UnityEngine.SceneManagement;
using AttackSkill.Character;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AttackSkill.Audio
{
    /// <summary>
    /// GameScene 背景音乐。进 GameScene 播 SeaBGM；传送进肉鸽平面后切 drone。
    /// </summary>
    public class SceneBgmPlayer : MonoBehaviour
    {
        public const string GameSceneName = "GameScene";

        [SerializeField] AudioClip seaBgm;
        [SerializeField] AudioClip droneBgm;
        [SerializeField, Range(0f, 1f)] float volume = 0.4f;
        [SerializeField] bool dontDestroyOnLoad = true;

        AudioSource _source;
        bool _rougeDrone;
        static SceneBgmPlayer _instance;

        public static SceneBgmPlayer EnsureExists()
        {
            if (_instance != null)
            {
                return _instance;
            }

            var existing = Object.FindObjectOfType<SceneBgmPlayer>();
            if (existing != null)
            {
                _instance = existing;
                return _instance;
            }

            var go = new GameObject("SceneBgmPlayer");
            _instance = go.AddComponent<SceneBgmPlayer>();
            return _instance;
        }

        /// <summary>进战斗场景后由 GameProgress 再调一次，避免场景名/时序漏播。</summary>
        public static void EnsurePlayingForActiveScene()
        {
            var player = EnsureExists();
            player.ApplyForActiveScene();
        }

        /// <summary>肉鸽传送后切 drone，直到离开 GameScene 或返回海滩。</summary>
        public static void PlayRougeDrone()
        {
            EnsureExists().SwitchToRougeDrone();
        }

        /// <summary>暂停重置回海滩：取消 drone，改播海滨 BGM。</summary>
        public static void PlayCampTheme()
        {
            EnsureExists().SwitchToCampTheme();
        }

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            if (dontDestroyOnLoad)
            {
                DontDestroyOnLoad(gameObject);
            }

            _source = gameObject.GetComponent<AudioSource>();
            if (_source == null)
            {
                _source = gameObject.AddComponent<AudioSource>();
            }

            _source.playOnAwake = false;
            _source.loop = true;
            _source.spatialBlend = 0f;
            _source.volume = volume;
            _source.ignoreListenerPause = false;

            ResolveClips();
            SceneManager.sceneLoaded += OnSceneLoaded;
            ApplyForActiveScene();
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_instance == this)
            {
                _instance = null;
            }
        }

        void ResolveClips()
        {
            var settings = CharacterRuntimeSettings.Get();
            if (seaBgm == null && settings != null && settings.seaBgm != null)
            {
                seaBgm = settings.seaBgm;
            }

            if (droneBgm == null && settings != null && settings.droneBgm != null)
            {
                droneBgm = settings.droneBgm;
            }

#if UNITY_EDITOR
            if (seaBgm == null)
            {
                seaBgm = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SeaBGM.wav");
            }

            if (droneBgm == null)
            {
                droneBgm = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/drone.mp3");
            }
#endif
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ApplyForScene(scene.name);
        }

        public void ApplyForActiveScene()
        {
            ApplyForScene(SceneManager.GetActiveScene().name);
        }

        void ApplyForScene(string sceneName)
        {
            if (IsGameScene(sceneName))
            {
                if (_rougeDrone)
                {
                    SwitchToRougeDrone();
                }
                else
                {
                    Play();
                }
            }
            else
            {
                _rougeDrone = false;
                Stop();
            }
        }

        static bool IsGameScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                return false;
            }

            return sceneName == GameSceneName ||
                   sceneName.IndexOf("GameScene", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public void Play()
        {
            ResolveClips();
            PlayClip(seaBgm, "SeaBGM");
        }

        void SwitchToRougeDrone()
        {
            _rougeDrone = true;
            ResolveClips();
            if (droneBgm == null)
            {
                Debug.LogWarning(
                    "[SceneBgmPlayer] drone BGM 未配置。请在 CharacterRuntimeSettings.droneBgm 指定 Assets/Audio/drone.mp3。",
                    this);
                return;
            }

            PlayClip(droneBgm, "drone");
        }

        void SwitchToCampTheme()
        {
            _rougeDrone = false;
            Play();
        }

        void PlayClip(AudioClip clip, string label)
        {
            if (_source == null || clip == null)
            {
                if (clip == null)
                {
                    Debug.LogWarning($"[SceneBgmPlayer] {label} 未配置。", this);
                }

                return;
            }

            // 导入设置 preloadAudioData=0 时，不显式加载会静音
            if (clip.loadState == AudioDataLoadState.Unloaded)
            {
                clip.LoadAudioData();
            }

            AudioListener.pause = false;
            _source.mute = false;
            _source.volume = volume;
            if (_source.clip != clip)
            {
                _source.clip = clip;
                _source.Play();
            }
            else if (!_source.isPlaying)
            {
                _source.Play();
            }

            if (!_source.isPlaying)
            {
                Debug.LogWarning(
                    $"[SceneBgmPlayer] Play 后仍未播放。clip={clip.name} loadState={clip.loadState}",
                    this);
            }
        }

        public void Stop()
        {
            if (_source != null && _source.isPlaying)
            {
                _source.Stop();
            }
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (seaBgm == null)
            {
                seaBgm = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SeaBGM.wav");
            }

            if (droneBgm == null)
            {
                droneBgm = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/drone.mp3");
            }
        }
#endif
    }
}
