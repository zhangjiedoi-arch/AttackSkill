using UnityEngine;
using UnityEngine.SceneManagement;
using AttackSkill.Character;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AttackSkill.Audio
{
    /// <summary>
    /// GameScene 背景音乐（SeaBGM）。进 GameScene 播放，离开停止。
    /// </summary>
    public class SceneBgmPlayer : MonoBehaviour
    {
        public const string GameSceneName = "GameScene";

        [SerializeField] AudioClip seaBgm;
        [SerializeField, Range(0f, 1f)] float volume = 0.4f;
        [SerializeField] bool dontDestroyOnLoad = true;

        AudioSource _source;
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

            ResolveClip();
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

        void ResolveClip()
        {
            if (seaBgm != null)
            {
                return;
            }

            var settings = CharacterRuntimeSettings.Get();
            if (settings != null && settings.seaBgm != null)
            {
                seaBgm = settings.seaBgm;
                return;
            }

#if UNITY_EDITOR
            seaBgm = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SeaBGM.wav");
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
                Play();
            }
            else
            {
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
            ResolveClip();
            if (_source == null || seaBgm == null)
            {
                Debug.LogWarning("[SceneBgmPlayer] SeaBGM 未配置。请在 CharacterRuntimeSettings.seaBgm 指定。", this);
                return;
            }

            // 导入设置 preloadAudioData=0 时，不显式加载会静音
            if (seaBgm.loadState == AudioDataLoadState.Unloaded)
            {
                seaBgm.LoadAudioData();
            }

            AudioListener.pause = false;
            _source.mute = false;
            _source.volume = volume;
            if (_source.clip != seaBgm)
            {
                _source.clip = seaBgm;
            }

            if (!_source.isPlaying)
            {
                _source.Play();
            }

            if (!_source.isPlaying)
            {
                Debug.LogWarning(
                    $"[SceneBgmPlayer] Play 后仍未播放。clip={seaBgm.name} loadState={seaBgm.loadState}",
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
        }
#endif
    }
}
