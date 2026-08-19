using AttackSkill.Character;
using AttackSkill.Core;
using AttackSkill.Game;
using AttackSkill.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace AttackSkill.UI
{
    public sealed class UIGameOverDialogArgs
    {
        public string titleKey;
    }

    /// <summary>全灭结算：重新开始肉鸽 / 退出游戏。</summary>
    public class UIGameOverDialog : UIBase
    {
        public const string DefaultTitleKey = "game_over_title";
        public const string RescueTitleKey = "game_over_rescue_title";

        [Header("UI Bindings")]
        [SerializeField] Text txtTitle;
        [SerializeField] Button btnReset;
        [SerializeField] Button btnQuit;

        bool _softBlockPushed;
        string _titleKey = DefaultTitleKey;

        public override void OnOpen(object args)
        {
            EnsureBound();
            _titleKey = DefaultTitleKey;
            if (args is UIGameOverDialogArgs goArgs && !string.IsNullOrEmpty(goArgs.titleKey))
            {
                _titleKey = goArgs.titleKey;
            }

            BindLocalizedTexts();
            BindClick(btnReset, OnClickReset);
            BindClick(btnQuit, OnClickQuit);

            if (!_softBlockPushed)
            {
                GameplayInputGate.PushSoftBlock(freezeTime: true);
                _softBlockPushed = true;
            }

            ReleaseCursor();
        }

        public override void OnClose()
        {
            if (_softBlockPushed)
            {
                GameplayInputGate.PopSoftBlock();
                _softBlockPushed = false;
            }

            RestoreCursor();
        }

        void EnsureBound()
        {
            if (txtTitle == null)
            {
                txtTitle = FindChildComponent<Text>(transform, "txtTitle");
            }

            if (btnReset == null)
            {
                btnReset = FindChildComponent<Button>(transform, "btnReset");
            }

            if (btnQuit == null)
            {
                btnQuit = FindChildComponent<Button>(transform, "btnQuit");
            }
        }

        void BindLocalizedTexts()
        {
            LocalizationService.EnsureInitialized();
            LocalizedText.EnsureOn(txtTitle, _titleKey);
            if (txtTitle != null)
            {
                txtTitle.text = LocalizationService.Get(LocalizationTableType.UI, _titleKey);
            }

            Text resetLabel = btnReset != null ? btnReset.GetComponentInChildren<Text>(true) : null;
            Text quitLabel = btnQuit != null ? btnQuit.GetComponentInChildren<Text>(true) : null;
            LocalizedText.EnsureOn(resetLabel, "game_over_reset");
            LocalizedText.EnsureOn(quitLabel, "pause_quit_game");
        }

        void OnClickReset()
        {
            PartyController.Instance?.RestartRougeRun();
        }

        static void OnClickQuit()
        {
            GamePauseController.QuitGame();
        }

        static T FindChildComponent<T>(Transform root, string name) where T : Component
        {
            if (root == null)
            {
                return null;
            }

            var all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == name)
                {
                    return all[i].GetComponent<T>();
                }
            }

            return null;
        }

        static void ReleaseCursor()
        {
            var cam = GameServices.ResolveCamera();
            if (cam != null)
            {
                cam.SetCursorLockedTemporary(false);
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        static void RestoreCursor()
        {
            if (GameplayInputGate.IsBlocked)
            {
                return;
            }

            var cam = GameServices.ResolveCamera();
            if (cam != null)
            {
                cam.RestoreDesiredCursorLock();
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }
}
