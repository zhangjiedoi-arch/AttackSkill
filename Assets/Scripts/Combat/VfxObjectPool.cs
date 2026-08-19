using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AttackSkill.Combat
{
    /// <summary>
    /// 轻量特效对象池：按 Prefab 复用。池根挂在当前场景（非 DDOL），切场景随场景销毁。
    /// 场景卸载 / OnDisable 路径禁止新建根节点，改 Destroy，避免 IsActive 断言与残留 GO。
    /// </summary>
    public static class VfxObjectPool
    {
        const string RootName = "[VfxObjectPool]";

        static Transform _root;
        static readonly Dictionary<int, Stack<VfxPoolMember>> Pools = new Dictionary<int, Stack<VfxPoolMember>>(64);
        static readonly List<VfxPoolMember> DeferredHierarchy = new List<VfxPoolMember>(32);
        static VfxPoolPump _pump;
        static bool _quitting;
        static bool _hooks;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _root = null;
            _pump = null;
            _quitting = false;
            Pools.Clear();
            DeferredHierarchy.Clear();
            EnsureHooks();
        }

        static void EnsureHooks()
        {
            if (_hooks)
            {
                return;
            }

            _hooks = true;
            Application.quitting += OnQuitting;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        static void OnQuitting()
        {
            _quitting = true;
            TearDownRoot(destroyObjects: true);
        }

        static void OnSceneUnloaded(Scene _)
        {
            // 池根若在被卸场景内，引用已失效；勿在卸载回调里 new GO
            _root = null;
            _pump = null;
            Pools.Clear();
            DeferredHierarchy.Clear();
        }

        static void TearDownRoot(bool destroyObjects)
        {
            if (destroyObjects && _root)
            {
                Object.Destroy(_root.gameObject);
            }

            _root = null;
            _pump = null;
            Pools.Clear();
            DeferredHierarchy.Clear();
        }

        static bool CanUsePool => Application.isPlaying && !_quitting;

        /// <summary>允许新建池根：仅正常玩法帧，不在退出/卸载中。</summary>
        static bool CanCreateRoot => CanUsePool;

        public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            EnsureHooks();
            if (prefab == null || !CanUsePool)
            {
                return null;
            }

            FlushDeferredHierarchy();

            int key = prefab.GetInstanceID();
            if (!Pools.TryGetValue(key, out var stack))
            {
                stack = new Stack<VfxPoolMember>(8);
                Pools[key] = stack;
            }

            VfxPoolMember member = null;
            while (stack.Count > 0 && member == null)
            {
                member = stack.Pop();
                if (member == null || !member)
                {
                    member = null;
                    continue;
                }

                if (member.gameObject == null)
                {
                    member = null;
                }
            }

            GameObject go;
            if (member == null)
            {
                go = Object.Instantiate(prefab);
                member = go.GetComponent<VfxPoolMember>();
                if (member == null)
                {
                    member = go.AddComponent<VfxPoolMember>();
                }

                member.BindPrefabKey(key);
            }
            else
            {
                go = member.gameObject;
            }

            Transform t = go.transform;
            if (parent != null)
            {
                t.SetParent(parent, false);
            }
            else
            {
                t.SetParent(null, false);
            }

            t.SetPositionAndRotation(position, rotation);
            t.localScale = Vector3.one;

            member.MarkPooled(false);
            if (!go.activeSelf)
            {
                go.SetActive(true);
            }

            RestartParticles(go);
            return go;
        }

        public static void Despawn(GameObject instance, float lifetime)
        {
            if (instance == null)
            {
                return;
            }

            var member = instance.GetComponent<VfxPoolMember>();
            if (member == null)
            {
                Object.Destroy(instance, Mathf.Max(0f, lifetime));
                return;
            }

            member.ScheduleReturn(Mathf.Max(0.01f, lifetime));
        }

        public static void RecycleNow(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            var member = instance.GetComponent<VfxPoolMember>();
            if (member == null)
            {
                Object.Destroy(instance);
                return;
            }

            member.RecycleNow();
        }

        public static void Prewarm(GameObject prefab, int count)
        {
            EnsureHooks();
            if (prefab == null || count <= 0 || !CanUsePool)
            {
                return;
            }

            FlushDeferredHierarchy();

            int key = prefab.GetInstanceID();
            if (!Pools.TryGetValue(key, out var stack))
            {
                stack = new Stack<VfxPoolMember>(count);
                Pools[key] = stack;
            }

            Transform root = GetOrCreateRoot();
            if (root == null)
            {
                return;
            }

            for (int i = stack.Count; i < count; i++)
            {
                GameObject go = Object.Instantiate(prefab, root);
                go.SetActive(false);
                var member = go.GetComponent<VfxPoolMember>();
                if (member == null)
                {
                    member = go.AddComponent<VfxPoolMember>();
                }

                member.BindPrefabKey(key);
                member.MarkPooled(true);
                stack.Push(member);
            }
        }

        internal static void Return(VfxPoolMember member)
        {
            ReturnInternal(member, hierarchyUnsafe: false);
        }

        /// <summary>
        /// 由 OnDisable / 卸载路径调用：禁止 SetActive/SetParent/新建池根。
        /// </summary>
        public static void ReturnFromDisable(VfxPoolMember member)
        {
            ReturnInternal(member, hierarchyUnsafe: true);
        }

        /// <summary>卸载/禁用中无法安全入池时直接销毁。</summary>
        internal static void Abandon(VfxPoolMember member)
        {
            if (member == null || !member)
            {
                return;
            }

            member.MarkPooled(true);
            GameObject go = member.gameObject;
            if (go != null)
            {
                Object.Destroy(go);
            }
        }

        static void ReturnInternal(VfxPoolMember member, bool hierarchyUnsafe)
        {
            EnsureHooks();
            if (member == null || !member)
            {
                return;
            }

            GameObject go = member.gameObject;
            if (go == null)
            {
                return;
            }

            if (!CanUsePool)
            {
                member.MarkPooled(true);
                Object.Destroy(go);
                return;
            }

            int key = member.PrefabKey;
            if (key == 0)
            {
                Object.Destroy(go);
                return;
            }

            if (!Pools.TryGetValue(key, out var stack))
            {
                stack = new Stack<VfxPoolMember>(8);
                Pools[key] = stack;
            }

            member.MarkPooled(true);
            StopParticles(go);

            // 禁用/卸载：禁止 SetActive / SetParent / 新建池根
            if (hierarchyUnsafe)
            {
                if (!Contains(stack, member))
                {
                    stack.Push(member);
                }

                Transform existingRoot = TryGetRoot();
                if (existingRoot != null)
                {
                    if (!DeferredHierarchy.Contains(member))
                    {
                        DeferredHierarchy.Add(member);
                    }

                    EnsurePump(existingRoot);
                }
                // 无根则留在原地（已 inactive），下次 Spawn/Prewarm 再 Flush；勿在此 GetOrCreateRoot
                return;
            }

            Transform root = GetOrCreateRoot();
            if (root == null)
            {
                Object.Destroy(go);
                return;
            }

            if (go.activeSelf)
            {
                go.SetActive(false);
            }

            if (go.transform.parent != root)
            {
                go.transform.SetParent(root, false);
            }

            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            if (!Contains(stack, member))
            {
                stack.Push(member);
            }
        }

        internal static void FlushDeferredHierarchy()
        {
            if (DeferredHierarchy.Count == 0 || !CanUsePool)
            {
                DeferredHierarchy.Clear();
                return;
            }

            Transform root = GetOrCreateRoot();
            if (root == null)
            {
                return;
            }

            for (int i = 0; i < DeferredHierarchy.Count; i++)
            {
                VfxPoolMember member = DeferredHierarchy[i];
                if (member == null || !member)
                {
                    continue;
                }

                GameObject go = member.gameObject;
                if (go == null)
                {
                    continue;
                }

                if (go.activeSelf)
                {
                    go.SetActive(false);
                }

                if (go.transform.parent != root)
                {
                    go.transform.SetParent(root, false);
                }

                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
            }

            DeferredHierarchy.Clear();
        }

        static bool Contains(Stack<VfxPoolMember> stack, VfxPoolMember member)
        {
            foreach (var m in stack)
            {
                if (ReferenceEquals(m, member))
                {
                    return true;
                }
            }

            return false;
        }

        static void RestartParticles(GameObject go)
        {
            var systems = go.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
            {
                var ps = systems[i];
                if (ps == null)
                {
                    continue;
                }

                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Play(true);
            }
        }

        static void StopParticles(GameObject go)
        {
            var systems = go.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
            {
                var ps = systems[i];
                if (ps == null)
                {
                    continue;
                }

                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        static Transform TryGetRoot()
        {
            return _root ? _root : null;
        }

        static Transform GetOrCreateRoot()
        {
            if (_root)
            {
                return _root;
            }

            if (!CanCreateRoot)
            {
                return null;
            }

            // 卸载中 activeScene 可能已不可用，此时新建会触发 IsActive 断言与残留警告
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return null;
            }

            // 场景内根：随场景销毁，避免退出 Play 残留 DDOL
            var go = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(go, scene);
            _root = go.transform;
            return _root;
        }

        static void EnsurePump(Transform root)
        {
            if (_pump || root == null)
            {
                return;
            }

            _pump = root.GetComponent<VfxPoolPump>();
            if (_pump == null)
            {
                _pump = root.gameObject.AddComponent<VfxPoolPump>();
            }
        }

        sealed class VfxPoolPump : MonoBehaviour
        {
            void LateUpdate()
            {
                FlushDeferredHierarchy();
            }

            void OnDestroy()
            {
                if (_pump == this)
                {
                    _pump = null;
                }

                if (_root != null && _root.gameObject == gameObject)
                {
                    _root = null;
                }
            }
        }
    }

    [DisallowMultipleComponent]
    public sealed class VfxPoolMember : MonoBehaviour
    {
        int _prefabKey;
        bool _pooled;
        bool _pending;
        float _returnAt = -1f;
        bool _returning;

        public int PrefabKey => _prefabKey;

        public void BindPrefabKey(int key)
        {
            _prefabKey = key;
        }

        public void MarkPooled(bool pooled)
        {
            _pooled = pooled;
            if (pooled)
            {
                _pending = false;
                _returnAt = -1f;
            }
        }

        public void ScheduleReturn(float lifeSeconds)
        {
            if (lifeSeconds <= 0f)
            {
                return;
            }

            _pending = true;
            _returnAt = Time.time + lifeSeconds;
        }

        public void RecycleNow()
        {
            if (_pooled || _returning)
            {
                return;
            }

            _pending = false;
            _returnAt = -1f;
            _returning = true;
            try
            {
                // 父级 OnDisable / 卸载中：禁止同步改层级与新建池根
                bool hierarchyUnsafe = !isActiveAndEnabled || !gameObject.activeInHierarchy;
                if (hierarchyUnsafe)
                {
                    VfxObjectPool.ReturnFromDisable(this);
                    return;
                }

                VfxObjectPool.Return(this);
                if (this && gameObject.activeSelf)
                {
                    gameObject.SetActive(false);
                }
            }
            finally
            {
                _returning = false;
            }
        }

        void Update()
        {
            if (!_pending || _pooled || _returning)
            {
                return;
            }

            if (Time.time < _returnAt)
            {
                return;
            }

            RecycleNow();
        }

        void OnDisable()
        {
            if (_returning || _pooled)
            {
                return;
            }

            if (_pending)
            {
                _pending = false;
                _returnAt = -1f;
                _returning = true;
                try
                {
                    VfxObjectPool.ReturnFromDisable(this);
                }
                finally
                {
                    _returning = false;
                }
            }
        }

        void OnDestroy()
        {
            _pending = false;
            _returnAt = -1f;
        }
    }
}
