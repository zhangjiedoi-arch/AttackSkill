using AttackSkill.Enemy;
using UnityEditor;
using UnityEngine;

namespace AttackSkill.Editor
{
    public static class EnemySetupMenu
    {
        const string DataFolder = "Assets/ScriptableObjects/Enemy";

        [MenuItem("GameObject/AttackSkill/创建野外刷怪组", false, 20)]
        static void CreateSpawnGroup()
        {
            EnsureFolder("Assets/ScriptableObjects");
            EnsureFolder(DataFolder);

            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            def.displayName = "Wild Grunt";
            def.maxHp = 80f;
            string defPath = AssetDatabase.GenerateUniqueAssetPath(DataFolder + "/EnemyDefinition_Wild.asset");
            AssetDatabase.CreateAsset(def, defPath);

            var groupDef = ScriptableObject.CreateInstance<SpawnGroupDefinition>();
            groupDef.slots = new[]
            {
                new SpawnSlot { definition = def, localOffset = Vector3.zero },
                new SpawnSlot { definition = def, localOffset = new Vector3(2f, 0f, 0f) },
                new SpawnSlot { definition = def, localOffset = new Vector3(-2f, 0f, 1f) }
            };
            string groupPath = AssetDatabase.GenerateUniqueAssetPath(DataFolder + "/SpawnGroup_Wild.asset");
            AssetDatabase.CreateAsset(groupDef, groupPath);

            // 简易敌人 Prefab：根上 CC + Health；子物体 Hurtbox 供玩家 Overlap 命中
            var enemyGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            enemyGo.name = "Enemy_WildGrunt";
            Object.DestroyImmediate(enemyGo.GetComponent<CapsuleCollider>());
            var cc = enemyGo.AddComponent<CharacterController>();
            cc.center = new Vector3(0f, 1f, 0f);
            cc.height = 2f;
            cc.radius = 0.4f;

            var hurtGo = new GameObject("Hurtbox");
            hurtGo.transform.SetParent(enemyGo.transform, false);
            var hurtCol = hurtGo.AddComponent<CapsuleCollider>();
            hurtCol.isTrigger = true;
            hurtCol.center = new Vector3(0f, 1f, 0f);
            hurtCol.height = 2f;
            hurtCol.radius = 0.45f;

            enemyGo.AddComponent<AttackSkill.Combat.Health>();
            enemyGo.AddComponent<EnemyAgent>();
            int enemyLayer = AttackSkill.Combat.CombatLayers.EnemyLayer;
            if (enemyLayer >= 0)
            {
                AttackSkill.Combat.CombatLayers.ApplyLayerRecursively(enemyGo, enemyLayer);
            }

            EnsureFolder("Assets/Prefabs");
            string prefabPath = AssetDatabase.GenerateUniqueAssetPath("Assets/Prefabs/Enemy_WildGrunt.prefab");
            var prefab = PrefabUtility.SaveAsPrefabAsset(enemyGo, prefabPath);
            Object.DestroyImmediate(enemyGo);

            def.prefab = prefab;
            EditorUtility.SetDirty(def);

            var groupGo = new GameObject("EnemySpawnGroup");
            if (Selection.activeTransform != null)
            {
                groupGo.transform.position = Selection.activeTransform.position;
            }

            var group = groupGo.AddComponent<EnemySpawnGroup>();
            var so = new SerializedObject(group);
            so.FindProperty("definition").objectReferenceValue = groupDef;
            // 菜单已手动建点，运行时不必再重建
            so.FindProperty("buildPointsFromDefinition").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();

            for (int i = 0; i < groupDef.slots.Length; i++)
            {
                SpawnSlot slot = groupDef.slots[i];
                var pointGo = new GameObject($"SpawnPoint_{i}");
                Undo.RegisterCreatedObjectUndo(pointGo, "SpawnPoint");
                pointGo.transform.SetParent(groupGo.transform, false);
                pointGo.transform.localPosition = slot.localOffset;
                pointGo.transform.localRotation = Quaternion.Euler(slot.localEuler);
                var point = pointGo.AddComponent<EnemySpawnPoint>();
                point.Configure(slot.definition, groupDef);
            }

            Undo.RegisterCreatedObjectUndo(groupGo, "Create Enemy Spawn Group");
            Selection.activeGameObject = groupGo;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"已创建刷怪组 + 定义：{groupPath}，敌人 Prefab：{prefabPath}。走近激活半径即可刷怪。");
        }

