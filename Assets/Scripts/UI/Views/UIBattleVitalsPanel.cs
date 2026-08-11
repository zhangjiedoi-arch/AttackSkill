using AttackSkill.Character;
using AttackSkill.Character.HSM;
using AttackSkill.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace AttackSkill.UI
{
    /// <summary>战斗 HUD 生存区：跟随 Active 角色当前/最大血量刷新条与数值。</summary>
    public partial class UIBattleVitalsPanel : UIBase
    {
        Health _boundHealth;
        bool _partySubscribed;

        public override void OnOpen(object args)
        {
            SubscribeParty(true);
            BindActiveHealth();
            RefreshHp();
        }

        public override void OnClose()
        {
            UnbindHealth();
            SubscribeParty(false);
        }

        void OnEnable()
        {
            SubscribeParty(true);
            BindActiveHealth();
            RefreshHp();
        }

        void OnDisable()
        {
            UnbindHealth();
            SubscribeParty(false);
        }

        void SubscribeParty(bool subscribe)
        {
            var party = PartyController.Instance;
            if (party == null)
            {
                _partySubscribed = false;
                return;
            }

            if (subscribe)
            {
                if (_partySubscribed)
                {
                    return;
                }

                party.ActiveChanged += OnActiveChanged;
                _partySubscribed = true;
                return;
            }

            if (!_partySubscribed)
            {
                return;
            }

            party.ActiveChanged -= OnActiveChanged;
            _partySubscribed = false;
        }

        void OnActiveChanged(int _)
        {
            BindActiveHealth();
            RefreshHp();
        }

        void BindActiveHealth()
        {
            Health next = null;
            GenshinLikeCharacter active = PartyController.Instance != null
                ? PartyController.Instance.Active
                : null;
            if (active != null)
            {
                next = active.Health;
            }

            if (_boundHealth == next)
            {
                return;
            }

            UnbindHealth();
            _boundHealth = next;
            if (_boundHealth != null)
            {
                _boundHealth.HpChanged += OnHpChanged;
            }
        }

        void UnbindHealth()
        {
            if (_boundHealth == null)
            {
                return;
            }

            _boundHealth.HpChanged -= OnHpChanged;
            _boundHealth = null;
        }

        void OnHpChanged()
        {
            RefreshHp();
        }

        void RefreshHp()
        {
            float current = 0f;
            float max = 1f;
            if (_boundHealth != null)
            {
                current = _boundHealth.CurrentHp;
                max = Mathf.Max(1f, _boundHealth.MaxHp);
            }
            else
            {
                // 尚未生成 Active 时按满血占位，避免条空/文本空白
                max = 20000f;
                current = 20000f;
            }

            float ratio = Mathf.Clamp01(current / max);
            if (imgHpFill != null)
            {
                imgHpFill.type = Image.Type.Filled;
                imgHpFill.fillMethod = Image.FillMethod.Horizontal;
                imgHpFill.fillOrigin = (int)Image.OriginHorizontal.Left;
                imgHpFill.fillAmount = ratio;
            }

            if (txtHpValue != null)
            {
                txtHpValue.text = $"{Mathf.CeilToInt(current)}/{Mathf.CeilToInt(max)}";
            }
        }
    }
}
