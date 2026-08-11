using AttackSkill.Combat;
using UnityEditor;
using UnityEngine;

namespace AttackSkill.Editor
{
    public static class CombatSetupMenu
    {
        const string VfxFolder = "Assets/VFX/Slash";
        const string PrefabPath = VfxFolder + "/SlashArc.prefab";
        const string MatPath = VfxFolder + "/SlashAdditive.mat";

        [MenuItem("GameObject/AttackSkill/创建测试木桩(敌人)", false, 12)]
        static void CreateTrainingDummy()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "TrainingDummy";
            go.tag = "Untagged";
            int enemyLayer = CombatLayers.EnemyLayer;
            if (enemyLayer >= 0)
            {
                CombatLayers.ApplyLayerRecursively(go, enemyLayer);
            }

            if (Selection.activeTransform != null)
            {
                go.transform.position = Selection.activeTransform.position + Selection.activeTransform.forward * 2f + Vector3.up;
            }
            else
            {
                go.transform.position = Vector3.up;
            }

            var health = go.AddComponent<Health>();
            var so = new SerializedObject(health);
            so.FindProperty("maxHp").floatValue = 100f;
            so.FindProperty("currentHp").floatValue = 100f;
            so.FindProperty("destroyOnDeath").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();

            Undo.RegisterCreatedObjectUndo(go, "Create Training Dummy");
            Selection.activeGameObject = go;
            Debug.Log("已创建训练木桩（Enemy 层）。旧木桩若打不中，请改到 Enemy 层。");
        }

        [MenuItem("GameObject/AttackSkill/创建刀光特效 Prefab", false, 13)]
        [MenuItem("工具/战斗/创建刀光特效 Prefab", false, 20)]
        static void CreateSlashVfxPrefab()
        {
            EnsureFolder("Assets/VFX");
            EnsureFolder(VfxFolder);

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if (mat == null)
            {
                var shader = Shader.Find("AttackSkill/VFX/SlashAdditive");
                if (shader == null)
                {
                    Debug.LogError("找不到 Shader：AttackSkill/VFX/SlashAdditive，请先等编译完成再试。");
                    return;
                }

                mat = new Material(shader)
                {
                    name = "SlashAdditive",
                    hideFlags = HideFlags.None
                };
                mat.SetColor("_Color", new Color(1f, 0.82f, 0.35f, 1f));
                mat.SetFloat("_Intensity", 3.8f);
                mat.SetFloat("_CoreBoost", 2.8f);
                AssetDatabase.CreateAsset(mat, MatPath);
            }

            var go = new GameObject("SlashArc");
            var filter = go.AddComponent<MeshFilter>();
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            go.AddComponent<SlashArcVfx>();

            PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            Object.DestroyImmediate(go);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log($"已创建刀光 Prefab：{PrefabPath}");
        }

        [MenuItem("工具/战斗/把刀光挂到选中角色", false, 21)]
        static void AssignSlashToSelected()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                CreateSlashVfxPrefab();
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            }

            if (prefab == null)
            {
                Debug.LogError("刀光 Prefab 创建失败。");
                return;
            }

            int count = 0;
            foreach (var go in Selection.gameObjects)
            {
                var relays = go.GetComponentsInChildren<AttackHitRelay>(true);
                foreach (var relay in relays)
                {
                    var so = new SerializedObject(relay);
                    var swings = so.FindProperty("swings");
                    if (swings == null || !swings.isArray)
                    {
                        continue;
                    }

                    for (int i = 0; i < swings.arraySize; i++)
                    {
                        var slash = swings.GetArrayElementAtIndex(i).FindPropertyRelative("SlashVfxPrefab");
                        if (slash != null)
                        {
                            slash.objectReferenceValue = prefab;
                        }
                    }

                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(relay);
                    count++;
                }
            }

            if (count == 0)
            {
                Debug.LogWarning("选中物体上没有 AttackHitRelay。请先选角色（或含子物体带 AttackHitRelay 的对象）。");
            }
            else
            {
                Debug.Log($"已为 {count} 个 AttackHitRelay 的全部连段填入刀光 Prefab。");
            }
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent ?? "Assets", name);
        }
    }
}
