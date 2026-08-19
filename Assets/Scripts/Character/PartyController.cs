using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using AttackSkill.CameraSystem;
using AttackSkill.Character.Exploration;
using AttackSkill.Character.HSM;
using AttackSkill.Combat;
using AttackSkill.Core;
using AttackSkill.Enemy;
using AttackSkill.Game;
using AttackSkill.Localization;
using AttackSkill.Rouge;
using AttackSkill.UI;

namespace AttackSkill.Character
{
    /// <summary>
    /// 鸣潮式切人：新角色立刻接管；旧角色若正在放大招则残留播完再消失。
    /// </summary>
    public class PartyController : MonoBehaviour, IPlayerTargetProvider
    {
        public static PartyController Instance { get; private set; }

        [Header("Roster")]
        [Tooltip("运行时队伍 Prefab；开启 Gender Roster 后会按性别重写为 [漂泊者, 千咲, 柯莱塔]")]
        [SerializeField] GameObject[] characterPrefabs;
        [Tooltip("开局生成并操控的下标")]
        [SerializeField] int startIndex;

        [Header("Gender Roster")]
        [Tooltip("按 LocalAccountStore 已选性别组装：0=男女漂泊者，1=千咲，2=柯莱塔")]
        [SerializeField] bool applyGenderRoster = true;
        [SerializeField] GameObject maleWandererPrefab;
        [SerializeField] GameObject femaleWandererPrefab;
        [SerializeField] GameObject qianxiaoPrefab;
        [SerializeField] GameObject colettaPrefab;

        [Header("Spawn")]
        [Tooltip("开局 / 单人死亡重生坐标")]
        [SerializeField] Vector3 spawnPosition = new Vector3(35f, 0f, 15f);
        [Tooltip("开局时把 PartyController 物体也移到 spawnPosition")]
        [SerializeField] bool syncTransformToSpawn = true;
        [Tooltip("有 GameProgress 时由进度系统驱动开局生成，避免与读档抢跑")]
        [SerializeField] bool deferBootToGameProgress = true;

        [Header("Camera")]
        [SerializeField] ThirdPersonCamera thirdPersonCamera;
        [SerializeField] bool autoFindCamera = true;

        [Header("Switch")]
        [SerializeField] KeyCode nextMemberKey = KeyCode.Tab;
        [SerializeField] KeyCode[] slotKeys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3 };
        [SerializeField] float switchCooldown = 0.35f;
        [Tooltip("新角色相对旧角色的出生偏移，减轻重叠（开局首次生成不使用）")]
        [SerializeField] Vector3 spawnOffset = new Vector3(0.35f, 0f, 0f);
        [Tooltip("残留体最长存活（秒），防止技能异常卡死")]
        [SerializeField] float residualTimeout = 8f;

        [Header("Death")]
        [Tooltip("单人队死亡后，在上次位置重生的延迟（秒）")]
        [SerializeField] float soloRespawnDelay = 2.5f;

        [Header("Debug")]
        [SerializeField] bool drawHud = false;

        GenshinLikeCharacter _active;
        GenshinLikeCharacter _residual;
        float _lastSwitchTime = -999f;
        float _residualSpawnTime;
        bool _soloRespawning;
        bool _playStarted;
        bool _genderRosterApplied;
        bool _gameOverShown;
        bool _restarting;
        Coroutine _soloRespawnCoroutine;
        readonly List<GenshinLikeCharacter> _spawned = new List<GenshinLikeCharacter>();
        bool[] _fallen;

        /// <summary>最近一次有效玩法位姿（切人/重生时沿用，避免回到默认出生点）。</summary>
        Vector3 _lastGameplayPos;
        Quaternion _lastGameplayRot = Quaternion.identity;
        bool _hasLastGameplayPose;

        public GenshinLikeCharacter Active => _active;
        public int ActiveIndex { get; private set; } = -1;
        public int MemberCount => characterPrefabs != null ? characterPrefabs.Length : 0;
        public bool PlayStarted => _playStarted;
        public bool IsGameOverShown => _gameOverShown;

        public bool IsSlotFallen(int index)
        {
            EnsureFallenArray();
            return _fallen != null && index >= 0 && index < _fallen.Length && _fallen[index];
        }

        /// <summary>当前队伍槽位对应的 Prefab（未组队时可能为空）。</summary>
        public GameObject GetMemberPrefab(int index)
        {
            if (characterPrefabs == null || index < 0 || index >= characterPrefabs.Length)
            {
                return null;
            }

            return characterPrefabs[index];
        }

