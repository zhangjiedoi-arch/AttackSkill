using System.Collections.Generic;

namespace AttackSkill.Combat
{
    /// <summary>
    /// 单次挥砍 / 命中窗口内的去重表（按目标 root InstanceId）。
    /// </summary>
    public sealed class HitSession
    {
        readonly HashSet<int> _hitIds = new HashSet<int>();

        public int HitCount => _hitIds.Count;

        public void Begin()
        {
            _hitIds.Clear();
        }

        public void Clear() => Begin();

        public bool TryRegister(int targetRootId)
        {
            return _hitIds.Add(targetRootId);
        }

        public bool Contains(int targetRootId) => _hitIds.Contains(targetRootId);
    }
}
