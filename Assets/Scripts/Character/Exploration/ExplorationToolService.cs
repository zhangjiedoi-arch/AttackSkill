using AttackSkill.Character.HSM;

namespace AttackSkill.Character.Exploration
{
    /// <summary>按 Kind 分发探索工具 T 键逻辑（经 <see cref="IExplorationTool"/>）。</summary>
    public static class ExplorationToolService
    {
        public static ExplorationToolCatalog Catalog => ExplorationToolCatalog.Get();

        public static ExplorationToolDefinition GetEquipped(int slotIndex) =>
            Catalog.GetSlot(slotIndex);

        public static bool TryToggleEquipped(GenshinLikeCharacter character, int slotIndex)
        {
            return TryToggleEquipped(character, slotIndex, out _);
        }

        /// <param name="entered">true=成功进入工具（应开 T 冷却）；false=退出或失败。</param>
        public static bool TryToggleEquipped(GenshinLikeCharacter character, int slotIndex, out bool entered)
        {
            entered = false;
            if (character == null)
            {
                return false;
            }

            var def = GetEquipped(slotIndex);
            if (def == null || !def.IsImplemented)
            {
                return false;
            }

            if (character.ExplorationTools != null)
            {
                return character.ExplorationTools.TryToggle(def, out entered);
            }

            // 兜底：未装配 Tools 时走旧入口
            bool wasActive = IsKindActive(character, def.Kind);
            bool ok;
            switch (def.Kind)
            {
                case ExplorationToolKind.WingFlight:
                    ok = character.TryToggleWingFlight();
                    break;
                case ExplorationToolKind.SwordFlight:
                    ok = character.TryToggleSwordFlight();
                    break;
                case ExplorationToolKind.Motorcycle:
                    ok = character.TryToggleMotorcycle(def);
                    break;
                default:
                    return false;
            }

            if (ok && !wasActive)
            {
                entered = true;
            }

            return ok;
        }

        static bool IsKindActive(GenshinLikeCharacter character, ExplorationToolKind kind)
        {
            switch (kind)
            {
                case ExplorationToolKind.WingFlight:
                    return character.IsWingFlying;
                case ExplorationToolKind.SwordFlight:
                    return character.IsSwordFlying;
                case ExplorationToolKind.Motorcycle:
                    return character.IsRidingMotorcycle;
                default:
                    return false;
            }
        }

        public static bool IsAnyWheelBlockingToolActive(GenshinLikeCharacter character)
        {
            if (character == null)
            {
                return false;
            }

            if (character.ExplorationTools != null)
            {
                return character.ExplorationTools.IsAnyWheelBlockingActive();
            }

            return character.IsWingFlying || character.IsSwordFlying || character.IsRidingMotorcycle;
        }

        public static int FindSlot(ExplorationToolKind kind) => Catalog.FindSlotIndex(kind);
    }
}
