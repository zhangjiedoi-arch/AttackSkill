using System.Collections.Generic;
using UnityEngine;

namespace AttackSkill.Rouge
{
    public static class RougeSkillRoller
    {
        public static List<RougePassiveDefData> RollThree(int playerLevel)
        {
            RougeCatalog.EnsureLoaded();
            var result = new List<RougePassiveDefData>(3);
            var used = new HashSet<string>();

            for (int i = 0; i < 3; i++)
            {
                var rarity = RollRarity(playerLevel);
                var pick = PickOne(rarity, used) ?? PickAny(used);
                if (pick == null)
                {
                    break;
                }

                used.Add(pick.id);
                result.Add(pick);
            }

            return result;
        }

        static RougeRarity RollRarity(int level)
        {
            var w = RougeCatalog.Passives?.rarityWeights ?? new RougeRarityWeights();
            int c, r, e;
            if (level >= 5)
            {
                c = w.commonFromLevel5;
                r = w.rareFromLevel5;
                e = w.epicFromLevel5;
            }
            else
            {
                c = w.common;
                r = w.rare;
                e = w.epic;
            }

            int total = Mathf.Max(1, c + r + e);
            int roll = Random.Range(0, total);
            if (roll < c)
            {
                return RougeRarity.Common;
            }

            if (roll < c + r)
            {
                return RougeRarity.Rare;
            }

            return RougeRarity.Epic;
        }

        static RougePassiveDefData PickOne(RougeRarity rarity, HashSet<string> used)
        {
            var all = RougeCatalog.Passives?.passives;
            if (all == null)
            {
                return null;
            }

            var pool = new List<RougePassiveDefData>(8);
            for (int i = 0; i < all.Length; i++)
            {
                var p = all[i];
                if (p == null || string.IsNullOrEmpty(p.id) || used.Contains(p.id))
                {
                    continue;
                }

                if (RougeCatalog.ParseRarity(p.rarity) != rarity)
                {
                    continue;
                }

                int stack = PartyRougeProgress.GetStack(p.id);
                if (stack >= Mathf.Max(1, p.maxStack))
                {
                    continue;
                }

                pool.Add(p);
            }

            if (pool.Count == 0)
            {
                return null;
            }

            return pool[Random.Range(0, pool.Count)];
        }

        static RougePassiveDefData PickAny(HashSet<string> used)
        {
            var all = RougeCatalog.Passives?.passives;
            if (all == null)
            {
                return null;
            }

            var pool = new List<RougePassiveDefData>(8);
            for (int i = 0; i < all.Length; i++)
            {
                var p = all[i];
                if (p == null || string.IsNullOrEmpty(p.id) || used.Contains(p.id))
                {
                    continue;
                }

                int stack = PartyRougeProgress.GetStack(p.id);
                if (stack >= Mathf.Max(1, p.maxStack))
                {
                    continue;
                }

                pool.Add(p);
            }

            if (pool.Count == 0)
            {
                return null;
            }

            return pool[Random.Range(0, pool.Count)];
        }
    }
}
