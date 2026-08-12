using AttackSkill.Character;
using AttackSkill.Core;
using AttackSkill.Enemy;
using UnityEngine;
using UnityEngine.UI;

namespace AttackSkill.UI.World
{
    /// <summary>头顶血条 / 跳字：世界挂点投影到 Screen Overlay，遮挡隐藏。</summary>
    [DefaultExecutionOrder(-80)]
    public sealed class WorldUiService : MonoBehaviour
    {
        public const string RootName = "WorldUI_Runtime";

        static WorldUiService _instance;

        [SerializeField] GameObject damageNumberPrefab;
        [SerializeField] GameObject enemyBloodPrefab;
        [SerializeField] GameObject obtainRemainsPrefab;
        [SerializeField] float bloodVisibleRange = 20f;
        [SerializeField] Vector3 bloodWorldOffset = new Vector3(0f, 2.6f, 0f);
        [SerializeField] float damageNumberLifetime = 0.9f;
        [Tooltip("遮挡检测层；建议含 Default/Environment，不含 Ignore Raycast")]
        [SerializeField] LayerMask occlusionMask = ~0;

        Transform _poolRoot;
        Canvas _overlayCanvas;
        Camera _cam;
        DamageNumberPool _damagePool;

        public static WorldUiService Instance => _instance;

        public float BloodVisibleRange => bloodVisibleRange;
        public Vector3 BloodWorldOffset => bloodWorldOffset;
        public float DamageNumberLifetime => damageNumberLifetime;
        public Camera WorldCamera => _cam != null ? _cam : Camera.main;
        public Transform PoolRoot => _poolRoot;

        public static WorldUiService EnsureExists()
        {
            if (_instance != null)
            {
                return _instance;
            }

            var existing = FindObjectOfType<WorldUiService>();
            if (existing != null)
            {
                _instance = existing;
                existing.Bootstrap();
                return _instance;
            }

            var go = new GameObject(RootName);
            _instance = go.AddComponent<WorldUiService>();
            SceneSingleton.ApplyDontDestroyOnLoad(_instance, true);
            _instance.Bootstrap();
            return _instance;
        }

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            SceneSingleton.ApplyDontDestroyOnLoad(this, true);
            Bootstrap();
        }

