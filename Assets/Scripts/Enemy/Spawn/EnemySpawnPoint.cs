using System;
using UnityEngine;

namespace AttackSkill.Enemy
{
    /// <summary>单点刷怪：生成、死亡、刷新倒计时。</summary>
    public class EnemySpawnPoint : MonoBehaviour
    {
        [SerializeField] EnemyDefinition definition;
        [SerializeField] bool spawnOnGroupActivate = true;

        EnemyAgent _alive;
        float _respawnAt = -1f;
        SpawnGroupDefinition _groupDef;
        bool _waitingRespawn;

        public EnemyDefinition Definition => definition;
        public bool HasAlive => _alive != null;
        public bool IsWaitingRespawn => _waitingRespawn;
        public bool IsAliveInCombat => _alive != null && !_alive.IsDead && _alive.IsInCombat;

        public event Action<EnemyAgent> Spawned;
        public event Action<EnemyAgent> Died;

        public void Configure(EnemyDefinition def, SpawnGroupDefinition groupDef)
        {
            if (def != null)
            {
                definition = def;
            }

            _groupDef = groupDef;
        }

        public void Tick(float deltaTime, Vector3 playerPos, bool groupActive)
        {
            if (!groupActive)
            {
                return;
            }

            if (_alive != null)
            {
                return;
            }

            if (!_waitingRespawn)
            {
                if (spawnOnGroupActivate)
                {
                    SpawnNow();
                }

                return;
            }

            if (_respawnAt < 0f || Time.time < _respawnAt)
            {
                return;
            }

            if (_groupDef != null && _groupDef.respawnOnlyWhenPlayerAway)
            {
                float away = _groupDef.activateRadius * 0.75f;
                if (Vector3.Distance(playerPos, transform.position) < away)
                {
                    return;
                }
            }

            SpawnNow();
        }

        public EnemyAgent SpawnNow()
        {
            if (definition == null)
            {
                Debug.LogWarning("[EnemySpawnPoint] 未指定 EnemyDefinition。", this);
                return null;
            }

            if (definition.prefab == null)
            {
                Debug.LogWarning($"[EnemySpawnPoint] {definition.name} 未指定 prefab。", this);
                return null;
            }

            if (_alive != null)
            {
                return _alive;
            }

            var go = Instantiate(definition.prefab, transform.position, transform.rotation);
            go.name = $"{definition.name}_{name}";
            var agent = go.GetComponent<EnemyAgent>();
            if (agent == null)
            {
                agent = go.AddComponent<EnemyAgent>();
            }

            // 确保有 Health / CC
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

            agent.Died += OnAgentDied;
            agent.Initialize(definition, transform.position, transform.rotation, this);
            _alive = agent;
            _waitingRespawn = false;
            _respawnAt = -1f;
            Spawned?.Invoke(agent);
            return agent;
        }

        public void DespawnAlive()
        {
            if (_alive == null)
            {
                return;
            }

            _alive.Died -= OnAgentDied;
            Destroy(_alive.gameObject);
            _alive = null;
        }

        public void HibernateAlive()
        {
            if (_alive == null || _alive.IsDead || _alive.IsInCombat)
            {
                return;
            }

            _alive.Hibernate();
        }

        public void WakeAlive()
        {
            if (_alive != null && !_alive.IsDead)
            {
                _alive.Wake();
            }
        }

        void OnAgentDied(EnemyAgent agent)
        {
            if (agent != _alive)
            {
                return;
            }

            agent.Died -= OnAgentDied;
            Died?.Invoke(agent);
            _alive = null;
            _waitingRespawn = true;
            float delay = _groupDef != null ? _groupDef.respawnDelay : 20f;
            _respawnAt = Time.time + Mathf.Max(1f, delay);
            // 尸体销毁由 EnemyDeathDirector 负责：声骸 echoCorpseLifetime，飘散结束立刻 Destroy
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position, 0.35f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.8f);
        }
#endif
    }
}
