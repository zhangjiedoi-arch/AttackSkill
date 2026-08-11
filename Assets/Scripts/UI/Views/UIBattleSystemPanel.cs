using UnityEngine;
using UnityEngine.UI;

namespace AttackSkill.UI
{
    /// <summary>战斗 HUD 右上系统入口：点击暂提示「该功能开发中」。</summary>
    public partial class UIBattleSystemPanel : UIBase
    {
        bool _clicksBound;

        public override void OnOpen(object args)
        {
            EnsureClickBindings();
        }

        void OnEnable()
        {
            EnsureClickBindings();
        }

        void EnsureClickBindings()
        {
            if (_clicksBound)
            {
                return;
            }

            Button[] entries =
            {
                btnEntry_1, btnEntry_2, btnEntry_3,
                btnEntry_4, btnEntry_5, btnEntry_6
            };

            for (int i = 0; i < entries.Length; i++)
            {
                BindClick(entries[i], ShowWipTip);
            }

            _clicksBound = true;
        }

        static void ShowWipTip()
        {
            UIManager.Instance?.ShowTip(L("feature_wip"));
        }
    }
}
