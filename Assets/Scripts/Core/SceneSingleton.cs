using UnityEngine;

namespace AttackSkill.Core
{
    /// <summary>
    /// 场景单例认领：重复实例销毁自身，可选 DontDestroyOnLoad。
    /// </summary>
    public static class SceneSingleton
    {
        /// <summary>
        /// 若已有其它实例则销毁 host 并返回 false；否则返回 true（由调用方赋值 Instance）。
        /// </summary>
        public static bool ShouldKeep(MonoBehaviour host, Object existingInstance)
        {
            if (host == null)
            {
                return false;
            }

            if (existingInstance != null && existingInstance != host)
            {
                Debug.LogWarning($"[{host.GetType().Name}] 已存在实例，销毁重复物体。", host);
                Object.Destroy(host.gameObject);
                return false;
            }

            return true;
        }

        public static void ApplyDontDestroyOnLoad(MonoBehaviour host, bool enabled)
        {
            if (enabled && host != null)
            {
                Object.DontDestroyOnLoad(host.gameObject);
            }
        }
    }
}
