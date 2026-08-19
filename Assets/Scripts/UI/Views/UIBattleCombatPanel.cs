using AttackSkill.Character;
using AttackSkill.Character.Exploration;
using AttackSkill.Character.HSM;
using AttackSkill.Combat;
using AttackSkill.Core;
using AttackSkill.Game;
using UnityEngine;
using UnityEngine.UI;

namespace AttackSkill.UI
{
    /// <summary>
    /// 战斗 HUD 右下操作区：Tab 开探索工具轮盘；T 执行当前装备工具；E/R 释放技能。
    /// T/Q/E/R 冷却：imgFill.fillAmount（0=刚进 CD，1=可用）；txtFill 显示剩余秒数。
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
            RefreshCooldownFills();
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

            RefreshCooldownFills();

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

            // E / R 由角色输入或按钮 Request，此处不再处理以免抢键/弹 WIP
            if (GameInput.GetKeyDown(KeyCode.Q))
            {
                OnSkillQ();
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

        void OnSkillE()
        {
            if (!IsActiveSkillEReady())
            {
                return;
            }

            CombatSkillInput.Request();
        }

        void OnSkillQ()
        {
            if (!PartySkillCooldown.IsQReady)
            {
                return;
            }

            PartySkillCooldown.BeginQ();
            ShowWipTip();
            RefreshCooldownFills();
        }

        void OnSkillR()
        {
            if (!IsActiveSkillRReady())
            {
                return;
            }

            CombatSkillRInput.Request();
        }

        void OnSkillT()
        {
            var character = PartyController.Instance != null
                ? PartyController.Instance.Active
                : null;

            if (character == null)
            {
                ShowWipTip();
                return;
            }

            // 飞行/御剑/摩托中允许随时退出；冷却只拦「再次进入」
            bool toolActive = ExplorationToolService.IsAnyWheelBlockingToolActive(character);
            if (!toolActive && !PartySkillCooldown.IsTReady)
            {
                return;
            }

            // 进入探索工具前：贴身搜索
            if (!toolActive)
            {
                CombatEngageUtility.TrySnapToNearestEnemy(character);
            }

            if (!ExplorationToolService.TryToggleEquipped(
                    character,
                    BattleSkillWheelState.SelectedIndex,
                    out bool entered))
            {
                ShowWipTip();
                return;
            }

            // 仅「进入」开 CD；退出不进冷却
            if (entered)
            {
                PartySkillCooldown.BeginT();
                RefreshCooldownFills();
            }
        }

        void OnSkillWheelCommitted(int _, Sprite icon)
        {
            ApplySkillTIcon(icon != null ? icon : BattleSkillWheelState.SelectedIcon);
        }

        void RefreshCooldownFills()
        {
            CombatStats stats = GetActiveCombatStats();
            SetCooldownVisual(
                imgFillT,
                txtFillT,
                PartySkillCooldown.TFillAmount,
                PartySkillCooldown.TRemaining);
            SetCooldownVisual(
                imgFillQ,
                txtFillQ,
                PartySkillCooldown.QFillAmount,
                PartySkillCooldown.QRemaining);
            SetCooldownVisual(
                imgFillE,
                txtFillE,
                stats != null ? stats.SkillEFillAmount : 1f,
                stats != null ? stats.SkillERemaining : 0f);
            SetCooldownVisual(
                imgFillR,
                txtFillR,
                stats != null ? stats.SkillRFillAmount : 1f,
                stats != null ? stats.SkillRRemaining : 0f);
        }

        static void SetCooldownVisual(Image fill, Text label, float fillAmount, float remaining)
        {
            if (fill != null)
            {
                fill.fillAmount = Mathf.Clamp01(fillAmount);
            }

            if (label == null)
            {
                return;
            }

            if (remaining <= 0.001f)
            {
                label.text = string.Empty;
                label.enabled = false;
                return;
            }

            label.enabled = true;
            if (remaining < 1f)
            {
                label.text = remaining.ToString("0.0");
            }
            else
            {
                label.text = Mathf.CeilToInt(remaining).ToString();
            }
        }

        static CombatStats GetActiveCombatStats()
        {
            var character = PartyController.Instance != null
                ? PartyController.Instance.Active
                : null;
            return character != null ? CombatStats.Find(character) : null;
        }

        static bool IsActiveSkillEReady()
        {
            CombatStats stats = GetActiveCombatStats();
            return stats == null || stats.IsSkillEReady;
        }

        static bool IsActiveSkillRReady()
        {
            CombatStats stats = GetActiveCombatStats();
            return stats == null || stats.IsSkillRReady;
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
