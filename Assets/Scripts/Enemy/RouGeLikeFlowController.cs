using System.Collections.Generic;
using AttackSkill.Audio;
using AttackSkill.CameraSystem;
using AttackSkill.Character;
using AttackSkill.Character.HSM;
using AttackSkill.Core;
using AttackSkill.Game;
using AttackSkill.Localization;
using AttackSkill.Rouge;
using AttackSkill.UI;
using UnityEngine;

namespace AttackSkill.Enemy
{
    /// <summary>
    /// 肉鸽平面流程：清掉初始 EnemySpawnGroup 波次 → 传送到 PlayerSpawn →
    /// 仅当玩家在 RouGeLikePlane 内时，在玩家 10m 半径内随机刷怪。
    /// PlayerSpawn 与 RouGeLikePlane 同级（场景根下）。
    /// </summary>
    public class RouGeLikeFlowController : MonoBehaviour
    {
        [Header("Refs（可空，运行时按名称回退）")]
        [SerializeField] EnemySpawnGroup introSpawnGroup;
        [SerializeField] Transform planeRoot;
        [SerializeField] Transform playerSpawn;
        [SerializeField] Transform enemyGroupRoot;
        [SerializeField] EnemyDefinition[] enemyPool;

        [Header("Spawn")]
        [Tooltip("两波出怪间隔（秒）")]
        [SerializeField] Vector2 spawnInterval = new Vector2(4f, 8f);
        [Tooltip("每波同时生成数量（含）")]
        [SerializeField] Vector2Int spawnBatchCount = new Vector2Int(8, 16);
        [Tooltip("相对玩家的刷怪半径（米）")]
        [SerializeField] float spawnRadius = 10f;
        [Tooltip("相对玩家的最近刷怪距离，避免刷在脚边")]
        [SerializeField] float spawnMinRadius = 3.5f;
        [Tooltip("Plane 本地 XZ 半宽（默认 Plane 网格 ±5）")]
        [SerializeField] float planeHalfExtent = 5f;
        [SerializeField] float planeHeightTolerance = 20f;
        [Tooltip("传送后首次刷怪延迟（秒）")]
        [SerializeField] float postTeleportSpawnDelay = 1.5f;
        [Tooltip("场上上限：基数")]
        [SerializeField] int maxAliveBase = MaxAliveBase;
        [Tooltip("场上上限：每级增加")]
        [SerializeField] int maxAlivePerLevel = MaxAlivePerLevel;
        [Tooltip("场上上限：封顶")]
        [SerializeField] int maxAliveCap = MaxAliveCap;

        readonly List<EnemyAgent> _aliveRouge = new List<EnemyAgent>(128);
        readonly List<EnemyDefinition> _eligibleScratch = new List<EnemyDefinition>(16);
        float _nextSpawnAt;
        bool _teleported;
        bool _introGateClosed;
        bool _boundIntro;

        public static RouGeLikeFlowController Instance { get; private set; }

        public const int MaxAliveBase = 30;
        public const int MaxAlivePerLevel = 5;
        public const int MaxAliveCap = 100;

        public bool HasTeleported => _teleported;
        public bool IsPlayerInArea => CheckPlayerInArea();
        public int MaxAliveNow => MaxAliveForLevel(PartyRougeProgress.Level);

        /// <summary>Lv1=30，之后每级 +5，封顶 100（可用 Inspector 覆盖）。</summary>
        public static int MaxAliveForLevel(int level)
        {
            int lv = Mathf.Max(1, level);
            int bas = Instance != null ? Mathf.Max(1, Instance.maxAliveBase) : MaxAliveBase;
            int per = Instance != null ? Mathf.Max(0, Instance.maxAlivePerLevel) : MaxAlivePerLevel;
            int cap = Instance != null ? Mathf.Max(1, Instance.maxAliveCap) : MaxAliveCap;
            return Mathf.Min(cap, bas + per * (lv - 1));
        }

        /// <summary>角色已生成：海滩 intro 立刻按 20m 判断一次（含已在范围内）。</summary>
        public void NotifyPlayerReady()
        {
            if (_teleported || introSpawnGroup == null || !introSpawnGroup.enabled)
            {
                return;
            }

            introSpawnGroup.EvaluateActivation();
        }

