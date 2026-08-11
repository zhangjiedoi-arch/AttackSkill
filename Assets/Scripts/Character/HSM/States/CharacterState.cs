namespace AttackSkill.Character.HSM
{
    /// <summary>
    /// 带 Context 的状态基类，方便子状态访问角色数据。
    /// </summary>
    public abstract class CharacterState : HState
    {
        protected CharacterContext Ctx { get; private set; }

        protected CharacterState(string name, CharacterContext ctx) : base(name)
        {
            Ctx = ctx;
        }

        protected void GoTo(HState next) => Machine.ChangeState(next);
    }
}
