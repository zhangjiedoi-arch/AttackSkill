using UnityEngine;
using AttackSkill.Combat;

namespace AttackSkill.Enemy
{
    /// <summary>敌人近战判定盒：Trigger 检出候选，经 <see cref="HitResolver"/> 结算。</summary>
    [RequireComponent(typeof(SphereCollider))]
    public class EnemyHitbox : MonoBehaviour
    {
        [SerializeField] SphereCollider sphere;

        float _damage;
        float _knockback;
        float _expireAt = -1f;
        GameObject _attacker;
        readonly HitSession _session = new HitSession();
        readonly Collider[] _overlapBuffer = new Collider[24];

        void Awake()
        {
            if (sphere == null)
            {
                sphere = GetComponent<SphereCollider>();
            }

            sphere.isTrigger = true;
            gameObject.SetActive(false);
        }

        public void EnableHit(float damage, float knockback, float radius, float forwardOffset, float duration, GameObject attacker)
        {
            _damage = damage;
            _knockback = knockback;
            _attacker = attacker;
            _session.Begin();
            _expireAt = Time.time + Mathf.Max(0.02f, duration);

            transform.localPosition = new Vector3(0f, 1f, forwardOffset);
            sphere.radius = Mathf.Max(0.2f, radius);
            gameObject.SetActive(true);

            // 开启当帧立刻扫一次：已重叠时不一定立刻收到 OnTriggerEnter
            SweepOnce();
        }

        public void DisableHit()
        {
            gameObject.SetActive(false);
            _expireAt = -1f;
        }

        void Update()
        {
            if (_expireAt > 0f && Time.time >= _expireAt)
            {
                DisableHit();
            }
        }

        void OnTriggerEnter(Collider other)
        {
            TryHit(other);
        }

        void OnTriggerStay(Collider other)
        {
            TryHit(other);
        }

        void SweepOnce()
        {
            if (sphere == null)
            {
                return;
            }

            Vector3 center = transform.TransformPoint(sphere.center);
            float radius = sphere.radius * Mathf.Max(
                transform.lossyScale.x,
                Mathf.Max(transform.lossyScale.y, transform.lossyScale.z));
            LayerMask mask = CombatLayers.DefaultPlayerHurtboxMask;
            int count = Physics.OverlapSphereNonAlloc(
                center,
                radius,
                _overlapBuffer,
                mask,
                QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                TryHit(_overlapBuffer[i]);
            }
        }

        void TryHit(Collider other)
        {
            if (!isActiveAndEnabled || other == null)
            {
                return;
            }

            IDamageable damageable = FanHitDetector.ResolveDamageable(other);
            if (damageable == null)
            {
                return;
            }

            Vector3 dir = other.transform.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f)
            {
                dir = transform.forward;
            }

            dir.Normalize();
            Vector3 point = other.ClosestPoint(transform.position);
            var info = new DamageInfo(_damage, point, dir, _knockback, 0, _attacker);

            HitResolver.TryApply(HitRequest.Create(
                info,
                damageable,
                other,
                HitResolver.DefaultEnemyOffense,
                _session,
                ownerRoot: _attacker != null ? _attacker.transform : transform));
        }
    }
}
