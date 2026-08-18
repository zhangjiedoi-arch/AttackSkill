using System.Collections;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using AttackSkill.Core;
using AttackSkill.Game;
using AttackSkill.Localization;

namespace AttackSkill.UI
{
    public enum OpenSceneGender
    {
        Female = 0,
        Male = 1
    }

    /// <summary>
    /// 开场：Scene1 → 2 → 3 + Timeline_Open → 4。
    /// 点击连接后按性别播 Select Timeline，再进 GameScene。
    /// </summary>
    public class OpenSceneFlowController : MonoBehaviour
    {
        [Header("Flow")]
        [SerializeField] bool playOnStart = true;
        [SerializeField] float scene1Seconds = 1.2f;
        [SerializeField] float scene2Seconds = 1.2f;
        [Tooltip("找不到 Timeline_Open 时的 Scene3 保底时长")]
        [SerializeField] float scene3FallbackSeconds = 2.0f;
        [SerializeField] KeyCode skipStepKey = KeyCode.Space;

        [Header("Timelines（可空，按名称自动查找）")]
        [SerializeField] PlayableDirector openTimeline;
        [SerializeField] PlayableDirector selectFemaleTimeline;
        [SerializeField] PlayableDirector selectMaleTimeline;
        [SerializeField] string openTimelineName = "Timeline_Open";
        [SerializeField] string selectFemaleTimelineName = "Timeline_Select_Female";
        [SerializeField] string selectMaleTimelineName = "Timeline_Select_Male";

        [Header("Gender")]
        [SerializeField] OpenSceneGender selectedGender = OpenSceneGender.Female;

        [Header("Enter Game")]
        [SerializeField] string gameSceneName = "GameScene";
        [SerializeField] bool closeUiOnEnter = true;
        [SerializeField] bool closeUiBeforeSelectTimeline = true;

        Coroutine _flow;
        Coroutine _linkFlow;
        bool _skipRequested;
        bool _loginGateDone;
        bool _genderGateDone;

        public OpenSceneGender SelectedGender => selectedGender;

        public void SetSelectedGender(OpenSceneGender gender)
        {
            selectedGender = gender;
        }

        public void NotifyLoginConfirmed()
        {
            _loginGateDone = true;
        }

        public void NotifyGenderConfirmed(OpenSceneGender gender)
        {
            selectedGender = gender;
            _genderGateDone = true;
        }

        void Awake()
        {
            ResolveTimelineRefs();
            LocalAccountStore.MigrateLegacySecrets();
            // 开场场景始终允许改性别（进 GameScene 后会再次 Lock）
            LocalAccountStore.UnlockGender();
            GameServices.Register(this);
        }

        void OnDestroy()
        {
            GameServices.Unregister(this);
        }

        void Start()
        {
            if (playOnStart)
            {
                BeginFlow();
            }
        }

        void Update()
        {
            if (GameInput.GetKeyDown(skipStepKey))
            {
                _skipRequested = true;
            }
        }

        public void BeginFlow()
        {
            if (_flow != null)
            {
                StopCoroutine(_flow);
            }

            _flow = StartCoroutine(FlowRoutine());
        }

        /// <summary>标题页「连接」：播选角 Timeline 后进游戏场景。</summary>
        public void OnLinkClicked()
        {
            if (_linkFlow != null)
            {
                return;
            }

            _linkFlow = StartCoroutine(LinkRoutine());
        }

        IEnumerator FlowRoutine()
        {
            var ui = UIManager.Instance;
            if (ui == null)
            {
                Debug.LogError("[OpenSceneFlow] 找不到 UIManager。");
                yield break;
            }

            ResolveTimelineRefs();

            ui.OpenPanel(UIId.OpenScene1);
            yield return WaitOrSkip(scene1Seconds);

            ui.OpenPanel(UIId.OpenScene2);
            yield return WaitOrSkip(scene2Seconds);

            var panel3 = ui.OpenPanel(UIId.OpenScene3) as UIOpenScene3Panel;
            panel3?.SetProgress(0f);

            if (openTimeline != null)
            {
                DeactivateDirector(selectFemaleTimeline);
                DeactivateDirector(selectMaleTimeline);
                yield return PlayDirectorAndWait(openTimeline, p => panel3?.SetProgress(p));
            }
            else
            {
                Debug.LogWarning($"[OpenSceneFlow] 未找到 {openTimelineName}，使用保底等待。");
                float t = 0f;
                while (t < scene3FallbackSeconds)
                {
                    if (_skipRequested)
                    {
                        _skipRequested = false;
                        break;
                    }

                    t += Time.unscaledDeltaTime;
                    panel3?.SetProgress(t / scene3FallbackSeconds);
                    yield return null;
                }
            }

            panel3?.SetProgress(1f);

            // 登录 / 选性别门闩：有本地存档则跳过
            yield return LoginAndGenderGate(ui);

            // 同步性别到选角 Timeline
            if (LocalAccountStore.HasGender)
            {
                selectedGender = LocalAccountStore.Gender;
            }

            ui.OpenPanel(UIId.OpenScene4);
            _flow = null;
        }