        void Awake()
        {
            Instance = this;
            RougeEnemySpawnCatalog.EnsureBuilt();
            ResolveRefs();
            BindIntroGroup();
            _nextSpawnAt = Time.time + Random.Range(spawnInterval.x, spawnInterval.y);
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            UnbindIntroGroup();
            for (int i = 0; i < _aliveRouge.Count; i++)
            {
                if (_aliveRouge[i] != null)
                {
                    _aliveRouge[i].Died -= OnRougeDied;
                }
            }
        }

        void Update()
        {
            CleanupDeadList();

            if (!_teleported)
            {
                return;
            }

            if (!IsPlayerInArea)
            {
                return;
            }

            if (_aliveRouge.Count >= MaxAliveNow)
            {
                return;
            }

            if (Time.time < _nextSpawnAt)
            {
                return;
            }

            int spawned = TrySpawnBatch();
            if (spawned > 0)
            {
                _nextSpawnAt = Time.time + Random.Range(
                    Mathf.Max(0.5f, spawnInterval.x),
                    Mathf.Max(spawnInterval.x, spawnInterval.y));
            }
            else
            {
                _nextSpawnAt = Time.time + 1f;
            }
        }

        void ResolveRefs()
        {
            if (planeRoot == null)
            {
                var plane = GameObject.Find("RouGeLikePlane");
                planeRoot = plane != null ? plane.transform : transform;
            }

            // PlayerSpawn 与 RouGeLikePlane 同级；EnemyGroup 仅作生成物父节点
            if (playerSpawn == null)
            {
                playerSpawn = FindSceneTransform("PlayerSpawn");
            }

            if (enemyGroupRoot == null)
            {
                enemyGroupRoot = FindSceneTransform("EnemyGroup");
            }

            if (introSpawnGroup == null)
            {
                introSpawnGroup = FindObjectOfType<EnemySpawnGroup>();
                if (introSpawnGroup != null)
                {
                    var all = Object.FindObjectsOfType<EnemySpawnGroup>();
                    if (all != null && all.Length > 1)
                    {
                        Debug.LogWarning(
                            $"[RouGeLike] 场景有 {all.Length} 个 EnemySpawnGroup，已绑定 \"{introSpawnGroup.name}\"。请在 Inspector 指定 introSpawnGroup。",
                            this);
                    }
                }
            }

            if ((enemyPool == null || enemyPool.Length == 0) && introSpawnGroup != null)
            {
                enemyPool = introSpawnGroup.CollectDefinitions();
            }

            // 优先用等级解锁表；无表时才退回 intro 池
            RougeEnemySpawnCatalog.EnsureBuilt();
        }

        static Transform FindSceneTransform(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
            {
                return null;
            }

            var go = GameObject.Find(objectName);
            return go != null ? go.transform : null;
        }

        void BindIntroGroup()
        {
            if (_boundIntro || introSpawnGroup == null)
            {
                return;
            }

            introSpawnGroup.InitialWaveClearedEvent -= OnIntroWaveCleared;
            introSpawnGroup.InitialWaveClearedEvent += OnIntroWaveCleared;
            _boundIntro = true;

            if (introSpawnGroup.InitialWaveCleared)
            {
                OnIntroWaveCleared();
            }
        }

        void UnbindIntroGroup()
        {
            if (!_boundIntro || introSpawnGroup == null)
            {
                return;
            }

            introSpawnGroup.InitialWaveClearedEvent -= OnIntroWaveCleared;
            _boundIntro = false;
        }

        void OnIntroWaveCleared()
        {
            if (_teleported || _introGateClosed || GameSaveService.HasPendingRestore)
            {
                return;
            }

            // 读档已有进度却未关闸时：只关 intro，禁止 ResetRun
            if (PartyRougeProgress.Level > 1 ||
                PartyRougeProgress.Exp > 0 ||
                PartyRougeProgress.Passives.Count > 0)
            {
                Debug.LogWarning("[RouGeLike] intro 清场时已有肉鸽进度，跳过 ResetRun，仅关闭海滩闸。", this);
                CloseIntroGate();
                return;
            }

            EnterRougeCombat(resetProgress: true, teleportPlayer: true, showTip: true);
        }

        /// <summary>读档：若本局已进过肉鸽区（或坐标已在平面内），开刷怪闸且不 ResetRun。</summary>
        public void ApplyRestoredEntry(bool hasTeleported, Vector3 playerPos, float battleTimeRemaining = -1f)
        {
            ResolveRefs();
            if (_teleported)
            {
                CloseIntroGate();
                return;
            }

            // 读档容差：边缘坐标仍视为在区内
            bool inArea = ContainsPoint(playerPos, restoreMargin: 2f);
            if (!hasTeleported && !inArea)
            {
                return;
            }

            EnterRougeCombat(
                resetProgress: false,
                teleportPlayer: false,
                showTip: false,
                battleTimeRemaining: battleTimeRemaining);
            CloseIntroGate();
        }

