using AttackSkill.Character.HSM;
using UnityEngine;

namespace AttackSkill.Character.Exploration
{
    /// <summary>
    /// 御剑 / 翅膀飞行视觉倾斜：由 WASD 输入驱动俯仰与侧倾（最大 45°），挂在 Avatar 子物体上。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FlightVisualTilt : MonoBehaviour
    {
        float _pitch;
        float _bank;

        public static FlightVisualTilt Ensure(Transform motorRoot, CharacterAvatar avatar)
        {
            Transform visual = ResolveVisualRoot(motorRoot, avatar);
            if (visual == null)
            {
                return null;
            }

            var tilt = visual.GetComponent<FlightVisualTilt>();
            if (tilt == null)
            {
                tilt = visual.gameObject.AddComponent<FlightVisualTilt>();
            }

            return tilt;
        }

        public static Transform ResolveVisualRoot(Transform motorRoot, CharacterAvatar avatar)
        {
            if (motorRoot == null)
            {
                return null;
            }

            if (avatar != null &&
                avatar.transform != null &&
                avatar.transform != motorRoot &&
                avatar.transform.IsChildOf(motorRoot))
            {
                return avatar.transform;
            }

            var animator = motorRoot.GetComponentInChildren<Animator>(true);
            if (animator != null &&
                animator.transform != motorRoot &&
                animator.transform.IsChildOf(motorRoot))
            {
                return animator.transform;
            }

            return null;
        }

        /// <summary>按目标俯仰/侧倾角平滑更新（度）。</summary>
        public void Tick(float targetPitchDeg, float targetBankDeg, CharacterMotorSettings settings, float deltaTime)
        {
            if (settings == null || deltaTime <= 0f)
            {
                return;
            }

            float maxPitch = Mathf.Max(settings.FlightPitchAscendDegrees, settings.FlightPitchDescendDegrees);
            float maxBank = settings.FlightBankDegrees;
            targetPitchDeg = Mathf.Clamp(targetPitchDeg, -maxPitch, maxPitch);
            targetBankDeg = Mathf.Clamp(targetBankDeg, -maxBank, maxBank);

            float lerp = 1f - Mathf.Exp(-settings.FlightTiltSmooth * deltaTime);
            _pitch = Mathf.Lerp(_pitch, targetPitchDeg, lerp);
            _bank = Mathf.Lerp(_bank, targetBankDeg, lerp);
            transform.localRotation = Quaternion.Euler(_pitch, 0f, _bank);
        }

        public void ResetTilt(bool immediate, CharacterMotorSettings settings, float deltaTime)
        {
            if (immediate || settings == null || deltaTime <= 0f)
            {
                _pitch = 0f;
                _bank = 0f;
                transform.localRotation = Quaternion.identity;
                return;
            }

            float lerp = 1f - Mathf.Exp(-settings.FlightTiltSmooth * 1.5f * deltaTime);
            _pitch = Mathf.Lerp(_pitch, 0f, lerp);
            _bank = Mathf.Lerp(_bank, 0f, lerp);
            transform.localRotation = Quaternion.Euler(_pitch, 0f, _bank);
            if (Mathf.Abs(_pitch) < 0.05f && Mathf.Abs(_bank) < 0.05f)
            {
                _pitch = 0f;
                _bank = 0f;
                transform.localRotation = Quaternion.identity;
            }
        }
    }
}
