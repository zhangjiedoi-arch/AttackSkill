using System;
using System.Collections.Generic;
using UnityEngine;

namespace AttackSkill.Rouge
{
    [Serializable]
    public class RougePassiveStack
    {
        public string id;
        public int stack;
    }

    /// <summary>全队共享：等级、经验、永久被动。</summary>
    public static class PartyRougeProgress
    {
        static int _level = 1;
        static int _exp;
        static readonly List<RougePassiveStack> _passives = new List<RougePassiveStack>(16);
        static int _pendingLevelUps;
        static bool _selectUiOpen;

        public static event Action Changed;
        public static event Action<int> LeveledUp;
        /// <summary>需要弹出三选一 UI 时触发（由 UI 层订阅并 Open）。</summary>
        public static event Action SkillSelectRequested;

        public static int Level => _level;
        public static int Exp => _exp;
        public static int PendingLevelUps => _pendingLevelUps;
        public static IReadOnlyList<RougePassiveStack> Passives => _passives;

        public static int ExpToNext
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

        public static void ResetRun()
        {
            _level = 1;
            _exp = 0;
            _passives.Clear();
            _pendingLevelUps = 0;
            _selectUiOpen = false;
            Changed?.Invoke();
        }

        public static void AddExp(int amount)
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
                LeveledUp?.Invoke(_level);
            }

            if (gained > 0)
            {
                _pendingLevelUps += gained;
                TryOpenSkillSelect();
            }

            Changed?.Invoke();
        }

        public static int GetStack(string id)
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

        public static bool TryAddPassive(string id)
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
                Changed?.Invoke();
                RougePassiveEffects.NotifyChanged();
                return true;
            }

            _passives.Add(new RougePassiveStack { id = id, stack = 1 });
            Changed?.Invoke();
            RougePassiveEffects.NotifyChanged();
            return true;
        }

        public static void ConsumePendingLevelUp()
        {
            if (_pendingLevelUps > 0)
            {
                _pendingLevelUps--;
            }

            _selectUiOpen = false;
        }

        public static void MarkSelectUiClosedWithoutPick()
        {
            _selectUiOpen = false;
        }

        /// <summary>技能面板关闭后，若仍有待选升级则再打开。</summary>
        public static void TryOpenSkillSelectIfPending()
        {
            TryOpenSkillSelect();
        }

        /// <summary>UI 成功打开后调用，防止重复弹窗。</summary>
        public static void NotifySkillSelectOpened()
        {
            _selectUiOpen = true;
        }

        /// <summary>UI 打开失败时回滚。</summary>
        public static void NotifySkillSelectOpenFailed()
        {
            _selectUiOpen = false;
        }

        static void TryOpenSkillSelect()
        {
            if (_selectUiOpen || _pendingLevelUps <= 0)
            {
                return;
            }

            SkillSelectRequested?.Invoke();
        }

        public static float SumMod(string modType)
        {
            float sum = 0f;
            for (int i = 0; i < _passives.Count; i++)
            {
                var stack = _passives[i];
                var def = RougeCatalog.GetPassive(stack.id);
                if (def?.mods == null)
                {
                    continue;
                }

                for (int m = 0; m < def.mods.Length; m++)
                {
                    var mod = def.mods[m];
                    if (mod != null && mod.type == modType)
                    {
                        sum += mod.perStack * stack.stack;
                    }
                }
            }

            return sum;
        }
    }
}
