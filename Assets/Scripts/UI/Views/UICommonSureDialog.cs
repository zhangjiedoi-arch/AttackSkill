using UnityEngine.UI;

namespace AttackSkill.UI
{
    public partial class UICommonSureDialog : UIBase
    {
        CommonSureArgs _args;

        public override void OnOpen(object args)
        {
            _args = args as CommonSureArgs;

            if (txtTitle != null && _args != null)
            {
                txtTitle.text = _args.title ?? string.Empty;
            }

            if (txtTip != null && _args != null)
            {
                txtTip.text = _args.tip ?? string.Empty;
            }

            BindClick(btnSure, () =>
            {
                var cb = _args?.onSure;
                CloseSelf();
                cb?.Invoke();
            });
            BindClick(btnCancel, () =>
            {
                var cb = _args?.onCancel;
                CloseSelf();
                cb?.Invoke();
            });
            BindClick(btnExit, () =>
            {
                var cb = _args?.onCancel;
                CloseSelf();
                cb?.Invoke();
            });
        }
    }
}
