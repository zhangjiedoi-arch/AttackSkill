using System.Collections.Generic;

namespace AttackSkill.Combat
{
    /// <summary>
    /// 单次挥砍 / 命中窗口内的去重表（按战斗单位 InstanceId，见 HitResolver.ResolveDedupId）。
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

        public bool TryRegister(int targetUnitId)
        {
            return _hitIds.Add(targetUnitId);
        }

        public bool Contains(int targetUnitId) => _hitIds.Contains(targetUnitId);
    }
}
