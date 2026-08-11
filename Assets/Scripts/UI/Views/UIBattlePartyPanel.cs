using AttackSkill.Character;
using AttackSkill.Game;
using UnityEngine;
using UnityEngine.UI;

namespace AttackSkill.UI
{
    /// <summary>
    /// 战斗 HUD 编队区：头像与当前队伍对应；点击切人；出战槽隐藏键位角标。
    /// 图标：女/男漂泊者、千咲、柯莱塔。
    /// </summary>
    public partial class UIBattlePartyPanel : UIBase
    {
        Button[] _slotButtons;
        bool _clicksBound;
        bool _partySubscribed;

        public override void OnOpen(object args)
        {
            EnsureClickBindings();
            SubscribeParty(true);
            Refresh();
        }

        public override void OnClose()
        {
            SubscribeParty(false);
        }

        void OnEnable()
        {
            EnsureClickBindings();
            SubscribeParty(true);
            Refresh();
        }

        void OnDisable()
        {
            SubscribeParty(false);
        }

        void EnsureClickBindings()
        {
            if (_clicksBound)
            {
                return;
            }

            _slotButtons = new[] { palAvatar1, palAvatar2, palAvatar3 };
            for (int i = 0; i < _slotButtons.Length; i++)
            {
                int slot = i;
                BindClick(_slotButtons[i], () => OnClickSlot(slot));
            }

            _clicksBound = true;
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
            Refresh();
        }

        void OnClickSlot(int index)
        {
            var party = PartyController.Instance;
            if (party == null || GameplayInputGate.IsBlocked)
            {
                return;
            }

            if (index < 0 || index >= party.MemberCount)
            {
                return;
            }

            if (index == party.ActiveIndex)
            {
                return;
            }

            party.TrySwitchTo(index);
        }

        void Refresh()
        {
            var party = PartyController.Instance;
            int memberCount = party != null ? party.MemberCount : 0;
            int activeIndex = party != null ? party.ActiveIndex : -1;
            bool paused = GameplayInputGate.IsBlocked;
            var settings = CharacterRuntimeSettings.Get();

            if (_slotButtons == null)
            {
                _slotButtons = new[] { palAvatar1, palAvatar2, palAvatar3 };
            }

            for (int i = 0; i < 3; i++)
            {
                bool exists = i < memberCount;
                bool isActive = exists && i == activeIndex;

                Button btn = _slotButtons != null && i < _slotButtons.Length ? _slotButtons[i] : null;
                if (btn != null)
                {
                    btn.gameObject.SetActive(exists);
                    btn.interactable = exists && !isActive && !paused;
                }

                if (imgKeyBadge != null && i < imgKeyBadge.Length && imgKeyBadge[i] != null)
                {
                    imgKeyBadge[i].gameObject.SetActive(exists && !isActive);
                }

                ApplyAvatarIcon(i, exists, party, settings);
            }
        }

        void ApplyAvatarIcon(int slot, bool exists, PartyController party, CharacterRuntimeSettings settings)
        {
            Image avatar = ResolveAvatarImage(slot);
            if (avatar == null)
            {
                return;
            }

            if (!exists || party == null)
            {
                avatar.enabled = false;
                return;
            }

            PartyPortraitId id = party.GetPortraitId(slot);
            Sprite sprite = settings != null ? settings.GetPartyPortrait(id) : null;
            if (sprite == null)
            {
                avatar.enabled = avatar.sprite != null;
                return;
            }

            avatar.sprite = sprite;
            avatar.enabled = true;
            Color c = avatar.color;
            c.a = 1f;
            avatar.color = c;
        }

        Image ResolveAvatarImage(int slot)
        {
            if (imgAvatar != null && slot >= 0 && slot < imgAvatar.Length && imgAvatar[slot] != null)
            {
                return imgAvatar[slot];
            }

            Button btn = _slotButtons != null && slot < _slotButtons.Length ? _slotButtons[slot] : null;
            if (btn == null)
            {
                return null;
            }

            Transform t = btn.transform.Find("imgAvatar");
            return t != null ? t.GetComponent<Image>() : null;
        }
    }
}