        IEnumerator LoginAndGenderGate(UIManager ui)
        {
            if (!LocalAccountStore.HasAccount)
            {
                _loginGateDone = false;
                var login = ui.OpenDialog(UIId.LogIn, new UILogInDialogArgs
                {
                    onConfirmed = () => _loginGateDone = true
                });
                if (login == null)
                {
                    Debug.LogError("[OpenSceneFlow] 无法打开登录界面，已跳过。");
                    _loginGateDone = true;
                }
                else
                {
                    float wait = 0f;
                    while (!_loginGateDone)
                    {
                        wait += Time.unscaledDeltaTime;
                        // 防止永久卡住：可 Space 跳过（调试）
                        if (_skipRequested)
                        {
                            _skipRequested = false;
                            Debug.LogWarning("[OpenSceneFlow] 跳过登录门闩");
                            break;
                        }

                        yield return null;
                    }
                }
            }

            if (!LocalAccountStore.HasGender)
            {
                _genderGateDone = false;
                var genderUi = ui.OpenDialog(UIId.ChooseGender, new UIChooseGenderDialogArgs
                {
                    onConfirmed = gender =>
                    {
                        selectedGender = gender;
                        _genderGateDone = true;
                    }
                });
                if (genderUi == null)
                {
                    Debug.LogError("[OpenSceneFlow] 无法打开性别界面，默认女。");
                    LocalAccountStore.SaveGender(OpenSceneGender.Female);
                    selectedGender = OpenSceneGender.Female;
                    _genderGateDone = true;
                }
                else
                {
                    while (!_genderGateDone)
                    {
                        if (_skipRequested)
                        {
                            _skipRequested = false;
                            LocalAccountStore.SaveGender(OpenSceneGender.Female);
                            selectedGender = OpenSceneGender.Female;
                            _genderGateDone = true;
                            break;
                        }

                        yield return null;
                    }
                }
            }
            else
            {
                selectedGender = LocalAccountStore.Gender;
            }
        }

        IEnumerator LinkRoutine()
        {
            ResolveTimelineRefs();

            if (closeUiBeforeSelectTimeline)
            {
                UIManager.Instance?.CloseAll();
            }

            PlayableDirector select = selectedGender == OpenSceneGender.Male
                ? selectMaleTimeline
                : selectFemaleTimeline;

            string expectName = selectedGender == OpenSceneGender.Male
                ? selectMaleTimelineName
                : selectFemaleTimelineName;

            DeactivateDirector(openTimeline);
            if (selectedGender == OpenSceneGender.Male)
            {
                DeactivateDirector(selectFemaleTimeline);
            }
            else
            {
                DeactivateDirector(selectMaleTimeline);
            }

            if (select != null)
            {
                yield return PlayDirectorAndWait(select);
            }
            else
            {
                Debug.LogWarning($"[OpenSceneFlow] 未找到 {expectName}，直接进入游戏。");
            }

            yield return EnterGameRoutine();
            _linkFlow = null;
        }

