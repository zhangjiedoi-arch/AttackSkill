using UnityEngine;

namespace AttackSkill.Enemy
{
    /// <summary>标记可进 <see cref="EnemyObjectPool"/> 的敌人实例。</summary>
    [DisallowMultipleComponent]
    public sealed class EnemyPoolMember : MonoBehaviour
    {
        public int PrefabKey { get; private set; }
        public GameObject SourcePrefab { get; private set; }
        public bool IsInPool { get; private set; }

        public void Bind(int prefabKey, GameObject sourcePrefab)
        {
            PrefabKey = prefabKey;
            SourcePrefab = sourcePrefab;
        }

        public void MarkInPool(bool inPool)
        {
            IsInPool = inPool;
        }
    }
}