        /// <summary>
        /// 槽位头像 ID：优先按 Prefab 引用匹配，标准性别阵容为
        /// 0=漂泊者(男/女)、1=千咲、2=柯莱塔。
        /// </summary>
        public PartyPortraitId GetPortraitId(int memberIndex)
        {
            EnsureGenderRoster();
            GameObject prefab = GetMemberPrefab(memberIndex);
            if (prefab != null)
            {
                if (prefab == femaleWandererPrefab)
                {
                    return PartyPortraitId.WandererFemale;
                }

                if (prefab == maleWandererPrefab)
                {
                    return PartyPortraitId.WandererMale;
                }

                if (prefab == qianxiaoPrefab)
                {
                    return PartyPortraitId.Qianxiao;
                }

                if (prefab == colettaPrefab)
                {
                    return PartyPortraitId.Coletta;
                }

                PartyPortraitId byName = ResolvePortraitIdByName(prefab.name);
                if (byName != PartyPortraitId.Unknown)
                {
                    return byName;
                }
            }

            // 标准三人阵容回退
            if (memberIndex == 0)
            {
                return LocalAccountStore.HasGender && LocalAccountStore.Gender == OpenSceneGender.Male
                    ? PartyPortraitId.WandererMale
                    : PartyPortraitId.WandererFemale;
            }

            if (memberIndex == 1)
            {
                return PartyPortraitId.Qianxiao;
            }

            if (memberIndex == 2)
            {
                return PartyPortraitId.Coletta;
            }

            return PartyPortraitId.Unknown;
        }

