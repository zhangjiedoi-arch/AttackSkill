using AttackSkill.Character.HSM;
using UnityEngine;

namespace AttackSkill.Combat
{
    /// <summary>
    /// 普攻 / E / T：若 2m 内有敌人，瞬移到其面前并朝向敌人。
    /// </summary>
    public static class CombatEngageUtility
    {
        public const float DefaultSearchRadius = 2f;
        public const float DefaultStandOff = 1.05f;

        static readonly Collider[] OverlapBuffer = new Collider[32];

        /// <returns>是否成功贴身并朝向某敌人。</returns>
        public static bool TrySnapToNearestEnemy(
            GenshinLikeCharacter character,
            float searchRadius = DefaultSearchRadius,
            float standOff = DefaultStandOff)
        {
            if (character == null)
            {
                return false;
            }

            Transform self = character.transform;
            if (!TryFindNearestEnemy(self.position, searchRadius, out Transform enemyRoot, out Vector3 enemyPos))
            {
                return false;
            }

            Vector3 from = self.position;
            Vector3 toEnemy = enemyPos - from;
            toEnemy.y = 0f;
            float dist = toEnemy.magnitude;
            Vector3 planarDir = dist > 0.001f ? toEnemy / dist : FlatForward(self);

            Vector3 dest = from;
            if (dist > standOff + 0.02f)
            {
                dest = enemyPos - planarDir * standOff;
                dest.y = from.y;
            }

            Quaternion face = Quaternion.LookRotation(planarDir, Vector3.up);
            character.TeleportTo(dest, face, resetMotion: true);
            return true;
        }

        public static bool TrySnapToNearestEnemy(
            Transform self,
            CharacterController controller,
            CharacterMotor motor,
            float searchRadius = DefaultSearchRadius,
            float standOff = DefaultStandOff)
        {
            if (self == null)
            {
                return false;
            }

            var character = self.GetComponentInParent<GenshinLikeCharacter>();
            if (character != null)
            {
                return TrySnapToNearestEnemy(character, searchRadius, standOff);
            }

            if (!TryFindNearestEnemy(self.position, searchRadius, out _, out Vector3 enemyPos))
            {
                return false;
            }

            Vector3 from = self.position;
            Vector3 toEnemy = enemyPos - from;
            toEnemy.y = 0f;
            float dist = toEnemy.magnitude;
            Vector3 planarDir = dist > 0.001f ? toEnemy / dist : FlatForward(self);

            Vector3 dest = from;
            if (dist > standOff + 0.02f)
            {
                dest = enemyPos - planarDir * standOff;
                dest.y = from.y;
            }

            Quaternion face = Quaternion.LookRotation(planarDir, Vector3.up);
            bool ccOn = controller != null && controller.enabled;
            if (controller != null)
            {
                controller.enabled = false;
            }

            self.SetPositionAndRotation(dest, face);
            Physics.SyncTransforms();
            motor?.ResetMotion();
            if (controller != null)
            {
                controller.enabled = ccOn;
                Physics.SyncTransforms();
            }

            return true;
        }

        static bool TryFindNearestEnemy(Vector3 origin, float radius, out Transform enemyRoot, out Vector3 enemyPos)
        {
            enemyRoot = null;
            enemyPos = origin;
            float r = Mathf.Max(0.1f, radius);
            int count = Physics.OverlapSphereNonAlloc(
                origin,
                r,
                OverlapBuffer,
                CombatLayers.PlayerOffenseHurtboxMask,
                QueryTriggerInteraction.Collide);

            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Collider col = OverlapBuffer[i];
                if (col == null)
                {
                    continue;
                }

                var agent = col.GetComponentInParent<AttackSkill.Enemy.EnemyAgent>();
                if (agent == null || agent.IsDead)
                {
                    continue;
                }

                Vector3 p = agent.transform.position;
                Vector3 d = p - origin;
                d.y = 0f;
                float sqr = d.sqrMagnitude;
                if (sqr > r * r || sqr >= best)
                {
                    continue;
                }

                best = sqr;
                enemyRoot = agent.transform;
                enemyPos = p;
            }

            return enemyRoot != null;
        }

        static Vector3 FlatForward(Transform t)
        {
            Vector3 f = t.forward;
            f.y = 0f;
            if (f.sqrMagnitude < 0.0001f)
            {
                return Vector3.forward;
            }

            return f.normalized;
        }
    }
}
