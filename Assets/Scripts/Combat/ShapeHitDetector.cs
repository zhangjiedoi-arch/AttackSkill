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

        public static int OverlapBox(
            Vector3 center,
            Vector3 halfExtents,
            Quaternion orientation,
            LayerMask mask,
            Collider[] buffer)
        {
            if (buffer == null || buffer.Length == 0)
            {
                return 0;
            }

            Vector3 half = halfExtents;
            half.x = Mathf.Max(0.01f, Mathf.Abs(half.x));
            half.y = Mathf.Max(0.01f, Mathf.Abs(half.y));
            half.z = Mathf.Max(0.01f, Mathf.Abs(half.z));
            return Physics.OverlapBoxNonAlloc(
                center,
                half,
                buffer,
                orientation,
                mask,
                QueryTriggerInteraction.Collide);
        }

        public static int OverlapCapsule(
            Vector3 point0,
            Vector3 point1,
            float radius,
            LayerMask mask,
            Collider[] buffer)
        {
            if (buffer == null || buffer.Length == 0)
            {
                return 0;
            }

            return Physics.OverlapCapsuleNonAlloc(
                point0,
                point1,
                Mathf.Max(0.01f, radius),
                buffer,
                mask,
                QueryTriggerInteraction.Collide);
        }

        /// <summary>按 Collider 类型做一次 Overlap（Box / Capsule / Sphere）。</summary>
        public static int OverlapCollider(Collider col, LayerMask mask, Collider[] buffer)
        {
            if (col == null || buffer == null || buffer.Length == 0)
            {
                return 0;
            }

            if (col is BoxCollider box)
            {
                Vector3 center = box.transform.TransformPoint(box.center);
                Vector3 half = Vector3.Scale(box.size * 0.5f, box.transform.lossyScale);
                return OverlapBox(center, half, box.transform.rotation, mask, buffer);
            }

            if (col is CapsuleCollider cap)
            {
                GetCapsuleWorldEnds(cap, out Vector3 p0, out Vector3 p1, out float radius);
                return OverlapCapsule(p0, p1, radius, mask, buffer);
            }

            if (col is SphereCollider sphere)
            {
                Vector3 center = sphere.transform.TransformPoint(sphere.center);
                float scale = Mathf.Max(
                    Mathf.Abs(sphere.transform.lossyScale.x),
                    Mathf.Max(
                        Mathf.Abs(sphere.transform.lossyScale.y),
                        Mathf.Abs(sphere.transform.lossyScale.z)));
                return OverlapSphere(center, sphere.radius * scale, mask, buffer);
            }

            return OverlapSphere(col.bounds.center, col.bounds.extents.magnitude, mask, buffer);
        }

        public static void GetCapsuleWorldEnds(
            CapsuleCollider cap,
            out Vector3 point0,
            out Vector3 point1,
            out float radius)
        {
            Transform t = cap.transform;
            Vector3 lossy = t.lossyScale;
            Vector3 axis;
            float heightScale;
            float radiusScale;
            if (cap.direction == 0)
            {
                axis = t.right;
                heightScale = Mathf.Abs(lossy.x);
                radiusScale = Mathf.Max(Mathf.Abs(lossy.y), Mathf.Abs(lossy.z));
            }
            else if (cap.direction == 2)
            {
                axis = t.forward;
                heightScale = Mathf.Abs(lossy.z);
                radiusScale = Mathf.Max(Mathf.Abs(lossy.x), Mathf.Abs(lossy.y));
            }
            else
            {
                axis = t.up;
                heightScale = Mathf.Abs(lossy.y);
                radiusScale = Mathf.Max(Mathf.Abs(lossy.x), Mathf.Abs(lossy.z));
            }

            radius = Mathf.Max(0.01f, cap.radius * radiusScale);
            float height = Mathf.Max(radius * 2f, cap.height * heightScale);
            float half = Mathf.Max(0f, height * 0.5f - radius);
            Vector3 center = t.TransformPoint(cap.center);
            Vector3 delta = axis.normalized * half;
            point0 = center + delta;
            point1 = center - delta;
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
