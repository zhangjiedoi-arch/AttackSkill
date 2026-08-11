#if UNITY_EDITOR
using AttackSkill.Character;
using AttackSkill.Combat;
using AttackSkill.Enemy;
using UnityEditor;
using UnityEngine;

namespace AttackSkill.EditorTools
{
    public static class SkillHitProfileMenu
    {
        const string CombatFolder = "Assets/Resources/Combat";
        const string PlayerEPath = CombatFolder + "/SkillHit_Player_E.asset";
        const string EnemyBasicPath = CombatFolder + "/SkillHit_Enemy_Basic.asset";
        const string SettingsPath = "Assets/Resources/CharacterRuntimeSettings.asset";
        const string EnemyDefPath = "Assets/ScriptableObjects/Enemy/EnemyDefinition_Wild.asset";
        const string SnowPath = "Assets/Prefabs/VFX/Snow hit.prefab";
        const string AoePath = "Assets/Prefabs/VFX/Ground AOE explosion.prefab";
        const string FatKickRPath = "Assets/Audio/FatKick_R.wav";
        const string FatKickLPath = "Assets/Audio/FatKick_L.wav";
        const string RootLandPath = "Assets/Audio/Hit_Root_Land.wav";

        [MenuItem("AttackSkill/Combat/Create Default Skill Hit Profiles")]
        public static void CreateDefaultProfiles()
        {
            EnsureFolder("Assets/Resources");
            EnsureFolder(CombatFolder);

            var snow = AssetDatabase.LoadAssetAtPath<GameObject>(SnowPath);
            var aoe = AssetDatabase.LoadAssetAtPath<GameObject>(AoePath);
            var kickR = AssetDatabase.LoadAssetAtPath<AudioClip>(FatKickRPath);
            var kickL = AssetDatabase.LoadAssetAtPath<AudioClip>(FatKickLPath);
            var rootLand = AssetDatabase.LoadAssetAtPath<AudioClip>(RootLandPath);

            var player = LoadOrCreateProfile(PlayerEPath);
            player.segments = new[]
            {
                Make("chest_r", HitSocketId.Hit_Chest_R, HitShapeType.Sphere, 1f, 0f, 28f, 1.2f, snow, kickR),
                Make("chest_l", HitSocketId.Hit_Chest_L, HitShapeType.Sphere, 1f, 0f, 28f, 1.2f, snow, kickL),
                Make("root_aoe", HitSocketId.Hit_Root, HitShapeType.Cylinder, 2f, 0.5f, 55f, 3f, aoe, rootLand),
            };
            EditorUtility.SetDirty(player);

            var enemy = LoadOrCreateProfile(EnemyBasicPath);
            enemy.segments = new[]
            {
                Make("enemy_chest_r", HitSocketId.Enemy_Hit_Chest_R, HitShapeType.Sphere, 1.5f, 0f, 12f, 1.2f, null, null),
            };
            EditorUtility.SetDirty(enemy);

            var settings = AssetDatabase.LoadAssetAtPath<CharacterRuntimeSettings>(SettingsPath);
            if (settings != null)
            {
                settings.playerSkillHitProfile = player;
                if (settings.snowHitVfxPrefab == null)
                {
                    settings.snowHitVfxPrefab = snow;
                }

                if (settings.groundAoeExplosionVfxPrefab == null)
                {
                    settings.groundAoeExplosionVfxPrefab = aoe;
                }

                settings.skillHitFatKickR = kickR;
                settings.skillHitFatKickL = kickL;
                settings.skillHitRootLand = rootLand;
                EditorUtility.SetDirty(settings);
            }

            var enemyDef = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(EnemyDefPath);
            if (enemyDef != null)
            {
                enemyDef.skillHitProfile = enemy;
                EditorUtility.SetDirty(enemyDef);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = player;
            Debug.Log(
                $"[SkillHitProfile] 已生成/更新：\n- {PlayerEPath}\n- {EnemyBasicPath}\n并写入 RuntimeSettings / EnemyDefinition_Wild。");
        }

        static SkillHitProfile LoadOrCreateProfile(string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<SkillHitProfile>(path);
            if (existing != null)
            {
                return existing;
            }

            var created = ScriptableObject.CreateInstance<SkillHitProfile>();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        static SkillHitSegment Make(
            string id,
            HitSocketId socket,
            HitShapeType shape,
            float radius,
            float height,
            float damage,
            float knockback,
            GameObject vfx,
            AudioClip sfx)
        {
            return new SkillHitSegment
            {
                id = id,
                socket = socket,
                shape = shape,
                radius = radius,
                height = height,
                damage = damage,
                knockback = knockback,
                vfxPrefab = vfx,
                vfxLife = 2.5f,
                sfxClip = sfx,
                sfxVolume = 1f,
            };
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

            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
