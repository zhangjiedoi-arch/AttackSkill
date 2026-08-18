using System.Collections.Generic;
using AttackSkill.Character.HSM;
using AttackSkill.Combat;
using UnityEngine;

namespace AttackSkill.Rouge
{
    /// <summary>环绕武器：跟 Active 玩家转圈，用 Prefab 上的 BoxCollider 出伤。</summary>
    public sealed class OrbitingBlade : MonoBehaviour
    {
        public const float ExtraDamagePerStack = 0.2f;
        public const float FireBaseSkillPercent = 200f;
        public const float WindBaseSkillPercent = 150f;
        public const float WindKnockback = 3.2f;

        const int OverlapBufferSize = 32;

        [SerializeField] float degreesPerSecond = 220f;
        [SerializeField] float hitCooldown = 0.45f;
        [SerializeField] float knockback;

        BoxCollider _box;
        CombatElement _element;
        float _baseSkillPercent = FireBaseSkillPercent;
        float _radius = 2f;
        float _height = 0.3f;
        float _angle;
        int _stack = 1;
        readonly Collider[] _overlapBuffer = new Collider[OverlapBufferSize];
        readonly Dictionary<int, float> _nextHitAt = new Dictionary<int, float>(16);

        public CombatElement Element => _element;
        public float Angle => _angle;

        public static float SkillPercent(int stack, float basePercent)
        {
            int s = Mathf.Max(1, stack);
            return Mathf.Max(1f, basePercent) * (1f + ExtraDamagePerStack * (s - 1));
        }

        public void Configure(CombatElement element, float startAngleDeg, int stack)
        {
            _element = element;
            _angle = startAngleDeg;
            if (element == CombatElement.Wind)
            {
                _baseSkillPercent = WindBaseSkillPercent;
                knockback = WindKnockback;
            }
            else
            {
                _baseSkillPercent = FireBaseSkillPercent;
                knockback = 0f;
            }

            SetStack(stack);
            CacheCollider();
            CaptureOrbitFromLocalPose();
        }

        public void SetStack(int stack)
        {
            _stack = Mathf.Max(1, stack);
        }

        public void Tick(GenshinLikeCharacter owner, Transform pivot, float deltaTime)
        {
            if (owner == null)
            {
                return;
            }

            _angle += degreesPerSecond * deltaTime;
            if (_angle >= 360f)
            {
                _angle -= 360f;
            }
            else if (_angle < 0f)
            {
                _angle += 360f;
            }

            Transform center = pivot != null ? pivot : owner.transform;
            float rad = _angle * Mathf.Deg2Rad;
            Vector3 pos = center.position;
            pos.x += Mathf.Cos(rad) * _radius;
            pos.y += _height;
            pos.z += Mathf.Sin(rad) * _radius;
            transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, -_angle, 0f));

            TryHit(owner);
        }

        void CacheCollider()
        {
            if (_box == null)
            {
                _box = GetComponent<BoxCollider>();
            }

            if (_box == null)
            {
                _box = GetComponentInChildren<BoxCollider>();
            }
        }

        void CaptureOrbitFromLocalPose()
        {
            Vector3 local = transform.localPosition;
            float xz = new Vector2(local.x, local.z).magnitude;
            if (xz > 0.05f)
            {
                _radius = xz;
            }

            _height = local.y;
        }

        void TryHit(GenshinLikeCharacter owner)
        {
            CacheCollider();
            if (_box == null || !_box.enabled || owner == null || owner.IsDead)
            {
                return;
            }

            Vector3 center = _box.transform.TransformPoint(_box.center);
            Vector3 lossy = _box.transform.lossyScale;
            Vector3 half = Vector3.Scale(_box.size * 0.5f, lossy);
            int count = ShapeHitDetector.OverlapBox(
                center,
                half,
                _box.transform.rotation,
                CombatLayers.PlayerOffenseHurtboxMask,
                _overlapBuffer);

            GameObject attackerGo = owner.gameObject;
            Transform ownerRoot = owner.transform;
            float skillPercent = SkillPercent(_stack, _baseSkillPercent);
            float now = Time.time;

            for (int i = 0; i < count; i++)
            {
                Collider col = _overlapBuffer[i];
                if (col == null || col == _box || col.transform.IsChildOf(transform))
                {
                    continue;
                }

                IDamageable damageable = FanHitDetector.ResolveDamageable(col);
                if (damageable == null)
                {
                    continue;
                }

                int id = HitResolver.ResolveDedupId(col.transform, damageable);
                if (_nextHitAt.TryGetValue(id, out float readyAt) && now < readyAt)
                {
                    continue;
                }

                Vector3 hitPoint = col.ClosestPoint(center);
                Vector3 dir = hitPoint - ownerRoot.position;
                dir.y = 0f;
                if (dir.sqrMagnitude < 0.0001f)
                {
                    dir = transform.right;
                    dir.y = 0f;
                }

                if (dir.sqrMagnitude < 0.0001f)
                {
                    dir = Vector3.forward;
                }
                else
                {
                    dir.Normalize();
                }

                var info = new DamageInfo(skillPercent, hitPoint, dir, knockback, -1, attackerGo)
                {
                    SkipCritical = true,
                    OverrideAttackElement = true,
                    AttackElement = _element,
                };

                if (!HitResolver.TryApply(HitRequest.Create(
                        info,
                        damageable,
                        col,
                        HitResolver.DefaultPlayerOffense,
                        session: null,
                        ownerRoot: ownerRoot)))
                {
                    continue;
                }

                _nextHitAt[id] = now + Mathf.Max(0.05f, hitCooldown);
            }

            if (_nextHitAt.Count > 48)
            {
                PruneCooldowns(now);
            }
        }

        void PruneCooldowns(float now)
        {
            var stale = new List<int>(8);
            foreach (var kv in _nextHitAt)
            {
                if (now >= kv.Value)
                {
                    stale.Add(kv.Key);
                }
            }

            for (int i = 0; i < stale.Count; i++)
            {
                _nextHitAt.Remove(stale[i]);
            }
        }
    }
}
