using System.Collections.Generic;
using UnityEngine;

namespace AttackSkill.Enemy
{
    /// <summary>
    /// 场上已启用的 <see cref="EnemyAgent"/> 登记表。
    /// Agent 在 OnEnable/OnDisable 登记；查询方勿每帧 FindObjectsOfType。
    /// </summary>
    public static class EnemyAgentRegistry
    {
        static readonly List<EnemyAgent> Agents = new List<EnemyAgent>(64);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Agents.Clear();
        }

        public static IReadOnlyList<EnemyAgent> Live => Agents;

        public static void Register(EnemyAgent agent)
        {
            if (agent == null || Agents.Contains(agent))
            {
                return;
            }

            Agents.Add(agent);
        }

        public static void Unregister(EnemyAgent agent)
        {
            if (agent == null)
            {
                return;
            }

            Agents.Remove(agent);
        }
    }
}
