using UnityEngine;

namespace AttackSkill.Combat
{
    /// <summary>
    /// 球形 / 直立圆柱 Overlap（不含伤害规则）。
    /// </summary>
    public static class ShapeHitDetector
    {
        public static int OverlapSphere(
            Vector3 center,
            float radius,
            LayerMask mask,
            Collider[] buffer)
        {
            if (buffer == null || buffer.Length == 0)
            {
                return 0;
            }

            return Physics.OverlapSphereNonAlloc(
                center,
                Mathf.Max(0.01f, radius),
                buffer,
                mask,
                QueryTriggerInteraction.Collide);
        }

        /// <summary>
        /// 用包络球粗查后按圆柱过滤：水平半径 + [origin.y, origin.y+height]。
        /// </summary>
        public static int OverlapCylinder(
            Vector3 bottomCenter,
            float radius,
            float height,
            LayerMask mask,
            Collider[] buffer,
            Collider[] scratch)
        {
            if (buffer == null || buffer.Length == 0)
            {
                return 0;
            }

            float r = Mathf.Max(0.01f, radius);
            float h = Mathf.Max(0.01f, height);
            Vector3 mid = bottomCenter + Vector3.up * (h * 0.5f);
            float queryRadius = Mathf.Sqrt(r * r + (h * 0.5f) * (h * 0.5f));

            Collider[] src = scratch != null && scratch.Length > 0 ? scratch : buffer;
            int raw = Physics.OverlapSphereNonAlloc(
                mid,
                queryRadius,
                src,
                mask,
                QueryTriggerInteraction.Collide);

            int written = 0;
            for (int i = 0; i < raw; i++)
            {
                Collider col = src[i];
                if (col == null)
                {
                    continue;
                }

                if (!TryPassCylinderFilter(col, bottomCenter, r, h, out _, out _))
                {
                    continue;
                }

                if (written < buffer.Length)
                {
                    buffer[written++] = col;
                }
            }

            return written;
        }

        public static bool TryPassSphereFilter(
            Collider col,
            Vector3 center,
            float radius,
            out Vector3 hitPoint,
            out Vector3 toHit)
        {
            hitPoint = default;
            toHit = default;
            if (col == null)
            {
                return false;
            }

            float maxR = Mathf.Max(0.01f, radius);
            hitPoint = col.ClosestPoint(center);
            toHit = hitPoint - center;
            return toHit.sqrMagnitude <= maxR * maxR;
        }

        public static bool TryPassCylinderFilter(
            Collider col,
            Vector3 bottomCenter,
            float radius,
            float height,
            out Vector3 hitPoint,
            out Vector3 planarToHit)
        {
            hitPoint = default;
            planarToHit = default;
            if (col == null)
            {
                return false;
            }

            float r = Mathf.Max(0.01f, radius);
            float h = Mathf.Max(0.01f, height);
            // 取柱轴上最近点作为参考，再做 ClosestPoint
            Vector3 axisSample = bottomCenter + Vector3.up * (h * 0.5f);
            hitPoint = col.ClosestPoint(axisSample);

            planarToHit = hitPoint - bottomCenter;
            planarToHit.y = 0f;
            if (planarToHit.sqrMagnitude > r * r)
            {
                return false;
            }

            float y = hitPoint.y;
            float yMin = bottomCenter.y;
            float yMax = bottomCenter.y + h;
            return y >= yMin - 0.05f && y <= yMax + 0.05f;
        }
    }
}
