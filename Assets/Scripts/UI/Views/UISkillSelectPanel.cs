using System.Collections.Generic;
using AttackSkill.CameraSystem;
using AttackSkill.Core;
using AttackSkill.Game;
using AttackSkill.Rouge;
using UnityEngine;
using UnityEngine.UI;

namespace AttackSkill.UI
{
    /// <summary>升级三选一：点卡片高亮，确认后写入共享被动。</summary>
    public class UISkillSelectPanel : UIBase
    {
        struct CardView
        {
            public Button root;
            public Image imgBg;
            public Image imgSelect;
            public Text txtName;
            public Text txtDesc;
            public RougePassiveDefData data;
        }

        readonly CardView[] _cards = new CardView[3];
        Button _btnSelect;
        Text _txtTitle;
        Text _txtSelect;
        int _selected = -1;
        bool _softBlockPushed;
        bool _picked;

        public override void OnOpen(object args)
        {
            EnsureBound();
            _picked = false;
            _selected = -1;

            var list = args is SkillSelectArgs selectArgs ? selectArgs.options : null;
            if (list == null || list.Count == 0)
            {
                list = RougeSkillRoller.RollThree(PartyRougeProgress.Level);
            }

            FillCards(list);

            if (_txtTitle != null)
            {
                _txtTitle.text = "选择技能";
            }

            if (_txtSelect != null)
            {
                _txtSelect.text = "确认";
            }

            RefreshSelectionVisual();

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

            if (!_picked)
            {
                PartyRougeProgress.MarkSelectUiClosedWithoutPick();
            }

            if (PartyRougeProgress.PendingLevelUps > 0)
            {
                UIManager.Instance?.StartCoroutine(ReopenNextFrame());
            }
        }

        System.Collections.IEnumerator ReopenNextFrame()
        {
            yield return null;
            PartyRougeProgress.TryOpenSkillSelectIfPending();
        }

        void FillCards(List<RougePassiveDefData> list)
        {
            for (int i = 0; i < _cards.Length; i++)
            {
                RougePassiveDefData data = i < list.Count ? list[i] : null;
                _cards[i].data = data;
                bool has = data != null;

                if (_cards[i].root != null)
                {
                    _cards[i].root.gameObject.SetActive(has);
                }

                if (!has)
                {
                    continue;
                }

                if (_cards[i].txtName != null)
                {
                    _cards[i].txtName.text = RougePassiveText.Name(data);
                }

                if (_cards[i].txtDesc != null)
                {
                    _cards[i].txtDesc.text = RougePassiveText.Desc(data);
                }

                if (_cards[i].imgBg != null)
                {
                    _cards[i].imgBg.color = RougeCatalog.RarityColor(RougeCatalog.ParseRarity(data.rarity));
                }
            }
        }

        void RefreshSelectionVisual()
        {
            for (int i = 0; i < _cards.Length; i++)
            {
                if (_cards[i].imgSelect != null)
                {
                    _cards[i].imgSelect.gameObject.SetActive(i == _selected);
                }
            }

            bool canConfirm = _selected >= 0 &&
                              _selected < _cards.Length &&
                              _cards[_selected].data != null;
            SetInteractable(_btnSelect, canConfirm);
            if (_btnSelect != null)
            {
                var img = _btnSelect.targetGraphic as Image;
                if (img != null)
                {
                    Color c = img.color;
                    c.a = canConfirm ? 1f : 0.45f;
                    img.color = c;
                }
            }
        }

        void OnCardClicked(int index)
        {
            if (index < 0 || index >= _cards.Length || _cards[index].data == null)
            {
                return;
            }

            _selected = index;
            RefreshSelectionVisual();
        }

        void OnConfirmClicked()
        {
            if (_selected < 0 || _selected >= _cards.Length)
            {
                return;
            }

            var data = _cards[_selected].data;
            if (data == null || string.IsNullOrEmpty(data.id))
            {
                return;
            }

            if (!PartyRougeProgress.TryAddPassive(data.id))
            {
                UIManager.Instance?.ShowTip("该技能已达上限");
                return;
            }

            _picked = true;
            PartyRougeProgress.ConsumePendingLevelUp();
            UIManager.Instance?.Close(UIId.SkillSelect);
        }

        void EnsureBound()
        {
            if (_btnSelect != null && _cards[0].root != null)
            {
                return;
            }

            Transform root = transform;
            _btnSelect = FindChildComponent<Button>(root, "btnSelect");
            _txtTitle = FindChildComponent<Text>(root, "txtTitle");
            _txtSelect = FindChildComponent<Text>(root, "txtSelect");

            for (int i = 0; i < 3; i++)
            {
                var cardTf = root.Find($"palCard{i}");
                if (cardTf == null)
                {
                    continue;
                }

                int captured = i;
                _cards[i].root = cardTf.GetComponent<Button>();
                _cards[i].imgBg = FindChildComponent<Image>(cardTf, "imgBg");
                _cards[i].imgSelect = FindChildComponent<Image>(cardTf, "imgSelect");
                _cards[i].txtName = FindChildComponent<Text>(cardTf, "txtName");
                _cards[i].txtDesc = FindChildComponent<Text>(cardTf, "txtDesc");

                if (_cards[i].root != null)
                {
                    BindClick(_cards[i].root, () => OnCardClicked(captured));
                }

                if (_cards[i].imgSelect != null)
                {
                    _cards[i].imgSelect.gameObject.SetActive(false);
                }
            }

            BindClick(_btnSelect, OnConfirmClicked);
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
