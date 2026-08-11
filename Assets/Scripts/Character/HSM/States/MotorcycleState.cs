using AttackSkill.Character.Exploration;

namespace AttackSkill.Character.HSM
{
    /// <summary>
    /// 摩托薄壳：Enter/Exit/Tick 转发给 <see cref="MotorcycleTool"/>。
    /// </summary>
    public class MotorcycleState : CharacterState
    {
        public MotorcycleState(CharacterContext ctx) : base("Motorcycle", ctx)
        {
        }

        MotorcycleTool Tool =>
            Ctx.Owner != null && Ctx.Owner.ExplorationTools != null
                ? Ctx.Owner.ExplorationTools.Motorcycle
                : null;

        ExplorationToolContext ToolCtx =>
            Ctx.Owner != null && Ctx.Owner.ExplorationTools != null
                ? Ctx.Owner.ExplorationTools.BuildContext()
                : new ExplorationToolContext(Ctx.Owner, Ctx);

        public override void OnEnter()
        {
            var tools = Ctx.Owner != null ? Ctx.Owner.ExplorationTools : null;
            var def = tools != null ? tools.ConsumePendingDefinition() : null;
            var tool = Tool;
            if (tool == null)
            {
                GoTo(Ctx.Owner.States.Grounded.Idle);
                return;
            }

            tool.Activate(ToolCtx, def);
        }

        public override void OnExit()
        {
            Tool?.Deactivate(ToolCtx);
        }

        public override void OnUpdate(float deltaTime)
        {
            if (Ctx.IsInWater)
            {
                GoTo(Ctx.Owner.States.Swim.Idle);
                return;
            }

            Tool?.OnUpdate(ToolCtx, deltaTime);
        }

        public override void OnFixedUpdate(float deltaTime)
        {
            Tool?.OnFixedUpdate(ToolCtx, deltaTime);
        }
    }
}
