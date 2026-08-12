using System;
using UnityEngine;
using AttackSkill.Combat;

namespace AttackSkill.Enemy
{
    /// <summary>野外敌人运行时入口。</summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(Health))]
    public class EnemyAgent : MonoBehaviour
    {
        [SerializeField] EnemyDefinition definitionOverride;
        [SerializeField] Animator animator;
        [SerializeField] Transform sensorOrigin;
        [SerializeField] bool drawDebug = false;

        CharacterController _controller;
        Health _health;
        EnemyHitbox _hitbox;
        EnemyDefinition _def;
        EnemyMotor _motor;
        EnemySensor _sensor;
        EnemyAggro _aggro;
        EnemyCombat _combat;
        EnemyBrain _brain;
        EnemyDeathGoldVisual _deathGold;
        EnemyDeathDissolveVisual _deathDissolve;
        EnemyDeathDirector _deathDirector;
        bool _hibernating;
        bool _deadNotified;

        public EnemyDefinition Definition => _def;
        public EnemyMotor Motor => _motor;
        public EnemySensor Sensor => _sensor;
        public EnemyAggro Aggro => _aggro;
        public EnemyCombat Combat => _combat;
        public EnemyBrain Brain => _brain;
        public Health Health => _health;
        public CharacterController Controller => _controller;
        public EnemyDeathDirector DeathDirector => _deathDirector;
        public EnemyDeathOutcome DeathOutcome =>
            _deathDirector != null ? _deathDirector.LastOutcome : EnemyDeathOutcome.Echo;
        public Vector3 HomePosition { get; private set; }
        public Quaternion HomeRotation { get; private set; }
        public EnemySpawnPoint OwnerPoint { get; private set; }
        public bool IsHibernating => _hibernating;
        public bool IsDead => _health != null && !_health.IsAlive;

        /// <summary>本帧感知结果（供 Brain 复用，避免重复 Raycast）。</summary>
        public bool PerceivedPlayerThisFrame { get; private set; }
        public Transform PerceivedPlayer { get; private set; }

        /// <summary>警戒/追击/交战或仍有仇恨时，刷怪组不得强制休眠。</summary>
        public bool IsInCombat
        {
            get
            {
                if (IsDead || _hibernating || _brain == null)
                {
                    return false;
                }

                var cur = _brain.Current;
                if (cur == _brain.Alert || cur == _brain.Chase || cur == _brain.Combat)
                {
                    return true;
                }

                return _aggro != null && _aggro.HasTarget;
            }
        }

        public event Action<EnemyAgent> Died;

        void Awake()
        {
            int enemyLayer = CombatLayers.EnemyLayer;
            if (enemyLayer >= 0)
            {
                CombatLayers.ApplyLayerRecursively(gameObject, enemyLayer);
            }

            _controller = GetComponent<CharacterController>();
            _health = GetComponent<Health>();
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            if (sensorOrigin == null)
            {
                sensorOrigin = transform;
            }

            EnsureHitbox();
            EnsureHurtboxCollider();
            EnsureAttackHitRelay();
            EnsureDeathVisuals();
            _motor = new EnemyMotor(_controller, transform);
            _sensor = new EnemySensor(sensorOrigin, transform);
            _aggro = new EnemyAggro();
            _combat = new EnemyCombat(this, _hitbox);
            _brain = new EnemyBrain(this);

            _health.Died += OnHealthDied;
        }

        void OnDestroy()
        {
            if (_health != null)
            {
                _health.Died -= OnHealthDied;
            }
        }

        public void Initialize(EnemyDefinition def, Vector3 home, Quaternion homeRot, EnemySpawnPoint owner)
        {
            _def = def != null ? def : definitionOverride;
            OwnerPoint = owner;
            HomePosition = home;
            HomeRotation = homeRot;
            _deadNotified = false;
            _deathDirector?.ResetForReuse();

            if (_def == null)
            {
                Debug.LogError("[EnemyAgent] 缺少 EnemyDefinition。", this);
                enabled = false;
                return;
            }

            _health.Configure(_def.maxHp, destroyWhenDead: false);
            _motor.Configure(_def.moveSpeed, _def.turnSpeed);
            _sensor.Configure(_def);
            _aggro.Configure(_def.loseTargetTime);
            _combat.Configure(_def);
            _motor.Teleport(home, homeRot);

            SetAnimBool("IsDead", false);
            SetAnimBool("InCombat", false);
            SetAnimBool("IsAttacking", false);
            SetAnimFloat("EnemySpeed", 0f);

            if (_controller != null)
            {
                _controller.enabled = true;
            }

            _hibernating = false;
            enabled = true;
            _brain.Start();

            AttackSkill.UI.World.WorldUiService.EnsureExists()?.AttachEnemyBlood(this);
        }

        public void Hibernate()
        {
            _hibernating = true;
            _combat.Interrupt();
            _aggro.Clear();
            _motor.Stop();
            if (_controller != null)
            {
                _controller.enabled = false;
            }

            enabled = false;
        }

        public void Wake()
        {
            if (IsDead)
            {
                return;
            }

            _hibernating = false;
            enabled = true;
            if (_controller != null)
            {
                _controller.enabled = true;
            }

            if (_brain.Current == null)
            {
                _brain.Start();
            }
        }

