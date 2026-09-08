using UnityEngine;

namespace AttackSkill.Character.HSM
{
    public enum CombatBufferAction
    {
        Attack = 0,
        Dodge = 1,
        Skill = 2,
        SkillR = 3
    }

    /// <summary>
    /// 战斗边沿输入短缓冲（约 6 帧）。Move/Jump 不进缓冲。
    /// 用 unscaledTime，暂停冻时不会把过期键拖到解冻后释放。
    /// </summary>
    public sealed class CombatInputBuffer
    {
        public const float DefaultLifetime = 0.1f;

        readonly float[] _expireAt = new float[4];

        public void Clear()
        {
            for (int i = 0; i < _expireAt.Length; i++)
            {
                _expireAt[i] = 0f;
            }
        }

        public void Capture(in CharacterInput input, float unscaledNow, float lifetime)
        {
            float life = lifetime > 0.01f ? lifetime : DefaultLifetime;
            if (input.AttackPressed)
            {
                Stamp(CombatBufferAction.Attack, unscaledNow, life);
            }

            if (input.DodgePressed)
            {
                Stamp(CombatBufferAction.Dodge, unscaledNow, life);
            }

            if (input.SkillPressed)
            {
                Stamp(CombatBufferAction.Skill, unscaledNow, life);
            }

            if (input.SkillRPressed)
            {
                Stamp(CombatBufferAction.SkillR, unscaledNow, life);
            }
        }

        public bool IsPending(CombatBufferAction action, float unscaledNow)
        {
            Expire(action, unscaledNow);
            return _expireAt[(int)action] > 0f;
        }

        public bool TryConsume(CombatBufferAction action, float unscaledNow)
        {
            if (!IsPending(action, unscaledNow))
            {
                return false;
            }

            _expireAt[(int)action] = 0f;
            return true;
        }

        void Stamp(CombatBufferAction action, float unscaledNow, float lifetime)
        {
            _expireAt[(int)action] = unscaledNow + lifetime;
        }

        void Expire(CombatBufferAction action, float unscaledNow)
        {
            int i = (int)action;
            if (_expireAt[i] > 0f && unscaledNow >= _expireAt[i])
            {
                _expireAt[i] = 0f;
            }
        }
    }
}
