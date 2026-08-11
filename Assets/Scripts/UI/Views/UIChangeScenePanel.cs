using UnityEngine;
using UnityEngine.UI;

namespace AttackSkill.UI
{
    public partial class UIChangeScenePanel : UIBase
    {
        public override void OnOpen(object args)
        {
            SetTip(args as string);
            SetProgress(0f);
        }

        public void SetProgress(float value01)
        {
            ApplyProgress01(slrPro, txtPro, value01);
        }

        public void SetTip(string tip)
        {
            if (txtTip != null && !string.IsNullOrEmpty(tip))
            {
                txtTip.text = tip;
            }
        }
    }
}
