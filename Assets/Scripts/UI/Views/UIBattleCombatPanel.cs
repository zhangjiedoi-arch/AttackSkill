using AttackSkill.Character;
using AttackSkill.Character.Exploration;
using AttackSkill.Character.HSM;
using AttackSkill.Core;
using AttackSkill.Game;
using UnityEngine;
using UnityEngine.UI;

namespace AttackSkill.UI
{
    /// <summary>
    /// 战斗 HUD 右下操作区：Tab 开探索工具轮盘；T 执行当前装备工具；E 释放技能。
    /// </summary>
    public partial class UIBattleCombatPanel : UIBase
    {
        bool _clicksBound;

        public override void OnOpen(object args)
        {
            EnsureClickBindings();
            EnsureSkillWheelSubscription();
            BattleSkillWheelState.EnsureIconResolved();
            ApplySkillTIcon(BattleSkillWheelState.SelectedIcon);
        }

        void OnEnable()
        {
            EnsureClickBindings();
            EnsureSkillWheelSubscription();
        }

        void OnDisable()
        {
            BattleSkillWheelState.SelectionCommitted -= OnSkillWheelCommitted;
        }

        void EnsureSkillWheelSubscription()
        {
            BattleSkillWheelState.SelectionCommitted -= OnSkillWheelCommitted;
            BattleSkillWheelState.SelectionCommitted += OnSkillWheelCommitted;
        }

        void Update()
        {
            if (!IsOpen || GamePause.IsPaused)
            {
                return;
            }

            var ui = UIManager.Instance;
            if (ui != null && ui.IsOpen(UIId.SkillWheel))
            {
                return;
            }

            if (GameplayInputGate.IsSoftBlocked)
            {
                return;
            }

            if (GameInput.GetKeyDown(KeyCode.Tab))
            {
                if (IsToolLocomotionBlockingSkillWheel())
                {
                    ui?.ShowTip(L("skill_wheel_blocked_by_tool"));
                    return;
                }

                ui?.Open(UIId.SkillWheel);
                return;
            }

            // E 由角色输入 / 按钮 OnSkillE 释放技能，此处不再处理以免弹 WIP
            if (GameInput.GetKeyDown(KeyCode.Q))
            {
                OnSkillQ();
            }
            else if (GameInput.GetKeyDown(KeyCode.R))
            {
                OnSkillR();
            }
            else if (GameInput.GetKeyDown(KeyCode.T))
            {
                OnSkillT();
            }
        }

        void EnsureClickBindings()
        {
            if (_clicksBound)
            {
                return;
            }

            BindClick(btnSkillE, OnSkillE);
            BindClick(btnSkillQ, OnSkillQ);
            BindClick(btnSkillR, OnSkillR);
            BindClick(btnSkillT, OnSkillT);

            _clicksBound = true;
        }

        void OnSkillE() => CombatSkillInput.Request();

        void OnSkillQ() => ShowWipTip();

        void OnSkillR() => ShowWipTip();

        void OnSkillT()
        {
            var character = PartyController.Instance != null
                ? PartyController.Instance.Active
                : null;

            if (character == null ||
                !ExplorationToolService.TryToggleEquipped(character, BattleSkillWheelState.SelectedIndex))
            {
                ShowWipTip();
            }
        }

        void OnSkillWheelCommitted(int _, Sprite icon)
        {
            ApplySkillTIcon(icon != null ? icon : BattleSkillWheelState.SelectedIcon);
        }

        void ApplySkillTIcon(Sprite icon)
        {
            if (icon == null || btnSkillT == null)
            {
                return;
            }

            Image iconImage = null;
            if (imgSkillT != null &&
                (imgSkillT.transform == btnSkillT.transform ||
                 imgSkillT.transform.IsChildOf(btnSkillT.transform)))
            {
                iconImage = imgSkillT;
            }
            else
            {
                Transform t = btnSkillT.transform.Find("imgSkillT");
                if (t == null)
                {
                    t = btnSkillT.transform.Find("imgSkill");
                }

                if (t != null)
                {
                    iconImage = t.GetComponent<Image>();
                }
            }

            if (iconImage != null)
            {
                iconImage.sprite = icon;
                iconImage.enabled = true;
                Color c = iconImage.color;
                c.a = 1f;
                iconImage.color = c;
            }

            Image face = btnSkillT.image;
            if (face != null && face != iconImage)
            {
                face.sprite = icon;
                face.enabled = true;
            }
        }

        static void ShowWipTip()
        {
            UIManager.Instance?.ShowTip(L("feature_wip"));
        }

        /// <summary>御剑 / 翅膀飞行 / 骑摩托等工具激活时禁止开探索工具轮盘。</summary>
        static bool IsToolLocomotionBlockingSkillWheel()
        {
            var character = PartyController.Instance != null
                ? PartyController.Instance.Active
                : null;
            return ExplorationToolService.IsAnyWheelBlockingToolActive(character);
        }
    }
}
