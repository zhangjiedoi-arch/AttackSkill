using UnityEngine;

namespace AttackSkill.Character.Exploration
{
    /// <summary>切人时继承的探索载具/飞行快照。</summary>
    public struct ExplorationLocomotionSnapshot
    {
        public ExplorationToolKind Kind;
        public Vector3 Velocity;
        public Vector3 PlanarVelocity;
        public float MotorcycleRideSpeed;
        public float MotorcycleJumpCooldownRemaining;

        public bool HasTool =>
            Kind == ExplorationToolKind.WingFlight ||
            Kind == ExplorationToolKind.SwordFlight ||
            Kind == ExplorationToolKind.Motorcycle;
    }
}
