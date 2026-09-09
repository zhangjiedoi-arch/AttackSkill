using System.Collections.Generic;
using AttackSkill.CameraSystem;
using AttackSkill.Character;
using AttackSkill.Character.HSM;
using AttackSkill.Core;
using AttackSkill.Enemy;
using AttackSkill.Game;
using UnityEngine;
using UnityEngine.UI;

namespace AttackSkill.UI
{
    /// <summary>
    /// 鸣潮式小地图：烘焙图随玩家平移，玩家钉在圆心，图标按 ΔXZ；
    /// 海滩 / 肉鸽两套 <see cref="MapBakeData"/>。
    /// </summary>
    [DefaultExecutionOrder(400)]
    public partial class UISmallMapUvPanel : UIBase
    {
        [SerializeField] MapBakeData beachBake;
        [SerializeField] MapBakeData rougeBake;
        [SerializeField] GameObject playerIconPrefab;
        [SerializeField] GameObject enemyIconPrefab;
        [SerializeField] float visibleRadiusMeters = 50f;

        RectTransform _mapRt;
        SmallMapIconView _playerIcon;
        MapBakeData _activeBake;
        Sprite _appliedSprite;
        float _ppm = 4f;
        PartyPortraitId _playerPortrait = PartyPortraitId.Unknown;

        readonly Dictionary<int, SmallMapIconView> _enemyIcons = new Dictionary<int, SmallMapIconView>(32);
        readonly Stack<SmallMapIconView> _enemyPool = new Stack<SmallMapIconView>(16);
        readonly List<int> _staleKeys = new List<int>(16);
        readonly HashSet<int> _visibleKeys = new HashSet<int>();

        public override void OnOpen(object args)
        {
            EnsureBound();
            EnsureBakes();
            EnsurePrefabs();
            EnsurePlayerIcon();
            PrewarmEnemyIcons(8);
        }

        public override void OnClose()
        {
            RecycleAllEnemies();
        }

        void OnDisable()
        {
            RecycleAllEnemies();
        }

        void LateUpdate()
        {
            if (!IsOpen)
            {
                return;
            }

            EnsureBound();
            if (textureMap == null || imgBg == null)
            {
                return;
            }

            var party = PartyController.Instance;
            GenshinLikeCharacter player = party != null ? party.Active : null;
            if (player == null)
            {
                if (_playerIcon != null)
                {
                    _playerIcon.gameObject.SetActive(false);
                }

                return;
            }

            MapBakeData bake = ResolveBake(player.transform.position);
            if (bake != _activeBake)
            {
                RecycleAllEnemies();
                ApplyBake(bake);
            }

            if (_activeBake == null)
            {
                return;
            }

            ScrollMap(player.transform.position);
            UpdatePlayerIcon(party, player);
            UpdateEnemyIcons(player.transform.position);
        }

        void EnsureBound()
        {
            if (textureMap == null)
            {
                Transform t = FindNamed(transform, "textureMap");
                if (t != null)
                {
                    textureMap = t.GetComponent<Image>();
                }
            }

            if (imgBg == null)
            {
                Transform t = FindNamed(transform, "imgBg");
                if (t != null)
                {
                    imgBg = t as RectTransform;
                }
            }

            if (textureMap != null)
            {
                _mapRt = textureMap.rectTransform;
                textureMap.raycastTarget = false;
                textureMap.preserveAspect = false;
            }
        }

        void EnsureBakes()
        {
            if (beachBake == null)
            {
                beachBake = LoadBake("SmallMap/MapBakeData_Beach", "Assets/Resources/SmallMap/MapBakeData_Beach.asset");
            }

            if (rougeBake == null)
            {
                rougeBake = LoadBake("SmallMap/MapBakeData_Rouge", "Assets/Resources/SmallMap/MapBakeData_Rouge.asset");
                if (rougeBake == null)
                {
                    rougeBake = LoadBake("SmallMap/MapBakeData", "Assets/Resources/SmallMap/MapBakeData.asset");
                }
            }
        }

        static MapBakeData LoadBake(string resourcesPath, string assetPath)
        {
#if UNITY_EDITOR
            var fromAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<MapBakeData>(assetPath);
            if (fromAsset != null)
            {
                return fromAsset;
            }
#endif
            return Resources.Load<MapBakeData>(resourcesPath);
        }

        void EnsurePrefabs()
        {
            if (playerIconPrefab == null)
            {
                playerIconPrefab = LoadUiPrefab("PlayerMapIcon");
            }

            if (enemyIconPrefab == null)
            {
                enemyIconPrefab = LoadUiPrefab("EnemyMapIcon");
            }
        }

