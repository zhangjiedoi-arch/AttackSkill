using System;
using UnityEngine;
using UnityEngine.Events;

namespace AttackSkill.Combat
{
    /// <summary>
    /// 简易生命组件：伤害、持续击退、可选无敌帧/硬直。
    /// </summary>
    public class Health : MonoBehaviour, IDamageable
    {
        [SerializeField] float maxHp = 100f;
        [SerializeField] float currentHp = 100f;
        [SerializeField] bool destroyOnDeath;
        [SerializeField] float destroyDelay = 1f;
        [SerializeField] UnityEvent onDamaged;
        [SerializeField] UnityEvent onDeath;

        [Header("Knockback")]
        [Tooltip("击退初速度倍率（Knockback 数值 × 本值）")]
        [SerializeField] float knockbackSpeedScale = 4.5f;
        [SerializeField] float knockbackMaxSpeed = 14f;
        [SerializeField] float knockbackDamping = 10f;

        [Header("Defense")]
        [SerializeField] bool useIFrames;
        [SerializeField] float iFrameDuration = 0.5f;
        [SerializeField] bool useHitStun;
        [SerializeField] float hitStunDuration = 0.18f;

        CharacterController _controller;
        Rigidbody _rigidbody;
        Vector3 _knockVelocity;
        float _iFrameUntil = -1f;
        float _stunUntil = -1f;

        public float MaxHp => maxHp;
        public float CurrentHp => currentHp;
        public bool IsAlive => currentHp > 0f;
        public bool IsInvulnerable => Time.time < _iFrameUntil;
        public bool IsHitStunned => useHitStun && Time.time < _stunUntil;
        public Vector3 KnockVelocity => _knockVelocity;

        public event Action Damaged;
        public event Action Died;
        /// <summary>当前/最大血量发生变化（受伤、读档设血、回满、Configure）。</summary>
        public event Action HpChanged;

        void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _rigidbody = GetComponent<Rigidbody>();
            currentHp = Mathf.Clamp(currentHp, 0f, maxHp);
            if (currentHp <= 0f)
            {
                currentHp = maxHp;
            }
        }

        public void Configure(float newMaxHp, bool destroyWhenDead = false)
        {
            maxHp = Mathf.Max(1f, newMaxHp);
            destroyOnDeath = destroyWhenDead;
            currentHp = maxHp;
            HpChanged?.Invoke();
        }

        public void ConfigureDefense(bool enableIFrames, float iFrames = 0.5f, float stun = 0.18f, bool enableHitStun = true)
        {
            useIFrames = enableIFrames;
            iFrameDuration = Mathf.Max(0f, iFrames);
            useHitStun = enableHitStun && stun > 0f;
            hitStunDuration = Mathf.Max(0f, stun);
        }

        /// <summary>临时无敌（二次心跳等）；不改 ConfigureDefense 配置。</summary>
        public void GrantIFrames(float duration)
        {
            if (duration <= 0f)
            {
                return;
            }

            _iFrameUntil = Mathf.Max(_iFrameUntil, Time.time + duration);
        }

        public void ReviveFull()
        {
            currentHp = maxHp;
            _iFrameUntil = -1f;
            _stunUntil = -1f;
            _knockVelocity = Vector3.zero;
            HpChanged?.Invoke();
        }

        /// <summary>治疗：增加当前生命，不超过 MaxHp；死亡中无效。</summary>
        public float Heal(float amount)
        {
            if (!IsAlive || amount <= 0f)
            {
                return 0f;
            }

            float before = currentHp;
            currentHp = Mathf.Min(maxHp, currentHp + amount);
            float gained = currentHp - before;
            if (gained > 0.0001f)
            {
                HpChanged?.Invoke();
            }

            return gained;
        }

        /// <summary>读档恢复当前血量（不会超过 MaxHp）。</summary>
        public void SetCurrentHp(float hp)
        {
            currentHp = Mathf.Clamp(hp, 0f, maxHp);
            _iFrameUntil = -1f;
            _stunUntil = -1f;
            _knockVelocity = Vector3.zero;
            HpChanged?.Invoke();
        }

        public void TakeDamage(DamageInfo info)
        {
            if (!IsAlive || IsInvulnerable)
            {
                return;
            }

            float nextHp = currentHp - info.Amount;
            currentHp = Mathf.Max(0f, nextHp);
            onDamaged?.Invoke();
            Damaged?.Invoke();
            HpChanged?.Invoke();

            if (info.Knockback > 0.01f)
            {
                ApplyKnockback(info.HitDirection, info.Knockback);
            }

            if (useIFrames && iFrameDuration > 0f)
            {
                _iFrameUntil = Time.time + iFrameDuration;
            }

            if (useHitStun && hitStunDuration > 0f)
            {
                _stunUntil = Time.time + hitStunDuration;
            }

            if (currentHp <= 0f)
            {
                _knockVelocity = Vector3.zero;
                onDeath?.Invoke();
                Died?.Invoke();
                if (destroyOnDeath)
                {
                    Destroy(gameObject, destroyDelay);
                }
            }
        }

        void ApplyKnockback(Vector3 hitDirection, float knockback)
        {
            Vector3 push = hitDirection;
            push.y = 0f;
            if (push.sqrMagnitude < 0.001f)
            {
                push = transform.forward;
            }

            push.Normalize();
            float speed = knockback * knockbackSpeedScale;
            _knockVelocity += push * speed;
            if (_knockVelocity.sqrMagnitude > knockbackMaxSpeed * knockbackMaxSpeed)
            {
                _knockVelocity = _knockVelocity.normalized * knockbackMaxSpeed;
            }

            // 有 CharacterController 时只走速度积分，避免与 RB 双重推动
            if (_controller != null)
            {
                return;
            }

            if (_rigidbody != null && !_rigidbody.isKinematic)
            {
                _rigidbody.AddForce(push * speed, ForceMode.VelocityChange);
            }
        }

        void FixedUpdate()
        {
            if (!IsAlive || _knockVelocity.sqrMagnitude < 0.0001f)
            {
                if (!IsAlive)
                {
                    _knockVelocity = Vector3.zero;
                }

                return;
            }

            float dt = Time.fixedDeltaTime;
            Vector3 delta = _knockVelocity * dt;

            if (_controller != null && _controller.enabled)
            {
                _controller.Move(delta);
            }
            else if (_rigidbody == null || _rigidbody.isKinematic)
            {
                transform.position += delta;
            }
            // 动态 RB：依赖 AddForce，不再改 transform

            _knockVelocity = Vector3.Lerp(_knockVelocity, Vector3.zero, knockbackDamping * dt);
            if (_knockVelocity.sqrMagnitude < 0.01f)
            {
                _knockVelocity = Vector3.zero;
            }
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position + Vector3.up, 0.35f);
        }
#endif
    }
}
