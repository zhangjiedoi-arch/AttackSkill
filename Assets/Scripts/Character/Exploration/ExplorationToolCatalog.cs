using UnityEngine;

namespace AttackSkill.Character.Exploration
{
    /// <summary>
    /// 探索工具轮盘目录：固定 8 槽，与 UI 扇区顺序一致。
    /// Resources 路径：ExplorationToolCatalog
    /// </summary>
    [CreateAssetMenu(
        menuName = "AttackSkill/Exploration/Tool Catalog",
        fileName = "ExplorationToolCatalog")]
    public class ExplorationToolCatalog : ScriptableObject
    {
        public const string ResourcesPath = "ExplorationToolCatalog";
        public const int SlotCount = 8;

        [Tooltip("长度应为 8；空槽视为 Stub。")]
        public ExplorationToolDefinition[] slots = new ExplorationToolDefinition[SlotCount];

        static ExplorationToolCatalog _cached;
        static bool _usingRuntimeFallback;

        public static ExplorationToolCatalog Get()
        {
            if (_cached != null)
            {
                return _cached;
            }

            var settings = CharacterRuntimeSettings.Get();
            if (settings != null && settings.explorationToolCatalog != null)
            {
                _cached = settings.explorationToolCatalog;
                _usingRuntimeFallback = false;
                EnsureSlotArray(_cached);
                return _cached;
            }

            _cached = Resources.Load<ExplorationToolCatalog>(ResourcesPath);
            if (_cached != null)
            {
                _usingRuntimeFallback = false;
                EnsureSlotArray(_cached);
                return _cached;
            }

            _cached = CreateRuntimeFallback();
            _usingRuntimeFallback = true;
            Debug.LogWarning(
                "[ExplorationToolCatalog] 未找到 Resources/ExplorationToolCatalog，已使用运行时默认目录。");
            return _cached;
        }

        public ExplorationToolDefinition GetSlot(int index)
        {
            EnsureSlotArray(this);
            if (index < 0 || index >= SlotCount)
            {
                return null;
            }

            return slots[index];
        }

        public int FindSlotIndex(ExplorationToolKind kind)
        {
            EnsureSlotArray(this);
            for (int i = 0; i < SlotCount; i++)
            {
                if (slots[i] != null && slots[i].Kind == kind)
                {
                    return i;
                }
            }

            return -1;
        }

        public string GetNameKey(int index)
        {
            var def = GetSlot(index);
            if (def != null && !string.IsNullOrEmpty(def.NameKey))
            {
                return def.NameKey;
            }

            return $"skill_wheel_{index + 1}";
        }

        public Sprite GetIcon(int index)
        {
            var def = GetSlot(index);
            return def != null ? def.Icon : null;
        }

        public bool IsImplemented(int index)
        {
            var def = GetSlot(index);
            return def != null && def.IsImplemented;
        }

        /// <summary>第一个已实现槽；全是 Stub 时返回 0。</summary>
        public int FindFirstImplementedIndex()
        {
            EnsureSlotArray(this);
            for (int i = 0; i < SlotCount; i++)
            {
                if (IsImplemented(i))
                {
                    return i;
                }
            }

            return 0;
        }

        static void EnsureSlotArray(ExplorationToolCatalog catalog)
        {
            if (catalog.slots != null && catalog.slots.Length == SlotCount)
            {
                return;
            }

            var next = new ExplorationToolDefinition[SlotCount];
            if (catalog.slots != null)
            {
                for (int i = 0; i < catalog.slots.Length && i < SlotCount; i++)
                {
                    next[i] = catalog.slots[i];
                }
            }

            catalog.slots = next;
        }

        /// <summary>与历史硬编码下标对齐的默认目录（无资产时兜底）。</summary>
        public static ExplorationToolCatalog CreateRuntimeFallback()
        {
            var catalog = CreateInstance<ExplorationToolCatalog>();
            catalog.slots = new ExplorationToolDefinition[SlotCount];
            catalog.slots[0] = Make("recon", "skill_wheel_1", ExplorationToolKind.Stub);
            catalog.slots[1] = Make("item_detector", "skill_wheel_2", ExplorationToolKind.Stub);
            catalog.slots[2] = Make("motorcycle", "skill_wheel_3", ExplorationToolKind.Motorcycle, requiresGround: true);
            catalog.slots[3] = Make("instant_camera", "skill_wheel_4", ExplorationToolKind.Stub);
            catalog.slots[4] = Make("imaging", "skill_wheel_5", ExplorationToolKind.Stub);
            catalog.slots[5] = Make("camera", "skill_wheel_6", ExplorationToolKind.Stub);
            catalog.slots[6] = Make("wing_flight", "skill_wheel_7", ExplorationToolKind.WingFlight);
            catalog.slots[7] = Make("sword_flight", "skill_wheel_8", ExplorationToolKind.SwordFlight);
            return catalog;
        }

        static ExplorationToolDefinition Make(
            string id,
            string nameKey,
            ExplorationToolKind kind,
            bool requiresGround = false)
        {
            var def = CreateInstance<ExplorationToolDefinition>();
            def.name = id;
            def.Id = id;
            def.NameKey = nameKey;
            def.Kind = kind;
            def.RequiresGroundToActivate = requiresGround;
            def.BlocksSkillWheelWhenActive = kind == ExplorationToolKind.WingFlight ||
                                             kind == ExplorationToolKind.SwordFlight ||
                                             kind == ExplorationToolKind.Motorcycle;
            return def;
        }

#if UNITY_EDITOR
        public static bool IsUsingRuntimeFallback => _usingRuntimeFallback;
#endif
    }
}
