using System;
using System.Collections.Generic;
using AttackSkill.Combat;
using UnityEngine;

namespace AttackSkill.Rouge
{
    [Serializable]
    public class RougePassiveStack
    {
        public string id;
        public int stack;
    }

    /// <summary>一局肉鸽可序列化快照（写入 GameSaveData）。</summary>
    [Serializable]
    public class RougeRunSave
    {
        public int level = 1;
        public int exp;
        public int pendingLevelUps;
        public RougePassiveStack[] passives;
        public bool hasTeleported;
        public bool[] fallenSlots;
        /// <summary>获救倒计时剩余秒；&lt;0 表示未进肉鸽战斗计时。</summary>
        public float battleTimeRemaining = -1f;
    }

    /// <summary>全队共享：等级、经验、永久被动。</summary>
    public static class PartyRougeProgress
    {
        static int _level = 1;
        static int _exp;
        static readonly List<RougePassiveStack> _passives = new List<RougePassiveStack>(16);
        static readonly Dictionary<string, float> _modSums = new Dictionary<string, float>(16);
        static int _pendingLevelUps;
        static bool _selectUiOpen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _level = 1;
            _exp = 0;
            _passives.Clear();
            _modSums.Clear();
            _pendingLevelUps = 0;
            _selectUiOpen = false;
            Changed = null;
            LeveledUp = null;
            SkillSelectRequested = null;
        }

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
            RebuildModCache();
            RougePassiveEffects.OnRunReset();
            Changed?.Invoke();
        }

        public static RougeRunSave Capture()
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

        /// <summary>读档恢复。不弹三选一（由 HUD 打开后再 <see cref="TryOpenSkillSelectIfPending"/>）。</summary>
        public static void Restore(RougeRunSave save)
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
                RougePassiveEffects.ApplyAbyssPactToActiveParty();
                CombatStats.RefreshAllHealthForRougeLevel();
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
                RebuildModCache();
                Changed?.Invoke();
                RougePassiveEffects.NotifyChanged();
                return true;
            }

            _passives.Add(new RougePassiveStack { id = id, stack = 1 });
            RebuildModCache();
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
            if (string.IsNullOrEmpty(modType))
            {
                return 0f;
            }

            return _modSums.TryGetValue(modType, out float sum) ? sum : 0f;
        }

        static void RebuildModCache()
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
