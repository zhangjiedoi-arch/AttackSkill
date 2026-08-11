using UnityEngine;

namespace AttackSkill.Enemy
{
    public struct PerceiveInfo
    {
        public bool CanSee;
        public bool CanHear;
        public float Distance;
    }

    public class EnemySensor
    {
        const int HitBufferSize = 32;

        readonly Transform _origin;
        Transform _selfRoot;
        float _sightRange;
        float _sightAngle;
        float _hearRange;
        LayerMask _losMask = ~0;
        readonly RaycastHit[] _hitBuffer = new RaycastHit[HitBufferSize];

        public EnemySensor(Transform origin, Transform selfRoot)
        {
            _origin = origin;
            _selfRoot = selfRoot != null ? selfRoot : origin;
        }

        public void Configure(EnemyDefinition def)
        {
            _sightRange = def.sightRange;
            _sightAngle = def.sightAngle;
            _hearRange = def.hearRange;
            _losMask = def.losMask.value != 0 ? def.losMask : ~0;
        }

        public bool TryPerceive(Transform target, out PerceiveInfo info)
        {
            info = default;
            if (target == null || _origin == null)
            {
                return false;
            }

            Vector3 eye = _origin.position + Vector3.up * 1.2f;
            Vector3 aim = target.position + Vector3.up * 1.0f;
            Vector3 to = aim - eye;
            float dist = to.magnitude;
            info.Distance = dist;

            if (dist <= _hearRange)
            {
                info.CanHear = true;
            }

            if (dist <= _sightRange)
            {
                Vector3 flat = to;
                flat.y = 0f;
                if (flat.sqrMagnitude > 0.0001f)
                {
                    float angle = Vector3.Angle(_origin.forward, flat.normalized);
                    if (angle <= _sightAngle * 0.5f && HasLineOfSight(eye, to.normalized, dist, target))
                    {
                        info.CanSee = true;
                    }
                }
            }

            return info.CanSee || info.CanHear;
        }

        bool HasLineOfSight(Vector3 eye, Vector3 dir, float dist, Transform target)
        {
            int count = Physics.RaycastNonAlloc(eye, dir, _hitBuffer, dist, _losMask, QueryTriggerInteraction.Ignore);
            if (count <= 0)
            {
                return true;
            }

            // 找最近的非自身命中
            Transform targetRoot = target.root;
            float bestDist = float.MaxValue;
            Transform best = null;
            for (int i = 0; i < count; i++)
            {
                Transform hitT = _hitBuffer[i].transform;
                if (hitT == null)
                {
                    continue;
                }

                if (_selfRoot != null && (hitT == _selfRoot || hitT.IsChildOf(_selfRoot)))
                {
                    continue;
                }

                float d = _hitBuffer[i].distance;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = hitT;
                }
            }

            if (best == null)
            {
                return true;
            }

            return best == target || best.IsChildOf(targetRoot);
        }
    }
}
