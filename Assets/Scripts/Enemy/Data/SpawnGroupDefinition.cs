using UnityEngine;

namespace AttackSkill.Enemy
{
    [System.Serializable]
    public class SpawnSlot
    {
        public EnemyDefinition definition;
        public Vector3 localOffset;
        public Vector3 localEuler;
    }

    [CreateAssetMenu(menuName = "AttackSkill/Enemy/Spawn Group Definition", fileName = "SpawnGroupDefinition")]
    public class SpawnGroupDefinition : ScriptableObject
    {
        public SpawnSlot[] slots =
        {
            new SpawnSlot()
        };

        [Header("Activation")]
        public float activateRadius = 20f;
        public float hibernateRadius = 40f;

        [Header("Respawn")]
        public float respawnDelay = 20f;
        public bool respawnOnlyWhenPlayerAway = true;
    }
}
