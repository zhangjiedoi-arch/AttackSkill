using AttackSkill.CameraSystem;
using AttackSkill.Character;
using UnityEditor;
using UnityEngine;

namespace AttackSkill.Editor
{
    public static class PartyControllerMenu
    {
        [MenuItem("GameObject/AttackSkill/创建队伍切换器 PartyController", false, 14)]
        static void CreatePartyController()
        {
            var go = new GameObject("PartyController");
            go.transform.position = new Vector3(-497.5f, 0.1f, -21.5f);

            var party = go.AddComponent<PartyController>();
            var tpc = Object.FindObjectOfType<ThirdPersonCamera>();
            if (tpc != null)
            {
                var so = new SerializedObject(party);
                so.FindProperty("thirdPersonCamera").objectReferenceValue = tpc;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            Undo.RegisterCreatedObjectUndo(go, "Create PartyController");
            Selection.activeGameObject = go;
            Debug.Log(
                "已创建 PartyController。characterPrefabs 可填：\n" +
                "· Avatar Prefab（仅 CharacterAvatar，推荐）\n" +
                "· 或旧完整 Actor Prefab（已挂 GenshinLikeCharacter）\n" +
                "生成时由 CharacterRuntimeAssembler 装配玩法。Tab / 1-3 切人。");
        }
    }
}
