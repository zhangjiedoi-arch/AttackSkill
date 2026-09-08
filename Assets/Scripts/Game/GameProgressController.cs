using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using AttackSkill.Audio;
using AttackSkill.Character;
using AttackSkill.Core;
using AttackSkill.Enemy;
using AttackSkill.Localization;
using AttackSkill.Rouge;
using AttackSkill.UI;

namespace AttackSkill.Game
{
    /// <summary>
    /// GameScene 循环导演：读档、唯一开局、阶段切换、HUD / 倒计时 / 三选一时机。
    /// F5 快速存档；退出 / 暂停时自动存。开场 Title 仍归 OpenSceneFlow。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameProgressController : MonoBehaviour
    {
        public const string GameSceneName = "GameScene";
        const string RougePlaneName = "RouGeLikePlane";

        public static GameProgressController Instance { get; private set; }

        public RunPhase Phase { get; private set; } = RunPhase.Booting;
        public RougeRun Run => _run;

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
        GameBootIntent _bootIntent;
        GameSaveData _bootSave;
        RougeRun _run;

        void Awake()
        {
            if (!SceneSingleton.ShouldKeep(this, Instance))
            {
                return;
            }

            Instance = this;
            SceneSingleton.ApplyDontDestroyOnLoad(this, dontDestroyOnLoad);
            _run = new RougeRun();
            RougeRun.Bind(_run);
            Phase = RunPhase.Booting;
            SceneBgmPlayer.EnsureExists();
            AttackSkill.UI.World.WorldUiService.EnsureExists();
            PartyRougeProgress.SkillSelectRequested -= OnSkillSelectRequested;
            PartyRougeProgress.SkillSelectRequested += OnSkillSelectRequested;
            PrepareRestoreFromDisk();
        }

        void PrepareRestoreFromDisk()
        {
            _bootSave = null;
            _bootIntent = GameBoot.ConsumeIntent();
            bool shouldLoadSave = _bootIntent == GameBootIntent.Continue ||
                                  (_bootIntent == GameBootIntent.Unspecified && loadSaveOnStart);

            if (_bootIntent == GameBootIntent.NewGame)
            {
                BattleSkillWheelState.ResetToDefault();
                PartyRougeProgress.ResetRun();
                _lastStatus = LocalizationService.Get(LocalizationTableType.Common, "progress_new_game");
                Debug.Log($"[GameProgress] NewGame（不读档）path={GameSaveService.SavePath}");
                return;
            }

            if (shouldLoadSave && GameSaveService.TryLoad(out GameSaveData data))
            {
                _bootSave = data;
                _lastStatus = LocalizationService.Format(LocalizationTableType.Common, "progress_load_save", data.sceneName);
                Debug.Log(
                    $"[GameProgress] 读档 intent={_bootIntent} → {data.sceneName} slot={data.activeIndex} rougeLv={data.rougeRun?.level ?? 1} pos={data.Position} path={GameSaveService.SavePath}");
                return;
            }

            if (_bootIntent == GameBootIntent.Continue)
            {
                Debug.LogWarning($"[GameProgress] Continue 但磁盘档无效：{GameSaveService.SavePath}");
            }
            else
            {
                Debug.Log($"[GameProgress] 无存档可读 intent={_bootIntent} exists={GameSaveService.Exists()} path={GameSaveService.SavePath}");
            }
        }

        void OnDestroy()
        {
            PartyRougeProgress.SkillSelectRequested -= OnSkillSelectRequested;
            if (Instance == this)
            {
                if (BootFinished)
                {
                    TrySave("Destroy");
                }

                RougeRun.Unbind(_run);
                Instance = null;
            }
        }

        IEnumerator Start()
        {
            BootFinished = false;
            IsLoadingScene = false;

            if (_bootSave != null &&
                !string.IsNullOrEmpty(_bootSave.sceneName) &&
                _bootSave.sceneName != SceneManager.GetActiveScene().name)
            {
                IsLoadingScene = true;
                _lastStatus = LocalizationService.Format(LocalizationTableType.Common, "progress_load_scene", _bootSave.sceneName);
                var op = SceneManager.LoadSceneAsync(_bootSave.sceneName, LoadSceneMode.Single);
                if (op == null)
                {
                    Debug.LogError($"[GameProgress] 无法加载场景（检查 Build Settings）：{_bootSave.sceneName}");
                    _bootSave = null;
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
            else if (_bootSave == null &&
                     _bootIntent != GameBootIntent.NewGame &&
                     !string.IsNullOrEmpty(defaultSceneName) &&
                     SceneManager.GetActiveScene().name != defaultSceneName)
            {
                IsLoadingScene = true;
                yield return SceneManager.LoadSceneAsync(defaultSceneName, LoadSceneMode.Single);
                IsLoadingScene = false;
            }

            EnsureRougeFlow();

            var party = PartyController.Instance ?? GameServices.Party;
            for (int i = 0; party == null && i < 8; i++)
            {
                yield return null;
                party = PartyController.Instance ?? GameServices.Party;
            }

            if (party != null)
            {
                party.BeginPlay(_bootSave);
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

            BattleSkillWheelState.EnsureIconResolved();
            UIManager.Instance?.OpenBattleHud();
            TryOpenRougeUiAfterHud();
            SceneBgmPlayer.EnsurePlayingForActiveScene();

            BootFinished = true;
            ResolvePhaseAfterBoot();
            if (autoSaveInterval > 0f)
            {
                _nextAutoSave = Time.unscaledTime + autoSaveInterval;
            }
        }

        void SetPhase(RunPhase phase)
        {
            Phase = phase;
        }

        void ResolvePhaseAfterBoot()
        {
            var party = PartyController.Instance ?? GameServices.Party;
            if (party != null && party.IsGameOverShown)
            {
                SetPhase(RunPhase.GameOver);
                return;
            }

            var flow = RouGeLikeFlowController.Instance;
            if (flow != null && flow.HasTeleported)
            {
                SetPhase(RunPhase.RougeCombat);
                return;
            }

            SetPhase(RunPhase.BeachExplore);
        }

        /// <summary>暂停「返回海滩」：清肉鸽、回默认出生点、任务回到海滩清波。</summary>
        public void RequestBeach()
        {
            if (Phase == RunPhase.Transition)
            {
                return;
            }

            SetPhase(RunPhase.Transition);
            var party = PartyController.Instance ?? GameServices.Party;
            party?.ResetToBeachRun();
            SetPhase(RunPhase.BeachExplore);
        }

        /// <summary>海滩 intro 清场后进入肉鸽平面。</summary>
        public void RequestEnterRougeFromIntro()
        {
            if (Phase == RunPhase.Transition ||
                Phase == RunPhase.GameOver ||
                Phase == RunPhase.RougeCombat)
            {
                return;
            }

            var flow = RouGeLikeFlowController.Instance;
            if (flow == null || flow.HasTeleported)
            {
                return;
            }

            SetPhase(RunPhase.Transition);
            bool entered = flow.EnterFromIntro();
            if (entered && flow.HasTeleported)
            {
                SetPhase(RunPhase.RougeCombat);
                return;
            }

            Debug.LogWarning("[GameProgress] intro 进入肉鸽失败，保持海滩阶段。");
            SetPhase(RunPhase.BeachExplore);
        }

        /// <summary>全灭 / 救援结算后重开肉鸽。</summary>
        public void RequestRestartRouge()
        {
            if (Phase == RunPhase.Transition)
            {
                return;
            }

            SetPhase(RunPhase.Transition);
            var party = PartyController.Instance ?? GameServices.Party;
            party?.RestartRougeRun();
            SetPhase(RunPhase.RougeCombat);
        }

        /// <summary>全灭或倒计时归零结算。</summary>
        public void RequestGameOver(bool rescue = false)
        {
            var party = PartyController.Instance ?? GameServices.Party;
            if (rescue)
            {
                party?.ShowRescueGameOver();
            }
            else
            {
                party?.ShowGameOver();
            }

            NotifyGameOver();
        }

        public void NotifyGameOver()
        {
            SetPhase(RunPhase.GameOver);
        }

        static void EnsureRougeFlow()
        {
            if (RouGeLikeFlowController.Instance != null)
            {
                return;
            }

            if (SceneManager.GetActiveScene().name != GameSceneName)
            {
                return;
            }

            var plane = GameObject.Find(RougePlaneName);
            if (plane == null)
            {
                Debug.LogError(
                    $"[GameProgress] 找不到 \"{RougePlaneName}\"，肉鸽流程无法挂载。");
                return;
            }

            plane.AddComponent<RouGeLikeFlowController>();
        }

        void TryOpenRougeUiAfterHud()
        {
            var flow = RouGeLikeFlowController.Instance;
            bool inRouge = flow != null && flow.HasTeleported;
            if (inRouge)
            {
                float remaining = _bootSave != null && _bootSave.rougeRun != null
                    ? _bootSave.rougeRun.battleTimeRemaining
                    : UIBattleTimePanel.CaptureRemainingSeconds();
                if (remaining >= 0f || UIBattleTimePanel.HasActiveOrPendingTimer)
                {
                    UIBattleTimePanel.TryOpenPendingAfterBoot();
                }
            }

            PartyRougeProgress.TryOpenSkillSelectIfPending();
        }

        void OnSkillSelectRequested()
        {
            var ui = UIManager.Instance;
            if (ui == null)
            {
                Debug.LogWarning("[GameProgress] 三选一：UIManager 未就绪，待 HUD 后再补开。");
                return;
            }

            PartyRougeProgress.NotifySkillSelectOpened();
            var opened = ui.Open(UIId.SkillSelect, new SkillSelectArgs
            {
                options = RougeSkillRoller.RollThree(PartyRougeProgress.Level)
            });
            if (opened == null)
            {
                PartyRougeProgress.NotifySkillSelectOpenFailed();
            }
        }

        void Update()
        {
            if (!BootFinished || IsLoadingScene)
            {
                return;
            }

            if (GameInput.GetKeyDown(quickSaveKey))
            {
                if (TrySave("QuickSave"))
                {
                    var party = PartyController.Instance ?? GameServices.Party;
                    Vector3 pos = party != null && party.Active != null
                        ? party.Active.transform.position
                        : Vector3.zero;
                    UIManager.Instance?.ShowTip(
                        LocalizationService.Format(LocalizationTableType.Common, "progress_saved_at", pos),
                        1.5f);
                }
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
                Debug.LogWarning($"[GameSave] 保存失败({reason})：小队尚未就绪");
                return false;
            }

            var data = party.CaptureSaveData();
            if (data == null)
            {
                Debug.LogWarning($"[GameSave] 保存失败({reason})：无法抓取进度");
                return false;
            }

            bool ok = GameSaveService.Save(data);
            if (ok)
            {
                _lastStatus = string.IsNullOrEmpty(reason)
                    ? LocalizationService.Format(LocalizationTableType.Common, "progress_saved_at", data.Position)
                    : $"{reason} → {data.sceneName}";
            }
            else
            {
                Debug.LogWarning($"[GameSave] 保存失败({reason})：写入磁盘失败");
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
                    $"{Phase} | {_lastStatus}",
                    GameSaveService.SavePath));
        }
    }
}