        [MenuItem("Assets/Create/AttackSkill/Enemy Definition")]
        static void CreateEnemyDefinition()
        {
            EnsureFolder("Assets/ScriptableObjects");
            EnsureFolder(DataFolder);
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            string path = AssetDatabase.GenerateUniqueAssetPath(DataFolder + "/EnemyDefinition.asset");
            AssetDatabase.CreateAsset(def, path);
            Selection.activeObject = def;
            AssetDatabase.SaveAssets();
        }

        [MenuItem("工具/敌人/重建死亡特效材质", false, 40)]
        static void RebuildDeathFxMaterials()
        {
            EnsureFolder("Assets/Resources");
            EnsureFolder("Assets/Resources/Enemy");

            var goldShader = Shader.Find(EnemyDeathVisualUtil.GoldShaderName);
            var dissolveShader = Shader.Find(EnemyDeathVisualUtil.DissolveShaderName);
            if (goldShader == null || dissolveShader == null)
            {
                Debug.LogError(
                    $"死亡 Shader 未编译成功。Gold={goldShader != null}, Dissolve={dissolveShader != null}。" +
                    "请打开 Console 看 Shader 报错，修好后再点本菜单。");
                return;
            }

            WriteOrReplaceMaterial(
                "Assets/Resources/Enemy/Mat_EnemyDeathGold.mat",
                goldShader,
                mat =>
                {
                    mat.SetColor("_Color", new Color(0.92f, 0.72f, 0.28f, 1f));
                    mat.SetColor("_RimColor", new Color(1f, 0.88f, 0.5f, 1f));
                    mat.SetFloat("_RimPower", 3.2f);
                    mat.SetFloat("_RimIntensity", 0.85f);
                    mat.SetFloat("_Emission", 0.28f);
                    mat.SetFloat("_Alpha", 0.26f);
                    mat.renderQueue = 3000;
                });

            WriteOrReplaceMaterial(
                "Assets/Resources/Enemy/Mat_EnemyDeathDissolve.mat",
                dissolveShader,
                mat =>
                {
                    mat.SetColor("_Color", new Color(1f, 0.82f, 0.35f, 1f));
                    mat.SetColor("_EdgeColor", new Color(1f, 0.9f, 0.4f, 1f));
                    mat.SetFloat("_Dissolve", 0f);
                    mat.SetFloat("_EdgeWidth", 0.08f);
                    mat.SetFloat("_NoiseScale", 3.5f);
                    mat.SetFloat("_HeightBias", 0.35f);
                    mat.SetFloat("_Emission", 2.5f);
                    mat.renderQueue = 2450;
                });

            const string refsPath = "Assets/Resources/Enemy/EnemyDeathShaderRefs.asset";
            var refs = AssetDatabase.LoadAssetAtPath<EnemyDeathShaderRefs>(refsPath);
            if (refs == null)
            {
                refs = ScriptableObject.CreateInstance<EnemyDeathShaderRefs>();
                AssetDatabase.CreateAsset(refs, refsPath);
            }

            refs.gold = goldShader;
            refs.dissolve = dissolveShader;
            EditorUtility.SetDirty(refs);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = refs;
            Debug.Log(
                "已重建死亡金透/飘散材质与 EnemyDeathShaderRefs。" +
                "若打包后仍粉红，请确认 Project Settings → Graphics → Always Included Shaders 含 DeathGold / DeathDissolve。");
        }

        static void WriteOrReplaceMaterial(string path, Shader shader, System.Action<Material> configure)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = shader;
            }

            configure?.Invoke(mat);
            EditorUtility.SetDirty(mat);
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
