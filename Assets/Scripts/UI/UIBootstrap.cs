using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using AttackSkill.Core;

namespace AttackSkill.UI
{
    /// <summary>
    /// UI 根引导：DontDestroyOnLoad、单一 EventSystem（Input System UI 模块）、进游戏后卸载开场 Flow。
    /// 挂在 UIRoot 上（与 UIManager 同物体）。
    /// </summary>
    [DefaultExecutionOrder(-160)]
    [DisallowMultipleComponent]
    public class UIBootstrap : MonoBehaviour
    {
        public static UIBootstrap Instance { get; private set; }

        [SerializeField] bool dontDestroyOnLoad = true;
        [SerializeField] bool manageEventSystem = true;
        [Tooltip("进游戏后销毁 OpenSceneFlowController，避免残留 Update/协程")]
        [SerializeField] bool destroyFlowAfterEnterGame = true;

        void Awake()
        {
            if (!SceneSingleton.ShouldKeep(this, Instance))
            {
                return;
            }

            Instance = this;
            SceneSingleton.ApplyDontDestroyOnLoad(this, dontDestroyOnLoad);

            if (manageEventSystem)
            {
                EnsureSingleEventSystem();
            }
        }

        void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (manageEventSystem)
            {
                EnsureSingleEventSystem();
            }
        }

        /// <summary>开场进游戏后由 Flow 调用。</summary>
        public void NotifyEnteredGame()
        {
            if (!destroyFlowAfterEnterGame)
            {
                return;
            }

            var flow = GameServices.OpenSceneFlow;
            if (flow == null)
            {
                flow = GetComponent<OpenSceneFlowController>();
            }

            if (flow == null)
            {
                return;
            }

            GameServices.Unregister(flow);
            Destroy(flow);
            Debug.Log("[UIBootstrap] 已卸载 OpenSceneFlowController。");
        }

        /// <summary>保留一个 EventSystem；多余的销毁；统一换 InputSystemUIInputModule。</summary>
        public static void EnsureSingleEventSystem()
        {
            var systems = Object.FindObjectsOfType<EventSystem>();
            if (systems == null || systems.Length == 0)
            {
                var go = new GameObject("EventSystem");
                go.AddComponent<EventSystem>();
                go.AddComponent<InputSystemUIInputModule>();
                if (Instance != null)
                {
                    go.transform.SetParent(Instance.transform, false);
                }

                Debug.Log("[UIBootstrap] 已创建 EventSystem（Input System UI）。");
                return;
            }

            EventSystem keep = null;
            if (Instance != null)
            {
                for (int i = 0; i < systems.Length; i++)
                {
                    if (systems[i] != null && systems[i].transform.IsChildOf(Instance.transform))
                    {
                        keep = systems[i];
                        break;
                    }
                }
            }

            if (keep == null)
            {
                keep = systems[0];
            }

            for (int i = 0; i < systems.Length; i++)
            {
                if (systems[i] == null)
                {
                    continue;
                }

                if (systems[i] != keep)
                {
                    Debug.Log($"[UIBootstrap] 销毁多余 EventSystem：{systems[i].gameObject.scene.name}/{systems[i].name}");
                    Destroy(systems[i].gameObject);
                    continue;
                }

                UpgradeToInputSystemUiModule(systems[i].gameObject);
            }
        }

        /// <summary>去掉 StandaloneInputModule，确保有 InputSystemUIInputModule。</summary>
        public static void UpgradeToInputSystemUiModule(GameObject eventSystemGo)
        {
            if (eventSystemGo == null)
            {
                return;
            }

            var legacy = eventSystemGo.GetComponent<StandaloneInputModule>();
            if (legacy != null)
            {
                Object.DestroyImmediate(legacy);
            }

            if (eventSystemGo.GetComponent<InputSystemUIInputModule>() == null)
            {
                eventSystemGo.AddComponent<InputSystemUIInputModule>();
                Debug.Log("[UIBootstrap] EventSystem 已切换为 InputSystemUIInputModule。", eventSystemGo);
            }
        }
    }
}
