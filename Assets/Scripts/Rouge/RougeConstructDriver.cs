using AttackSkill.Character;
using AttackSkill.Character.HSM;
using AttackSkill.Combat;
using AttackSkill.Core;
using UnityEngine;

namespace AttackSkill.Rouge
{
    /// <summary>肉鸽生成物：冰之哀伤 / 火之高兴 / 雪之哀霜 / 诱敌之树。落点在角色周围，不跟身。</summary>
    [DefaultExecutionOrder(260)]
    public sealed class RougeConstructDriver : MonoBehaviour
    {
        public const string IceSorrowId = "ice_sorrow";
        public const string FireJoyId = "fire_joy";
        public const string SnowFrostId = "snow_frost";
        public const string DecoyTreeId = "decoy_tree";

        public static RougeConstructDriver Instance { get; private set; }

        RougeAuraZone _ice;
        RougeAuraZone _fire;
        RougeAuraZone _snow;
        RougeDecoyTree _tree;

        public static RougeConstructDriver Ensure()
        {
            if (Instance != null)
            {
                return Instance;
            }

            var go = new GameObject(nameof(RougeConstructDriver));
            return go.AddComponent<RougeConstructDriver>();
        }

        public static bool HasAnyConstruct()
        {
            return PartyRougeProgress.GetStack(IceSorrowId) > 0 ||
                   PartyRougeProgress.GetStack(FireJoyId) > 0 ||
                   PartyRougeProgress.GetStack(SnowFrostId) > 0 ||
                   PartyRougeProgress.GetStack(DecoyTreeId) > 0;
        }

        void Awake()
        {
            if (!SceneSingleton.ShouldKeep(this, Instance))
            {
                return;
            }

            Instance = this;
        }

        void OnEnable()
        {
            PartyRougeProgress.Changed -= OnProgressChanged;
            PartyRougeProgress.Changed += OnProgressChanged;
            SyncAll();
        }

        void OnDisable()
        {
            PartyRougeProgress.Changed -= OnProgressChanged;
        }

        void OnDestroy()
        {
            PartyRougeProgress.Changed -= OnProgressChanged;
            if (Instance == this)
            {
                Instance = null;
            }
        }

        void LateUpdate()
        {
            var owner = ResolveOwner();
            TickAura(_ice, owner);
            TickAura(_fire, owner);
            TickAura(_snow, owner);
            if (_tree != null)
            {
                _tree.TickWorld(owner);
            }
        }

        void OnProgressChanged()
        {
            SyncAll();
        }

        public void SyncAll()
        {
            SyncAura(ref _ice, IceSorrowId, CombatElement.Ice, GetIcePrefab());
            SyncAura(ref _fire, FireJoyId, CombatElement.Fire, GetFirePrefab());
            SyncAura(ref _snow, SnowFrostId, CombatElement.Dark, GetSnowPrefab());
            SyncTree();
        }

        public void DespawnAll()
        {
            DestroyAura(ref _ice);
            DestroyAura(ref _fire);
            DestroyAura(ref _snow);
            if (_tree != null)
            {
                Destroy(_tree.gameObject);
                _tree = null;
            }
        }

        void SyncAura(ref RougeAuraZone zone, string id, CombatElement element, GameObject prefab)
        {
            int stack = PartyRougeProgress.GetStack(id);
            if (stack <= 0)
            {
                DestroyAura(ref zone);
                return;
            }

            if (zone == null)
            {
                if (prefab == null)
                {
                    Debug.LogWarning($"[RougeConstruct] 缺少 {id} Prefab。请在 CharacterRuntimeSettings 指定 Prefabs/Weapon。", this);
                    return;
                }

                var go = Instantiate(prefab, transform);
                go.name = prefab.name;
                zone = go.GetComponent<RougeAuraZone>();
                if (zone == null)
                {
                    zone = go.AddComponent<RougeAuraZone>();
                }

                zone.Configure(element, stack);
                zone.RelocateNear(ResolveOwner());
            }
            else
            {
                zone.SetStack(stack);
            }

            if (!zone.gameObject.activeSelf)
            {
                zone.gameObject.SetActive(true);
            }
        }

        void SyncTree()
        {
            int stack = PartyRougeProgress.GetStack(DecoyTreeId);
            if (stack <= 0)
            {
                if (_tree != null)
                {
                    Destroy(_tree.gameObject);
                    _tree = null;
                }

                return;
            }

            if (_tree == null)
            {
                GameObject prefab = GetTreePrefab();
                if (prefab == null)
                {
                    Debug.LogWarning("[RougeConstruct] 缺少 诱敌之树 Prefab。", this);
                    return;
                }

                var go = Instantiate(prefab, transform);
                go.name = prefab.name;
                _tree = go.GetComponent<RougeDecoyTree>();
                if (_tree == null)
                {
                    _tree = go.AddComponent<RougeDecoyTree>();
                }

                _tree.Configure(stack);
                _tree.RelocateNear(ResolveOwner());
                return;
            }

            _tree.Configure(stack);
            if (!_tree.gameObject.activeSelf)
            {
                _tree.gameObject.SetActive(true);
            }
        }

        static void TickAura(RougeAuraZone zone, GenshinLikeCharacter owner)
        {
            if (zone != null && zone.isActiveAndEnabled)
            {
                zone.TickWorld(owner);
            }
        }

        static void DestroyAura(ref RougeAuraZone zone)
        {
            if (zone != null)
            {
                Destroy(zone.gameObject);
                zone = null;
            }
        }

        static GenshinLikeCharacter ResolveOwner()
        {
            var party = PartyController.Instance;
            return party != null ? party.Active : null;
        }

        static GameObject GetIcePrefab()
        {
            var settings = CharacterRuntimeSettings.Get();
            return settings != null ? settings.GetIceSorrowPrefab() : null;
        }

        static GameObject GetFirePrefab()
        {
            var settings = CharacterRuntimeSettings.Get();
            return settings != null ? settings.GetFireJoyPrefab() : null;
        }

        static GameObject GetSnowPrefab()
        {
            var settings = CharacterRuntimeSettings.Get();
            return settings != null ? settings.GetSnowFrostPrefab() : null;
        }

        static GameObject GetTreePrefab()
        {
            var settings = CharacterRuntimeSettings.Get();
            return settings != null ? settings.GetDecoyTreePrefab() : null;
        }
    }
}