        static GameObject LoadUiPrefab(string name)
        {
            var fromRes = Resources.Load<GameObject>("UI/SmallMap/" + name);
            if (fromRes != null)
            {
                return fromRes;
            }

#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                $"Assets/Prefabs/UI/SmallMap/{name}.prefab");
#else
            return null;
#endif
        }

        MapBakeData ResolveBake(Vector3 playerPos)
        {
            bool rouge = IsRougeMap(playerPos);
            if (rouge)
            {
                return rougeBake != null ? rougeBake : beachBake;
            }

            return beachBake != null ? beachBake : rougeBake;
        }

        static bool IsRougeMap(Vector3 playerPos)
        {
            var progress = GameProgressController.Instance;
            RunPhase phase = progress != null ? progress.Phase : RunPhase.BeachExplore;
            if (phase == RunPhase.RougeCombat)
            {
                return true;
            }

            if (phase == RunPhase.BeachExplore || phase == RunPhase.Booting)
            {
                return false;
            }

            var flow = RouGeLikeFlowController.Instance;
            if (flow != null && flow.HasTeleported)
            {
                return true;
            }

            return playerPos.x >= 500f;
        }

        Sprite ResolveBakeSprite(MapBakeData bake)
        {
            if (bake != null)
            {
                Sprite fromBake = bake.ResolveSprite();
                if (fromBake != null)
                {
                    return fromBake;
                }
            }

            bool rouge = bake != null && rougeBake != null && bake == rougeBake;
            return LoadPngSprite(rouge);
        }

        static Sprite LoadPngSprite(bool rouge)
        {
#if UNITY_EDITOR
            string path = rouge
                ? "Assets/Sprites/SmallMap/MapBakeData_Rouge.png"
                : "Assets/Sprites/SmallMap/MapBakeData_Beach.png";
            Sprite sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
            {
                return sprite;
            }

            Object[] all = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
            if (all != null)
            {
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] is Sprite sp)
                    {
                        return sp;
                    }
                }
            }
