using UnityEngine;

namespace AttackSkill.Enemy
{
    /// <summary>
    /// 死亡表现分流：声骸（金色透明残留）/ 飘散溶解。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyDeathDirector : MonoBehaviour
    {
        EnemyAgent _agent;
        EnemyDeathGoldVisual _gold;
        EnemyDeathDissolveVisual _dissolve;

        public EnemyDeathOutcome LastOutcome { get; private set; } = EnemyDeathOutcome.Echo;
        public bool HasBegun { get; private set; }

        public void Bind(EnemyAgent agent, EnemyDeathGoldVisual gold, EnemyDeathDissolveVisual dissolve)
        {
            _agent = agent;
            _gold = gold;
            _dissolve = dissolve;
        }

        public void ResetForReuse()
        {
            HasBegun = false;
            LastOutcome = EnemyDeathOutcome.Echo;
            _gold?.Restore();
            _dissolve?.Restore();
            if (_agent != null)
            {
                var interact = _agent.GetComponent<AttackSkill.UI.World.EchoRemainInteract>();
                interact?.Deactivate();
            }
        }

        public void Begin()
        {
            if (HasBegun)
            {
                return;
            }

            HasBegun = true;
            EnemyDefinition def = _agent != null ? _agent.Definition : null;
            if (_dissolve != null)
            {
                _dissolve.ConfigureFromDefinition(def);
            }

            // 无论分支，先关碰撞，避免尸体挡路
            EnemyDeathVisualUtil.DisableBlockingColliders(gameObject, includeTriggers: true);

            LastOutcome = Roll(def);
            switch (LastOutcome)
            {
                case EnemyDeathOutcome.Dissolve:
                    BeginDissolve(def);
                    break;

                default:
                    BeginEcho(def);
                    break;
            }
        }

        void BeginEcho(EnemyDefinition def)
        {
            _dissolve?.Restore();
            if (_gold != null && _gold.Play())
            {
                LastOutcome = EnemyDeathOutcome.Echo;
                ScheduleEchoDespawn(def);
                EnableEchoInteract();
                return;
            }

            // 金透失败：尽量改走溶解；再失败则短延时销毁
            if (_dissolve != null && _dissolve.Play(OnDissolveFinished))
            {
                LastOutcome = EnemyDeathOutcome.Dissolve;
                return;
            }

            LastOutcome = EnemyDeathOutcome.Echo;
            Destroy(gameObject, 0.5f);
        }

        void BeginDissolve(EnemyDefinition def)
        {
            _gold?.Restore();
            if (_dissolve != null && _dissolve.Play(OnDissolveFinished))
            {
                LastOutcome = EnemyDeathOutcome.Dissolve;
                return;
            }

            // 溶解失败：回退金透声骸
            BeginEcho(def);
        }

        void EnableEchoInteract()
        {
            if (_agent == null)
            {
                return;
            }

            var interact = _agent.GetComponent<AttackSkill.UI.World.EchoRemainInteract>();
            if (interact == null)
            {
                interact = _agent.gameObject.AddComponent<AttackSkill.UI.World.EchoRemainInteract>();
            }

            interact.Activate(_agent);
        }

        public static EnemyDeathOutcome Roll(EnemyDefinition def)
        {
            if (def != null)
            {
                switch (def.deathForceMode)
                {
                    case EnemyDeathForceMode.ForceEcho:
                        return EnemyDeathOutcome.Echo;
                    case EnemyDeathForceMode.ForceDissolve:
                        return EnemyDeathOutcome.Dissolve;
                }

                float chance = Mathf.Clamp01(def.echoChance);
                return Random.value <= chance ? EnemyDeathOutcome.Echo : EnemyDeathOutcome.Dissolve;
            }

            return Random.value <= 0.35f ? EnemyDeathOutcome.Echo : EnemyDeathOutcome.Dissolve;
        }

        void ScheduleEchoDespawn(EnemyDefinition def)
        {
            float life = def != null ? Mathf.Max(0.5f, def.echoCorpseLifetime) : 20f;
            Destroy(gameObject, life);
        }

        void OnDissolveFinished()
        {
            Destroy(gameObject);
        }
    }
}
