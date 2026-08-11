using UnityEngine;
using AttackSkill.Core;

namespace AttackSkill.UI
{
    public partial class UIOpenScene4Panel : UIBase
    {
        public override void OnOpen(object args)
        {
            BindClick(btnSetting, () => UIManager.Instance?.OpenDialog(UIId.Setting));
            BindClick(btnTool, () => UIManager.Instance?.OpenDialog(UIId.Tools));
            BindClick(btnAgeRem, () => UIManager.Instance?.OpenDialog(UIId.AgeRem));
            BindClick(btnNotice, () => UIManager.Instance?.ShowTip(L("notice_wip")));
            BindClick(btnAccount, () => UIManager.Instance?.OpenDialog(UIId.JoinGame));
            BindClick(btnLink, OnClickLink);
            BindClick(btnExit, () =>
            {
                UIManager.Instance?.OpenSure(L("exit"), L("confirm_quit_game"), () =>
                {
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#else
                    Application.Quit();
#endif
                });
            });

            BindClick(btnFemale, () => SetGender(OpenSceneGender.Female));
            BindClick(btnMale, () => SetGender(OpenSceneGender.Male));

            // OpenScene 始终可改性别
            LocalAccountStore.UnlockGender();
            SetInteractable(btnFemale, true);
            SetInteractable(btnMale, true);

            if (LocalAccountStore.HasGender)
            {
                SetGender(LocalAccountStore.Gender);
            }
        }

        void OnClickLink()
        {
            var flow = GameServices.OpenSceneFlow;
            if (flow == null)
            {
                Debug.LogError("[UIOpenScene4] OpenSceneFlow 未注册。");
                UIManager.Instance?.CloseAll();
                return;
            }

            flow.OnLinkClicked();
        }

        void SetGender(OpenSceneGender gender)
        {
            if (LocalAccountStore.IsGenderLocked)
            {
                return;
            }

            LocalAccountStore.SaveGender(gender);
            GameServices.OpenSceneFlow?.SetSelectedGender(gender);
        }
    }
}
