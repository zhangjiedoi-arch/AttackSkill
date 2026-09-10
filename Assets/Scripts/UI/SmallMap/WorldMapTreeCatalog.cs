using System;
using System.Collections.Generic;
using UnityEngine;

namespace AttackSkill.UI
{
    [Serializable]
    public class WorldMapSubRegionRow
    {
        public string id;
        public string nameKey;
        public string[] maps;
    }

    [Serializable]
    public class WorldMapRegionRow
    {
        public string id;
        public string nameKey;
        public string[] maps;
        public WorldMapSubRegionRow[] children;
    }

    [Serializable]
    public class WorldMapTreeTable
    {
        public WorldMapRegionRow[] regions;
    }

    /// <summary>大地图切图目录：大区 → 子区 → 具体烘焙图 id（beach / rouge）。</summary>
    public static class WorldMapTreeCatalog
    {
        const string Path = "SmallMap/WorldMapTreeTable";

        static WorldMapTreeTable _table;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _table = null;
        }

        public static IReadOnlyList<WorldMapRegionRow> Regions
        {
            get
            {
                EnsureLoaded();
                return _table != null && _table.regions != null
                    ? _table.regions
                    : Array.Empty<WorldMapRegionRow>();
            }
        }

        public static void EnsureLoaded()
        {
            if (_table != null)
            {
                return;
            }

            var asset = Resources.Load<TextAsset>(Path);
            if (asset != null)
            {
                _table = JsonUtility.FromJson<WorldMapTreeTable>(asset.text);
            }

            if (_table == null || _table.regions == null || _table.regions.Length == 0)
            {
                _table = CreateFallback();
            }
        }

        public static bool RegionHasChildren(WorldMapRegionRow row)
        {
            return row != null && row.children != null && row.children.Length > 0;
        }

        public static string[] MapsOf(WorldMapRegionRow region, WorldMapSubRegionRow sub)
        {
            if (sub != null && sub.maps != null && sub.maps.Length > 0)
            {
                return sub.maps;
            }

            if (region != null && region.maps != null)
            {
                return region.maps;
            }

            return Array.Empty<string>();
        }

        public static string TitleKeyForMap(string mapId)
        {
            return mapId == "rouge" ? "world_map_rouge" : "world_map_beach";
        }

        static WorldMapTreeTable CreateFallback()
        {
            return new WorldMapTreeTable
            {
                regions = new[]
                {
                    new WorldMapRegionRow
                    {
                        id = "qiqiu",
                        nameKey = "world_map_region_qiqiu",
                        maps = Array.Empty<string>(),
                        children = new[]
                        {
                            new WorldMapSubRegionRow
                            {
                                id = "black_shores",
                                nameKey = "world_map_region_black_shores",
                                maps = new[] { "beach", "rouge" }
                            }
                        }
                    }
                }
            };
        }
    }
}
