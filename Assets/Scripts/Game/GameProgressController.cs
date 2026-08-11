using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using AttackSkill.Audio;
using AttackSkill.Character;
using AttackSkill.Core;
using AttackSkill.Localization;
using AttackSkill.UI;

namespace AttackSkill.Game
{
    /// <summary>
    /// 进游戏读档：必要时切场景，再让 Party 按存档坐标生成。
    /// F5 快速存档；退出 / 暂停时自动存。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameProgressController : MonoBehaviour
    {
        public static GameProgressController Instance { get; private set; }

        [Header("Boot")]
        [SerializeField] bool loadSaveOnStart = true;
        [Tooltip("无存档或不加载时使用的默认场景；空则留在当前场景")]
        [SerializeField] string defaultSceneName;
        [SerializeField] bool dontDestroyOnLoad = true;

        [Header("Save")]
        [SerializeField] bool saveOnQuit = true;
        [SerializeField] bool saveOnPause = true;
        [SerializeField] KeyCode quickSaveKey = KeyCode.F5;
        [SerializeField] KeyCode deleteSaveKey = KeyCode.F6;
        [Tooltip("定时自动存档（秒），0=关闭")]
        [SerializeField] float autoSaveInterval = 60f;

        [Header("Debug")]
        [SerializeField] bool drawHud = false;

        public bool BootFinished { get; private set; }
        public bool IsLoadingScene { get; private set; }

        float _nextAutoSave = -1f;
        string _lastStatus = "Boot...";

        void Awake()
        {
            if (!SceneSingleton.ShouldKeep(this, Instance))
            {
                return;
            }

            Instance = this;
            SceneSingleton.ApplyDontDestroyOnLoad(this, dontDestroyOnLoad);
            SceneBgmPlayer.EnsureExists();
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        IEnumerator Start()
        {
            BootFinished = false;
            IsLoadingScene = false;

            GameBootIntent intent = GameBoot.ConsumeIntent();
            bool shouldLoadSave = intent == GameBootIntent.Continue ||
                                  (intent == GameBootIntent.Unspecified && loadSaveOnStart);

            if (intent == GameBootIntent.NewGame)
            {
                GameSaveService.ClearPendingRestore();
                BattleSkillWheelState.ResetToDefault();
                _lastStatus = LocalizationService.Get(LocalizationTableType.Common, "progress_new_game");
            }
            else if (shouldLoadSave && GameSaveService.TryLoad(out GameSaveData data))
            {
                GameSaveService.SetPendingRestore(data);
                _lastStatus = LocalizationService.Format(LocalizationTableType.Common, "progress_load_save", data.sceneName);

                string current = SceneManager.GetActiveScene().name;
                if (!string.IsNullOrEmpty(data.sceneName) && data.sceneName != current)
                {
                    IsLoadingScene = true;
                    _lastStatus = LocalizationService.Format(LocalizationTableType.Common, "progress_load_scene", data.sceneName);
                    var op = SceneManager.LoadSceneAsync(data.sceneName, LoadSceneMode.Single);
                    if (op == null)
                    {
                        Debug.LogError($"[GameProgress] 无法加载场景（检查 Build Settings）：{data.sceneName}");
                        GameSaveService.TryConsumePendingRestore(out _);
                        _lastStatus = LocalizationService.Get(LocalizationTableType.Common, "progress_load_scene_fail");
                    }
                    else
                    {
                        while (!op.isDone)
                        {
                            yield return null;
                        }
                    }

                    IsLoadingScene = false;
                }
            }
            else if (!string.IsNullOrEmpty(defaultSceneName) &&
                     SceneManager.GetActiveScene().name != defaultSceneName)
            {
                IsLoadingScene = true;
                yield return SceneManager.LoadSceneAsync(defaultSceneName, LoadSceneMode.Single);
                IsLoadingScene = false;
            }

            // 等 Party 进场景
            yield return null;

            var party = PartyController.Instance ?? GameServices.Party;
            if (party != null)
            {
                party.BeginPlayFromSaveOrDefault();
                string sceneName = SceneManager.GetActiveScene().name;
                _lastStatus = party.Active != null
                    ? LocalizationService.Format(LocalizationTableType.Common, "progress_ready_at", sceneName)
                    : LocalizationService.Get(LocalizationTableType.Common, "progress_ready_default");
            }
            else
            {
                _lastStatus = LocalizationService.Get(LocalizationTableType.Common, "progress_no_party");
                Debug.LogWarning("[GameProgress] 场景中没有 PartyController。");
            }

            // Continue 时 Party 已 Restore(equippedSkillIndex)；此处只补图标
            BattleSkillWheelState.EnsureIconResolved();
            UIManager.Instance?.OpenBattlePartyHud();
            SceneBgmPlayer.EnsurePlayingForActiveScene();

            BootFinished = true;
            if (autoSaveInterval > 0f)
            {
                _nextAutoSave = Time.unscaledTime + autoSaveInterval;
            }
        }

        void Update()
        {
            if (!BootFinished || IsLoadingScene)
            {
                return;
            }

            // 暂停时仍可 F5/F6（timeScale=0 用 GetKeyDown 仍可用）
            if (GameInput.GetKeyDown(quickSaveKey))
            {
                TrySave("QuickSave");
            }

            if (GameInput.GetKeyDown(deleteSaveKey))
            {
                GameSaveService.Delete();
                _lastStatus = LocalizationService.Get(LocalizationTableType.Common, "progress_deleted");
            }

            if (GamePause.IsPaused)
            {
                return;
            }

            if (autoSaveInterval > 0f && Time.unscaledTime >= _nextAutoSave)
            {
                _nextAutoSave = Time.unscaledTime + autoSaveInterval;
                TrySave("Auto");
            }
        }

        void OnApplicationQuit()
        {
            if (saveOnQuit)
            {
                TrySave("Quit");
            }
        }

        void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && saveOnPause)
            {
                TrySave("Pause");
            }
        }

        public bool TrySave(string reason = null)
        {
            var party = PartyController.Instance ?? GameServices.Party;
            if (party == null || party.Active == null)
            {
                return false;
            }

            var data = party.CaptureSaveData();
            if (data == null)
            {
                return false;
            }

            bool ok = GameSaveService.Save(data);
            if (ok)
            {
                _lastStatus = string.IsNullOrEmpty(reason)
                    ? LocalizationService.Format(LocalizationTableType.Common, "progress_saved_at", data.Position)
                    : $"{reason} → {data.sceneName}";
            }

            return ok;
        }

        void OnGUI()
        {
            if (!drawHud)
            {
                return;
            }

            GUI.Label(
                new Rect(12, 60, 1000, 22),
                LocalizationService.Format(
                    LocalizationTableType.Common,
                    "progress_hud",
                    _lastStatus,
                    GameSaveService.SavePath));
        }
    }
}
