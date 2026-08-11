using UnityEngine;

namespace AttackSkill.Enemy
{
    public class EnemyBrain
    {
        readonly EnemyAgent _agent;
        IEnemyState _current;

        public readonly EnemyIdleState Idle = new EnemyIdleState();
        public readonly EnemyAlertState Alert = new EnemyAlertState();
        public readonly EnemyChaseState Chase = new EnemyChaseState();
        public readonly EnemyCombatState Combat = new EnemyCombatState();
        public readonly EnemyReturnState Return = new EnemyReturnState();
        public readonly EnemyDeadState Dead = new EnemyDeadState();

        public IEnemyState Current => _current;
        public string CurrentName => _current != null ? _current.Name : "None";

        public EnemyBrain(EnemyAgent agent)
        {
            _agent = agent;
        }

        public void Start()
        {
            SetState(Idle);
        }

        public void SetState(IEnemyState next)
        {
            if (next == null || _current == next)
            {
                return;
            }

            _current?.Exit(_agent);
            _current = next;
            _current.Enter(_agent);
        }

        public void Tick(float deltaTime)
        {
            _current?.Tick(_agent, deltaTime);
        }
    }

    public class EnemyIdleState : IEnemyState
    {
        public string Name => "Idle";

        public void Enter(EnemyAgent agent)
        {
            agent.Motor.Stop();
            agent.SetAnimFloat("EnemySpeed", 0f);
            agent.SetAnimBool("InCombat", false);
            agent.CrossFadeAnim("Idle", 0.15f);
        }

        public void Exit(EnemyAgent agent) { }

        public void Tick(EnemyAgent agent, float deltaTime)
        {
            agent.Motor.Tick(deltaTime);

            // 复用 EnemyAgent 本帧感知，避免重复 Raycast
            if (agent.PerceivedPlayerThisFrame && agent.PerceivedPlayer != null)
            {
                agent.Aggro.SetTarget(agent.PerceivedPlayer);
                agent.Brain.SetState(agent.Brain.Alert);
            }
        }
    }

    public class EnemyAlertState : IEnemyState
    {
        float _timer;
        public string Name => "Alert";

        public void Enter(EnemyAgent agent)
        {
            _timer = agent.Definition.alertDuration;
            agent.Motor.Stop();
            agent.SetAnimFloat("EnemySpeed", 0f);
            agent.SetAnimTrigger("EnemyAlert");
        }

        public void Exit(EnemyAgent agent) { }

        public void Tick(EnemyAgent agent, float deltaTime)
        {
            if (agent.Aggro.CurrentTarget != null)
            {
                Vector3 to = agent.Aggro.CurrentTarget.position - agent.transform.position;
                agent.Motor.FaceDirection(to, deltaTime);
            }

            agent.Motor.Tick(deltaTime);
            _timer -= deltaTime;
            if (_timer <= 0f)
            {
                agent.Brain.SetState(agent.Brain.Chase);
            }
        }
    }

    public class EnemyChaseState : IEnemyState
    {
        public string Name => "Chase";

        public void Enter(EnemyAgent agent)
        {
            agent.SetAnimBool("InCombat", true);
        }

        public void Exit(EnemyAgent agent) { }

        public void Tick(EnemyAgent agent, float deltaTime)
        {
            Transform target = agent.Aggro.CurrentTarget;
            if (target == null)
            {
                agent.Brain.SetState(agent.Brain.Return);
                return;
            }

            if (EnemyAiRules.ShouldLeashHome(agent) || EnemyAiRules.ShouldDisengage(agent, target))
            {
                agent.Aggro.Clear();
                agent.Brain.SetState(agent.Brain.Return);
                return;
            }

            float dist = Vector3.Distance(agent.transform.position, target.position);
            if (dist <= agent.Definition.attackRange)
            {
                agent.Brain.SetState(agent.Brain.Combat);
                return;
            }

            agent.Motor.MoveTowards(target.position, deltaTime);
            agent.SetAnimFloat("EnemySpeed", EnemyAiRules.PlanarSpeed(agent));
        }
    }

    public class EnemyCombatState : IEnemyState
    {
        public string Name => "Combat";

        public void Enter(EnemyAgent agent)
        {
            agent.Motor.Stop();
            agent.SetAnimFloat("EnemySpeed", 0f);
            agent.SetAnimBool("InCombat", true);
        }

