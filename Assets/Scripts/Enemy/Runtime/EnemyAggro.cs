using UnityEngine;

namespace AttackSkill.Enemy
{
    public class EnemyAggro
    {
        Transform _target;
        float _loseTimer;
        float _loseTargetTime = 2.5f;

        public Transform CurrentTarget => _target;
        public bool HasTarget => _target != null;

        public void Configure(float loseTargetTime)
        {
            _loseTargetTime = loseTargetTime;
        }

        public void SetTarget(Transform t)
        {
            _target = t;
            _loseTimer = 0f;
        }

        public void Clear()
        {
            _target = null;
            _loseTimer = 0f;
        }

        public void Tick(float deltaTime, bool currentlyPerceived)
        {
            // 目标失效（切人销毁/残留）时清掉
            if (_target != null)
            {
                var character = _target.GetComponentInParent<Character.HSM.GenshinLikeCharacter>();
                if (character != null && !character.IsActive)
                {
                    // 尝试切到新的 Active
                    Transform active = PlayerTargetLocator.GetActivePlayerTransform();
                    if (active != null)
                    {
                        _target = active;
                    }
                    else
                    {
                        Clear();
                        return;
                    }
                }
            }

            if (_target == null)
            {
                return;
            }

            if (currentlyPerceived)
            {
                _loseTimer = 0f;
            }
            else
            {
                _loseTimer += deltaTime;
                if (_loseTimer >= _loseTargetTime)
                {
                    Clear();
                }
            }
        }
    }
}
