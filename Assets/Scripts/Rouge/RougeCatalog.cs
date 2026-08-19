using System;
using System.Collections.Generic;
using UnityEngine;

namespace AttackSkill.Rouge
{
    [Serializable]
    public class RougeLevelTableData
    {
        public int maxLevel = 15;
        public int[] expToNext;
    }

    [Serializable]
    public class RougeExpOrbConfig
    {
        public float dropChance = 0.55f;
        public int expAmount = 10;
        public float lifetime = 20f;
        public float pickRadius = 3f;
    }

    [Serializable]
    public class RougeRarityWeights
    {
        public int common = 70;
        public int rare = 25;
        public int epic = 5;
        public int commonFromLevel5 = 60;
        public int rareFromLevel5 = 30;
        public int epicFromLevel5 = 10;
    }

    [Serializable]
    public class RougePassiveModData
    {
        public string type;
        public float perStack;
    }

    [Serializable]
    public class RougePassiveDefData
    {
        public string id;
        public string rarity;
        public int maxStack = 1;
        public string nameKey;
        public string descKey;
        public RougePassiveModData[] mods;
    }

    [Serializable]
    public class RougePassiveTableData
    {
        public RougeExpOrbConfig expOrb = new RougeExpOrbConfig();
        public RougeRarityWeights rarityWeights = new RougeRarityWeights();
        public RougePassiveDefData[] passives;
    }

    public enum RougeRarity
    {
        Common = 0,
        Rare = 1,
        Epic = 2
    }

    public static class RougeCatalog
    {
        const string LevelPath = "Rouge/RougeLevelTable";
        const string PassivePath = "Rouge/RougePassiveTable";

        static RougeLevelTableData _levels;
        static RougePassiveTableData _passives;
        static Dictionary<string, RougePassiveDefData> _byId;

        public static RougeLevelTableData Levels
        {
            get
            {
                EnsureLoaded();
                return _levels;
            }
        }

        public static RougePassiveTableData Passives
        {
            get
            {
                EnsureLoaded();
                return _passives;
            }
        }

        public static RougeExpOrbConfig ExpOrb
        {
            get
            {
                EnsureLoaded();
                return _passives != null && _passives.expOrb != null
                    ? _passives.expOrb
                    : new RougeExpOrbConfig();
            }
        }

        public static void EnsureLoaded()
        {
            if (_levels != null && _passives != null && _byId != null)
            {
                return;
            }

            var levelAsset = Resources.Load<TextAsset>(LevelPath);
            _levels = levelAsset != null
                ? JsonUtility.FromJson<RougeLevelTableData>(levelAsset.text)
                : CreateDefaultLevels();

            var passiveAsset = Resources.Load<TextAsset>(PassivePath);
            _passives = passiveAsset != null
                ? JsonUtility.FromJson<RougePassiveTableData>(passiveAsset.text)
                : new RougePassiveTableData { passives = Array.Empty<RougePassiveDefData>() };

            RebuildPassiveIndex();
        }

        /// <summary>编辑器改表后可强制重载。</summary>
        public static void Reload()
        {
            _levels = null;
            _passives = null;
            _byId = null;
            EnsureLoaded();
        }

        static void RebuildPassiveIndex()
        {
            _byId = new Dictionary<string, RougePassiveDefData>(32);
            if (_passives?.passives == null)
            {
                return;
            }

            for (int i = 0; i < _passives.passives.Length; i++)
            {
                var p = _passives.passives[i];
                if (p != null && !string.IsNullOrEmpty(p.id))
                {
                    _byId[p.id] = p;
                }
            }
        }

        public static RougePassiveDefData GetPassive(string id)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(id) || _byId == null)
            {
                return null;
            }

            return _byId.TryGetValue(id, out var def) ? def : null;
        }

        public static RougeRarity ParseRarity(string rarity)
        {
            if (string.Equals(rarity, "rare", StringComparison.OrdinalIgnoreCase))
            {
                return RougeRarity.Rare;
            }

            if (string.Equals(rarity, "epic", StringComparison.OrdinalIgnoreCase))
            {
                return RougeRarity.Epic;
            }

            return RougeRarity.Common;
        }

        public static Color RarityColor(RougeRarity rarity)
        {
            switch (rarity)
            {
                case RougeRarity.Rare:
                    return new Color(0.25f, 0.55f, 1f, 0.92f);
                case RougeRarity.Epic:
                    return new Color(0.72f, 0.35f, 1f, 0.95f);
                default:
                    return new Color(0.55f, 0.55f, 0.55f, 0.9f);
            }
        }

        static RougeLevelTableData CreateDefaultLevels()
        {
            return new RougeLevelTableData
            {
                maxLevel = 15,
                expToNext = new[]
                {
                    30, 45, 60, 80, 100, 125, 150, 180, 220, 260, 300, 350, 400, 460
                }
            };
        }
    }
}
