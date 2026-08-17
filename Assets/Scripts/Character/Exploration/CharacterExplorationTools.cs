using AttackSkill.Character.HSM;

namespace AttackSkill.Character.Exploration
{
    /// <summary>
    /// 每角色一份工具实例；T 键 Toggle 与 HSM 薄壳共用。
    /// </summary>
    public sealed class CharacterExplorationTools
    {
        readonly GenshinLikeCharacter _owner;
        readonly MotorcycleTool _motorcycle;
        readonly WingFlightTool _wingFlight;
        readonly SwordFlightTool _swordFlight;

        ExplorationToolDefinition _pendingDefinition;
        bool _suppressEnterSfx;

        public CharacterExplorationTools(GenshinLikeCharacter owner)
        {
            _owner = owner;
            _motorcycle = new MotorcycleTool();
            _wingFlight = new WingFlightTool();
            _swordFlight = new SwordFlightTool();
        }

        public MotorcycleTool Motorcycle => _motorcycle;
        public WingFlightTool WingFlight => _wingFlight;
        public SwordFlightTool SwordFlight => _swordFlight;

        /// <summary>切人续态时为 true，避免再播起飞音。</summary>
        public bool SuppressEnterSfx
        {
            get => _suppressEnterSfx;
            set => _suppressEnterSfx = value;
        }

        public IExplorationTool Get(ExplorationToolKind kind)
        {
            switch (kind)
            {
                case ExplorationToolKind.Motorcycle:
                    return _motorcycle;
                case ExplorationToolKind.WingFlight:
                    return _wingFlight;
                case ExplorationToolKind.SwordFlight:
                    return _swordFlight;
                default:
                    return null;
            }
        }

        public bool IsActive(ExplorationToolKind kind)
        {
            var tool = Get(kind);
            return tool != null && tool.IsActive;
        }

        public bool IsAnyWheelBlockingActive()
        {
            return (_motorcycle.IsActive && _motorcycle.BlocksSkillWheelWhenActive) ||
                   (_wingFlight.IsActive && _wingFlight.BlocksSkillWheelWhenActive) ||
                   (_swordFlight.IsActive && _swordFlight.BlocksSkillWheelWhenActive);
        }

        public ExplorationToolContext BuildContext()
        {
            return new ExplorationToolContext(_owner, _owner != null ? _owner.Context : null);
        }

        public void SetPendingDefinition(ExplorationToolDefinition definition)
        {
            _pendingDefinition = definition;
        }

        public ExplorationToolDefinition ConsumePendingDefinition()
        {
            var def = _pendingDefinition;
            _pendingDefinition = null;
            return def;
        }

        public ExplorationToolDefinition PeekPendingDefinition() => _pendingDefinition;

        /// <summary>按装备定义切换工具；成功进入/退出对应 HSM 薄壳。</summary>
        public bool TryToggle(ExplorationToolDefinition definition)
        {
            return TryToggle(definition, out _);
        }

        public bool TryToggle(ExplorationToolDefinition definition, out bool entered)
        {
            entered = false;
            if (_owner == null || definition == null || !definition.IsImplemented)
            {
                return false;
            }

            var tool = Get(definition.Kind);
            if (tool == null)
            {
                return false;
            }

            bool wasActive = tool.IsActive;
            bool ok = _owner.TryToggleExplorationTool(tool, definition);
            if (ok && !wasActive)
            {
                entered = true;
            }

            return ok;
        }
    }
}