        public void Exit(EnemyAgent agent)
        {
            agent.Combat.Interrupt();
        }

        public void Tick(EnemyAgent agent, float deltaTime)
        {
            Transform target = agent.Aggro.CurrentTarget;
            if (target == null)
            {
                agent.Brain.SetState(agent.Brain.Return);
                return;
            }

            if (EnemyAiRules.ShouldLeashHome(agent) || EnemyAiRules.ShouldDisengage(agent, target))
            {
                agent.Aggro.Clear();
                agent.Brain.SetState(agent.Brain.Return);
                return;
            }

            float dist = Vector3.Distance(agent.transform.position, target.position);

            Vector3 to = target.position - agent.transform.position;
            agent.Motor.FaceDirection(to, deltaTime);

            if (!agent.Combat.IsBusy)
            {
                if (dist > agent.Definition.attackRange * 1.15f)
                {
                    agent.Brain.SetState(agent.Brain.Chase);
                    return;
                }

                agent.Combat.TryStartAttack();
            }

            agent.Combat.Tick(deltaTime);
            agent.Motor.Tick(deltaTime);
        }
    }

    public class EnemyReturnState : IEnemyState
    {
        const float ArriveDistanceXZ = 0.75f;

        public string Name => "Return";

        public void Enter(EnemyAgent agent)
        {
            agent.Combat.Interrupt();
            agent.SetAnimBool("InCombat", false);
        }

        public void Exit(EnemyAgent agent) { }

        public void Tick(EnemyAgent agent, float deltaTime)
        {
            // 只用水平距离判定到家，避免 Y 差导致永远进不了 Idle，却仍被写成走路速度
            Vector3 toHome = agent.HomePosition - agent.transform.position;
            toHome.y = 0f;
            if (toHome.sqrMagnitude <= ArriveDistanceXZ * ArriveDistanceXZ)
            {
                ArriveHome(agent);
                return;
            }

            // 回位途中再次发现玩家则重新开战（复用本帧感知）
            if (agent.PerceivedPlayerThisFrame && agent.PerceivedPlayer != null)
            {
                agent.Aggro.SetTarget(agent.PerceivedPlayer);
                agent.Brain.SetState(agent.Brain.Chase);
                return;
            }

            agent.Motor.MoveTowards(agent.HomePosition, deltaTime, 1.1f);
            agent.SetAnimFloat("EnemySpeed", EnemyAiRules.PlanarSpeed(agent));
        }

        static void ArriveHome(EnemyAgent agent)
        {
            agent.Motor.Stop();
            agent.SetAnimFloat("EnemySpeed", 0f);
            agent.Motor.Teleport(agent.HomePosition, agent.HomeRotation);
            // 脱战不回满血，保留当前 HP
            agent.Brain.SetState(agent.Brain.Idle);
        }
    }

    public class EnemyDeadState : IEnemyState
    {
        public string Name => "Dead";

        public void Enter(EnemyAgent agent)
        {
            agent.Combat.Interrupt();
            agent.Motor.Stop();
            agent.SetAnimBool("InCombat", false);
            agent.SetAnimBool("IsDead", true);
            agent.SetAnimTrigger("EnemyDie");
            if (agent.Controller != null)
            {
                agent.Controller.enabled = false;
            }
        }

        public void Exit(EnemyAgent agent)
        {
            agent.SetAnimBool("IsDead", false);
            if (agent.Controller != null)
            {
                agent.Controller.enabled = true;
            }
        }

        public void Tick(EnemyAgent agent, float deltaTime) { }
    }

    static class EnemyAiRules
    {
        public static float PlanarSpeed(EnemyAgent agent)
        {
            Vector3 v = agent.Motor.Velocity;
            return new Vector3(v.x, 0f, v.z).magnitude;
        }

        public static bool ShouldDisengage(EnemyAgent agent, Transform target)
        {
            if (agent?.Definition == null || target == null)
            {
                return true;
            }

            return Vector3.Distance(agent.transform.position, target.position) >= agent.Definition.disengageRange;
        }

        public static bool ShouldLeashHome(EnemyAgent agent)
        {
            if (agent?.Definition == null)
            {
                return false;
            }

            float leash = agent.Definition.returnHomeRange;
            if (leash <= 0.01f)
            {
                return false;
            }

            return Vector3.Distance(agent.transform.position, agent.HomePosition) >= leash;
        }
    }
}