        /// <summary>Flow 缺失时由 Party 调用：强制关掉所有海滩 intro 组。</summary>
        public static void ForceDisableIntroGroups()
        {
            var groups = Object.FindObjectsOfType<EnemySpawnGroup>();
            for (int i = 0; i < groups.Length; i++)
            {
                if (groups[i] != null)
                {
                    groups[i].MarkIntroClearedAndDisable();
                }
            }

            if (Instance != null)
            {
                Instance._introGateClosed = true;
            }
        }

        void CloseIntroGate()
        {
            _introGateClosed = true;
            if (introSpawnGroup != null)
            {
                introSpawnGroup.MarkIntroClearedAndDisable();
            }
        }

        void EnterRougeCombat(
            bool resetProgress,
            bool teleportPlayer,
            bool showTip,
            float battleTimeRemaining = -1f)
        {
            if (teleportPlayer)
            {
                if (playerSpawn == null)
                {
                    Debug.LogError("[RouGeLike] 缺少 PlayerSpawn。", this);
                    return;
                }

                Vector3 pos = playerSpawn.position;
                pos.y += 0.05f;
                Quaternion rot = playerSpawn.rotation;

                var party = PartyController.Instance;
                if (party != null)
                {
                    party.TeleportActiveTo(pos, rot);
                }
                else
                {
                    Transform player = PlayerTargetLocator.GetActivePlayerTransform();
                    var character = player != null
                        ? player.GetComponentInParent<GenshinLikeCharacter>()
                        : null;
                    character?.TeleportTo(pos, rot);
                }

                var cam = GameServices.ResolveCamera();
                if (cam != null)
                {
                    cam.SnapToFollowTarget();
                }
            }

            _teleported = true;
            _introGateClosed = true;
            _nextSpawnAt = Time.time + Mathf.Max(0.1f, postTeleportSpawnDelay);
            if (resetProgress)
            {
                PartyRougeProgress.ResetRun();
            }

            PrewarmEnemyPool();
            SceneBgmPlayer.PlayRougeDrone();
            // 新进肉鸽 / 未记录剩余：满时长；读档：续跑剩余秒（0 则立刻结算）
            float timer = resetProgress || battleTimeRemaining < 0f
                ? -1f
                : battleTimeRemaining;
            UIBattleTimePanel.BeginRougeTimer(timer);
            if (showTip)
            {
                ShowTeleportTip();
            }

            Debug.Log(
                resetProgress
                    ? "[RouGeLike] 初始波次已清，传送至 RouGeLikePlane。"
                    : "[RouGeLike] 读档恢复肉鸽区，跳过海滩闸门。",
                this);
        }

        void PrewarmEnemyPool()
        {
            int playerLevel = Mathf.Max(1, AttackSkill.Rouge.PartyRougeProgress.Level);
            var eligible = RougeEnemySpawnCatalog.CollectEligible(playerLevel, _eligibleScratch);
            if (eligible.Count == 0)
            {
                return;
            }

            int cap = Mathf.Max(1, MaxAliveNow);
            int perType = Mathf.Max(8, Mathf.CeilToInt(cap / (float)eligible.Count));
            perType = Mathf.Min(perType, cap);
            for (int i = 0; i < eligible.Count; i++)
            {
                EnemyObjectPool.Prewarm(eligible[i], perType);
            }
        }

        static void ShowTeleportTip()
        {
            LocalizationService.EnsureInitialized();
            string tip = LocalizationService.Get(LocalizationTableType.UI, "rouge_like_teleport_tip");
            if (string.IsNullOrEmpty(tip) ||
                tip.StartsWith("#") ||
                tip == "rouge_like_teleport_tip")
            {
                tip = "激怒了深渊，被带到了未知之地...";
            }

            UIManager.Instance?.ShowTip(tip, 3f);
        }

