using AttackSkill.Character;
using AttackSkill.Character.HSM;
using AttackSkill.Combat;
using AttackSkill.Rouge;
using UnityEngine;
using UnityEngine.UI;

namespace AttackSkill.UI
{
    /// <summary>战斗 HUD 生存区：血量 + 共享等级/经验条。</summary>
    public partial class UIBattleVitalsPanel : UIBase
    {
        Health _boundHealth;
        bool _partySubscribed;

        public override void OnOpen(object args)
        {
            SubscribeParty(true);
            SubscribeRouge(true);
            BindActiveHealth();
            RefreshHp();
            RefreshLevelAndExp();
            PartyRougeProgress.TryOpenSkillSelectIfPending();
        }

        public override void OnClose()
        {
            UnbindHealth();
            SubscribeParty(false);
            SubscribeRouge(false);
        }

        void OnEnable()
        {
            SubscribeParty(true);
            SubscribeRouge(true);
            BindActiveHealth();
            RefreshHp();
            RefreshLevelAndExp();
        }

        void OnDisable()
        {
            UnbindHealth();
            SubscribeParty(false);
            SubscribeRouge(false);
        }

        void SubscribeRouge(bool subscribe)
        {
            if (subscribe)
            {
                PartyRougeProgress.Changed -= OnRougeChanged;
                PartyRougeProgress.Changed += OnRougeChanged;
            }
            else
            {
                PartyRougeProgress.Changed -= OnRougeChanged;
            }
        }

        void OnRougeChanged()
        {
            RefreshLevelAndExp();
            RefreshHp();
        }

        void RefreshLevelAndExp()
        {
            int lv = Mathf.Max(1, PartyRougeProgress.Level);
            if (txtLv != null)
            {
                txtLv.text = lv.ToString();
            }

            if (txtLvText != null && string.IsNullOrEmpty(txtLvText.text))
            {
                txtLvText.text = "Lv";
            }

            int exp = Mathf.Max(0, PartyRougeProgress.Exp);
            int need = PartyRougeProgress.ExpToNext;
            bool maxed = need <= 0;

            if (imgExpFill != null)
            {
                imgExpFill.type = Image.Type.Filled;
                imgExpFill.fillMethod = Image.FillMethod.Horizontal;
                imgExpFill.fillOrigin = (int)Image.OriginHorizontal.Left;
                imgExpFill.fillAmount = maxed ? 1f : Mathf.Clamp01((float)exp / Mathf.Max(1, need));
            }

            if (txtExpValue != null)
            {
                txtExpValue.text = maxed ? $"{exp} / MAX" : $"{exp} / {need}";
            }
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
