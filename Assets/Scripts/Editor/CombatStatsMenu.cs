using AttackSkill.Character;
using AttackSkill.Combat;
using UnityEditor;
using UnityEngine;

namespace AttackSkill.EditorTools
{
    /// <summary>生成默认角色/怪物战斗属性 SO 到 Resources。</summary>
    public static class CombatStatsMenu
    {
        const string CharacterDir = "Assets/Resources/Combat/Stats/Characters";
        const string EnemyDir = "Assets/Resources/Combat/Stats/Enemies";

        [MenuItem("AttackSkill/Combat/Create Default Combat Stats")]
        public static void CreateDefaultCombatStats()
        {
            EnsureFolder("Assets/Resources");
            EnsureFolder("Assets/Resources/Combat");
            EnsureFolder("Assets/Resources/Combat/Stats");
            EnsureFolder(CharacterDir);
            EnsureFolder(EnemyDir);

            CreateCharacter("WandererFemale", "女漂泊者", PartyPortraitId.WandererFemale, CombatElement.Light);
            CreateCharacter("WandererMale", "男漂泊者", PartyPortraitId.WandererMale, CombatElement.Light);
            CreateCharacter("Qianxiao", "千咲", PartyPortraitId.Qianxiao, CombatElement.Dark);
            CreateCharacter("Coletta", "柯莱塔", PartyPortraitId.Coletta, CombatElement.Ice);
            CreateEnemy("Enemy_Thunder", "野外雷属性怪", CombatStatBlock.DefaultEnemyThunder());

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                "[CombatStats] 已生成默认属性表：\n" +
                $"{CharacterDir}（四角色同数值，元素不同）\n" +
                $"{EnemyDir}/Enemy_Thunder");
        }

        static void CreateCharacter(
            string fileName,
            string displayName,
            PartyPortraitId portraitId,
            CombatElement element)
        {
            string path = $"{CharacterDir}/{fileName}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<CharacterCombatStatsDefinition>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<CharacterCombatStatsDefinition>();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.displayName = displayName;
            asset.portraitId = portraitId;
            asset.stats = CombatStatBlock.DefaultCharacter(element);
            asset.skillECooldown = 5f;
            asset.skillRCooldown = 10f;
            EditorUtility.SetDirty(asset);
        }

        static void CreateEnemy(string fileName, string displayName, CombatStatBlock stats)
        {
            string path = $"{EnemyDir}/{fileName}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<EnemyCombatStatsDefinition>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<EnemyCombatStatsDefinition>();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.displayName = displayName;
            asset.stats = stats;
            EditorUtility.SetDirty(asset);
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
