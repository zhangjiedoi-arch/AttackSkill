using AttackSkill.Character.HSM;
using UnityEngine;

namespace AttackSkill.Character.Exploration
{
    /// <summary>工具运行时上下文（不直接塞整个角色 API 以外的依赖）。</summary>
    public readonly struct ExplorationToolContext
    {
        public readonly GenshinLikeCharacter Owner;
        public readonly CharacterContext Character;

        public ExplorationToolContext(GenshinLikeCharacter owner, CharacterContext character)
        {
            Owner = owner;
            Character = character;
        }

        public Transform Transform => Character != null ? Character.Transform : null;
        public Transform CameraYaw => Character != null ? Character.CameraYaw : null;
        public CharacterMotor Motor => Character != null ? Character.Motor : null;
        public CharacterMotorSettings Settings => Character != null ? Character.Settings : null;
        public CharacterInput Input => Character != null ? Character.Input : default;
        public CharacterAudio Audio => Character != null ? Character.Audio : null;
        public bool IsInWater => Character != null && Character.IsInWater;

        public void SetAnimBool(int hash, bool value) => Character?.SetAnimBool(hash, value);
        public void SetAnimFloat(int hash, float value) => Character?.SetAnimFloat(hash, value);
        public void SetAnimTrigger(int hash) => Character?.SetAnimTrigger(hash);
    }
}
