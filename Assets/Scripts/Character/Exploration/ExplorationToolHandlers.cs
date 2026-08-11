using AttackSkill.Character.HSM;

namespace AttackSkill.Character.Exploration
{
    /// <summary>
    /// 兼容旧 Handler 接口：转发到角色 <see cref="CharacterExplorationTools"/>。
    /// </summary>
    public class ExplorationToolHandlerAdapter : IExplorationToolHandler
    {
        readonly ExplorationToolKind _kind;

        public ExplorationToolHandlerAdapter(ExplorationToolKind kind)
        {
            _kind = kind;
        }

        public ExplorationToolKind Kind => _kind;

        public bool IsActive(GenshinLikeCharacter character)
        {
            if (character == null)
            {
                return false;
            }

            if (character.ExplorationTools != null)
            {
                return character.ExplorationTools.IsActive(_kind);
            }

            switch (_kind)
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

        public bool TryToggle(GenshinLikeCharacter character, ExplorationToolDefinition definition)
        {
            if (character == null)
            {
                return false;
            }

            if (character.ExplorationTools != null)
            {
                return character.ExplorationTools.TryToggle(definition);
            }

            return ExplorationToolService.TryToggleEquipped(character, ExplorationToolService.FindSlot(_kind));
        }
    }

    public sealed class StubToolHandler : IExplorationToolHandler
    {
        public ExplorationToolKind Kind => ExplorationToolKind.Stub;

        public bool IsActive(GenshinLikeCharacter character) => false;

        public bool TryToggle(GenshinLikeCharacter character, ExplorationToolDefinition definition) =>
            false;
    }

    // 保留类型名以免外部引用断裂
    public sealed class WingFlightToolHandler : ExplorationToolHandlerAdapter
    {
        public WingFlightToolHandler() : base(ExplorationToolKind.WingFlight)
        {
        }
    }

    public sealed class SwordFlightToolHandler : ExplorationToolHandlerAdapter
    {
        public SwordFlightToolHandler() : base(ExplorationToolKind.SwordFlight)
        {
        }
    }

    public sealed class MotorcycleToolHandler : ExplorationToolHandlerAdapter
    {
        public MotorcycleToolHandler() : base(ExplorationToolKind.Motorcycle)
        {
        }
    }
}
