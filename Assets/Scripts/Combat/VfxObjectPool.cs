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
            EnsurePool(key, prefab, 0);

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

        public static void Prewarm(GameObject prefab, int count)
        {
            if (prefab == null || count <= 0)
            {
                return;
            }

            EnsurePool(prefab.GetInstanceID(), prefab, count);
        }

        internal static void Return(VfxPoolMember member)
        {
            if (member == null)
            {
                return;
            }

            GameObject go = member.gameObject;
            StopParticles(go);
            go.SetActive(false);

            Transform root = GetRoot();
            go.transform.SetParent(root, false);

            int key = member.PrefabKey;
            if (!Pools.TryGetValue(key, out Stack<GameObject> stack))
            {
                stack = new Stack<GameObject>(8);
                Pools[key] = stack;
            }

            stack.Push(go);
        }

        static void EnsurePool(int key, GameObject prefab, int extraCount)
        {
            if (!Pools.TryGetValue(key, out Stack<GameObject> stack))
            {
                stack = new Stack<GameObject>(8);
                Pools[key] = stack;
            }

            int need = Mathf.Max(0, extraCount);
            if (stack.Count == 0 && need == 0)
            {
                need = DefaultPrewarm;
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

        float _returnAt = -1f;
        bool _pending;

        public void Bind(int prefabKey, GameObject _)
        {
            PrefabKey = prefabKey;
        }

        public void ScheduleReturn(float lifetime)
        {
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
            _pending = false;
            _returnAt = -1f;
        }
    }
}
