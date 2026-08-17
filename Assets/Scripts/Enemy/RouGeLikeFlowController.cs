using System.Collections.Generic;
using AttackSkill.CameraSystem;
using AttackSkill.Character;
using AttackSkill.Character.HSM;
using AttackSkill.Core;
using AttackSkill.Localization;
using AttackSkill.Rouge;
using AttackSkill.UI;
using UnityEngine;

namespace AttackSkill.Enemy
{
    /// <summary>
    /// 肉鸽平面流程：清掉初始 EnemySpawnGroup 波次 → 传送到 PlayerSpawn →
    /// 仅当玩家在 RouGeLikePlane 内时，于 EnemyGroup 子节点随机刷怪。
    /// PlayerSpawn / EnemyGroup 与 RouGeLikePlane 同级（场景根下）。
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
        [Tooltip("每波同时占用的出生点数量（含）")]
        [SerializeField] Vector2Int spawnBatchCount = new Vector2Int(2, 4);
        [Tooltip("场上同时存活上限")]
        [SerializeField] int maxAlive = 6;
        [Tooltip("Plane 本地 XZ 半宽（默认 Plane 网格 ±5）")]
        [SerializeField] float planeHalfExtent = 5f;
        [SerializeField] float planeHeightTolerance = 20f;

        readonly List<EnemyAgent> _aliveRouge = new List<EnemyAgent>(16);
        readonly List<EnemyDefinition> _eligibleScratch = new List<EnemyDefinition>(16);
        readonly List<Transform> _emptySlotScratch = new List<Transform>(16);
        Transform[] _spawnSlots;
        float _nextSpawnAt;
        bool _teleported;
        bool _boundIntro;

        public bool HasTeleported => _teleported;
        public bool IsPlayerInArea => CheckPlayerInArea();

        void Awake()
        {
            RougeEnemySpawnCatalog.EnsureBuilt();
            ResolveRefs();
            CacheSpawnSlots();
            BindIntroGroup();
            _nextSpawnAt = Time.time + Random.Range(spawnInterval.x, spawnInterval.y);
        }

        void OnDestroy()
        {
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

            if (_aliveRouge.Count >= Mathf.Max(1, maxAlive))
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

            // PlayerSpawn / EnemyGroup 已与 RouGeLikePlane 同级，不再挂在平面下
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

        void CacheSpawnSlots()
        {
            if (enemyGroupRoot == null)
            {
                _spawnSlots = System.Array.Empty<Transform>();
                return;
            }

            int n = enemyGroupRoot.childCount;
            _spawnSlots = new Transform[n];
            for (int i = 0; i < n; i++)
            {
                _spawnSlots[i] = enemyGroupRoot.GetChild(i);
            }
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
            if (_teleported)
            {
                return;
            }

            TeleportToRougePlane();
        }

        void TeleportToRougePlane()
        {
            if (playerSpawn == null)
            {
                Debug.LogError("[RouGeLike] 缺少 PlayerSpawn。", this);
                return;
            }

            Vector3 pos = playerSpawn.position;
            // 略抬高，避免卡进地面
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

            _teleported = true;
            _nextSpawnAt = Time.time + 1.5f;
            AttackSkill.Rouge.PartyRougeProgress.ResetRun();
            ShowTeleportTip();
            Debug.Log("[RouGeLike] 初始波次已清，传送至 RouGeLikePlane。", this);
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

        /// <summary>一波在多个空闲出生点同时刷怪。</summary>
        int TrySpawnBatch()
        {
            int room = Mathf.Max(0, Mathf.Max(1, maxAlive) - _aliveRouge.Count);
            if (room <= 0)
            {
                return 0;
            }

            int minBatch = Mathf.Max(1, spawnBatchCount.x);
            int maxBatch = Mathf.Max(minBatch, spawnBatchCount.y);
            int want = Random.Range(minBatch, maxBatch + 1);
            want = Mathf.Min(want, room);

            CollectEmptySlots(_emptySlotScratch);
            if (_emptySlotScratch.Count == 0)
            {
                return 0;
            }

            // 打乱空闲点，取前 want 个
            for (int i = _emptySlotScratch.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                var tmp = _emptySlotScratch[i];
                _emptySlotScratch[i] = _emptySlotScratch[j];
                _emptySlotScratch[j] = tmp;
            }

            int spawnCount = Mathf.Min(want, _emptySlotScratch.Count);
            int spawned = 0;
            for (int i = 0; i < spawnCount; i++)
            {
                if (TrySpawnAt(_emptySlotScratch[i]))
                {
                    spawned++;
                }
            }

            return spawned;
        }

        bool TrySpawnAt(Transform slot)
        {
            if (slot == null)
            {
                return false;
            }

            EnemyDefinition def = PickDefinition();
            if (def == null)
            {
                return false;
            }

            EnemyAgent agent = EnemySpawnPoint.SpawnAt(def, slot.position, slot.rotation, enemyGroupRoot);
            if (agent == null)
            {
                return false;
            }

            agent.Died += OnRougeDied;
            _aliveRouge.Add(agent);
            return true;
        }

        void CollectEmptySlots(List<Transform> buffer)
        {
            buffer.Clear();
            if (_spawnSlots == null)
            {
                return;
            }

            for (int i = 0; i < _spawnSlots.Length; i++)
            {
                Transform slot = _spawnSlots[i];
                if (slot == null)
                {
                    continue;
                }

                if (!HasAliveNear(slot.position, 1.25f))
                {
                    buffer.Add(slot);
                }
            }
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

        bool CheckPlayerInArea()
        {
            Transform player = PlayerTargetLocator.GetActivePlayerTransform();
            if (player == null || planeRoot == null)
            {
                return false;
            }

            Vector3 local = planeRoot.InverseTransformPoint(player.position);
            float half = Mathf.Max(0.5f, planeHalfExtent);
            if (Mathf.Abs(local.x) > half || Mathf.Abs(local.z) > half)
            {
                return false;
            }

            return local.y > -2f && local.y < planeHeightTolerance;
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Transform root = planeRoot != null ? planeRoot : transform;
            Gizmos.matrix = root.localToWorldMatrix;
            Gizmos.color = new Color(1f, 0.55f, 0.1f, 0.35f);
            float h = Mathf.Max(0.5f, planeHalfExtent);
            Gizmos.DrawWireCube(Vector3.up * 0.5f, new Vector3(h * 2f, 1f, h * 2f));
        }
#endif
    }
}
