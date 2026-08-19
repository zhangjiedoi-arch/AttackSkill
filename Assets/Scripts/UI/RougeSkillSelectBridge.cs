using AttackSkill.Rouge;
using UnityEngine;

namespace AttackSkill.UI
{
    /// <summary>订阅肉鸽升级事件，打开三选一界面。</summary>
    public static class RougeSkillSelectBridge
    {
        static bool _hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook()
        {
            if (_hooked)
            {
                return;
            }

            _hooked = true;
            PartyRougeProgress.SkillSelectRequested -= OnSkillSelectRequested;
            PartyRougeProgress.SkillSelectRequested += OnSkillSelectRequested;
        }

        static void OnSkillSelectRequested()
        {
            var ui = UIManager.Instance;
            if (ui == null)
            {
                // Boot 末 GameProgress 会再调 TryOpenSkillSelectIfPending
                Debug.LogWarning("[RougeSkillSelect] UIManager 未就绪，等待 Boot 补开。");
                return;
            }

            PartyRougeProgress.NotifySkillSelectOpened();
            var opened = ui.Open(UIId.SkillSelect, new SkillSelectArgs
            {
                options = RougeSkillRoller.RollThree(PartyRougeProgress.Level)
            });
            if (opened == null)
            {
                PartyRougeProgress.NotifySkillSelectOpenFailed();
            }
        }
    }
}
