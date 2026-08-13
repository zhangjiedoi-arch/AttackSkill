using AttackSkill.Character.HSM;
using UnityEngine;

namespace AttackSkill.Character.Exploration
{
    /// <summary>
    /// 骑摩托时切换 CharacterController 体型；center.y 固定为骑乘值，退出后还原。
    /// </summary>
    public static class MotorcycleColliderFit
    {
        public struct StandSnapshot
        {
            public bool Valid;
            public float Height;
            public float Radius;
            public float StepOffset;
            public float SkinWidth;
            public Vector3 Center;
        }

        public static StandSnapshot Capture(CharacterController cc)
        {
            var snap = new StandSnapshot();
            if (cc == null)
            {
                return snap;
            }

            snap.Valid = true;
            snap.Height = cc.height;
            snap.Radius = cc.radius;
            snap.StepOffset = cc.stepOffset;
            snap.SkinWidth = cc.skinWidth;
            snap.Center = cc.center;
            return snap;
        }

        public static void ApplyBikeShape(
            CharacterController cc,
            CharacterMotorSettings settings,
            ref StandSnapshot stand)
        {
            if (cc == null || settings == null)
            {
                return;
            }

            if (!stand.Valid)
            {
                stand = Capture(cc);
            }

            float worldH = Mathf.Max(0.6f, settings.BikeControllerWorldHeight);
            float worldR = Mathf.Max(0.2f, settings.BikeControllerWorldRadius);
            float worldStep = Mathf.Max(0.05f, settings.BikeControllerWorldStep);
            float centerY = settings.BikeControllerCenterY;

            Vector3 lossy = cc.transform.lossyScale;
            float sx = Mathf.Max(0.0001f, Mathf.Abs(lossy.x));
            float sy = Mathf.Max(0.0001f, Mathf.Abs(lossy.y));
            float sz = Mathf.Max(0.0001f, Mathf.Abs(lossy.z));
            float sRadius = Mathf.Max(sx, sz);

            bool wasEnabled = cc.enabled;
            cc.enabled = false;

            cc.height = worldH / sy;
            cc.radius = worldR / sRadius;
            cc.center = new Vector3(0f, centerY, 0f);
            cc.skinWidth = Mathf.Max(0.01f, stand.Valid ? stand.SkinWidth : 0.08f);

            float maxStep = worldH + worldR * 2f;
            cc.stepOffset = Mathf.Min(worldStep, maxStep - 0.01f);

            cc.enabled = wasEnabled;
            Physics.SyncTransforms();
        }

        public static void RestoreStandShape(CharacterController cc, ref StandSnapshot stand)
        {
            if (!stand.Valid || cc == null)
            {
                stand = default;
                return;
            }

            bool wasEnabled = cc.enabled;
            cc.enabled = false;
            cc.height = stand.Height;
            cc.radius = stand.Radius;
            cc.center = stand.Center;
            cc.stepOffset = stand.StepOffset;
            cc.skinWidth = stand.SkinWidth;
            cc.enabled = wasEnabled;
            Physics.SyncTransforms();

            stand = default;
        }
    }
}
