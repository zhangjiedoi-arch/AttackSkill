using System;
using System.Collections.Generic;
using UnityEngine;

namespace AttackSkill.Enemy
{
    /// <summary>肉鸽刷怪：按角色等级解锁敌人池。</summary>
    [CreateAssetMenu(menuName = "AttackSkill/Rouge/Enemy Spawn Catalog", fileName = "RougeEnemySpawnCatalog")]
    public class RougeEnemySpawnCatalog : ScriptableObject
    {
        public const string ResourcesPath = "Rouge/RougeEnemySpawnCatalog";

        [Serializable]
        public class Entry
        {
            public EnemyDefinition definition;
            [Tooltip("角色达到该等级后才可刷出")]
            public int minLevel = 1;
        }

        public Entry[] entries;

        static RougeEnemySpawnCatalog _cached;
        static readonly List<EnemyDefinition> Scratch = new List<EnemyDefinition>(16);

        public static RougeEnemySpawnCatalog Get()
        {
            if (_cached == null)
            {
                _cached = Resources.Load<RougeEnemySpawnCatalog>(ResourcesPath);
            }

            return _cached;
        }

        /// <summary>收集角色等级可用的敌人定义（均匀随机池）。</summary>
        public static List<EnemyDefinition> CollectEligible(int playerLevel, List<EnemyDefinition> buffer = null)
        {
            if (buffer == null)
            {
                buffer = Scratch;
            }

            buffer.Clear();
            EnsureBuilt();
            var catalog = Get();
            if (catalog?.entries == null)
            {
                return buffer;
            }

            int lv = Mathf.Max(1, playerLevel);
            for (int i = 0; i < catalog.entries.Length; i++)
            {
                var e = catalog.entries[i];
                if (e == null || e.definition == null || e.definition.prefab == null)
                {
                    continue;
                }

                if (lv >= Mathf.Max(1, e.minLevel))
                {
                    buffer.Add(e.definition);
                }
            }

            return buffer;
        }

        /// <summary>编辑器 / 运行时：按约定表组装（Resources 资产优先，否则按路径加载）。</summary>
        public static void EnsureBuilt()
        {
            var catalog = Get();
            if (catalog != null && catalog.entries != null && catalog.entries.Length > 0)
            {
                bool any = false;
                for (int i = 0; i < catalog.entries.Length; i++)
                {
                    if (catalog.entries[i]?.definition != null)
                    {
                        any = true;
                        break;
                    }
                }

                if (any)
                {
                    return;
                }
            }

#if UNITY_EDITOR
            BuildDefaultCatalogAsset();
#endif
        }

#if UNITY_EDITOR
        public static readonly (string assetName, int minLevel)[] DefaultUnlockTable =
        {
            ("云海妖精", 1),
            ("铲子布偶", 1),
            ("流放者女", 1),
            ("流放者男", 1),
            ("卡迪安特", 4),
            ("朔雷之麟", 5),
            ("荣耀狮像", 6),
            ("踏光兽", 7),
            ("鳞人", 8),
        };

        public static RougeEnemySpawnCatalog BuildDefaultCatalogAsset()
        {
            const string assetPath = "Assets/Resources/Rouge/RougeEnemySpawnCatalog.asset";
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<RougeEnemySpawnCatalog>(assetPath);
            if (catalog == null)
            {
                catalog = CreateInstance<RougeEnemySpawnCatalog>();
                UnityEditor.AssetDatabase.CreateAsset(catalog, assetPath);
            }

            var list = new List<Entry>(DefaultUnlockTable.Length);
            for (int i = 0; i < DefaultUnlockTable.Length; i++)
            {
                string name = DefaultUnlockTable[i].assetName;
                int min = DefaultUnlockTable[i].minLevel;
                var def = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyDefinition>(
                    $"Assets/ScriptableObjects/Enemy/{name}.asset");
                if (def == null)
                {
                    Debug.LogWarning($"[RougeEnemySpawn] 缺少 EnemyDefinition：{name}");
                    continue;
                }

                list.Add(new Entry { definition = def, minLevel = min });
            }

            catalog.entries = list.ToArray();
            UnityEditor.EditorUtility.SetDirty(catalog);
            UnityEditor.AssetDatabase.SaveAssets();
            _cached = catalog;
            return catalog;
        }
#endif
    }
}
