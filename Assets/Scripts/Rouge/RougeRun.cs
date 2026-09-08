using System;
using System.Collections.Generic;
using AttackSkill.Combat;
using UnityEngine;

namespace AttackSkill.Rouge
{
    /// <summary>
    /// 一局肉鸽会话。由 <see cref="AttackSkill.Game.GameProgressController"/> Bind；
    /// 战斗公式通过 <see cref="Current"/> 读取。
    /// </summary>
    public sealed class RougeRun
    {
        int _level = 1;
        int _exp;
        readonly List<RougePassiveStack> _passives = new List<RougePassiveStack>(16);
        readonly Dictionary<string, float> _modSums = new Dictionary<string, float>(16);
        int _pendingLevelUps;
        bool _selectUiOpen;

        public static RougeRun Current { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Current = null;
        }

        public static RougeRun Ensure()
        {
            if (Current == null)
            {
                Current = new RougeRun();
            }

            return Current;
        }

        public static void Bind(RougeRun run)
        {
            Current = run ?? new RougeRun();
        }

        public static void Unbind(RougeRun run)
        {
            if (Current == run)
            {
                Current = null;
            }
        }

        public int Level => _level;
        public int Exp => _exp;
        public int PendingLevelUps => _pendingLevelUps;
        public IReadOnlyList<RougePassiveStack> Passives => _passives;

        public int ExpToNext
        {
            get
            {
                var table = RougeCatalog.Levels;
                if (table == null || table.expToNext == null || table.expToNext.Length == 0)
                {
                    return 30;
                }

                if (_level >= table.maxLevel)
                {
                    return 0;
                }

                int idx = Mathf.Clamp(_level - 1, 0, table.expToNext.Length - 1);
                return Mathf.Max(1, table.expToNext[idx]);
            }
        }

        public bool HasProgressBeyondFreshStart =>
            _level > 1 || _exp > 0 || _passives.Count > 0 || _pendingLevelUps > 0;

        public void ResetRun()
        {
            _level = 1;
            _exp = 0;
            _passives.Clear();
            _pendingLevelUps = 0;
            _selectUiOpen = false;
            RebuildModCache();
            RougePassiveEffects.OnRunReset();
            PartyRougeProgress.EmitChanged();
        }

        public RougeRunSave Capture()
        {
            var save = new RougeRunSave
            {
                level = Mathf.Max(1, _level),
                exp = Mathf.Max(0, _exp),
                pendingLevelUps = Mathf.Max(0, _pendingLevelUps),
                passives = _passives.Count > 0
                    ? new RougePassiveStack[_passives.Count]
                    : Array.Empty<RougePassiveStack>()
            };

            for (int i = 0; i < _passives.Count; i++)
            {
                var src = _passives[i];
                save.passives[i] = new RougePassiveStack
                {
                    id = src != null ? src.id : null,
                    stack = src != null ? src.stack : 0
                };
            }

            return save;
        }

        public void Restore(RougeRunSave save)
        {
            if (save == null)
            {
                ResetRun();
                return;
            }

            RougeCatalog.EnsureLoaded();
            var table = RougeCatalog.Levels;
            int maxLv = table != null ? Mathf.Max(1, table.maxLevel) : 15;

            _level = Mathf.Clamp(save.level, 1, maxLv);
            _exp = Mathf.Max(0, save.exp);
            _pendingLevelUps = Mathf.Max(0, save.pendingLevelUps);
            _selectUiOpen = false;
            _passives.Clear();

            if (save.passives != null)
            {
                for (int i = 0; i < save.passives.Length; i++)
                {
                    var src = save.passives[i];
                    if (src == null || string.IsNullOrEmpty(src.id))
                    {
                        continue;
                    }

                    var def = RougeCatalog.GetPassive(src.id);
                    if (def == null)
                    {
                        continue;
                    }

                    int max = Mathf.Max(1, def.maxStack);
                    int stack = Mathf.Clamp(src.stack, 1, max);
                    _passives.Add(new RougePassiveStack { id = src.id, stack = stack });
                }
            }

            RebuildModCache();
            RougePassiveEffects.NotifyChanged();
            CombatStats.RefreshAllHealthForRougeLevel();
            PartyRougeProgress.EmitChanged();
        }

