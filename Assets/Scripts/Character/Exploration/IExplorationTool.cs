namespace AttackSkill.Character.Exploration
{
    /// <summary>
    /// 探索工具插件协议：门闩 / 激活 / 运动 Tick。
    /// HSM 只保留薄壳状态，把真实逻辑放在实现类里。
    /// </summary>
    public interface IExplorationTool
    {
        ExplorationToolKind Kind { get; }

        bool IsActive { get; }

        bool BlocksSkillWheelWhenActive { get; }

        /// <summary>是否允许激活（含 SO 接地等）。</summary>
        bool CanActivate(in ExplorationToolContext ctx, ExplorationToolDefinition definition);

        /// <summary>进入工具态时由 HSM 薄壳调用。</summary>
        void Activate(in ExplorationToolContext ctx, ExplorationToolDefinition definition);

        /// <summary>离开工具态时由 HSM 薄壳调用。</summary>
        void Deactivate(in ExplorationToolContext ctx);

        void OnUpdate(in ExplorationToolContext ctx, float deltaTime);

        void OnFixedUpdate(in ExplorationToolContext ctx, float deltaTime);
    }
}
