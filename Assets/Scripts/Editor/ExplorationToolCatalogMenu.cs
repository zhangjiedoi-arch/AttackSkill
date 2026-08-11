#if UNITY_EDITOR
using AttackSkill.Character.Exploration;
using UnityEditor;
using UnityEngine;

namespace AttackSkill.EditorTools
{
    /// <summary>生成默认探索工具 SO 到 Resources。</summary>
    public static class ExplorationToolCatalogMenu
    {
        const string ResourcesDir = "Assets/Resources";
        const string ToolsDir = "Assets/Resources/ExplorationTools";
        const string CatalogPath = "Assets/Resources/ExplorationToolCatalog.asset";

        [MenuItem("AttackSkill/Exploration/Generate Default Tool Catalog")]
        public static void Generate()
        {
            EnsureFolder("Assets", "Resources");
            EnsureFolder("Assets/Resources", "ExplorationTools");

            var slots = new ExplorationToolDefinition[ExplorationToolCatalog.SlotCount];
            slots[0] = CreateOrLoadDef("recon", "skill_wheel_1", ExplorationToolKind.Stub);
            slots[1] = CreateOrLoadDef("item_detector", "skill_wheel_2", ExplorationToolKind.Stub);
            slots[2] = CreateOrLoadDef("motorcycle", "skill_wheel_3", ExplorationToolKind.Motorcycle, requiresGround: true);
            slots[3] = CreateOrLoadDef("instant_camera", "skill_wheel_4", ExplorationToolKind.Stub);
            slots[4] = CreateOrLoadDef("imaging", "skill_wheel_5", ExplorationToolKind.Stub);
            slots[5] = CreateOrLoadDef("camera", "skill_wheel_6", ExplorationToolKind.Stub);
            slots[6] = CreateOrLoadDef("wing_flight", "skill_wheel_7", ExplorationToolKind.WingFlight);
            slots[7] = CreateOrLoadDef("sword_flight", "skill_wheel_8", ExplorationToolKind.SwordFlight);

            var catalog = AssetDatabase.LoadAssetAtPath<ExplorationToolCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ExplorationToolCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.slots = slots;
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = catalog;
            Debug.Log($"[ExplorationToolCatalog] 已生成/更新：{CatalogPath}");
        }

        static ExplorationToolDefinition CreateOrLoadDef(
            string id,
            string nameKey,
            ExplorationToolKind kind,
            bool requiresGround = false)
        {
            string path = $"{ToolsDir}/{id}.asset";
            var def = AssetDatabase.LoadAssetAtPath<ExplorationToolDefinition>(path);
            if (def == null)
            {
                def = ScriptableObject.CreateInstance<ExplorationToolDefinition>();
                AssetDatabase.CreateAsset(def, path);
            }

            def.Id = id;
            def.NameKey = nameKey;
            def.Kind = kind;
            def.RequiresGroundToActivate = requiresGround;
            def.BlocksSkillWheelWhenActive = kind == ExplorationToolKind.WingFlight ||
                                             kind == ExplorationToolKind.SwordFlight ||
                                             kind == ExplorationToolKind.Motorcycle;
            EditorUtility.SetDirty(def);
            return def;
        }

        static void EnsureFolder(string parent, string name)
        {
            string path = $"{parent}/{name}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }
    }
}
#endif
