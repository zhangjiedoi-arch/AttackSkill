using System;
using UnityEngine;
using UnityEngine.UI;

namespace AttackSkill.UI
{
    public class UILogInDialogArgs
    {
        public Action onConfirmed;
    }

    public partial class UILogInDialog : UIBase
    {
        UILogInDialogArgs _args;

        void Awake()
        {
            BindButtons();
        }

        public override void OnOpen(object args)
        {
            _args = args as UILogInDialogArgs;
            BindButtons();
            LocalAccountStore.MigrateLegacySecrets();

            if (inputAccount != null)
            {
                inputAccount.text = LocalAccountStore.Account ?? string.Empty;
            }

            if (inputPassword != null)
            {
                inputPassword.contentType = InputField.ContentType.Password;
                inputPassword.text = string.Empty;
            }
        }

        void BindButtons()
        {
            BindClick(btnSure, OnSure);
            BindClick(btnCancel, OnCancelQuit);
            BindClick(btnExit, OnCancelQuit);
        }

        void OnSure()
        {
            string account = inputAccount != null ? inputAccount.text.Trim() : string.Empty;
            if (string.IsNullOrEmpty(account))
            {
                UIManager.Instance?.ShowTip(L("login_tip_account_empty"));
                return;
            }

            string password = inputPassword != null ? inputPassword.text : string.Empty;
            if (string.IsNullOrEmpty(password))
            {
                UIManager.Instance?.ShowTip(L("login_tip_password_empty"));
                return;
            }

            if (LocalAccountStore.HasPasswordHash &&
                string.Equals(LocalAccountStore.Account, account, StringComparison.Ordinal) &&
                !LocalAccountStore.ValidateCredentials(account, password))
            {
                UIManager.Instance?.ShowTip(L("login_tip_invalid"));
                return;
            }

            LocalAccountStore.SaveAccount(account, password);

            var cb = _args?.onConfirmed;
            UIManager.Instance?.Close(UIId.LogIn);

            if (cb == null)
            {
                Debug.LogError("[UILogIn] 未传入 onConfirmed，请由 OpenSceneFlow 打开并注入回调。");
                return;
            }

            cb.Invoke();
        }

        void OnCancelQuit()
        {
            Debug.Log("[UILogIn] 取消登录，退出游戏");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