        public void AddExp(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            RougeCatalog.EnsureLoaded();
            var table = RougeCatalog.Levels;
            int maxLv = table != null ? Mathf.Max(1, table.maxLevel) : 15;

            if (_level >= maxLv)
            {
                return;
            }

            _exp += amount;
            int gained = 0;
            while (_level < maxLv)
            {
                int need = ExpToNext;
                if (need <= 0 || _exp < need)
                {
                    break;
                }

                _exp -= need;
                _level++;
                gained++;
                PartyRougeProgress.EmitLeveledUp(_level);
            }

            if (gained > 0)
            {
                _pendingLevelUps += gained;
                RougePassiveEffects.ApplyAbyssPactToActiveParty();
                CombatStats.RefreshAllHealthForRougeLevel();
                TryOpenSkillSelect();
            }

            PartyRougeProgress.EmitChanged();
        }

        public int GetStack(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return 0;
            }

            for (int i = 0; i < _passives.Count; i++)
            {
                if (_passives[i].id == id)
                {
                    return _passives[i].stack;
                }
            }

            return 0;
        }

        public bool TryAddPassive(string id)
        {
            var def = RougeCatalog.GetPassive(id);
            if (def == null)
            {
                return false;
            }

            int max = Mathf.Max(1, def.maxStack);
            for (int i = 0; i < _passives.Count; i++)
            {
                if (_passives[i].id != id)
                {
                    continue;
                }

                if (_passives[i].stack >= max)
                {
                    return false;
                }

                _passives[i].stack++;
                RebuildModCache();
                PartyRougeProgress.EmitChanged();
                RougePassiveEffects.NotifyChanged();
                return true;
            }

            _passives.Add(new RougePassiveStack { id = id, stack = 1 });
            RebuildModCache();
            PartyRougeProgress.EmitChanged();
            RougePassiveEffects.NotifyChanged();
            return true;
        }

        public void ConsumePendingLevelUp()
        {
            if (_pendingLevelUps > 0)
            {
                _pendingLevelUps--;
            }

            _selectUiOpen = false;
        }

        public void MarkSelectUiClosedWithoutPick()
        {
            _selectUiOpen = false;
        }

        public void TryOpenSkillSelectIfPending()
        {
            TryOpenSkillSelect();
        }

        public void NotifySkillSelectOpened()
        {
            _selectUiOpen = true;
        }

        public void NotifySkillSelectOpenFailed()
        {
            _selectUiOpen = false;
        }

        public float SumMod(string modType)
        {
            if (string.IsNullOrEmpty(modType))
            {
                return 0f;
            }

            return _modSums.TryGetValue(modType, out float sum) ? sum : 0f;
        }

        void TryOpenSkillSelect()
        {
            if (_selectUiOpen || _pendingLevelUps <= 0)
            {
                return;
            }

            PartyRougeProgress.EmitSkillSelectRequested();
        }

        void RebuildModCache()
        {
            _modSums.Clear();
            for (int i = 0; i < _passives.Count; i++)
            {
                var stack = _passives[i];
                var def = RougeCatalog.GetPassive(stack.id);
                if (def?.mods == null)
                {
                    continue;
                }

                int n = Mathf.Max(0, stack.stack);
                if (n <= 0)
                {
                    continue;
                }

                for (int m = 0; m < def.mods.Length; m++)
                {
                    var mod = def.mods[m];
                    if (mod == null || string.IsNullOrEmpty(mod.type))
                    {
                        continue;
                    }

                    _modSums.TryGetValue(mod.type, out float cur);
                    _modSums[mod.type] = cur + mod.perStack * n;
                }
            }
        }
    }
}
