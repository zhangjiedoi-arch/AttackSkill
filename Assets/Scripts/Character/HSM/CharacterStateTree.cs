using UnityEngine;

namespace AttackSkill.Character.HSM
{
    /// <summary>
    /// 类原神基础角色状态树入口。
    /// </summary>
    public class CharacterStateTree
    {
        public GroundedState Grounded { get; }
        public AirborneState Airborne { get; }
        public ClimbState Climb { get; }
        public SwimState Swim { get; }
        public CombatState Combat { get; }

        public CharacterStateTree(CharacterContext ctx, HStateMachine machine)
        {
            Grounded = new GroundedState(ctx);
            Airborne = new AirborneState(ctx);
            Climb = new ClimbState(ctx);
            Swim = new SwimState(ctx);
            Combat = new CombatState(ctx);

            Bind(machine, Grounded, null);
            Bind(machine, Grounded.Idle, Grounded);
            Bind(machine, Grounded.Move, Grounded);
            Bind(machine, Grounded.Sprint, Grounded);
            Bind(machine, Grounded.Dodge, Grounded);
            Bind(machine, Grounded.Motorcycle, Grounded);

            Bind(machine, Airborne, null);
            Bind(machine, Airborne.Jump, Airborne);
            Bind(machine, Airborne.Fall, Airborne);
            Bind(machine, Airborne.Glide, Airborne);
            Bind(machine, Airborne.WingFlight, Airborne);
            Bind(machine, Airborne.SwordFlight, Airborne);

            Bind(machine, Climb, null);

            Bind(machine, Swim, null);
            Bind(machine, Swim.Idle, Swim);
            Bind(machine, Swim.Move, Swim);

            Bind(machine, Combat, null);
            Bind(machine, Combat.Attack, Combat);
            Bind(machine, Combat.Skill, Combat);
            Bind(machine, Combat.SkillR, Combat);
        }

        static void Bind(HStateMachine machine, HState state, HState parent)
        {
            state.Bind(machine, parent);
        }
    }
}
