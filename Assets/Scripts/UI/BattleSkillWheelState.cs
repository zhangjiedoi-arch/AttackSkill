using System;
using AttackSkill.Character.Exploration;
using UnityEngine;
using UnityEngine.UI;

namespace AttackSkill.UI
{
    /// <summary>
    /// 战斗技能轮盘当前装备槽（0–7）。
    /// 持久化只走 <see cref="AttackSkill.Game.GameSaveData.equippedSkillIndex"/>：
    /// New Game 重置默认槽；Continue 由 Party 读档 <see cref="Restore"/>。
    /// </summary>
    public static class BattleSkillWheelState
    {
        public const int SkillCount = ExplorationToolCatalog.SlotCount;

        static readonly string[] SegmentNames =
        {
            "palSkill1",
            "palSkill2",
            "palSkill3",
            "palSkill4",
            "palSkill5",
            "palSkill6",
            "palSkill7",
            "palSkill8",
        };

        public static ExplorationToolDefinition EquippedTool =>
            ExplorationToolService.GetEquipped(SelectedIndex);

        public static bool IsFlightEquipped =>
            EquippedTool != null && EquippedTool.Kind == ExplorationToolKind.WingFlight;

        public static bool IsSwordFlightEquipped =>
            EquippedTool != null && EquippedTool.Kind == ExplorationToolKind.SwordFlight;

        public static bool IsMotorcycleEquipped =>
            EquippedTool != null && EquippedTool.Kind == ExplorationToolKind.Motorcycle;

        public static bool IsOpen { get; private set; }

        public static int SelectedIndex { get; private set; }

        public static Sprite SelectedIcon { get; private set; }

        public static event Action<int, Sprite> SelectionCommitted;

        public static string GetNameKey(int index) =>
            ExplorationToolCatalog.Get().GetNameKey(index);

        public static bool IsImplemented(int index) =>
            ExplorationToolCatalog.Get().IsImplemented(index);

        /// <summary>默认装备：目录中第一个已实现槽。</summary>
        public static int ResolveDefaultSkillIndex() =>
            ExplorationToolCatalog.Get().FindFirstImplementedIndex();

        public static void SetOpen(bool open) => IsOpen = open;

        /// <summary>新开局：装备默认已实现槽并通知 HUD。</summary>
        public static void ResetToDefault()
        {
            Apply(ResolveDefaultSkillIndex(), icon: null, notify: true);
        }

        public static void Commit(int index, Sprite icon)
        {
            if (!IsImplemented(index))
            {
                return;
            }

            Apply(index, icon, notify: true);
        }

        /// <summary>
        /// 读档恢复装备；非法下标或 Stub 槽回默认已实现槽。
        /// </summary>
        public static void Restore(int index, Sprite icon = null)
        {
            if (index < 0 || index >= SkillCount || !IsImplemented(index))
            {
                ResetToDefault();
                return;
            }

            Apply(index, icon, notify: true);
        }

        public static void EnsureIconResolved()
        {
            if (SelectedIcon != null)
            {
                return;
            }

            SelectedIcon = ResolveIcon(SelectedIndex);
        }

        static void Apply(int index, Sprite icon, bool notify)
        {
            if (index < 0 || index >= SkillCount)
            {
                return;
            }

            SelectedIndex = index;
            SelectedIcon = icon != null ? icon : ResolveIcon(index);

            if (notify)
            {
                SelectionCommitted?.Invoke(SelectedIndex, SelectedIcon);
            }
        }

        static Sprite ResolveIcon(int index)
        {
            Sprite fromDef = ExplorationToolCatalog.Get().GetIcon(index);
            if (fromDef != null)
            {
                return fromDef;
            }

            return TryResolveIconFromWheelPrefab(index);
        }

        static Sprite TryResolveIconFromWheelPrefab(int index)
        {
            if (index < 0 || index >= SegmentNames.Length)
            {
                return null;
            }

            var ui = UIManager.Instance;
            if (ui == null || !ui.TryGetPrefab(UIId.SkillWheel, out GameObject prefab) || prefab == null)
            {
                return null;
            }

            Transform seg = FindDeep(prefab.transform, SegmentNames[index]);
            if (seg == null)
            {
                return null;
            }

            Transform iconTf = seg.Find("imgSkill");
            if (iconTf == null)
            {
                return null;
            }

            var img = iconTf.GetComponent<Image>();
            return img != null ? img.sprite : null;
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == name)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
