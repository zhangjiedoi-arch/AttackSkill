using System.Collections.Generic;
using UnityEngine;

namespace AttackSkill.UI
{
    /// <summary>场景动态标记登记。查询勿每帧 Find。</summary>
    public static class MapMarkerRegistry
    {
        static readonly List<MapMarkerBinder> Binders = new List<MapMarkerBinder>(16);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Binders.Clear();
        }

        public static IReadOnlyList<MapMarkerBinder> Live => Binders;

        public static void Register(MapMarkerBinder binder)
        {
            if (binder == null || Binders.Contains(binder))
            {
                return;
            }

            Binders.Add(binder);
        }

        public static void Unregister(MapMarkerBinder binder)
        {
            if (binder == null)
            {
                return;
            }

            Binders.Remove(binder);
        }
    }
}
