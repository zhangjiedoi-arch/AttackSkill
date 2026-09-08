namespace AttackSkill.Game
{
    /// <summary>GameScene 内循环阶段（开场 Title 仍归 OpenSceneFlow）。</summary>
    public enum RunPhase
    {
        Booting,
        BeachExplore,
        RougeCombat,
        GameOver,
        Transition
    }
}