        /// <summary>一波在玩家附近同时刷怪。</summary>
        int TrySpawnBatch()
        {
            int room = Mathf.Max(0, MaxAliveNow - _aliveRouge.Count);
            if (room <= 0)
            {
                return 0;
            }

            Transform player = PlayerTargetLocator.GetActivePlayerTransform();
            if (player == null)
            {
                return 0;
            }

            int minBatch = Mathf.Max(1, spawnBatchCount.x);
            int maxBatch = Mathf.Max(minBatch, spawnBatchCount.y);
            int want = Random.Range(minBatch, maxBatch + 1);
            want = Mathf.Min(want, room);

            int spawned = 0;
            for (int i = 0; i < want; i++)
            {
                if (TrySpawnNearPlayer(player))
                {
                    spawned++;
                }
            }

            return spawned;
        }

        bool TrySpawnNearPlayer(Transform player)
        {
            EnemyDefinition def = PickDefinition();
            if (def == null || player == null)
            {
                return false;
            }

            if (!TryPickSpawnPose(player, out Vector3 pos, out Quaternion rot))
            {
                return false;
            }

            EnemyAgent agent = EnemySpawnPoint.SpawnAt(def, pos, rot, enemyGroupRoot);
            if (agent == null)
            {
                return false;
            }

            agent.IsRougeEncounter = true;
            agent.Died += OnRougeDied;
            _aliveRouge.Add(agent);
            return true;
        }

        bool TryPickSpawnPose(Transform player, out Vector3 pos, out Quaternion rot)
        {
            pos = player.position;
            rot = Quaternion.identity;
            float maxR = Mathf.Max(1f, spawnRadius);
            float minR = Mathf.Clamp(spawnMinRadius, 0.5f, maxR * 0.85f);

            for (int attempt = 0; attempt < 12; attempt++)
            {
                float ang = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float r = Random.Range(minR, maxR);
                Vector3 candidate = player.position;
                candidate.x += Mathf.Cos(ang) * r;
                candidate.z += Mathf.Sin(ang) * r;
                candidate = ClampToPlane(candidate);
                candidate.y = player.position.y;

                Vector3 planar = candidate - player.position;
                planar.y = 0f;
                float dist = planar.magnitude;
                if (dist < minR * 0.6f || dist > maxR + 0.75f)
                {
                    continue;
                }

                if (HasAliveNear(candidate, 1.5f))
                {
                    continue;
                }

                pos = candidate;
                if (planar.sqrMagnitude > 0.0001f)
                {
                    rot = Quaternion.LookRotation(-planar.normalized, Vector3.up);
                }
                else
                {
                    rot = player.rotation;
                }

                return true;
            }

            return false;
        }

        Vector3 ClampToPlane(Vector3 worldPos)
        {
            if (planeRoot == null)
            {
                return worldPos;
            }

            Vector3 local = planeRoot.InverseTransformPoint(worldPos);
            float half = Mathf.Max(0.5f, planeHalfExtent);
            const float edgePad = 0.6f;
            float limit = Mathf.Max(0.2f, half - edgePad);
            local.x = Mathf.Clamp(local.x, -limit, limit);
            local.z = Mathf.Clamp(local.z, -limit, limit);
            return planeRoot.TransformPoint(local);
        }

        EnemyDefinition PickDefinition()
        {
            int playerLevel = Mathf.Max(1, PartyRougeProgress.Level);
            var eligible = RougeEnemySpawnCatalog.CollectEligible(playerLevel, _eligibleScratch);
            if (eligible.Count > 0)
            {
                return eligible[Random.Range(0, eligible.Count)];
            }

            // 回退：旧 enemyPool（无等级过滤）
            if (enemyPool == null || enemyPool.Length == 0)
            {
                return null;
            }

            for (int attempt = 0; attempt < 8; attempt++)
            {
                var def = enemyPool[Random.Range(0, enemyPool.Length)];
                if (def != null && def.prefab != null)
                {
                    return def;
                }
            }

            return null;
        }

        bool HasAliveNear(Vector3 pos, float radius)
        {
            float r2 = radius * radius;
            for (int i = 0; i < _aliveRouge.Count; i++)
            {
                EnemyAgent a = _aliveRouge[i];
                if (a == null || a.IsDead)
                {
                    continue;
                }

                if ((a.transform.position - pos).sqrMagnitude <= r2)
                {
                    return true;
                }
            }

            return false;
        }

        void OnRougeDied(EnemyAgent agent)
        {
            if (agent != null)
            {
                agent.Died -= OnRougeDied;
            }
        }

