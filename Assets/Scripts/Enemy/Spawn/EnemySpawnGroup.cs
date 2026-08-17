using System;
using UnityEngine;

namespace AttackSkill.Enemy
{
    /// <summary>刷怪组：距离激活/休眠，驱动子级 SpawnPoint。</summary>
    public class EnemySpawnGroup : MonoBehaviour
    {
        [SerializeField] SpawnGroupDefinition definition;
        [SerializeField] bool buildPointsFromDefinition = true;
        [SerializeField] bool drawGizmos = true;

        EnemySpawnPoint[] _points;
        bool _activated;
        int _deathCount;
        bool _initialWaveCleared;

        public bool IsActivated => _activated;
        public SpawnGroupDefinition Definition => definition;
        public int PointCount => _points != null ? _points.Length : 0;
        public bool InitialWaveCleared => _initialWaveCleared;

        /// <summary>初始波次全部死亡一次后触发（之后若关闭刷新则不再刷）。</summary>
        public event Action InitialWaveClearedEvent;

        void Awake()
        {
            _points = GetComponentsInChildren<EnemySpawnPoint>(true);
            if (buildPointsFromDefinition && definition != null && definition.slots != null &&
                (_points == null || _points.Length == 0))
            {
                BuildPointsFromDefinition();
                _points = GetComponentsInChildren<EnemySpawnPoint>(true);
            }

            if (_points == null)
            {
                return;
            }

            for (int i = 0; i < _points.Length; i++)
            {
                EnemyDefinition slotDef = null;
                if (definition != null && definition.slots != null && i < definition.slots.Length)
                {
                    slotDef = definition.slots[i].definition;
                }

                _points[i].Configure(slotDef, definition);
                _points[i].Died -= OnPointDied;
                _points[i].Died += OnPointDied;
            }
        }

        void OnDestroy()
        {
            if (_points == null)
            {
                return;
            }

            for (int i = 0; i < _points.Length; i++)
            {
                if (_points[i] != null)
                {
                    _points[i].Died -= OnPointDied;
                }
            }
        }

        void OnPointDied(EnemyAgent _)
        {
            if (_initialWaveCleared)
            {
                return;
            }

            _deathCount++;
            if (_deathCount < PointCount)
            {
                return;
            }

            _initialWaveCleared = true;
            DisableRespawn();
            InitialWaveClearedEvent?.Invoke();
        }

        public void DisableRespawn()
        {
            if (_points == null)
            {
                return;
            }

            for (int i = 0; i < _points.Length; i++)
            {
                if (_points[i] != null)
                {
                    _points[i].AllowRespawn = false;
                }
            }
        }

        public int CountAlive()
        {
            if (_points == null)
            {
                return 0;
            }

            int n = 0;
            for (int i = 0; i < _points.Length; i++)
            {
                if (_points[i] != null && _points[i].HasAlive)
                {
                    n++;
                }
            }

            return n;
        }

        public EnemyDefinition[] CollectDefinitions()
        {
            if (_points == null || _points.Length == 0)
            {
                return Array.Empty<EnemyDefinition>();
            }

            var list = new System.Collections.Generic.List<EnemyDefinition>(_points.Length);
            for (int i = 0; i < _points.Length; i++)
            {
                var def = _points[i] != null ? _points[i].Definition : null;
                if (def != null && !list.Contains(def))
                {
                    list.Add(def);
                }
            }

            return list.ToArray();
        }

        void Update()
        {
            Transform player = PlayerTargetLocator.GetActivePlayerTransform();
            Vector3 playerPos = player != null ? player.position : transform.position + Vector3.one * 9999f;
            float dist = Vector3.Distance(playerPos, transform.position);

            float activateR = definition != null ? definition.activateRadius : 35f;
            float hibernateR = definition != null ? definition.hibernateRadius : 50f;

            if (!_activated && dist <= activateR)
            {
                ActivateGroup();
            }
            else if (_activated && dist >= hibernateR && !AnyAliveInCombat())
            {
                HibernateGroup();
            }

            if (_points == null)
            {
                return;
            }

            for (int i = 0; i < _points.Length; i++)
            {
                _points[i].Tick(Time.deltaTime, playerPos, _activated);
            }
        }

        void ActivateGroup()
        {
            _activated = true;
            if (_points == null)
            {
                return;
            }

            for (int i = 0; i < _points.Length; i++)
            {
                if (_points[i].HasAlive)
                {
                    _points[i].WakeAlive();
                }
                else if (!_points[i].IsWaitingRespawn)
                {
                    _points[i].SpawnNow();
                }
            }
        }

        bool AnyAliveInCombat()
        {
            if (_points == null)
            {
                return false;
            }

            for (int i = 0; i < _points.Length; i++)
            {
                if (_points[i] != null && _points[i].IsAliveInCombat)
                {
                    return true;
                }
            }

            return false;
        }

        void HibernateGroup()
        {
            _activated = false;
            if (_points == null)
            {
                return;
            }

            for (int i = 0; i < _points.Length; i++)
            {
                _points[i].HibernateAlive();
            }
        }

        void BuildPointsFromDefinition()
        {
            var existing = GetComponentsInChildren<EnemySpawnPoint>(true);
            for (int i = 0; i < existing.Length; i++)
            {
                if (existing[i] != null && existing[i].gameObject != gameObject)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(existing[i].gameObject);
                    }
                    else
                    {
                        DestroyImmediate(existing[i].gameObject);
                    }
                }
            }

            for (int i = 0; i < definition.slots.Length; i++)
            {
                SpawnSlot slot = definition.slots[i];
                var pointGo = new GameObject($"SpawnPoint_{i}");
                pointGo.transform.SetParent(transform, false);
                pointGo.transform.localPosition = slot.localOffset;
                pointGo.transform.localRotation = Quaternion.Euler(slot.localEuler);
                var point = pointGo.AddComponent<EnemySpawnPoint>();
                point.Configure(slot.definition, definition);
            }
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (!drawGizmos)
            {
                return;
            }

            float a = definition != null ? definition.activateRadius : 35f;
            float h = definition != null ? definition.hibernateRadius : 50f;
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, a);
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, h);
        }
#endif
    }
}
