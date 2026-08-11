using UnityEngine;

namespace AttackSkill.Combat
{
    public struct DamageInfo
    {
        public float Amount;
        public Vector3 HitPoint;
        public Vector3 HitDirection;
        public float Knockback;
        public int ComboIndex;
        public GameObject Attacker;

        public DamageInfo(float amount, Vector3 hitPoint, Vector3 hitDirection, float knockback, int comboIndex, GameObject attacker)
        {
            Amount = amount;
            HitPoint = hitPoint;
            HitDirection = hitDirection;
            Knockback = knockback;
            ComboIndex = comboIndex;
            Attacker = attacker;
        }
    }

    public interface IDamageable
    {
        bool IsAlive { get; }
        void TakeDamage(DamageInfo info);
    }
}
