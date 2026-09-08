using UnityEngine;

namespace AttackSkill.Combat
{
    /// <summary>
    /// 把世界坐标落到地表。刷怪 / 场地技能用角色 XZ，不要沿用滞空时的 Y。
    /// </summary>
    public static class WorldGroundSnap
    {
        const float DefaultProbeUp = 16f;
        const float DefaultProbeDown = 96f;

        public static Vector3 Snap(Vector3 pos, float hover = 0.05f)
        {
            int mask = CombatLayers.DefaultCameraCollisionMask;
            int enemy = CombatLayers.EnemyLayer;
            if (enemy >= 0)
            {
                mask &= ~(1 << enemy);
            }

            Vector3 origin = pos + Vector3.up * DefaultProbeUp;
            float distance = DefaultProbeUp + DefaultProbeDown;
            if (Physics.Raycast(
                    origin,
                    Vector3.down,
                    out RaycastHit hit,
                    distance,
                    mask,
                    QueryTriggerInteraction.Ignore))
            {
                Vector3 grounded = hit.point;
                grounded.y += hover;
                return grounded;
            }

            return pos;
        }
    }
}
