using UnityEngine;

namespace AttackSkill.UI
{
    public class UIJoinGameDialog : UIBase
    {
        const float AutoCloseSeconds = 2f;
        float _closeAt = -1f;

        public override void OnOpen(object args)
        {
            _closeAt = Time.unscaledTime + AutoCloseSeconds;
            BindCloseAllButtons();
        }

        public override void OnClose()
        {
            _closeAt = -1f;
        }

        void Update()
        {
            if (!IsOpen || _closeAt < 0f)
            {
                return;
            }

            if (Time.unscaledTime >= _closeAt)
            {
                _closeAt = -1f;
                CloseSelf();
            }
        }
    }
}
