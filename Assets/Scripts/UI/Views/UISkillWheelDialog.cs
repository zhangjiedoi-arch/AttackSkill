using AttackSkill.CameraSystem;
using AttackSkill.Core;
using AttackSkill.Game;
using AttackSkill.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace AttackSkill.UI
{
    /// <summary>
    /// 战斗技能轮盘：按住 Tab 打开，按鼠标相对屏幕中心角度高亮扇形，松 Tab 确认并关闭。
    /// 打开期间软阻塞玩法输入并冻结 timeScale，释放鼠标供选扇区。
    /// Stub 扇区灰色显示；确认未实现槽位时提示 WIP 且不切换装备。
    /// </summary>
    public partial class UISkillWheelDialog : UIBase
    {
        static readonly Color32 SelectedColor = new Color32(0xFF, 0x81, 0x23, 0xB9);
        static readonly Color32 NormalColor = new Color32(0x00, 0x00, 0x00, 0xB9);
        static readonly Color32 StubColor = new Color32(0x2A, 0x2A, 0x2A, 0x90);
        static readonly Color32 StubHoverColor = new Color32(0x55, 0x55, 0x55, 0xB0);

        const float DeadZonePixels = 48f;

        Image[] _segments;
        int _hoverIndex = -1;
        bool _softBlockPushed;

        public override void OnOpen(object args)
        {
            CacheSegments();
            LocalizedText.EnsureOn(txtTip, "release_switch");

            if (!_softBlockPushed)
            {
                GameplayInputGate.PushSoftBlock(freezeTime: true);
                _softBlockPushed = true;
            }

            BattleSkillWheelState.SetOpen(true);
            ReleaseCursorForWheel();

            _hoverIndex = -1;
            RefreshHover(force: true);
        }

        public override void OnClose()
        {
            BattleSkillWheelState.SetOpen(false);
            if (_softBlockPushed)
            {
                GameplayInputGate.PopSoftBlock();
                _softBlockPushed = false;
            }

            RestoreCursorAfterWheel();
        }

        void Update()
        {
            if (!IsOpen)
            {
                return;
            }

            // ESC 全局暂停时先关轮盘（不提交）
            if (GamePause.IsPaused)
            {
                CloseWithoutCommit();
                return;
            }

            RefreshHover(force: false);

            // 按住打开；松开即确认（Open 当帧 Tab 仍按下，不会误关）
            if (!GameInput.GetKey(KeyCode.Tab))
            {
                CommitAndClose();
            }
        }

        void CacheSegments()
        {
            if (_segments != null && _segments.Length == BattleSkillWheelState.SkillCount)
            {
                return;
            }

            _segments = new[]
            {
                palSkill1, palSkill2, palSkill3, palSkill4,
                palSkill5, palSkill6, palSkill7, palSkill8
            };
        }

        void RefreshHover(bool force)
        {
            int index = ResolveSectorIndex();
            if (!force && index == _hoverIndex)
            {
                return;
            }

            _hoverIndex = index;
            ApplySegmentColors();
            LocalizedText.EnsureOn(txtName, BattleSkillWheelState.GetNameKey(index));

            bool stub = !BattleSkillWheelState.IsImplemented(index);
            LocalizedText.EnsureOn(txtTip, stub ? "feature_wip" : "release_switch");
        }

        int ResolveSectorIndex()
        {
            Vector3 mouse = GameInput.MousePosition;
            float dx = mouse.x - Screen.width * 0.5f;
            float dy = mouse.y - Screen.height * 0.5f;
            if (dx * dx + dy * dy < DeadZonePixels * DeadZonePixels)
            {
                return _hoverIndex >= 0 ? _hoverIndex : BattleSkillWheelState.SelectedIndex;
            }

            // 0°=正上，顺时针增大。扇区从 22.5° 起算：
            // 1: 22.5–67.5，2: 67.5–112.5，…，8: 337.5–22.5（跨 0°）
            float angle = Mathf.Atan2(dx, dy) * Mathf.Rad2Deg;
            if (angle < 0f)
            {
                angle += 360f;
            }

            angle -= 22.5f;
            if (angle < 0f)
            {
                angle += 360f;
            }

            return Mathf.FloorToInt(angle / 45f) % BattleSkillWheelState.SkillCount;
        }

        void ApplySegmentColors()
        {
            CacheSegments();
            for (int i = 0; i < _segments.Length; i++)
            {
                if (_segments[i] == null)
                {
                    continue;
                }

                bool stub = !BattleSkillWheelState.IsImplemented(i);
                bool hover = i == _hoverIndex;
                if (stub)
                {
                    _segments[i].color = hover ? StubHoverColor : StubColor;
                }
                else
                {
                    _segments[i].color = hover ? SelectedColor : NormalColor;
                }
            }
        }

        void CommitAndClose()
        {
            CacheSegments();
            int index = _hoverIndex >= 0 ? _hoverIndex : BattleSkillWheelState.SelectedIndex;

            if (!BattleSkillWheelState.IsImplemented(index))
            {
                UIManager.Instance?.ShowTip(L("feature_wip"));
                UIManager.Instance?.Close(UIId.SkillWheel);
                return;
            }

            Sprite icon = null;
            if (index >= 0 && index < _segments.Length && _segments[index] != null)
            {
                var iconTf = _segments[index].transform.Find("imgSkill");
                if (iconTf != null)
                {
                    var img = iconTf.GetComponent<Image>();
                    if (img != null)
                    {
                        icon = img.sprite;
                    }
                }
            }

            BattleSkillWheelState.Commit(index, icon);
            UIManager.Instance?.Close(UIId.SkillWheel);
        }

        void CloseWithoutCommit()
        {
            UIManager.Instance?.Close(UIId.SkillWheel);
        }

        static void ReleaseCursorForWheel()
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

        static void RestoreCursorAfterWheel()
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
