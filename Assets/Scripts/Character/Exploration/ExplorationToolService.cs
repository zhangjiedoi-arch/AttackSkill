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
                return character.ExplorationTools.TryToggle(def);
            }

            // 兜底：未装配 Tools 时走旧入口
            switch (def.Kind)
            {
                case ExplorationToolKind.WingFlight:
                    return character.TryToggleWingFlight();
                case ExplorationToolKind.SwordFlight:
                    return character.TryToggleSwordFlight();
                case ExplorationToolKind.Motorcycle:
                    return character.TryToggleMotorcycle(def);
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