        IEnumerator PlayDirectorAndWait(PlayableDirector director, System.Action<float> onProgress = null)
        {
            if (director == null)
            {
                yield break;
            }

            _skipRequested = false;
            director.gameObject.SetActive(true);

            // Hold 模式下播完 state 仍为 Playing，会导致死等；强制 None + 按时间结束
            director.extrapolationMode = DirectorWrapMode.None;
            director.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
            director.Stop();
            director.initialTime = 0;
            director.time = 0;
            director.Evaluate();
            director.Play();

            // 等一帧，让 PlayableGraph 建好后再读 duration
            yield return null;

            double duration = director.duration;
            if ((duration <= 0.0001 || double.IsInfinity(duration) || double.IsNaN(duration))
                && director.playableAsset != null)
            {
                duration = director.playableAsset.duration;
            }

            // Infinite Clip / 异常时长：保底，避免永久卡住
            if (duration <= 0.0001 || double.IsInfinity(duration) || double.IsNaN(duration))
            {
                duration = scene3FallbackSeconds;
                Debug.LogWarning(
                    $"[OpenSceneFlow] {director.gameObject.name} duration 无效，改用保底 {duration:F2}s",
                    director);
            }

            float safety = (float)duration + 2f;
            float waited = 0f;
            while (waited < safety)
            {
                if (_skipRequested)
                {
                    _skipRequested = false;
                    break;
                }

                double t = director.time;
                onProgress?.Invoke(Mathf.Clamp01((float)(t / duration)));

                // 以时间为主；Hold 时 state 不会变 Stopped
                if (t >= duration - 0.01)
                {
                    break;
                }

                // 已被外部 Stop，且时间几乎没走
                if (director.state != PlayState.Playing && t <= 0.0001)
                {
                    break;
                }

                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            onProgress?.Invoke(1f);
            director.time = duration;
            director.Evaluate();
            director.Stop();
        }

        IEnumerator WaitOrSkip(float seconds)
        {
            _skipRequested = false;
            float t = 0f;
            while (t < seconds)
            {
                if (_skipRequested)
                {
                    _skipRequested = false;
                    yield break;
                }

                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        void ResolveTimelineRefs()
        {
            if (openTimeline == null)
            {
                openTimeline = FindDirectorInScene(openTimelineName);
            }

            if (selectFemaleTimeline == null)
            {
                selectFemaleTimeline = FindDirectorInScene(selectFemaleTimelineName);
            }

            if (selectMaleTimeline == null)
            {
                selectMaleTimeline = FindDirectorInScene(selectMaleTimelineName);
            }
        }

        static PlayableDirector FindDirectorInScene(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
            {
                return null;
            }

            var directors = Resources.FindObjectsOfTypeAll<PlayableDirector>();
            for (int i = 0; i < directors.Length; i++)
            {
                var d = directors[i];
                if (d == null || d.gameObject == null)
                {
                    continue;
                }

                if (!d.gameObject.scene.IsValid() || !d.gameObject.scene.isLoaded)
                {
                    continue;
                }

                if (d.gameObject.name == objectName)
                {
                    return d;
                }
            }

            return null;
        }

        static void DeactivateDirector(PlayableDirector director)
        {
            if (director == null)
            {
                return;
            }

            director.Stop();
            if (director.gameObject.activeSelf)
            {
                director.gameObject.SetActive(false);
            }
        }

        /// <summary>进入游戏：关开场 UI，显示切场景进度条并异步加载。</summary>
        public void EnterGame()
        {
            StartCoroutine(EnterGameRoutine());
        }

        IEnumerator EnterGameRoutine()
        {
            var ui = UIManager.Instance;
            if (closeUiOnEnter)
            {
                ui?.CloseAll();
            }

            if (string.IsNullOrEmpty(gameSceneName))
            {
                Debug.Log("[OpenSceneFlow] EnterGame：已关闭 UI（未配置 gameSceneName）。");
                yield break;
            }

            Debug.Log($"[OpenSceneFlow] EnterGame → 加载 {gameSceneName}");

            // 开场连接 = 新开局：不加载进度档（磁盘档保留，可供以后「继续」）
            GameBoot.SetIntent(GameBootIntent.NewGame);
            GameSaveService.ClearPendingRestore();

            UIChangeScenePanel loading = null;
            if (ui != null)
            {
                loading = ui.OpenPanel(UIId.ChangeScene, LocalizationService.Get(LocalizationTableType.Story, "change_scene_story_1")) as UIChangeScenePanel;
                loading?.SetProgress(0f);
            }

            AsyncOperation op = SceneManager.LoadSceneAsync(gameSceneName);
            if (op == null)
            {
                Debug.LogError($"[OpenSceneFlow] LoadSceneAsync 失败：{gameSceneName}");
                yield break;
            }

            op.allowSceneActivation = false;
            while (op.progress < 0.9f)
            {
                float p = Mathf.Clamp01(op.progress / 0.9f);
                loading?.SetProgress(p);
                yield return null;
            }

            loading?.SetProgress(1f);
            yield return null;
            op.allowSceneActivation = true;

            while (!op.isDone)
            {
                yield return null;
            }

            ui?.Close(UIId.ChangeScene);
            ui?.OpenBattlePartyHud();
            // 性别锁定改由 Party 开局成功后统一执行

            // 卸载开场 Flow，避免 DDOL UIRoot 上残留逻辑
            if (UIBootstrap.Instance != null)
            {
                UIBootstrap.Instance.NotifyEnteredGame();
            }
            else
            {
                enabled = false;
                GameServices.Unregister(this);
                Destroy(this);
            }

            _flow = null;
            _linkFlow = null;
        }
    }
}
