using UnityEngine;

namespace AttackSkill.Combat
{
    /// <summary>
    /// 水平扇形 Overlap 查询（不含伤害规则）。供普攻 / 技能窗口复用。
    /// </summary>
    public static class FanHitDetector
    {
        public static int Overlap(
            Vector3 origin,
            Vector3 planarForward,
            float hitRadius,
            float fanAngleDegrees,
            float minHitDistance,
            LayerMask mask,
            Collider[] buffer)
        {
            if (buffer == null || buffer.Length == 0)
            {
                return 0;
            }

            float maxDist = Mathf.Max(0f, hitRadius);
            return Physics.OverlapSphereNonAlloc(
                origin,
                maxDist,
                buffer,
                mask,
                QueryTriggerInteraction.Collide);
        }

        /// <summary>
        /// 候选是否落在扇形环带内。命中点取 ClosestPoint；方向输出为水平向量（未归一化）。
        /// </summary>
        public static bool TryPassFanFilter(
            Collider col,
            Vector3 origin,
            Vector3 planarForward,
            float hitRadius,
            float fanAngleDegrees,
            float minHitDistance,
            out Vector3 hitPoint,
            out Vector3 planarToHit)
        {
            hitPoint = default;
            planarToHit = default;
            if (col == null)
            {
                return false;
            }

            float halfAngle = Mathf.Max(1f, fanAngleDegrees) * 0.5f;
            float minDist = Mathf.Max(0f, minHitDistance);
            float maxDist = Mathf.Max(minDist, hitRadius);
            float maxDistSq = maxDist * maxDist;
            float minDistSq = minDist * minDist;

            hitPoint = col.ClosestPoint(origin);
            planarToHit = hitPoint - origin;
            planarToHit.y = 0f;
            float distSq = planarToHit.sqrMagnitude;
            if (distSq > maxDistSq || distSq < minDistSq)
            {
                return false;
            }

            if (distSq > 0.0001f)
            {
                float angle = Vector3.Angle(planarForward, planarToHit);
                if (angle > halfAngle)
                {
                    return false;
                }
            }

            return true;
        }

        public static IDamageable ResolveDamageable(Collider col)
        {
            return col != null ? col.GetComponentInParent<IDamageable>() : null;
        }
    }
}
