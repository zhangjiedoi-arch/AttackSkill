using AttackSkill.Character;
using AttackSkill.Character.HSM;
using AttackSkill.Combat;
using AttackSkill.Core;
using UnityEngine;

namespace AttackSkill.Rouge
{
    /// <summary>肉鸽环绕武器：火之刃 / 风之刃可同时存在，跟随 Active 切人。</summary>
    [DefaultExecutionOrder(250)]
    public sealed class RougeOrbitWeaponDriver : MonoBehaviour
    {
        public const string FireBladeId = "fire_blade";
        public const string WindBladeId = "wind_blade";

        public static RougeOrbitWeaponDriver Instance { get; private set; }

        OrbitingBlade _fire;
        OrbitingBlade _wind;
        PartyController _boundParty;
        GenshinLikeCharacter _boundOwner;

        public static RougeOrbitWeaponDriver Ensure()
        {
            if (Instance != null)
            {
                return Instance;
            }

            var go = new GameObject(nameof(RougeOrbitWeaponDriver));
            return go.AddComponent<RougeOrbitWeaponDriver>();
        }

        /// <summary>切人后立刻改挂到新 Active 的 R_Hit_Root。</summary>
        public static void BindToActiveImmediate()
        {
            if (Instance == null)
            {
                return;
            }

            Instance.EnsurePartyBound();
            Instance.BindToActive(snap: true);
        }

        /// <summary>销毁角色前先从该角色上摘下环绕刃，避免跟着被 Destroy。</summary>
        public static void DetachFromCharacter(GenshinLikeCharacter character)
        {
            if (Instance == null || character == null)
            {
                return;
            }

            Instance.DetachIfChildOf(character.transform);
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
            EnsurePartyBound();
            SyncBlades();
            BindToActive(snap: true);
        }

        void OnDisable()
        {
            PartyRougeProgress.Changed -= OnProgressChanged;
            UnbindParty();
        }

        void OnDestroy()
        {
            PartyRougeProgress.Changed -= OnProgressChanged;
            UnbindParty();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        void LateUpdate()
        {
            EnsurePartyBound();
            var owner = ResolveOwner();
            if (owner != _boundOwner)
            {
                BindToActive(snap: true);
                return;
            }

            TickBlades(owner, ResolvePivot(owner), Time.deltaTime);
        }

        void OnProgressChanged()
        {
            SyncBlades();
            BindToActive(snap: true);
        }

        void OnActiveChanged(int _)
        {
            BindToActive(snap: true);
        }

        void EnsurePartyBound()
        {
            var party = PartyController.Instance;
            if (party == _boundParty)
            {
                return;
            }

            UnbindParty();
            _boundParty = party;
            if (_boundParty != null)
            {
                _boundParty.ActiveChanged += OnActiveChanged;
            }
        }

        void UnbindParty()
        {
            if (_boundParty != null)
            {
                _boundParty.ActiveChanged -= OnActiveChanged;
                _boundParty = null;
            }
        }

        void BindToActive(bool snap)
        {
            SyncBlades();
            var owner = ResolveOwner();
            Transform pivot = ResolvePivot(owner);
            _boundOwner = owner;
            TickBlades(owner, pivot, snap ? 0f : Time.deltaTime);
        }

        void TickBlades(GenshinLikeCharacter owner, Transform pivot, float deltaTime)
        {
            TickOne(_fire, owner, pivot, deltaTime);
            TickOne(_wind, owner, pivot, deltaTime);
        }

        void TickOne(OrbitingBlade blade, GenshinLikeCharacter owner, Transform pivot, float deltaTime)
        {
            if (blade == null)
            {
                return;
            }

            AttachToPivot(blade, pivot);
            if (owner != null && blade.isActiveAndEnabled)
            {
                blade.Tick(owner, pivot, deltaTime);
            }
        }

        void SyncBlades()
        {
            int fireStack = PartyRougeProgress.GetStack(FireBladeId);
            int windStack = PartyRougeProgress.GetStack(WindBladeId);
            float fireStart = 0f;
            float windStart = 0f;
            if (fireStack > 0 && windStack > 0)
            {
                if (_fire != null)
                {
                    windStart = _fire.Angle + 180f;
                }
                else if (_wind != null)
                {
                    fireStart = _wind.Angle + 180f;
                }
                else
                {
                    windStart = 180f;
                }
            }

            SyncOne(ref _fire, fireStack, CombatElement.Fire, fireStart, GetFirePrefab());
            SyncOne(ref _wind, windStack, CombatElement.Wind, windStart, GetWindPrefab());
        }

        void SyncOne(
            ref OrbitingBlade blade,
            int stack,
            CombatElement element,
            float startAngle,
            GameObject prefab)
        {
            if (stack <= 0)
            {
                if (blade != null)
                {
                    Destroy(blade.gameObject);
                    blade = null;
                }

                return;
            }

            if (blade == null)
            {
                if (prefab == null)
                {
                    Debug.LogWarning(
                        $"[RougeOrbit] 缺少 {element} 环绕武器 Prefab。请在 CharacterRuntimeSettings 指定 Prefabs/Weapon。",
                        this);
                    return;
                }

                var go = Instantiate(prefab, transform);
                go.name = prefab.name;
                blade = go.GetComponent<OrbitingBlade>();
                if (blade == null)
                {
                    blade = go.AddComponent<OrbitingBlade>();
                }

                blade.Configure(element, startAngle, stack);
            }
            else
            {
                blade.SetStack(stack);
            }

            if (!blade.gameObject.activeSelf)
            {
                blade.gameObject.SetActive(true);
            }
        }

        void DetachIfChildOf(Transform characterRoot)
        {
            DetachOne(_fire, characterRoot);
            DetachOne(_wind, characterRoot);
            if (_boundOwner != null && _boundOwner.transform == characterRoot)
            {
                _boundOwner = null;
            }
        }

        void DetachOne(OrbitingBlade blade, Transform characterRoot)
        {
            if (blade == null || characterRoot == null)
            {
                return;
            }

            if (blade.transform.IsChildOf(characterRoot))
            {
                blade.transform.SetParent(transform, worldPositionStays: true);
            }
        }

        static GenshinLikeCharacter ResolveOwner()
        {
            var party = PartyController.Instance;
            return party != null ? party.Active : null;
        }

        Transform ResolvePivot(GenshinLikeCharacter owner)
        {
            if (owner == null)
            {
                return transform;
            }

            Transform socket = HitSocketResolver.Resolve(owner.transform, HitSocketId.R_Hit_Root);
            return socket != null ? socket : owner.transform;
        }

        static void AttachToPivot(OrbitingBlade blade, Transform pivot)
        {
            if (blade == null || pivot == null || blade.transform.parent == pivot)
            {
                return;
            }

            blade.transform.SetParent(pivot, worldPositionStays: true);
            if (!blade.gameObject.activeSelf)
            {
                blade.gameObject.SetActive(true);
            }
        }

        static GameObject GetFirePrefab()
        {
            var settings = CharacterRuntimeSettings.Get();
            return settings != null ? settings.GetFireOrbitBladePrefab() : null;
        }

        static GameObject GetWindPrefab()
        {
            var settings = CharacterRuntimeSettings.Get();
            return settings != null ? settings.GetWindOrbitBladePrefab() : null;
        }
    }
}
