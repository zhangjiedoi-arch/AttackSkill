namespace AttackSkill.Enemy
{
    public interface IEnemyState
    {
        string Name { get; }
        void Enter(EnemyAgent agent);
        void Exit(EnemyAgent agent);
        void Tick(EnemyAgent agent, float deltaTime);
    }
}