        void Update()
        {
            if (_hibernating || _def == null || IsDead)
            {
                return;
            }

            Transform player = PlayerTargetLocator.GetActivePlayerTransform();
            PerceivedPlayerThisFrame = false;
            PerceivedPlayer = null;
            if (player != null)
            {
                PerceivedPlayerThisFrame = _sensor.TryPerceive(player, out _);
                if (PerceivedPlayerThisFrame)
                {
                    PerceivedPlayer = player;
                    if (!_aggro.HasTarget)
                    {
                        _aggro.SetTarget(player);
                    }
                }
            }

            _aggro.Tick(Time.deltaTime, PerceivedPlayerThisFrame);
            _brain.Tick(Time.deltaTime);
        }

        void OnHealthDied()
        {
            if (_deadNotified)
            {
                return;
            }

            _deadNotified = true;
            _combat.Interrupt();
            _aggro.Clear();
            _brain.SetState(_brain.Dead);
            _deathDirector?.Begin();
            Died?.Invoke(this);
        }

        public void SetAnimFloat(string name, float value)
        {
            if (animator != null)
            {
                animator.SetFloat(name, value);
            }
        }

        public void SetAnimBool(string name, bool value)
        {
            if (animator != null)
            {
                animator.SetBool(name, value);
            }
        }

        public void SetAnimTrigger(string name)
        {
            if (animator != null)
            {
                animator.SetTrigger(name);
            }
        }

        public void CrossFadeAnim(string stateName, float fixedTransitionDuration = 0.1f)
        {
            if (animator != null && !string.IsNullOrEmpty(stateName))
            {
                animator.CrossFadeInFixedTime(stateName, fixedTransitionDuration);
            }
        }

        void EnsureHitbox()
        {
            _hitbox = GetComponentInChildren<EnemyHitbox>(true);
            if (_hitbox != null)
            {
                return;
            }

            var go = new GameObject("EnemyHitbox");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 1f, 0.9f);
            var col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 1f;
            _hitbox = go.AddComponent<EnemyHitbox>();
            go.SetActive(false);
        }

        /// <summary>
        /// OverlapSphere 对纯 CharacterController 经常扫不到；补一个 Trigger Capsule 作受击盒。
        /// </summary>
        void EnsureHurtboxCollider()
        {
            var existing = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < existing.Length; i++)
            {
                Collider c = existing[i];
                if (c == null || c is CharacterController)
                {
                    continue;
                }

                // 跳过进攻用 Hitbox、未激活物体上的碰撞
                if (!c.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (c.GetComponent<EnemyHitbox>() != null ||
                    c.GetComponentInParent<EnemyHitbox>() != null)
                {
                    continue;
                }

                if (c.enabled)
                {
                    return;
                }
            }

            if (_controller == null)
            {
                _controller = GetComponent<CharacterController>();
            }

            var go = new GameObject("EnemyHurtbox");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.layer = gameObject.layer;

            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.isTrigger = true;
            if (_controller != null)
            {
                capsule.height = Mathf.Max(0.5f, _controller.height);
                capsule.radius = Mathf.Max(0.2f, _controller.radius);
                capsule.center = _controller.center;
            }
            else
            {
                capsule.height = 1.8f;
                capsule.radius = 0.4f;
                capsule.center = new Vector3(0f, 0.9f, 0f);
            }
        }

        void EnsureAttackHitRelay()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            GameObject host = animator != null ? animator.gameObject : gameObject;
            var relay = host.GetComponent<EnemyAttackHitRelay>();
            if (relay == null)
            {
                relay = host.AddComponent<EnemyAttackHitRelay>();
            }

            if (_def != null && _def.skillHitProfile != null)
            {
                relay.SetSkillHitProfile(_def.skillHitProfile);
            }
        }

        void EnsureDeathVisuals()
        {
            _deathGold = GetComponent<EnemyDeathGoldVisual>();
            if (_deathGold == null)
            {
                _deathGold = gameObject.AddComponent<EnemyDeathGoldVisual>();
            }

            _deathDissolve = GetComponent<EnemyDeathDissolveVisual>();
            if (_deathDissolve == null)
            {
                _deathDissolve = gameObject.AddComponent<EnemyDeathDissolveVisual>();
            }

            _deathDirector = GetComponent<EnemyDeathDirector>();
            if (_deathDirector == null)
            {
                _deathDirector = gameObject.AddComponent<EnemyDeathDirector>();
            }

            _deathDirector.Bind(this, _deathGold, _deathDissolve);
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (!drawDebug)
            {
                return;
            }

            EnemyDefinition def = _def != null ? _def : definitionOverride;
            if (def == null)
            {
                return;
            }

            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, def.sightRange);
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, def.attackRange);
        }

        void OnGUI()
        {
            if (!drawDebug || !Application.isPlaying || _brain == null || _hibernating)
            {
                return;
            }

            Vector3 screen = Camera.main != null ? Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 2f) : Vector3.zero;
            if (screen.z < 0f)
            {
                return;
            }

            GUI.Label(new Rect(screen.x - 40f, Screen.height - screen.y, 160f, 20f), _brain.CurrentName);
        }
#endif
    }
}
