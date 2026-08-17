#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace AttackSkill.Enemy
{
    public static class RougeEnemySpawnCatalogMenu
    {
        [MenuItem("工具/Rouge/重建肉鸽刷怪等级表", false, 50)]
        public static void Rebuild()
        {
            var catalog = RougeEnemySpawnCatalog.BuildDefaultCatalogAsset();
            int n = catalog != null && catalog.entries != null ? catalog.entries.Length : 0;
            EditorUtility.DisplayDialog(
                "Rouge Enemy Spawn",
                $"已写入 Resources/Rouge/RougeEnemySpawnCatalog（{n} 条）。\n" +
                "1–3：云海妖精/铲子布偶/流放者女/流放者男\n" +
                "4：+卡迪安特  5：+朔雷之麟  6：+荣耀狮像\n" +
                "7：+踏光兽  8：+鳞人",
                "OK");
        }
    }
}
#endif
