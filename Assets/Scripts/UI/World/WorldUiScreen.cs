using AttackSkill.CameraSystem;
using AttackSkill.Core;
using UnityEngine;

namespace AttackSkill.UI.World
{
    /// <summary>世界挂点 → 屏幕投影、遮挡检测。</summary>
    public static class WorldUiScreen
    {
        static readonly RaycastHit[] Hits = new RaycastHit[8];

        public static Camera ResolveRenderCamera(Camera cached = null)
        {
            if (cached != null && cached.isActiveAndEnabled)
            {
                return cached;
            }

            ThirdPersonCamera tpc = GameServices.ResolveCamera();
            if (tpc == null)
            {
                tpc = Object.FindObjectOfType<ThirdPersonCamera>();
            }

            if (tpc != null)
            {
                if (tpc.ControlledCamera != null && tpc.ControlledCamera.isActiveAndEnabled)
                {
                    return tpc.ControlledCamera;
                }

                var childCam = tpc.GetComponentInChildren<Camera>(true);
                if (childCam != null && childCam.isActiveAndEnabled)
                {
                    return childCam;
                }
            }

            if (Camera.main != null && Camera.main.isActiveAndEnabled)
            {
                return Camera.main;
            }

            return Object.FindObjectOfType<Camera>();
        }

        public static void PrepareOverlayItem(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            var rt = root.transform as RectTransform;
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
            }

            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
        }

        /// <summary>投影并写入 Overlay 位置；在相机后方返回 false。</summary>
        public static bool TrySetOverlayFromWorld(
            RectTransform rt,
            Camera cam,
            Vector3 worldPos,
            float uniformScale = 1f)
        {
            if (rt == null)
            {
                return false;
            }

            cam = ResolveRenderCamera(cam);
            if (cam == null)
            {
                return false;
            }

            Vector3 screenPos = cam.WorldToScreenPoint(worldPos);
            if (screenPos.z <= 0.05f)
            {
                return false;
            }

            rt.position = screenPos;
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one * Mathf.Max(0.01f, uniformScale);
            return true;
        }

        public static bool IsOccluded(
            Camera cam,
            Vector3 worldPos,
            LayerMask mask,
            Transform ignoreA,
            Transform ignoreB = null)
        {
            cam = ResolveRenderCamera(cam);
            if (cam == null)
            {
                return false;
            }

            Vector3 origin = cam.transform.position;
            Vector3 delta = worldPos - origin;
            float dist = delta.magnitude;
            if (dist <= 0.08f)
            {
                return false;
            }

            Vector3 dir = delta / dist;
            float castDist = Mathf.Max(0.05f, dist - 0.08f);
            int count = Physics.RaycastNonAlloc(
                origin,
                dir,
                Hits,
                castDist,
                mask,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                Collider col = Hits[i].collider;
                if (col == null)
                {
                    continue;
                }

                Transform t = col.transform;
                if (ignoreA != null && (t == ignoreA || t.IsChildOf(ignoreA)))
                {
                    continue;
                }

                if (ignoreB != null && (t == ignoreB || t.IsChildOf(ignoreB)))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        /// <summary>CharacterController 顶部 + 0.4m。</summary>
        public static Vector3 ResolveEnemyHeadWorldPos(Enemy.EnemyAgent agent, Vector3 fallbackOffset)
        {
            if (agent == null)
            {
                return fallbackOffset;
            }

            CharacterController cc = agent.Controller ?? agent.GetComponent<CharacterController>();
            if (cc != null)
            {
                Vector3 topLocal = cc.center + Vector3.up * (cc.height * 0.5f);
                return agent.transform.TransformPoint(topLocal) + Vector3.up * 0.4f;
            }

            return agent.transform.position + fallbackOffset;
        }
    }
}
