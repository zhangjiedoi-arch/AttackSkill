using UnityEngine;

namespace AttackSkill.Combat
{
    /// <summary>
    /// 场景内特效池根。存在时 <see cref="VfxObjectPool"/> 挂到本物体，不再新建 <c>[VfxObjectPool]</c>。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-200)]
    public sealed class VfxObjectPoolHost : MonoBehaviour
    {
        void Awake()
        {
            VfxObjectPool.BindSceneRoot(transform);
        }

        void OnDestroy()
        {
            VfxObjectPool.UnbindSceneRoot(transform);
        }
    }
}
