using UnityEngine;

namespace AttackSkill.Enemy
{
    /// <summary>基于 CharacterController 的简易敌人移动。</summary>
    public class EnemyMotor
    {
        readonly CharacterController _controller;
        readonly Transform _transform;
        float _moveSpeed;
        float _turnSpeed;
        float _gravity = -25f;

        public Vector3 Velocity { get; private set; }
        public bool IsGrounded => _controller != null && _controller.enabled && _controller.isGrounded;

        public EnemyMotor(CharacterController controller, Transform transform)
        {
            _controller = controller;
            _transform = transform;
        }

        public void Configure(float moveSpeed, float turnSpeed)
        {
            _moveSpeed = moveSpeed;
            _turnSpeed = turnSpeed;
        }

        public void Stop()
        {
            Velocity = new Vector3(0f, Velocity.y, 0f);
        }

        public void MoveTowards(Vector3 worldTarget, float deltaTime, float speedScale = 1f)
        {
            Vector3 flat = worldTarget - _transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.0001f)
            {
                Vector3 dir = flat.normalized;
                FaceDirection(dir, deltaTime);
                Velocity = new Vector3(dir.x * _moveSpeed * speedScale, Velocity.y, dir.z * _moveSpeed * speedScale);
            }
            else
            {
                Velocity = new Vector3(0f, Velocity.y, 0f);
            }

            Tick(deltaTime);
        }

        public void FaceDirection(Vector3 dir, float deltaTime)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Quaternion target = Quaternion.LookRotation(dir.normalized, Vector3.up);
            _transform.rotation = Quaternion.Slerp(_transform.rotation, target, _turnSpeed * deltaTime);
        }

        public void Tick(float deltaTime)
        {
            if (_controller == null || !_controller.enabled)
            {
                return;
            }

            float vy = Velocity.y;
            if (_controller.isGrounded && vy < 0f)
            {
                vy = -2f;
            }
            else
            {
                vy += _gravity * deltaTime;
            }

            Velocity = new Vector3(Velocity.x, vy, Velocity.z);
            _controller.Move(Velocity * deltaTime);
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            bool wasEnabled = _controller != null && _controller.enabled;
            if (_controller != null)
            {
                _controller.enabled = false;
            }

            _transform.SetPositionAndRotation(position, rotation);
            Velocity = Vector3.zero;

            if (_controller != null)
            {
                _controller.enabled = wasEnabled;
            }
        }
    }
}