        static PartyPortraitId ResolvePortraitIdByName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return PartyPortraitId.Unknown;
            }

            string n = name;
            if (ContainsIgnoreCase(n, "Qianxiao") || n.Contains("千咲"))
            {
                return PartyPortraitId.Qianxiao;
            }

            if (ContainsIgnoreCase(n, "Coletta") || ContainsIgnoreCase(n, "Kelaita") || n.Contains("柯莱塔"))
            {
                return PartyPortraitId.Coletta;
            }

            if (ContainsIgnoreCase(n, "Female") || n.Contains("女"))
            {
                return PartyPortraitId.WandererFemale;
            }

            if (ContainsIgnoreCase(n, "Male") || n.Contains("男"))
            {
                return PartyPortraitId.WandererMale;
            }

            return PartyPortraitId.Unknown;
        }

        static bool ContainsIgnoreCase(string haystack, string needle)
        {
            return haystack.IndexOf(needle, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>切人成功（含开局首次生成）后触发，参数为新 Active 槽位下标。</summary>
        public event Action<int> ActiveChanged;

        /// <summary>本局阵亡槽位变化（头像置灰 / 禁止切人）。</summary>
        public event Action FallenChanged;

        void Awake()
        {
            if (!SceneSingleton.ShouldKeep(this, Instance))
            {
                return;
            }

            Instance = this;
            PlayerTargetLocator.Register(this);
            AttackSkill.UI.World.WorldUiService.EnsureExists();

            if (autoFindCamera && thirdPersonCamera == null)
            {
                thirdPersonCamera = GameServices.ResolveCamera();
            }

            // 阵容在 BeginPlay 里按（可能已从存档同步的）性别组装，Awake 不提前 Lock
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            PlayerTargetLocator.Unregister(this);

            if (_soloRespawnCoroutine != null)
            {
                StopCoroutine(_soloRespawnCoroutine);
                _soloRespawnCoroutine = null;
            }

            if (_residual != null)
            {
                _residual.ResidualFinished -= OnResidualFinished;
            }
        }

        public Transform GetActivePlayerTransform()
        {
            // 死亡等待重生期间不算有效目标，避免敌人继续追尸体
            if (_active == null || _active.IsDead)
            {
                return null;
            }

            return _active.transform;
        }

        public bool IsActivePlayer(Component component)
        {
            if (component == null || _active == null || _active.IsDead)
            {
                return false;
            }

            return component.transform == _active.transform ||
                   component.transform.IsChildOf(_active.transform) ||
                   component.GetComponentInParent<GenshinLikeCharacter>() == _active;
        }

        void Start()
        {
            if (ShouldDeferBoot())
            {
                return;
            }

            BeginPlayFromSaveOrDefault();
        }

        bool ShouldDeferBoot()
        {
            if (GameSaveService.HasPendingRestore)
            {
                return true;
            }

            if (!deferBootToGameProgress)
            {
                return false;
            }

            var progress = GameProgressController.Instance;
            return progress != null && !progress.BootFinished;
        }

        /// <summary>
        /// 按已选性别组装队伍：漂泊者(男/女) + 千咲 + 柯莱塔。
        /// </summary>
        public void EnsureGenderRoster()
        {
            if (!applyGenderRoster || _genderRosterApplied)
            {
                return;
            }

            _genderRosterApplied = true;
            ResolveGenderRosterPrefabs();

            OpenSceneGender gender = LocalAccountStore.HasGender
                ? LocalAccountStore.Gender
                : OpenSceneGender.Female;

            if (!TryBuildRoster(gender, out GameObject lead))
            {
                return;
            }

            characterPrefabs = new[] { lead, qianxiaoPrefab, colettaPrefab };
            Debug.Log(
                $"[PartyController] 性别阵容 → {gender}: {lead.name}, {qianxiaoPrefab.name}, {colettaPrefab.name}",
                this);
        }

        bool TryBuildRoster(OpenSceneGender gender, out GameObject lead)
        {
            lead = gender == OpenSceneGender.Male
                ? maleWandererPrefab
                : femaleWandererPrefab;

            if (lead == null)
            {
                lead = maleWandererPrefab != null ? maleWandererPrefab : femaleWandererPrefab;
                Debug.LogWarning(
                    $"[PartyController] 缺少{(gender == OpenSceneGender.Male ? "男" : "女")}漂泊者 Prefab，已回退。",
                    this);
            }

            if (lead == null || qianxiaoPrefab == null || colettaPrefab == null)
            {
                Debug.LogError(
                    "[PartyController] 性别阵容未配齐（需男/女漂泊者、千咲、柯莱塔）。",
                    this);
                return false;
            }

            return true;
        }

        void ResolveGenderRosterPrefabs()
        {
            // 场景序列化引用优先
            if (characterPrefabs != null)
            {
                if (maleWandererPrefab == null && characterPrefabs.Length > 0)
                {
                    maleWandererPrefab = characterPrefabs[0];
                }

                if (qianxiaoPrefab == null && characterPrefabs.Length > 1)
                {
                    qianxiaoPrefab = characterPrefabs[1];
                }

                if (colettaPrefab == null && characterPrefabs.Length > 2)
                {
                    colettaPrefab = characterPrefabs[2];
                }
            }

            // 包体回退：Resources/CharacterRuntimeSettings（禁止 AssetDatabase）
            var settings = CharacterRuntimeSettings.Get();
            if (settings == null)
            {
                if (femaleWandererPrefab == null || maleWandererPrefab == null ||
                    qianxiaoPrefab == null || colettaPrefab == null)
                {
                    Debug.LogError(
                        "[PartyController] 阵容 Prefab 未配齐，且缺少 Resources/CharacterRuntimeSettings。",
                        this);
                }

                return;
            }

            if (maleWandererPrefab == null)
            {
                maleWandererPrefab = settings.maleWandererPrefab;
            }

            if (femaleWandererPrefab == null)
            {
                femaleWandererPrefab = settings.femaleWandererPrefab;
            }

            if (qianxiaoPrefab == null)
            {
                qianxiaoPrefab = settings.qianxiaoPrefab;
            }

            if (colettaPrefab == null)
            {
                colettaPrefab = settings.colettaPrefab;
            }
        }

        /// <summary>读档复位或默认出生点开局。由 GameProgress 在场景就绪后调用。</summary>
        public void BeginPlayFromSaveOrDefault()
        {
            SyncGenderFromPendingIfNeeded();
            EnsureGenderRoster();

            if (characterPrefabs == null || characterPrefabs.Length == 0)
            {
                Debug.LogError("[PartyController] 未配置 characterPrefabs。", this);
                return;
            }

            if (GameSaveService.TryPeekPendingRestore(out GameSaveData save))
            {
                ClearFallen(notify: false);
                if (_residual != null)
                {
                    DespawnResidual(_residual);
                }

                if (_active != null)
                {
                    DespawnCharacter(_active);
                    ActiveIndex = -1;
                }

                _playStarted = false;
                if (TryApplyRestore(save))
                {
                    GameSaveService.TryConsumePendingRestore(out _);
                    _playStarted = true;
                    LocalAccountStore.LockGender();
                    RouGeLikeFlowController.Instance?.NotifyPlayerReady();
                    return;
                }

                Debug.LogError(
                    "[PartyController] 读档生成失败，保留 Pending；回退默认出生点。",
                    this);
            }

            if (_playStarted || _active != null)
            {
                return;
            }

            ClearFallen(notify: false);
            if (!TrySpawnAtDefault())
            {
                Debug.LogError("[PartyController] 开局生成失败。", this);
                return;
            }

            _playStarted = true;
            LocalAccountStore.LockGender();
            RouGeLikeFlowController.Instance?.NotifyPlayerReady();
        }

        /// <summary>
        /// 组队前：未锁定则用存档性别补齐；已锁定且与存档不一致只打日志。
        /// </summary>
        void SyncGenderFromPendingIfNeeded()
        {
            if (!GameSaveService.TryPeekPendingRestore(out GameSaveData save))
            {
                return;
            }

            if (!LocalAccountStore.IsGenderLocked)
            {
                if (LocalAccountStore.SaveGender(save.Gender))
                {
                    _genderRosterApplied = false;
                    Debug.Log($"[PartyController] 读档前同步性别 → {save.Gender}");
                }

                return;
            }

            if (LocalAccountStore.HasGender && LocalAccountStore.Gender != save.Gender)
            {
                Debug.LogWarning(
                    $"[PartyController] 账号性别已锁定为 {LocalAccountStore.Gender}，存档快照为 {save.Gender}，以账号为准组队。");
            }
        }

        bool TryApplyRestore(GameSaveData save)
        {
            if (save == null)
            {
                return false;
            }

            PartyRougeProgress.Restore(save.rougeRun);
            RestoreFallenSlots(save.rougeRun != null ? save.rougeRun.fallenSlots : null);

            Vector3 pos = save.Position;
            Quaternion rot = save.Rotation;
            int preferred = Mathf.Clamp(save.activeIndex, 0, characterPrefabs.Length - 1);
            int idx = FirstLivingIndex(preferred);
            bool wipe = idx < 0;
            if (wipe)
            {
                idx = preferred;
            }

            if (syncTransformToSpawn)
            {
                transform.position = pos;
            }

            var flow = RouGeLikeFlowController.Instance;
            bool hasTeleported = save.rougeRun != null && save.rougeRun.hasTeleported;
            float battleTime = save.rougeRun != null ? save.rougeRun.battleTimeRemaining : -1f;
            // 先开肉鸽闸并关掉海滩 intro，避免 SwitchTo 后 intro 清场把等级 ResetRun 掉。
            flow?.ApplyRestoredEntry(hasTeleported, pos, battleTime);

            SwitchTo(idx, pos, rot, force: true, applySpawnOffset: false, useRequestedPose: true);

            if (_active == null)
            {
                ClearFallen(notify: false);
                return false;
            }

            RougePassiveEffects.NotifyChanged();
            RougePassiveEffects.ApplyMaxHpMul(_active);

            bool sameLivingSlot = !wipe && idx == preferred;
            if (sameLivingSlot && save.activeHp >= 0f && _active.Health != null)
            {
                _active.Health.SetCurrentHp(save.activeHp);
            }

            BattleSkillWheelState.Restore(save.equippedSkillIndex);

            if (wipe)
            {
                ShowGameOver();
            }

            Debug.Log(
                $"[PartyController] 读档复位：slot={idx} hp={save.activeHp} gender={save.Gender} skillT={save.equippedSkillIndex} rougeLv={PartyRougeProgress.Level} teleported={hasTeleported} @ {pos} ({SceneManager.GetActiveScene().name})");
            UIManager.Instance?.ShowTip(
                LocalizationService.Format(LocalizationTableType.Common, "progress_load_save", save.sceneName),
                2f);
            return true;
        }

        bool TrySpawnAtDefault()
        {
            PartyRougeProgress.ResetRun();
            ClearFallen(notify: false);
            if (syncTransformToSpawn)
            {
                transform.position = spawnPosition;
            }

            int idx = Mathf.Clamp(startIndex, 0, characterPrefabs.Length - 1);
            SwitchTo(idx, spawnPosition, transform.rotation, force: true, applySpawnOffset: false);
            return _active != null;
        }

        void SpawnAtDefault()
        {
            TrySpawnAtDefault();
        }

        /// <summary>抓取进度存档（账号资料不在此写入）。</summary>
        public GameSaveData CaptureSaveData()
        {
            if (_active == null)
            {
                return null;
            }

            int idx = ActiveIndex >= 0 ? ActiveIndex : Mathf.Clamp(startIndex, 0, Mathf.Max(0, MemberCount - 1));
            float hp = _active.Health != null ? _active.Health.CurrentHp : -1f;
            RememberActivePose();
            Vector3 pos = _hasLastGameplayPose ? _lastGameplayPos : _active.transform.position;
            Quaternion rot = _hasLastGameplayPose ? _lastGameplayRot : _active.transform.rotation;
            var data = GameSaveData.Create(
                SceneManager.GetActiveScene().name,
                pos,
                rot,
                idx,
                LocalAccountStore.HasGender ? LocalAccountStore.Gender : OpenSceneGender.Female,
                hp,
                BattleSkillWheelState.SelectedIndex);

            var rouge = PartyRougeProgress.Capture();
            var flow = RouGeLikeFlowController.Instance;
            rouge.hasTeleported = flow != null && (flow.HasTeleported || flow.ContainsPoint(pos));
            rouge.fallenSlots = CaptureFallenSlots();
            if (rouge.hasTeleported)
            {
                float rem = UIBattleTimePanel.CaptureRemainingSeconds();
                rouge.battleTimeRemaining = rem >= 0f ? rem : UIBattleTimePanel.DurationSeconds;
            }
            else
            {
                rouge.battleTimeRemaining = -1f;
            }

            data.rougeRun = rouge;
            return data;
        }

        void Update()
        {
            if (MemberCount <= 0 || GameplayInputGate.IsBlocked)
            {
                return;
            }

            // Tab 留给技能轮盘；战斗 HUD 打开时不占用 nextMemberKey=Tab
            bool tabReservedForSkillWheel = nextMemberKey == KeyCode.Tab &&
                                           UIManager.Instance != null &&
                                           UIManager.Instance.IsOpen(UIId.BattleCombat);
            if (!tabReservedForSkillWheel && GameInput.GetKeyDown(nextMemberKey))
            {
                int next = FindNextLivingIndex(ActiveIndex);
                if (next >= 0)
                {
                    TrySwitchTo(next);
                }
            }

            if (slotKeys != null)
            {
                for (int i = 0; i < slotKeys.Length && i < MemberCount; i++)
                {
                    if (GameInput.GetKeyDown(slotKeys[i]))
                    {
                        TrySwitchTo(i);
                    }
                }
            }

            if (_residual != null && residualTimeout > 0f && Time.time - _residualSpawnTime >= residualTimeout)
            {
                DespawnResidual(_residual);
            }
        }

        public bool TrySwitchTo(int index)
        {
            if (index < 0 || index >= MemberCount)
            {
                return false;
            }

            if (IsSlotFallen(index))
            {
                return false;
            }

            if (ActiveIndex >= 0 && index == ActiveIndex)
            {
                return false;
            }

            if (ActiveIndex >= 0 &&
                Time.time < _lastSwitchTime + Mathf.Max(0.1f, switchCooldown))
            {
                return false;
            }

            RememberActivePose();
            ResolveSpawnPose(out Vector3 pos, out Quaternion rot);
            // 切人继承原点，不再加横向偏移（偏移易被误认为「回到出生点」）
            return SwitchTo(index, pos, rot, force: false, applySpawnOffset: false);
        }

        bool SwitchTo(int index, Vector3 worldPos, Quaternion worldRot, bool force, bool applySpawnOffset = true, bool useRequestedPose = false)
        {
            if (!force && ActiveIndex >= 0 && index == ActiveIndex)
            {
                return false;
            }

            if (!force && IsSlotFallen(index))
            {
                return false;
            }

            if (characterPrefabs[index] == null)
            {
                Debug.LogError($"[PartyController] Prefab[{index}] 为空。", this);
                return false;
            }

            var old = _active;
            bool linger = old != null && !old.IsDead && old.IsLingeringSkill;
            var locomotionSnap = default(ExplorationLocomotionSnapshot);
            if (old != null && !old.IsDead)
            {
                locomotionSnap = old.CaptureExplorationLocomotion();
            }

            // 新残留前清掉上一个残留
            if (_residual != null)
            {
                DespawnResidual(_residual);
            }

            // 切人前先记下旧角色位姿（读档强制用存档坐标，不继承当前出生点）
            if (old != null && !useRequestedPose)
            {
                RememberPose(old.transform.position, old.transform.rotation);
                worldPos = old.transform.position;
                worldRot = old.transform.rotation;
            }
            else
            {
                RememberPose(worldPos, worldRot);
            }

            Vector3 spawnPos = applySpawnOffset ? worldPos + worldRot * spawnOffset : worldPos;
            // 活体切人：原位继承。死亡/开局：贴地。读档保持存档坐标（高空肉鸽平面贴地射线会打偏）。
            bool inheritLivePose = !useRequestedPose && old != null && !old.IsDead;
            if (!inheritLivePose && !useRequestedPose)
            {
                spawnPos = SnapSpawnToGround(spawnPos);
            }

            // 先卸旧角色探索工具挂点，避免与新人双份翅膀/摩托
            if (old != null && locomotionSnap.HasTool)
            {
                old.HandoffClearExplorationTools();
            }

            var next = SpawnMember(index, spawnPos, worldRot);
            if (next == null)
            {
                return false;
            }

            BindCharacter(next);
            // 续态时不要清速度，由 Resume 写入快照动量
            bool preserveMotion = locomotionSnap.HasTool;
            next.BecomeActive(thirdPersonCamera, resetMotion: !preserveMotion);
            next.TeleportTo(spawnPos, worldRot, resetMotion: !preserveMotion);
            if (preserveMotion)
            {
                next.ResumeExplorationLocomotion(locomotionSnap);
            }

            if (thirdPersonCamera != null)
            {
                thirdPersonCamera.FollowTarget = next.transform;
                thirdPersonCamera.SnapToFollowTarget();
                thirdPersonCamera.RestoreDesiredCursorLock();
            }

            _active = next;
            ActiveIndex = index;
            _lastSwitchTime = Time.time;
            RememberPose(spawnPos, worldRot);
            PlayerTargetLocator.InvalidateCache();
            AttackSkill.Rouge.RougePassiveEffects.ApplyAbyssPactToActiveParty();
            AttackSkill.Rouge.RougeOrbitWeaponDriver.BindToActiveImmediate();

            if (old != null)
            {
                if (linger)
                {
                    old.ResidualFinished -= OnResidualFinished;
                    old.ResidualFinished += OnResidualFinished;
                    old.BecomeResidual();
                    _residual = old;
                    _residualSpawnTime = Time.time;
                }
                else
                {
                    DespawnCharacter(old);
                }
            }

            // 物理步进后再钉一次，防止 CC 首帧挤开
            StartCoroutine(ConfirmSpawnPoseNextFrame(next, spawnPos, worldRot, locomotionSnap));

            ActiveChanged?.Invoke(ActiveIndex);
            return true;
        }

        IEnumerator ConfirmSpawnPoseNextFrame(
            GenshinLikeCharacter character,
            Vector3 pos,
            Quaternion rot,
            ExplorationLocomotionSnapshot locomotionSnap)
        {
            yield return null;
            if (character == null || character != _active)
            {
                yield break;
            }

            float drift = Vector3.Distance(character.transform.position, pos);
            if (drift > 0.05f)
            {
                character.TeleportTo(pos, rot, resetMotion: !locomotionSnap.HasTool);
                if (locomotionSnap.HasTool)
                {
                    character.ResumeExplorationLocomotion(locomotionSnap);
                }
            }

            RememberPose(character.transform.position, character.transform.rotation);
            if (thirdPersonCamera != null && thirdPersonCamera.FollowTarget == character.transform)
            {
                thirdPersonCamera.SnapToFollowTarget();
            }
        }

        static Vector3 SnapSpawnToGround(Vector3 pos)
        {
            // 排除 Player，避免打到残留/尸体胶囊把落点抬高
            int mask = ~0;
            int playerLayer = CombatLayers.PlayerLayer;
            if (playerLayer >= 0)
            {
                mask &= ~(1 << playerLayer);
            }

            Vector3 origin = pos + Vector3.up * 2f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 8f, mask, QueryTriggerInteraction.Ignore))
            {
                Vector3 flat = hit.point;
                if (Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(flat.x, flat.z)) < 1.5f)
                {
                    return hit.point;
                }
            }

            return pos;
        }

        void LateUpdate()
        {
            RememberActivePose();
        }

        void RememberActivePose()
        {
            if (_active == null || _active.IsDead)
            {
                return;
            }

            RememberPose(_active.transform.position, _active.transform.rotation);
        }

        public void TeleportActiveTo(Vector3 worldPos, Quaternion worldRot)
        {
            if (_active != null && !_active.IsDead)
            {
                _active.TeleportTo(worldPos, worldRot, resetMotion: true);
            }

            RememberPose(worldPos, worldRot);

            if (thirdPersonCamera != null)
            {
                if (_active != null)
                {
                    thirdPersonCamera.FollowTarget = _active.transform;
                }

                thirdPersonCamera.SnapToFollowTarget();
            }
        }

        void RememberPose(Vector3 pos, Quaternion rot)
        {
            _lastGameplayPos = pos;
            _lastGameplayRot = rot;
            _hasLastGameplayPose = true;
        }

        void ResolveSpawnPose(out Vector3 pos, out Quaternion rot)
        {
            if (_active != null && !_active.IsDead)
            {
                pos = _active.transform.position;
                rot = _active.transform.rotation;
                return;
            }

            if (_hasLastGameplayPose)
            {
                pos = _lastGameplayPos;
                rot = _lastGameplayRot;
                return;
            }

            pos = spawnPosition;
            rot = transform.rotation;
        }

        GenshinLikeCharacter SpawnMember(int index, Vector3 pos, Quaternion rot)
        {
            // Avatar-only 或旧完整 Prefab 均走装配器
            var character = CharacterRuntimeAssembler.Spawn(
                characterPrefabs[index],
                pos,
                rot,
                $"{characterPrefabs[index].name}_P{index}");

            if (character == null)
            {
                Debug.LogError(
                    $"[PartyController] Prefab[{index}] 装配失败（需 CharacterAvatar 或 GenshinLikeCharacter）。",
                    characterPrefabs[index]);
                return null;
            }

            CharacterRuntimeAssembler.ApplyCombatStatsForPortrait(
                character.gameObject,
                GetPortraitId(index));
            AttackSkill.Rouge.RougePassiveEffects.ApplyMaxHpMul(character);

            PlayerHurtbox.Ensure(character.gameObject);

            _spawned.Add(character);
            return character;
        }

        void BindCharacter(GenshinLikeCharacter character)
        {
            if (character == null)
            {
                return;
            }

            character.Died -= OnActiveCharacterDied;
            character.Died += OnActiveCharacterDied;
        }

        void UnbindCharacter(GenshinLikeCharacter character)
        {
            if (character == null)
            {
                return;
            }

            character.Died -= OnActiveCharacterDied;
        }

        void OnActiveCharacterDied(GenshinLikeCharacter character)
        {
            if (_restarting || character == null || character != _active)
            {
                return;
            }

            PlayerTargetLocator.InvalidateCache();

            Vector3 deathPos = character.transform.position;
            Quaternion deathRot = character.transform.rotation;
            RememberPose(deathPos, deathRot);

            int deadIndex = ActiveIndex;
            MarkFallen(deadIndex);

            int next = FindNextLivingIndex(deadIndex);
            if (next >= 0)
            {
                SwitchTo(next, deathPos, deathRot, force: true, applySpawnOffset: false);
                return;
            }

            ShowGameOver();
        }

        /// <summary>暂停「返回海滩」：清肉鸽进度、删档重写、回默认出生点、任务回到海滩清波。</summary>
        public void ResetToBeachRun()
        {
            if (_restarting)
            {
                return;
            }

            _restarting = true;
            try
            {
                _gameOverShown = false;
                _playStarted = true;

                if (_soloRespawnCoroutine != null)
                {
                    StopCoroutine(_soloRespawnCoroutine);
                    _soloRespawnCoroutine = null;
                    _soloRespawning = false;
                }

                ClearFallen(notify: false);
                PartyRougeProgress.ResetRun();
                BattleSkillWheelState.ResetToDefault();
                GameSaveService.ClearPendingRestore();
                GameSaveService.Delete();

                if (_residual != null)
                {
                    DespawnResidual(_residual);
                }

                Vector3 pos = spawnPosition;
                Quaternion rot = transform.rotation;
                if (syncTransformToSpawn)
                {
                    transform.position = pos;
                }

                if (_active != null)
                {
                    DespawnCharacter(_active);
                    ActiveIndex = -1;
                }

                int idx = Mathf.Clamp(startIndex, 0, Mathf.Max(0, MemberCount - 1));
                SwitchTo(idx, pos, rot, force: true, applySpawnOffset: false, useRequestedPose: true);

                var rouge = RouGeLikeFlowController.Instance;
                rouge?.ResetToCamp();

                var ui = UIManager.Instance;
                if (ui != null)
                {
                    if (ui.IsOpen(UIId.GameOver))
                    {
                        ui.Close(UIId.GameOver);
                    }

                    if (ui.IsOpen(UIId.SkillSelect))
                    {
                        ui.Close(UIId.SkillSelect);
                    }

                    if (ui.IsOpen(UIId.SkillWheel))
                    {
                        ui.Close(UIId.SkillWheel);
                    }
                }

                FallenChanged?.Invoke();
                GameProgressController.Instance?.TrySave("ResetBeach");
                Debug.Log($"[PartyController] 已返回海滩并重置存档 @ {pos}", this);
            }
            finally
            {
                _restarting = false;
            }
        }

        /// <summary>全灭后重新开始：清等级/被动、复活全员、回到肉鸽出生点。</summary>
        public void RestartRougeRun()
        {
            if (_restarting)
            {
                return;
            }

            _restarting = true;
            try
            {
            _gameOverShown = false;

            if (_soloRespawnCoroutine != null)
            {
                StopCoroutine(_soloRespawnCoroutine);
                _soloRespawnCoroutine = null;
                _soloRespawning = false;
            }

            ClearFallen(notify: false);
            PartyRougeProgress.ResetRun();

            if (_residual != null)
            {
                DespawnResidual(_residual);
            }

            Vector3 pos = spawnPosition;
            Quaternion rot = transform.rotation;
            var rouge = RouGeLikeFlowController.Instance;
            if (rouge == null || !rouge.TryGetPlayerSpawnPose(out Vector3 spawnPos, out Quaternion spawnRot))
            {
                Debug.LogWarning("[PartyController] 未找到 PlayerSpawn，回退默认出生点。", this);
            }
            else
            {
                pos = spawnPos;
                rot = spawnRot;
            }

            if (syncTransformToSpawn)
            {
                transform.position = pos;
            }

            if (_active != null)
            {
                DespawnCharacter(_active);
            }

            int idx = Mathf.Clamp(startIndex, 0, Mathf.Max(0, MemberCount - 1));
            SwitchTo(idx, pos, rot, force: true, applySpawnOffset: false);
            rouge?.ResetEncounterForRestart();

            var ui = UIManager.Instance;
            if (ui != null)
            {
                if (ui.IsOpen(UIId.GameOver))
                {
                    ui.Close(UIId.GameOver);
                }

                if (ui.IsOpen(UIId.SkillSelect))
                {
                    ui.Close(UIId.SkillSelect);
                }

                if (ui.IsOpen(UIId.SkillWheel))
                {
                    ui.Close(UIId.SkillWheel);
                }
            }

            FallenChanged?.Invoke();
            }
            finally
            {
                _restarting = false;
            }
        }

        void ShowGameOver()
        {
            if (_gameOverShown)
            {
                return;
            }

            _gameOverShown = true;
            FallenChanged?.Invoke();
            UIBattleTimePanel.EndRougeTimer();

            var ui = UIManager.Instance;
            if (ui == null)
            {
                Debug.LogError("[PartyController] 全灭但无 UIManager，无法打开结算。", this);
                return;
            }

            if (ui.Open(UIId.GameOver) == null)
            {
                Debug.LogError("[PartyController] 无法打开 UI_GameOver_Dialog。", this);
            }
        }

        /// <summary>肉鸽倒计时归零：派蒙救援结算（同 GameOver 交互）。</summary>
        public void ShowRescueGameOver()
        {
            if (_gameOverShown)
            {
                return;
            }

            _gameOverShown = true;
            FallenChanged?.Invoke();
            UIBattleTimePanel.EndRougeTimer();

            var ui = UIManager.Instance;
            if (ui == null)
            {
                Debug.LogError("[PartyController] 救援结算但无 UIManager。", this);
                return;
            }

            if (ui.Open(UIId.GameOver, new UIGameOverDialogArgs
                {
                    titleKey = UIGameOverDialog.RescueTitleKey
                }) == null)
            {
                Debug.LogError("[PartyController] 无法打开救援结算 UI_GameOver_Dialog。", this);
            }
        }

        void MarkFallen(int index)
        {
            EnsureFallenArray();
            if (_fallen == null || index < 0 || index >= _fallen.Length)
            {
                return;
            }

            _fallen[index] = true;
            FallenChanged?.Invoke();
        }

        void ClearFallen(bool notify)
        {
            EnsureFallenArray();
            if (_fallen != null)
            {
                for (int i = 0; i < _fallen.Length; i++)
                {
                    _fallen[i] = false;
                }
            }

            if (notify)
            {
                FallenChanged?.Invoke();
            }
        }

        void EnsureFallenArray()
        {
            int n = Mathf.Max(0, MemberCount);
            if (_fallen != null && _fallen.Length == n)
            {
                return;
            }

            var next = n > 0 ? new bool[n] : Array.Empty<bool>();
            if (_fallen != null && n > 0)
            {
                int copy = Mathf.Min(_fallen.Length, n);
                Array.Copy(_fallen, next, copy);
            }

            _fallen = next;
        }

        int FindNextLivingIndex(int fromIndex)
        {
            EnsureFallenArray();
            if (MemberCount <= 0)
            {
                return -1;
            }

            int start = fromIndex < 0 ? -1 : fromIndex;
            for (int i = 1; i <= MemberCount; i++)
            {
                int idx = (start + i) % MemberCount;
                if (!IsSlotFallen(idx))
                {
                    return idx;
                }
            }

            return -1;
        }

        bool[] CaptureFallenSlots()
        {
            EnsureFallenArray();
            if (_fallen == null || _fallen.Length == 0)
            {
                return Array.Empty<bool>();
            }

            var copy = new bool[_fallen.Length];
            Array.Copy(_fallen, copy, _fallen.Length);
            return copy;
        }

        void RestoreFallenSlots(bool[] slots)
        {
            EnsureFallenArray();
            ClearFallen(notify: false);
            if (slots != null && _fallen != null)
            {
                int n = Mathf.Min(_fallen.Length, slots.Length);
                for (int i = 0; i < n; i++)
                {
                    _fallen[i] = slots[i];
                }
            }

            FallenChanged?.Invoke();
        }

        int FirstLivingIndex(int preferred)
        {
            EnsureFallenArray();
            if (preferred >= 0 && preferred < MemberCount && !IsSlotFallen(preferred))
            {
                return preferred;
            }

            if (preferred >= 0 && preferred < MemberCount)
            {
                int next = FindNextLivingIndex(preferred);
                if (next >= 0)
                {
                    return next;
                }
            }

            for (int i = 0; i < MemberCount; i++)
            {
                if (!IsSlotFallen(i))
                {
                    return i;
                }
            }

            return -1;
        }

        IEnumerator SoloRespawnRoutine(int index)
        {
            _soloRespawning = true;
            PlayerTargetLocator.InvalidateCache();
            yield return new WaitForSeconds(Mathf.Max(0.1f, soloRespawnDelay));

            if (characterPrefabs == null || characterPrefabs.Length == 0)
            {
                _soloRespawning = false;
                _soloRespawnCoroutine = null;
                yield break;
            }

            index = Mathf.Clamp(index, 0, characterPrefabs.Length - 1);
            ResolveSpawnPose(out Vector3 pos, out Quaternion rot);
            if (syncTransformToSpawn)
            {
                transform.position = pos;
            }

            SwitchTo(index, pos, rot, force: true, applySpawnOffset: false);
            _soloRespawning = false;
            _soloRespawnCoroutine = null;
            PlayerTargetLocator.InvalidateCache();
        }

        void OnResidualFinished(GenshinLikeCharacter character)
        {
            if (character == null)
            {
                return;
            }

            character.ResidualFinished -= OnResidualFinished;
            if (_residual == character)
            {
                _residual = null;
            }

            DespawnCharacter(character);
        }

        void DespawnResidual(GenshinLikeCharacter character)
        {
            if (character == null)
            {
                return;
            }

            character.ResidualFinished -= OnResidualFinished;
            if (_residual == character)
            {
                _residual = null;
            }

            // 超时强制停技能再删
            if (character.SkillPlayer != null && character.SkillPlayer.IsPlaying)
            {
                character.SkillPlayer.Stop();
            }

            DespawnCharacter(character);
        }

        void DespawnCharacter(GenshinLikeCharacter character)
        {
            if (character == null)
            {
                return;
            }

            UnbindCharacter(character);
            character.ResidualFinished -= OnResidualFinished;
            _spawned.Remove(character);
            if (_active == character)
            {
                _active = null;
            }

            AttackSkill.Rouge.RougeOrbitWeaponDriver.DetachFromCharacter(character);
            Destroy(character.gameObject);
            PlayerTargetLocator.InvalidateCache();
        }

        void OnGUI()
        {
            if (!drawHud)
            {
                return;
            }

            string activeName = _active != null ? _active.name : "null";
            string residualName = _residual != null ? _residual.name : "-";
            string deathHint = _soloRespawning
                ? LocalizationService.Get(LocalizationTableType.Common, "party_respawning")
                : (_active != null && _active.IsDead ? " | Dead" : string.Empty);
            GUI.Label(
                new Rect(12, 36, 900, 24),
                LocalizationService.Format(
                    LocalizationTableType.Common,
                    "party_hud",
                    ActiveIndex + 1,
                    MemberCount,
                    activeName,
                    residualName,
                    deathHint));
        }
    }
}
