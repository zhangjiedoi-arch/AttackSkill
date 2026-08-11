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
                    // 出伤改由动画 Event Enemy_Hit_Chest_R（EnemyAttackHitRelay）触发，
                    // 不再在 Active 阶段自动开 Trigger Hitbox，避免重复结算。
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

        public void Interrupt()
        {
            _hitbox?.DisableHit();
            _phase = EnemyAttackPhase.None;
            _phaseTimer = 0f;
            _agent.SetAnimBool("IsAttacking", false);
        }
    }
}
