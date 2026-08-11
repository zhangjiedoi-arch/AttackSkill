using AttackSkill.CameraSystem;
using AttackSkill.Character.HSM;
using UnityEditor;
using UnityEngine;

namespace AttackSkill.Editor
{
    public static class ThirdPersonCameraMenu
    {
        [MenuItem("GameObject/AttackSkill/第三人称相机 Rig", false, 10)]
        static void CreateThirdPersonCameraRig()
        {
            Transform target = Selection.activeTransform;

            var rig = ThirdPersonCamera.CreateRig(target);
            Undo.RegisterCreatedObjectUndo(rig.gameObject, "Create Third Person Camera Rig");

            if (target != null)
            {
                var character = target.GetComponentInParent<GenshinLikeCharacter>();
                if (character != null)
                {
                    var so = new SerializedObject(character);
                    var prop = so.FindProperty("cameraYaw");
                    if (prop != null)
                    {
                        prop.objectReferenceValue = rig.YawTransform;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
            }

            Selection.activeGameObject = rig.gameObject;
        }

        [MenuItem("GameObject/AttackSkill/第三人称相机 Rig", true)]
        static bool ValidateCreate()
        {
            return !Application.isPlaying;
        }
    }
}