        void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }

            _damagePool?.Dispose();
            HitResolverBridge.Unbind();
        }

        void LateUpdate()
        {
            _cam = WorldUiScreen.ResolveRenderCamera(_cam);
        }

        void Bootstrap()
        {
            ResolvePrefabs();
            EnsurePoolRoot();
            _cam = WorldUiScreen.ResolveRenderCamera(null);
            ConfigureDefaultOcclusionMask();

            if (_damagePool == null)
            {
                _damagePool = new DamageNumberPool(damageNumberPrefab, _poolRoot, this);
                _damagePool.Prewarm(6);
            }

            HitResolverBridge.Bind(this);
        }

        void ResolvePrefabs()
        {
            var settings = CharacterRuntimeSettings.Get();
            if (settings != null)
            {
                if (damageNumberPrefab == null)
                {
                    damageNumberPrefab = settings.GetDamageNumberPrefab();
                }

                if (enemyBloodPrefab == null)
                {
                    enemyBloodPrefab = settings.GetEnemyBloodPrefab();
                }

                if (obtainRemainsPrefab == null)
                {
                    obtainRemainsPrefab = settings.GetObtainRemainsPrefab();
                }

                if (bloodVisibleRange <= 0.01f)
                {
                    bloodVisibleRange = settings.enemyBloodVisibleRange;
                }
            }

            if (damageNumberPrefab == null)
            {
                damageNumberPrefab = Resources.Load<GameObject>("UI/WorldUI/DamageNumber");
            }

            if (enemyBloodPrefab == null)
            {
                enemyBloodPrefab = Resources.Load<GameObject>("UI/WorldUI/Enemy_blood");
            }

            if (obtainRemainsPrefab == null)
            {
                obtainRemainsPrefab = Resources.Load<GameObject>("UI/WorldUI/ObtainRemains");
            }
        }

        void EnsurePoolRoot()
        {
            if (_poolRoot != null && _overlayCanvas != null)
            {
                return;
            }

            var go = new GameObject("WorldUiOverlay");
            go.transform.SetParent(transform, false);
            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0)
            {
                go.layer = uiLayer;
            }

            _overlayCanvas = go.AddComponent<Canvas>();
            _overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _overlayCanvas.overrideSorting = true;
            _overlayCanvas.sortingOrder = 80;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _poolRoot = go.transform;
        }

        void ConfigureDefaultOcclusionMask()
        {
            if (occlusionMask.value != ~0 && occlusionMask.value != -1)
            {
                return;
            }

            int mask = ~0;
            int ignore = LayerMask.NameToLayer("Ignore Raycast");
            int ui = LayerMask.NameToLayer("UI");
            if (ignore >= 0)
            {
                mask &= ~(1 << ignore);
            }

            if (ui >= 0)
            {
                mask &= ~(1 << ui);
            }

            occlusionMask = mask;
        }

        public bool IsWorldPointOccluded(Vector3 worldPos, Transform ignoreA, Transform ignoreB = null)
        {
            return WorldUiScreen.IsOccluded(WorldCamera, worldPos, occlusionMask, ignoreA, ignoreB);
        }

        public void SpawnDamageNumber(float amount, Vector3 worldPosition, Transform ignoreRoot)
        {
            if (_damagePool == null)
            {
                Bootstrap();
            }

            _damagePool?.Spawn(amount, worldPosition, damageNumberLifetime, ignoreRoot);
        }

        public EnemyBloodHud AttachEnemyBlood(EnemyAgent agent)
        {
            if (agent == null)
            {
                return null;
            }

            ResolvePrefabs();
            if (enemyBloodPrefab == null)
            {
                Debug.LogWarning(
                    "[WorldUi] 缺少 Enemy_blood Prefab。请放到 Resources/UI/WorldUI/Enemy_blood，或在 CharacterRuntimeSettings 指定。",
                    this);
                return null;
            }

            EnsurePoolRoot();
            var existing = agent.GetComponent<EnemyBloodHudHost>();
            if (existing != null && existing.Hud != null)
            {
                existing.Hud.Bind(agent, this);
                return existing.Hud;
            }

            var go = Instantiate(enemyBloodPrefab, _poolRoot, false);
            go.name = $"EnemyBlood_{agent.GetInstanceID()}";
            WorldUiScreen.PrepareOverlayItem(go);

            var hud = go.GetComponent<EnemyBloodHud>();
            if (hud == null)
            {
                hud = go.AddComponent<EnemyBloodHud>();
            }

            hud.Bind(agent, this);

            var host = agent.gameObject.GetComponent<EnemyBloodHudHost>();
            if (host == null)
            {
                host = agent.gameObject.AddComponent<EnemyBloodHudHost>();
            }

            host.Hud = hud;
            return hud;
        }

        public ObtainRemainsHud AttachObtainRemains(EnemyAgent agent)
        {
            if (agent == null)
            {
                return null;
            }

            ResolvePrefabs();
            if (obtainRemainsPrefab == null)
            {
                Debug.LogWarning(
                    "[WorldUi] 缺少 ObtainRemains Prefab。请放到 Resources/UI/WorldUI/ObtainRemains，或在 CharacterRuntimeSettings 指定。",
                    this);
                return null;
            }

            EnsurePoolRoot();
            var existing = agent.GetComponent<ObtainRemainsHudHost>();
            if (existing != null && existing.Hud != null)
            {
                existing.Hud.Bind(agent, this);
                return existing.Hud;
            }

            var go = Instantiate(obtainRemainsPrefab, _poolRoot, false);
            go.name = $"ObtainRemains_{agent.GetInstanceID()}";
            WorldUiScreen.PrepareOverlayItem(go);

            var hud = go.GetComponent<ObtainRemainsHud>();
            if (hud == null)
            {
                hud = go.AddComponent<ObtainRemainsHud>();
            }

            hud.Bind(agent, this);

            var host = agent.gameObject.GetComponent<ObtainRemainsHudHost>();
            if (host == null)
            {
                host = agent.gameObject.AddComponent<ObtainRemainsHudHost>();
            }

            host.Hud = hud;
            return hud;
        }
    }

    public sealed class EnemyBloodHudHost : MonoBehaviour
    {
        public EnemyBloodHud Hud;

        void OnDestroy()
        {
            if (Hud != null)
            {
                Destroy(Hud.gameObject);
                Hud = null;
            }
        }
    }

    public sealed class ObtainRemainsHudHost : MonoBehaviour
    {
        public ObtainRemainsHud Hud;

        void OnDestroy()
        {
            if (Hud != null)
            {
                Destroy(Hud.gameObject);
                Hud = null;
            }
        }
    }

    static class HitResolverBridge
    {
        static WorldUiService _svc;
        static bool _bound;

        public static void Bind(WorldUiService svc)
        {
            _svc = svc;
            if (_bound)
            {
                return;
            }

            AttackSkill.Combat.HitResolver.Applied += OnHitApplied;
            _bound = true;
        }

        public static void Unbind()
        {
            if (!_bound)
            {
                return;
            }

            AttackSkill.Combat.HitResolver.Applied -= OnHitApplied;
            _bound = false;
            _svc = null;
        }

        static void OnHitApplied(AttackSkill.Combat.DamageInfo info, AttackSkill.Combat.IDamageable target)
        {
            if (_svc == null || info.Amount <= 0.01f)
            {
                return;
            }

            if (!(target is Component c))
            {
                return;
            }

            var agent = c.GetComponentInParent<EnemyAgent>();
            if (agent == null)
            {
                return;
            }

            Transform enemyRoot = agent.transform;
            Vector3 pos = info.HitPoint;
            if (pos.sqrMagnitude < 0.0001f)
            {
                pos = enemyRoot.position + Vector3.up * 1.2f;
            }
            else
            {
                pos += Vector3.up * 0.25f;
            }

            _svc.SpawnDamageNumber(info.Amount, pos, enemyRoot);
        }
    }
}
