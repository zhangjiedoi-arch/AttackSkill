using AttackSkill.Character.HSM;

namespace AttackSkill.Character.Exploration
{
    /// <summary>探索工具 T 键行为。</summary>
    public interface IExplorationToolHandler
    {
        ExplorationToolKind Kind { get; }

        bool IsActive(GenshinLikeCharacter character);

        /// <summary>切换进出该工具。失败返回 false（调用方可提示 WIP）。</summary>
        bool TryToggle(GenshinLikeCharacter character, ExplorationToolDefinition definition);
    }
}
