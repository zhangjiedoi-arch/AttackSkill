using UnityEngine.UI;

namespace AttackSkill.UI
{
    public partial class UIToolsDialog : UIBase
    {
        public override void OnOpen(object args)
        {
            BindClose(btnExit);
            BindClick(btnRemovePatch, () => UIManager.Instance?.ShowTip(L("tools_clear_patch")));
            BindClick(btnNetworkTest, () => UIManager.Instance?.ShowTip(L("tools_network_test")));
            BindClick(btnLogUpload, () => UIManager.Instance?.ShowTip(L("tools_log_upload")));
        }
    }
}
