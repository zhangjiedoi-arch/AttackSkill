using UnityEngine;
using AttackSkill.Combat;

namespace AttackSkill.Character.HSM
{
    /// <summary>
    /// 状态共享黑板：输入、电机、环境探测、战斗计数。
    /// </summary>
    public class CharacterContext
    {
        public Transform Transform;
        public Transform CameraYaw;
        public Animator Animator;
        public CharacterMotor Motor;
        public CharacterMotorSettings Settings;
        public ICharacterInputSource InputSource;
        public AttackHitRelay AttackHits;
        public CharacterAudio Audio;
        public CharacterSkillPlayer SkillPlayer;

        public CharacterInput Input;
        public bool CanGlide = true;
        public bool IsInWater;
        public bool IsNearClimbable;
        public Vector3 ClimbNormal = Vector3.back;
        /// <summary>上一帧是否在空中，用于落地音。</summary>
        public bool WasAirborne;

        public int AttackComboIndex;
        public float LastAttackTime;
        public float LastDodgeTime = -999f;

        public GenshinLikeCharacter Owner;

        public bool CanDodge =>
            Settings != null && Time.time >= LastDodgeTime + Settings.DodgeCooldown;

        public void RefreshInput()
        {
            Input = InputSource != null ? InputSource.Read() : default;
        }

        public void SetAnimBool(int hash, bool value)
        {
            if (Animator != null)
            {
                Animator.SetBool(hash, value);
            }
        }

        public void SetAnimTrigger(int hash)
        {
            if (Animator != null)
            {
                Animator.SetTrigger(hash);
            }
        }

        public void ResetAnimTrigger(int hash)
        {
            if (Animator != null)
            {
                Animator.ResetTrigger(hash);
            }
        }

        public void SetAnimInt(int hash, int value)
        {
            if (Animator != null)
            {
                Animator.SetInteger(hash, value);
            }
        }

        public void SetAnimFloat(int hash, float value)
        {
            if (Animator != null)
            {
                Animator.SetFloat(hash, value);
            }
        }
    }
}