#endif
            return null;
        }

        void ApplyBake(MapBakeData bake)
        {
            _activeBake = bake;
            if (textureMap == null || imgBg == null)
            {
                return;
            }

            textureMap.enabled = true;
            Sprite sp = ResolveBakeSprite(bake);
            if (sp != null)
            {
                _appliedSprite = sp;
                textureMap.sprite = sp;
                textureMap.SetAllDirty();
            }

            float diameter = Mathf.Max(32f, imgBg.rect.width);
            float radius = Mathf.Max(8f, visibleRadiusMeters);
            _ppm = diameter / (radius * 2f);

            if (_mapRt != null && bake != null)
            {
                _mapRt.anchorMin = new Vector2(0.5f, 0.5f);
                _mapRt.anchorMax = new Vector2(0.5f, 0.5f);
                _mapRt.pivot = new Vector2(0.5f, 0.5f);
                _mapRt.sizeDelta = new Vector2(
                    Mathf.Max(1f, bake.extentX * _ppm),
                    Mathf.Max(1f, bake.extentZ * _ppm));
            }
        }

        void ScrollMap(Vector3 playerPos)
        {
            if (_mapRt == null || _activeBake == null)
            {
                return;
            }

            float dx = playerPos.x - _activeBake.origin.x;
            float dz = playerPos.z - _activeBake.origin.z;
            if (_activeBake.flipY)
            {
                dz = -dz;
            }

            _mapRt.anchoredPosition = new Vector2(-dx * _ppm, -dz * _ppm);
        }

        void EnsurePlayerIcon()
        {
            if (_playerIcon != null || playerIconPrefab == null || imgBg == null)
            {
                return;
            }

            var go = Instantiate(playerIconPrefab, imgBg, false);
            go.name = "PlayerMapIcon";
            _playerIcon = go.GetComponent<SmallMapIconView>() ?? go.AddComponent<SmallMapIconView>();
            _playerIcon.EnsureBound();
            go.SetActive(true);
            go.transform.SetAsLastSibling();
        }

        void UpdatePlayerIcon(PartyController party, GenshinLikeCharacter player)
        {
            EnsurePlayerIcon();
            if (_playerIcon == null)
            {
                return;
            }

            if (!_playerIcon.gameObject.activeSelf)
            {
                _playerIcon.gameObject.SetActive(true);
            }

            PartyPortraitId portrait = party.GetPortraitId(party.ActiveIndex);
            if (portrait != _playerPortrait || !_playerIcon.HasIcon)
            {
                Sprite icon = SmallMapIconCatalog.ForPlayer(portrait);
                _playerIcon.BindPlayer(icon);
                if (icon != null && _playerIcon.HasIcon)
                {
                    _playerPortrait = portrait;
                }
            }

            _playerIcon.Place(Vector2.zero, -ResolveViewYaw());
            _playerIcon.transform.SetAsLastSibling();
        }

        static float ResolveViewYaw()
        {
            var orbit = GameServices.ResolveCamera();
            if (orbit != null && orbit.YawTransform != null)
            {
                return orbit.YawTransform.eulerAngles.y;
            }

            Camera cam = Camera.main;
            return cam != null ? cam.transform.eulerAngles.y : 0f;
        }

        void UpdateEnemyIcons(Vector3 playerPos)
        {
            if (enemyIconPrefab == null || imgBg == null)
            {
                return;
            }

            float radiusPx = imgBg.rect.width * 0.5f - 8f;
            float radiusSq = Mathf.Max(16f, radiusPx * radiusPx);

            _visibleKeys.Clear();
            IReadOnlyList<EnemyAgent> agents = EnemyAgentRegistry.Live;
            for (int i = 0; i < agents.Count; i++)
            {
                EnemyAgent agent = agents[i];
                if (agent == null || !agent.gameObject.activeInHierarchy || agent.IsDead)
                {
                    continue;
                }

                Vector2 pos = WorldDeltaToMap(playerPos, agent.transform.position);
                if (pos.sqrMagnitude > radiusSq)
                {
                    continue;
                }

                int key = agent.GetInstanceID();
                _visibleKeys.Add(key);
                if (!_enemyIcons.TryGetValue(key, out SmallMapIconView view) || view == null)
                {
                    view = RentEnemyIcon();
                    _enemyIcons[key] = view;
                    view.BindEnemy(agent, SmallMapIconCatalog.ForEnemy(agent.Definition));
                }

                if (!view.gameObject.activeSelf)
                {
                    view.gameObject.SetActive(true);
                }

                view.Place(pos, -agent.transform.eulerAngles.y);
            }

            _staleKeys.Clear();
            foreach (var kv in _enemyIcons)
            {
                if (!_visibleKeys.Contains(kv.Key))
                {
                    _staleKeys.Add(kv.Key);
                }
            }

            for (int i = 0; i < _staleKeys.Count; i++)
            {
                int key = _staleKeys[i];
                if (_enemyIcons.TryGetValue(key, out SmallMapIconView view))
                {
                    ReturnEnemyIcon(view);
                }

                _enemyIcons.Remove(key);
            }
        }

        Vector2 WorldDeltaToMap(Vector3 playerPos, Vector3 world)
        {
            float dx = world.x - playerPos.x;
            float dz = world.z - playerPos.z;
            if (_activeBake != null && _activeBake.flipY)
            {
                dz = -dz;
            }

            return new Vector2(dx * _ppm, dz * _ppm);
        }

        void PrewarmEnemyIcons(int count)
        {
            if (enemyIconPrefab == null || imgBg == null)
            {
                return;
            }

            int need = count - _enemyPool.Count;
            for (int i = 0; i < need; i++)
            {
                var go = Instantiate(enemyIconPrefab, imgBg, false);
                go.name = "EnemyMapIcon";
                var view = go.GetComponent<SmallMapIconView>() ?? go.AddComponent<SmallMapIconView>();
                view.EnsureBound();
                view.Recycle();
                _enemyPool.Push(view);
            }
        }

        SmallMapIconView RentEnemyIcon()
        {
            SmallMapIconView view = null;
            while (_enemyPool.Count > 0 && view == null)
            {
                view = _enemyPool.Pop();
            }

            if (view == null)
            {
                var go = Instantiate(enemyIconPrefab, imgBg, false);
                go.name = "EnemyMapIcon";
                view = go.GetComponent<SmallMapIconView>() ?? go.AddComponent<SmallMapIconView>();
                view.EnsureBound();
            }

            view.transform.SetParent(imgBg, false);
            view.gameObject.SetActive(true);
            return view;
        }

        void ReturnEnemyIcon(SmallMapIconView view)
        {
            if (view == null)
            {
                return;
            }

            view.Recycle();
            _enemyPool.Push(view);
        }

        void RecycleAllEnemies()
        {
            foreach (var kv in _enemyIcons)
            {
                ReturnEnemyIcon(kv.Value);
            }

            _enemyIcons.Clear();
            _visibleKeys.Clear();
        }

        static Transform FindNamed(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            var all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == name)
                {
                    return all[i];
                }
            }

            return null;
        }
    }
}
