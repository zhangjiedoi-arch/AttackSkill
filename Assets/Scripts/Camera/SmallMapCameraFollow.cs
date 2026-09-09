using UnityEngine;
using AttackSkill.Character;
using AttackSkill.Enemy;

namespace AttackSkill.CameraSystem
{
    /// <summary>
    /// 小地图俯视相机：硬跟随当前操控角色，固定在其上方（默认 20m），始终朝下。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(320)]
    public class SmallMapCameraFollow : MonoBehaviour
    {
        [SerializeField] Transform followTarget;
        [SerializeField] bool autoFindPlayer = true;
        [Tooltip("相对跟随目标原点（角色根）向上的高度。")]
        [SerializeField] float heightOffset = 20f;
        [Tooltip("勾选后小地图随角色朝向旋转；关闭则北向上。")]
        [SerializeField] bool rotateWithPlayer;

        public Transform FollowTarget
        {
            get => followTarget;
            set => followTarget = value;
        }

        void LateUpdate()
        {
            Transform target = ResolveTarget();
            if (target == null)
            {
                return;
            }

            transform.position = target.position + Vector3.up * heightOffset;
            if (rotateWithPlayer)
            {
                float yaw = target.eulerAngles.y;
                transform.rotation = Quaternion.Euler(90f, yaw, 0f);
            }
            else
            {
                transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            }
        }

        Transform ResolveTarget()
        {
            if (followTarget != null)
            {
                return followTarget;
            }

            if (!autoFindPlayer)
            {
                return null;
            }

            var party = PartyController.Instance;
            if (party != null && party.Active != null)
            {
                followTarget = party.Active.transform;
                return followTarget;
            }

            return PlayerTargetLocator.GetActivePlayerTransform();
        }
    }
}
