using System.Collections.Generic;
using UnityEngine;

namespace AttackSkill.Combat
{
    /// <summary>
    /// 特效对象池：按 Prefab 实例 ID 分桶，避免频繁 Instantiate/Destroy。
    /// </summary>
    public static class VfxObjectPool
    {
        const string PoolRootName = "VfxObjectPool";
        const int DefaultPrewarm = 2;

        static readonly Dictionary<int, Stack<GameObject>> Pools = new Dictionary<int, Stack<GameObject>>(16);
        static Transform _root;

        public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null)
            {
                return null;
            }

            int key = prefab.GetInstanceID();
            EnsurePool(key, prefab, minIdleCount: 0);

            GameObject go = null;
            var stack = Pools[key];
            while (stack.Count > 0)
            {
                go = stack.Pop();
                if (go != null)
                {
                    break;
                }

                go = null;
            }

            if (go == null)
            {
                go = Object.Instantiate(prefab);
                var member = go.GetComponent<VfxPoolMember>();
                if (member == null)
                {
                    member = go.AddComponent<VfxPoolMember>();
                }

                member.Bind(key, prefab);
            }

            go.transform.SetParent(null, false);
            go.transform.SetPositionAndRotation(position, rotation);
            go.SetActive(true);
            RestartParticles(go);
            return go;
        }

        /// <summary>延迟回收；lifetime&lt;=0 时下一帧回收。</summary>
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

        /// <summary>确保空闲池至少有 count 个；已有足够数量时不再新建（切人重复 Prewarm 安全）。</summary>
        public static void Prewarm(GameObject prefab, int count)
        {
            if (prefab == null || count <= 0)
            {
                return;
            }

            EnsurePool(prefab.GetInstanceID(), prefab, minIdleCount: count);
        }

        internal static void Return(VfxPoolMember member)
        {
            if (member == null)
            {
                return;
            }

            GameObject go = member.gameObject;
            if (go == null)
            {
                return;
            }

            // 已在池根下且未激活：视为已归还，避免 OnDisable/重复 Return 再 Push
            Transform root = GetRoot();
            if (!go.activeSelf && go.transform.parent == root && member.IsPooled)
            {
                return;
            }

            StopParticles(go);
            member.MarkPooled(true);
            if (go.activeSelf)
            {
                go.SetActive(false);
            }

            go.transform.SetParent(root, false);

            int key = member.PrefabKey;
            if (!Pools.TryGetValue(key, out Stack<GameObject> stack))
            {
                stack = new Stack<GameObject>(8);
                Pools[key] = stack;
            }

            stack.Push(go);
        }

        /// <param name="minIdleCount">空闲实例下限；0 表示仅在桶为空时补 DefaultPrewarm。</param>
        static void EnsurePool(int key, GameObject prefab, int minIdleCount)
        {
            if (!Pools.TryGetValue(key, out Stack<GameObject> stack))
            {
                stack = new Stack<GameObject>(8);
                Pools[key] = stack;
            }

            int target = minIdleCount;
            if (target <= 0 && stack.Count == 0)
            {
                target = DefaultPrewarm;
            }

            int need = target - stack.Count;
            if (need <= 0)
            {
                return;
            }

            Transform root = GetRoot();
            for (int i = 0; i < need; i++)
            {
                var go = Object.Instantiate(prefab, root, false);
                go.name = prefab.name + "_Pooled";
                go.SetActive(false);
                var member = go.GetComponent<VfxPoolMember>();
                if (member == null)
                {
                    member = go.AddComponent<VfxPoolMember>();
                }

                member.Bind(key, prefab);
                member.MarkPooled(true);
                stack.Push(go);
            }
        }

        static Transform GetRoot()
        {
            if (_root != null)
            {
                return _root;
            }

            var existing = GameObject.Find(PoolRootName);
            if (existing == null)
            {
                existing = new GameObject(PoolRootName);
                Object.DontDestroyOnLoad(existing);
            }

            _root = existing.transform;
            return _root;
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
    }

    /// <summary>池化实例标记；负责延迟归还。</summary>
    public sealed class VfxPoolMember : MonoBehaviour
    {
        public int PrefabKey { get; private set; }
        public bool IsPooled { get; private set; }

        float _returnAt = -1f;
        bool _pending;
        bool _returning;

        public void Bind(int prefabKey, GameObject _)
        {
            PrefabKey = prefabKey;
            IsPooled = false;
        }

        public void MarkPooled(bool pooled)
        {
            IsPooled = pooled;
        }

        public void ScheduleReturn(float lifetime)
        {
            IsPooled = false;
            _pending = true;
            _returnAt = Time.time + Mathf.Max(0.01f, lifetime);
            enabled = true;
        }

        void Update()
        {
            if (!_pending || Time.time < _returnAt)
            {
                return;
            }

            _pending = false;
            _returnAt = -1f;
            VfxObjectPool.Return(this);
        }

        void OnDisable()
        {
            // 延迟回收被提前失活时仍归还，避免游离实例 + 下次 Spawn 再新建导致池膨胀
            if (_pending && !_returning)
            {
                _pending = false;
                _returnAt = -1f;
                _returning = true;
                VfxObjectPool.Return(this);
                _returning = false;
                return;
            }

            _pending = false;
            _returnAt = -1f;
        }

        void OnEnable()
        {
            IsPooled = false;
        }
    }
}
