using System.Collections.Generic;
using UnityEngine;

namespace AttackSkill.Enemy
{
    /// <summary>肉鸽敌人对象池：按 Prefab 分桶复用，避免频繁 Instantiate/Destroy。</summary>
    public static class EnemyObjectPool
    {
        const string PoolRootName = "EnemyObjectPool";
        const int DefaultPrewarm = 16;

        static readonly Dictionary<int, Stack<GameObject>> Pools = new Dictionary<int, Stack<GameObject>>(16);
        static Transform _root;

        public static bool IsPooledInstance(GameObject go)
        {
            return go != null && go.GetComponent<EnemyPoolMember>() != null;
        }

        public static EnemyAgent Spawn(
            EnemyDefinition definition,
            Vector3 position,
            Quaternion rotation,
            Transform parent = null)
        {
            if (definition == null || definition.prefab == null)
            {
                return null;
            }

            GameObject prefab = definition.prefab;
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
                EnsureRuntimeComponents(go);
                var member = go.GetComponent<EnemyPoolMember>();
                if (member == null)
                {
                    member = go.AddComponent<EnemyPoolMember>();
                }

                member.Bind(key, prefab);
            }

            var poolMember = go.GetComponent<EnemyPoolMember>();
            poolMember?.MarkInPool(false);

            go.name = $"{definition.name}_Rouge";
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            if (!go.activeSelf)
            {
                go.SetActive(true);
            }

            var agent = go.GetComponent<EnemyAgent>();
            if (agent == null)
            {
                agent = go.AddComponent<EnemyAgent>();
            }

            agent.Initialize(definition, position, rotation, owner: null);
            return agent;
        }

        public static void Prewarm(EnemyDefinition definition, int count)
        {
            if (definition == null || definition.prefab == null || count <= 0)
            {
                return;
            }

            EnsurePool(definition.prefab.GetInstanceID(), definition.prefab, minIdleCount: count);
        }

        public static bool TryRelease(GameObject go)
        {
            if (go == null)
            {
                return false;
            }

            var member = go.GetComponent<EnemyPoolMember>();
            if (member == null)
            {
                return false;
            }

            var agent = go.GetComponent<EnemyAgent>();
            agent?.PrepareForPool();

            member.MarkInPool(true);
            if (go.activeSelf)
            {
                go.SetActive(false);
            }

            go.transform.SetParent(GetRoot(), false);

            int key = member.PrefabKey;
            if (!Pools.TryGetValue(key, out Stack<GameObject> stack))
            {
                stack = new Stack<GameObject>(8);
                Pools[key] = stack;
            }

            stack.Push(go);
            return true;
        }

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
                EnsureRuntimeComponents(go);
                var member = go.GetComponent<EnemyPoolMember>();
                if (member == null)
                {
                    member = go.AddComponent<EnemyPoolMember>();
                }

                member.Bind(key, prefab);
                member.MarkInPool(true);
                stack.Push(go);
            }
        }

        static void EnsureRuntimeComponents(GameObject go)
        {
            if (go.GetComponent<CharacterController>() == null)
            {
                var cc = go.AddComponent<CharacterController>();
                cc.center = new Vector3(0f, 1f, 0f);
                cc.height = 2f;
                cc.radius = 0.4f;
            }

            if (go.GetComponent<Combat.Health>() == null)
            {
                go.AddComponent<Combat.Health>();
            }

            if (go.GetComponent<EnemyAgent>() == null)
            {
                go.AddComponent<EnemyAgent>();
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
    }
}
