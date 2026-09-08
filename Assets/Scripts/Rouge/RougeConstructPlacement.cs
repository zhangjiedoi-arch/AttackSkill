using AttackSkill.Combat;
using UnityEngine;

namespace AttackSkill.Rouge
{
    /// <summary>在角色周围圆环上随机落点（水平 XZ），再贴地。</summary>
    public static class RougeConstructPlacement
    {
        public static Vector3 PickRing(Vector3 origin, float minRadius, float maxRadius)
        {
            float maxR = Mathf.Max(0.5f, maxRadius);
            float minR = Mathf.Clamp(minRadius, 0.1f, maxR);
            Vector3 pos = origin;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                float ang = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float r = Random.Range(minR, maxR);
                Vector3 candidate = origin;
                candidate.x += Mathf.Cos(ang) * r;
                candidate.z += Mathf.Sin(ang) * r;

                Vector3 planar = candidate - origin;
                planar.y = 0f;
                float dist = planar.magnitude;
                if (dist < minR * 0.85f || dist > maxR + 0.25f)
                {
                    continue;
                }

                return WorldGroundSnap.Snap(candidate);
            }

            float fallback = (minR + maxR) * 0.5f;
            pos.x += fallback;
            return WorldGroundSnap.Snap(pos);
        }
    }
}
