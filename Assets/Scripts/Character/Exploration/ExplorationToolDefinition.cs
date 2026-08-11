using UnityEngine;

namespace AttackSkill.Character.Exploration
{
    /// <summary>
    /// 单个探索工具定义（轮盘一格）。
    /// </summary>
    [CreateAssetMenu(
        menuName = "AttackSkill/Exploration/Tool Definition",
        fileName = "ExplorationTool")]
    public class ExplorationToolDefinition : ScriptableObject
    {
        [Tooltip("稳定 ID，便于调试/以后存档迁移。")]
        public string Id;

        [Tooltip("本地化 key，如 skill_wheel_7。")]
        public string NameKey;

        [Tooltip("可选：覆盖轮盘 Prefab 上的图标。")]
        public Sprite Icon;

        public ExplorationToolKind Kind = ExplorationToolKind.Stub;

        [Tooltip("激活时是否要求站在地面（如摩托上车）。")]
        public bool RequiresGroundToActivate;

        [Tooltip("该工具激活期间禁止打开探索轮盘。")]
        public bool BlocksSkillWheelWhenActive = true;

        public bool IsImplemented =>
            Kind != ExplorationToolKind.None && Kind != ExplorationToolKind.Stub;
    }
}
