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

    /// <summary>
    /// 肉鸽进度门面：转发给 <see cref="RougeRun.Current"/>，供战斗 / HUD 少改调用点。
    /// </summary>
    public static class PartyRougeProgress
    {
        static RougeRun Run => RougeRun.Ensure();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Changed = null;
            LeveledUp = null;
            SkillSelectRequested = null;
        }

        public static event Action Changed;
        public static event Action<int> LeveledUp;
        public static event Action SkillSelectRequested;

        public static int Level => Run.Level;
        public static int Exp => Run.Exp;
        public static int PendingLevelUps => Run.PendingLevelUps;
        public static IReadOnlyList<RougePassiveStack> Passives => Run.Passives;
        public static int ExpToNext => Run.ExpToNext;
        public static bool HasProgressBeyondFreshStart => Run.HasProgressBeyondFreshStart;

        public static void ResetRun() => Run.ResetRun();

        public static RougeRunSave Capture() => Run.Capture();

        public static void Restore(RougeRunSave save) => Run.Restore(save);

        public static void AddExp(int amount) => Run.AddExp(amount);

        public static int GetStack(string id) => Run.GetStack(id);

        public static bool TryAddPassive(string id) => Run.TryAddPassive(id);

        public static void ConsumePendingLevelUp() => Run.ConsumePendingLevelUp();

        public static void MarkSelectUiClosedWithoutPick() => Run.MarkSelectUiClosedWithoutPick();

        public static void TryOpenSkillSelectIfPending() => Run.TryOpenSkillSelectIfPending();

        public static void NotifySkillSelectOpened() => Run.NotifySkillSelectOpened();

        public static void NotifySkillSelectOpenFailed() => Run.NotifySkillSelectOpenFailed();

        public static float SumMod(string modType) => Run.SumMod(modType);

        internal static void EmitChanged() => Changed?.Invoke();

        internal static void EmitLeveledUp(int level) => LeveledUp?.Invoke(level);

        internal static void EmitSkillSelectRequested() => SkillSelectRequested?.Invoke();
    }
}
