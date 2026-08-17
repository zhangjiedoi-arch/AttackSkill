using UnityEngine;

namespace AttackSkill.Enemy
{
    public enum EnemyAttackPhase
    {
        None,
        Windup,
        Active,
        Recovery
    }

    public class EnemyCombat
    {
        readonly EnemyAgent _agent;
        readonly EnemyHitbox _hitbox;
        EnemyDefinition _def;
        EnemyAttackPhase _phase;
        float _phaseTimer;
        float _cooldownTimer;

        public bool IsBusy => _phase != EnemyAttackPhase.None;
        public EnemyAttackPhase Phase => _phase;

        public EnemyCombat(EnemyAgent agent, EnemyHitbox hitbox)
        {
            _agent = agent;
            _hitbox = hitbox;
        }

        public void Configure(EnemyDefinition def)
        {
            _def = def;
        }

        public bool CanAttack => !IsBusy && _cooldownTimer <= 0f;

        public bool TryStartAttack()
        {
            if (!CanAttack || _def == null)
            {
                return false;
            }

            _phase = EnemyAttackPhase.Windup;
            _phaseTimer = _def.attackWindup;
            _agent.SetAnimTrigger("EnemyAttack");
            _agent.SetAnimBool("IsAttacking", true);
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (_cooldownTimer > 0f)
            {
                _cooldownTimer -= deltaTime;
            }

            if (_phase == EnemyAttackPhase.None)
            {
                return;
            }

            _phaseTimer -= deltaTime;
            if (_phaseTimer > 0f)
            {
                return;
            }

            switch (_phase)
            {
                case EnemyAttackPhase.Windup:
                    _phase = EnemyAttackPhase.Active;
                    _phaseTimer = _def.attackActive;
                    // 动画未绑 SkillHit / Enemy_Hit_Chest_R Event，Active 开始时主动出伤
                    FireActiveDamage();
                    break;

                case EnemyAttackPhase.Active:
                    _hitbox?.DisableHit();
                    _phase = EnemyAttackPhase.Recovery;
                    _phaseTimer = _def.attackRecovery;
                    break;

                case EnemyAttackPhase.Recovery:
                    _phase = EnemyAttackPhase.None;
                    _cooldownTimer = _def.attackCooldown;
                    _agent.SetAnimBool("IsAttacking", false);
                    break;
            }
        }

        void FireActiveDamage()
        {
            if (_def == null || _agent == null)
            {
                return;
            }

            // Active 窗口开 Trigger 盒（动画未绑 Event 时的可靠出伤）
            // 若以后在 Attack 动画上加了 SkillHit Event，请关掉这里以免双倍结算
            _hitbox?.EnableHit(
                _def.attackDamage,
                _def.attackKnockback,
                _def.hitRadius,
                _def.hitForwardOffset,
                _def.attackActive,
                _agent.gameObject);
        }

        public void Interrupt()
        {
            _hitbox?.DisableHit();
            _phase = EnemyAttackPhase.None;
            _phaseTimer = 0f;
            _agent.SetAnimBool("IsAttacking", false);
        }
    }
}