        void CleanupDeadList()
        {
            for (int i = _aliveRouge.Count - 1; i >= 0; i--)
            {
                EnemyAgent a = _aliveRouge[i];
                if (a == null || a.IsDead)
                {
                    _aliveRouge.RemoveAt(i);
                }
            }
        }

        /// <summary>肉鸽出生点位姿（PlayerSpawn）。</summary>
        public bool TryGetPlayerSpawnPose(out Vector3 pos, out Quaternion rot)
        {
            if (playerSpawn == null)
            {
                ResolveRefs();
            }

            if (playerSpawn == null)
            {
                pos = default;
                rot = Quaternion.identity;
                return false;
            }

            pos = playerSpawn.position;
            pos.y += 0.05f;
            rot = playerSpawn.rotation;
            return true;
        }

        /// <summary>暂停重置：离开肉鸽区，恢复海滩 intro 波并切回海滨 BGM。</summary>
        public void ResetToCamp()
        {
            ResolveRefs();
            _teleported = false;
            _introGateClosed = false;
            ClearAliveRouge();
            if (introSpawnGroup != null)
            {
                introSpawnGroup.ResetWaveForReplay();
            }

            BindIntroGroup();
            SceneBgmPlayer.PlayCampTheme();
            UIBattleTimePanel.EndRougeTimer();

            var cam = GameServices.ResolveCamera();
            if (cam != null)
            {
                cam.SnapToFollowTarget();
            }

            Debug.Log("[RouGeLike] 已重置回海滩清波。", this);
        }

        /// <summary>重新开始：清场上肉鸽怪并重置刷怪计时，保持已进入肉鸽区。</summary>
        public void ResetEncounterForRestart()
        {
            ResolveRefs();
            ClearAliveRouge();
            _teleported = true;
            _introGateClosed = true;
            _nextSpawnAt = Time.time + Mathf.Max(0.1f, postTeleportSpawnDelay);

            var cam = GameServices.ResolveCamera();
            if (cam != null)
            {
                cam.SnapToFollowTarget();
            }

            SceneBgmPlayer.PlayRougeDrone();
            UIBattleTimePanel.BeginRougeTimer();
        }

        void ClearAliveRouge()
        {
            for (int i = _aliveRouge.Count - 1; i >= 0; i--)
            {
                EnemyAgent agent = _aliveRouge[i];
                if (agent == null)
                {
                    continue;
                }

                agent.Died -= OnRougeDied;
                if (!EnemyObjectPool.TryRelease(agent.gameObject))
                {
                    Destroy(agent.gameObject);
                }
            }

            _aliveRouge.Clear();
        }

        /// <summary>世界坐标是否落在肉鸽平面内（含肉鸽刷出的敌人）。</summary>
        public static bool ContainsWorldPoint(Vector3 worldPos)
        {
            return Instance != null && Instance.ContainsPoint(worldPos);
        }

        public bool ContainsPoint(Vector3 worldPos) => ContainsPoint(worldPos, restoreMargin: 0f);

        /// <param name="restoreMargin">读档时额外放宽半宽（米），避免边缘坐标判区外。</param>
        public bool ContainsPoint(Vector3 worldPos, float restoreMargin)
        {
            if (planeRoot == null)
            {
                return false;
            }

            Vector3 local = planeRoot.InverseTransformPoint(worldPos);
            float half = Mathf.Max(0.5f, planeHalfExtent) + Mathf.Max(0f, restoreMargin);
            if (Mathf.Abs(local.x) > half || Mathf.Abs(local.z) > half)
            {
                return false;
            }

            return local.y > -2f && local.y < planeHeightTolerance;
        }

        bool CheckPlayerInArea()
        {
            Transform player = PlayerTargetLocator.GetActivePlayerTransform();
            if (player == null)
            {
                return false;
            }

            return ContainsPoint(player.position);
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Transform root = planeRoot != null ? planeRoot : transform;
            Gizmos.matrix = root.localToWorldMatrix;
            Gizmos.color = new Color(1f, 0.55f, 0.1f, 0.35f);
            float h = Mathf.Max(0.5f, planeHalfExtent);
            Gizmos.DrawWireCube(Vector3.up * 0.5f, new Vector3(h * 2f, 1f, h * 2f));

            Transform player = PlayerTargetLocator.GetActivePlayerTransform();
            if (player != null)
            {
                Gizmos.matrix = Matrix4x4.identity;
                Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.35f);
                Gizmos.DrawWireSphere(player.position, Mathf.Max(1f, spawnRadius));
            }
        }
#endif
    }
}
