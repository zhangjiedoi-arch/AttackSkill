using System.Collections.Generic;
using AttackSkill.Character.HSM;
using AttackSkill.Combat;
using UnityEngine;

namespace AttackSkill.Rouge
{
    /// <summary>肉鸽生成物光环：落在角色附近，按所挂 Collider 每秒出伤，到期换点。</summary>
    public sealed class RougeAuraZone : MonoBehaviour
    {
        public const float BaseSkillPercent = 120f;
        public const float ExtraPerStack = 0.10f;
        public const float PulseInterval = 1f;
        public const float IceLifetime = 2f;
        public const float FireLifetime = 5f;
        public const float SnowLifetime = 4f;
        public const float SpawnMinRadius = 5f;
        public const float SpawnMaxRadius = 10f;

        const int OverlapBufferSize = 128;

        CombatElement _element = CombatElement.Ice;
        int _stack = 1;
        float _lifetime = IceLifetime;
        float _expireAt = -1f;
        float _nextPulseAt;
        bool _placed;
        Collider _volume;
        ParticleSystem[] _particles;
        AudioSource[] _audios;
        readonly Collider[] _overlapBuffer = new Collider[OverlapBufferSize];
        readonly HitSession _session = new HitSession();
        readonly HashSet<int> _hitThisPulse = new HashSet<int>(64);

        public CombatElement Element => _element;

        public static float SkillPercent(int stack)
        {
            int s = Mathf.Clamp(stack, 1, 5);
            return BaseSkillPercent * (1f + ExtraPerStack * (s - 1));
        }

        public static float LifetimeFor(CombatElement element)
        {
            switch (element)
            {
                case CombatElement.Fire:
                    return FireLifetime;
                case CombatElement.Dark:
                    return SnowLifetime;
                default:
                    return IceLifetime;
            }
        }

        public void Configure(CombatElement element, int stack)
        {
            _element = element;
            _lifetime = LifetimeFor(element);
            SetStack(stack);
            CacheVolume();
            if (_volume != null)
            {
                _volume.isTrigger = true;
                _volume.enabled = true;
            }

            CacheFx();
        }

        public void SetStack(int stack)
        {
            _stack = Mathf.Clamp(stack, 1, 5);
        }

        public void TickWorld(GenshinLikeCharacter owner)
        {
            if (owner == null)
            {
                return;
            }

            if (!_placed || Time.time >= _expireAt)
            {
                RelocateNear(owner);
            }

            if (Time.time >= _nextPulseAt)
            {
                _nextPulseAt = Time.time + PulseInterval;
                Pulse(owner);
            }
        }

        public void RelocateNear(GenshinLikeCharacter owner)
        {
            if (owner == null)
            {
                return;
            }

            Vector3 pos = RougeConstructPlacement.PickRing(
                owner.transform.position,
                SpawnMinRadius,
                SpawnMaxRadius);
            transform.SetPositionAndRotation(pos, Quaternion.identity);
            _placed = true;
            _expireAt = Time.time + Mathf.Max(0.1f, _lifetime);
            _nextPulseAt = Time.time;
            RestartFx();
        }

        void CacheVolume()
        {
            if (_volume == null)
            {
                _volume = GetComponent<Collider>();
            }

            if (_volume == null)
            {
                _volume = GetComponentInChildren<Collider>();
            }
        }

        void CacheFx()
        {
            if (_particles == null)
            {
                _particles = GetComponentsInChildren<ParticleSystem>(true);
            }

            if (_audios == null)
            {
                _audios = GetComponentsInChildren<AudioSource>(true);
            }
        }

        void RestartFx()
        {
            CacheFx();
            if (_particles != null)
            {
                for (int i = 0; i < _particles.Length; i++)
                {
                    ParticleSystem ps = _particles[i];
                    if (ps == null)
                    {
                        continue;
                    }

                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    ps.Play(true);
                }
            }

            if (_audios != null)
            {
                for (int i = 0; i < _audios.Length; i++)
                {
                    AudioSource src = _audios[i];
                    if (src == null || src.clip == null)
                    {
                        continue;
                    }

                    src.Stop();
                    src.Play();
                }
            }
        }

        void Pulse(GenshinLikeCharacter owner)
        {
            CacheVolume();
            if (_volume == null || owner == null || owner.IsDead)
            {
                return;
            }

            int count = ShapeHitDetector.OverlapCollider(
                _volume,
                CombatLayers.PlayerOffenseHurtboxMask,
                _overlapBuffer);

            GameObject attackerGo = owner.gameObject;
            Transform ownerRoot = owner.transform;
            float skillPercent = SkillPercent(_stack);
            _session.Begin();
            _hitThisPulse.Clear();

            for (int i = 0; i < count; i++)
            {
                Collider col = _overlapBuffer[i];
                if (col == null || col == _volume || col.transform.IsChildOf(transform))
                {
                    continue;
                }

                IDamageable damageable = FanHitDetector.ResolveDamageable(col);
                if (damageable == null)
                {
                    continue;
                }

                int id = HitResolver.ResolveDedupId(col.transform, damageable);
                if (!_hitThisPulse.Add(id))
                {
                    continue;
                }

                Vector3 hitPoint = col.ClosestPoint(transform.position);
                Vector3 dir = hitPoint - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude < 0.0001f)
                {
                    dir = Vector3.forward;
                }
                else
                {
                    dir.Normalize();
                }

                var info = new DamageInfo(skillPercent, hitPoint, dir, 0f, -1, attackerGo)
                {
                    SkipCritical = true,
                    OverrideAttackElement = true,
                    AttackElement = _element,
                };

                HitResolver.TryApply(HitRequest.Create(
                    info,
                    damageable,
                    col,
                    HitResolver.DefaultPlayerOffense,
                    _session,
                    ownerRoot: ownerRoot));
            }
        }
    }
}
