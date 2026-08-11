using System;
using UnityEngine;

namespace AttackSkill.UI
{
    public class UIChooseGenderDialogArgs
    {
        public Action<OpenSceneGender> onConfirmed;
    }

    public partial class UIChooseGenderDialog : UIBase
    {
        UIChooseGenderDialogArgs _args;

        void Awake()
        {
            BindButtons();
        }

        public override void OnOpen(object args)
        {
            _args = args as UIChooseGenderDialogArgs;
            BindButtons();

            OpenSceneGender g = LocalAccountStore.HasGender
                ? LocalAccountStore.Gender
                : OpenSceneGender.Female;
            SetGenderDropdownValue(dropGender, g);
        }

        void BindButtons()
        {
            BindClick(btnSure, OnSure);
            BindClick(btnCancel, OnCancelDefaultFemale);
            BindClick(btnExit, OnCancelDefaultFemale);
        }

        void OnSure()
        {
            OpenSceneGender gender = GenderUi.FromDropdownIndex(dropGender != null ? dropGender.value : 1);
            Finish(gender);
        }

        void OnCancelDefaultFemale()
        {
            Finish(OpenSceneGender.Female);
        }

        void Finish(OpenSceneGender gender)
        {
            LocalAccountStore.SaveGender(gender);
            var cb = _args?.onConfirmed;
            UIManager.Instance?.Close(UIId.ChooseGender);

            if (cb == null)
            {
                Debug.LogError("[UIChooseGender] 未传入 onConfirmed，请由 OpenSceneFlow 打开并注入回调。");
                return;
            }

            cb.Invoke(gender);
        }

        // 兼容旧调用点
        public static int GenderToDropdownIndex(OpenSceneGender gender) => GenderUi.ToDropdownIndex(gender);

        public static OpenSceneGender DropdownIndexToGender(int index) => GenderUi.FromDropdownIndex(index);
    }
}
