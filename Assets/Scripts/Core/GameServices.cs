using AttackSkill.CameraSystem;
using AttackSkill.Character;
using AttackSkill.UI;
using UnityEngine;

namespace AttackSkill.Core
{
    /// <summary>
    /// 场景服务注册表：替代到处 FindObjectOfType。
    /// 由各服务在 Awake 注册、OnDestroy 注销。
    /// </summary>
    public static class GameServices
    {
        public static OpenSceneFlowController OpenSceneFlow { get; private set; }
        public static ThirdPersonCamera Camera { get; private set; }

        public static PartyController Party => PartyController.Instance;
        public static UIManager UI => UIManager.Instance;

        public static void Register(OpenSceneFlowController flow)
        {
            if (flow == null)
            {
                return;
            }

            if (OpenSceneFlow != null && OpenSceneFlow != flow)
            {
                Debug.LogWarning("[GameServices] OpenSceneFlow 被替换。", flow);
            }

            OpenSceneFlow = flow;
        }

        public static void Unregister(OpenSceneFlowController flow)
        {
            if (OpenSceneFlow == flow)
            {
                OpenSceneFlow = null;
            }
        }

        public static void Register(ThirdPersonCamera camera)
        {
            if (camera == null)
            {
                return;
            }

            if (Camera != null && Camera != camera)
            {
                Debug.LogWarning("[GameServices] ThirdPersonCamera 被替换。", camera);
            }

            Camera = camera;
        }

        public static void Unregister(ThirdPersonCamera camera)
        {
            if (Camera == camera)
            {
                Camera = null;
            }
        }

        public static ThirdPersonCamera ResolveCamera(ThirdPersonCamera preferred = null)
        {
            if (preferred != null)
            {
                return preferred;
            }

            return Camera;
        }
    }
}
