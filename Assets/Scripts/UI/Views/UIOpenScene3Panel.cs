using UnityEngine.UI;

namespace AttackSkill.UI
{
    public partial class UIOpenScene3Panel : UIBase
    {
        public Slider Progress => slrPro;

        public override void OnOpen(object args)
        {
            SetProgress(0f);
        }

        public void SetProgress(float value01)
        {
            ApplyProgress01(slrPro, txtPro, value01);
        }
    }
}
