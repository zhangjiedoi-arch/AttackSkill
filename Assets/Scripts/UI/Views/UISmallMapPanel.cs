using System.Collections.Generic;
using AttackSkill.CameraSystem;
using AttackSkill.Character;
using AttackSkill.Character.HSM;
using AttackSkill.Enemy;
using UnityEngine;
using UnityEngine.UI;

namespace AttackSkill.UI
{
    /// <summary>
    /// 左下角俯视小地图：<c>textureMap</c> 显示 SmallMapCamera RT；
    /// 视野内敌人出 <c>EnemyMapIcon</c>（对象池），当前玩家出 <c>PlayerMapIcon</c>。
    /// </summary>
    [DefaultExecutionOrder(400)]
    public partial class UISmallMapPanel : UIBase
    {
        [SerializeField] GameObject playerIconPrefab;
        [SerializeField] GameObject enemyIconPrefab;

        SmallMapIconView _playerIcon;
        Camera _mapCamera;
        readonly Dictionary<int, SmallMapIconView> _enemyIcons = new Dictionary<int, SmallMapIconView>(32);
        readonly Stack<SmallMapIconView> _enemyPool = new Stack<SmallMapIconView>(16);
        readonly List<int> _staleKeys = new List<int>(16);
        readonly HashSet<int> _visibleKeys = new HashSet<int>();

        PartyPortraitId _playerPortrait = PartyPortraitId.Unknown;

        public override void OnOpen(object args)
        {
            EnsureBound();
            EnsurePrefabs();
            EnsurePlayerIcon();
            CacheMapCamera();
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
            if (textureMap == null)
            {
                return;
            }

            CacheMapCamera();
            if (_mapCamera == null)
            {
                return;
            }

            UpdatePlayerIcon();
            UpdateEnemyIcons();
        }

        void EnsureBound()
        {
            if (textureMap == null)
            {
                Transform t = FindNamed(transform, "textureMap");
                if (t != null)
                {
                    textureMap = t.GetComponent<RawImage>();
                }
            }
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

        void CacheMapCamera()
        {
            if (_mapCamera != null)
            {
                return;
            }

            var follow = FindObjectOfType<SmallMapCameraFollow>();
            if (follow != null)
            {
                _mapCamera = follow.GetComponent<Camera>();
            }
        }

        void PrewarmEnemyIcons(int count)
        {
            if (enemyIconPrefab == null || textureMap == null)
            {
                return;
            }

            int need = count - _enemyPool.Count;
            for (int i = 0; i < need; i++)
            {
                var go = Instantiate(enemyIconPrefab, textureMap.transform, false);
                go.name = "EnemyMapIcon";
                var view = go.GetComponent<SmallMapIconView>() ?? go.AddComponent<SmallMapIconView>();
                view.EnsureBound();
                view.Recycle();
                _enemyPool.Push(view);
            }
        }

        void EnsurePlayerIcon()
        {
            if (_playerIcon != null || playerIconPrefab == null || textureMap == null)
            {
                return;
            }

            var go = Instantiate(playerIconPrefab, textureMap.transform, false);
            go.name = "PlayerMapIcon";
            _playerIcon = go.GetComponent<SmallMapIconView>() ?? go.AddComponent<SmallMapIconView>();
            _playerIcon.EnsureBound();
            go.SetActive(true);
            go.transform.SetAsLastSibling();
        }

        void UpdatePlayerIcon()
        {
            EnsurePlayerIcon();
            if (_playerIcon == null)
            {
                return;
            }

            var party = PartyController.Instance;
            GenshinLikeCharacter player = party != null ? party.Active : null;
            if (player == null)
            {
                _playerIcon.gameObject.SetActive(false);
                return;
            }

            if (!_playerIcon.gameObject.activeSelf)
            {
                _playerIcon.gameObject.SetActive(true);
            }

            PartyPortraitId portrait = party.GetPortraitId(party.ActiveIndex);
            if (portrait != _playerPortrait)
            {
                _playerPortrait = portrait;
                _playerIcon.BindPlayer(SmallMapIconCatalog.ForPlayer(portrait));
            }

            if (!TryWorldToMap(player.transform.position, out Vector2 pos))
            {
                pos = Vector2.zero;
            }

            _playerIcon.Place(pos, FacingZ(player.transform));
            _playerIcon.transform.SetAsLastSibling();
        }

        void UpdateEnemyIcons()
        {
            if (enemyIconPrefab == null || textureMap == null)
            {
                return;
            }

            _visibleKeys.Clear();
            IReadOnlyList<EnemyAgent> agents = EnemyAgentRegistry.Live;
            for (int i = 0; i < agents.Count; i++)
            {
                EnemyAgent agent = agents[i];
                if (agent == null || !agent.gameObject.activeInHierarchy || agent.IsDead)
                {
                    continue;
                }

                if (!TryWorldToMap(agent.transform.position, out Vector2 pos))
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

                view.Place(pos, FacingZ(agent.transform));
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

        SmallMapIconView RentEnemyIcon()
        {
            SmallMapIconView view = null;
            while (_enemyPool.Count > 0 && view == null)
            {
                view = _enemyPool.Pop();
                if (view == null)
                {
                    view = null;
                }
            }

            if (view == null)
            {
                var go = Instantiate(enemyIconPrefab, textureMap.transform, false);
                go.name = "EnemyMapIcon";
                view = go.GetComponent<SmallMapIconView>() ?? go.AddComponent<SmallMapIconView>();
                view.EnsureBound();
            }

            view.transform.SetParent(textureMap.transform, false);
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

        bool TryWorldToMap(Vector3 world, out Vector2 anchored)
        {
            anchored = default;
            if (_mapCamera == null || textureMap == null)
            {
                return false;
            }

            Vector3 vp = _mapCamera.WorldToViewportPoint(world);
            if (vp.z < 0f || vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f)
            {
                return false;
            }

            Rect r = textureMap.rectTransform.rect;
            anchored = new Vector2((vp.x - 0.5f) * r.width, (vp.y - 0.5f) * r.height);
            return true;
        }

        float FacingZ(Transform t)
        {
            if (t == null || _mapCamera == null)
            {
                return 0f;
            }

            float camYaw = _mapCamera.transform.eulerAngles.y;
            return camYaw - t.eulerAngles.y;
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
