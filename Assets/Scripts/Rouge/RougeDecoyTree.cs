using AttackSkill.Character.HSM;
using AttackSkill.Combat;
using UnityEngine;

namespace AttackSkill.Rouge
{
    /// <summary>诱敌之树：10m 内敌人优先打它；基础 2000 血，每级 +10%，满 5 级。</summary>
    public sealed class RougeDecoyTree : MonoBehaviour
    {
        public const float BaseHp = 2000f;
        public const float ExtraHpPerStack = 0.10f;
        public const float TauntRadius = 10f;
        public const float RespawnDelay = 5f;
        public const float SpawnMinRadius = 10f;
        public const float SpawnMaxRadius = 20f;

        static RougeDecoyTree _alive;

        Health _health;
        int _stack;
        bool _placed;

        float _respawnAt = -1f;
        bool _dead;

        public static RougeDecoyTree AliveInstance =>
            _alive != null && _alive.IsAlive ? _alive : null;

        public bool IsAlive => !_dead && _health != null && _health.IsAlive;

        public static float MaxHpForStack(int stack)
        {
            int s = Mathf.Clamp(stack, 1, 5);
            return BaseHp * (1f + ExtraHpPerStack * (s - 1));
        }

        public static bool IsDecoy(Component hint)
        {
            return hint != null && hint.GetComponentInParent<RougeDecoyTree>() != null;
        }

        public static bool TryGetTauntTarget(Vector3 from, out Transform target)
        {
            target = null;
            var tree = AliveInstance;
            if (tree == null)
            {
                return false;
            }

            Vector3 delta = tree.transform.position - from;
            delta.y = 0f;
            if (delta.sqrMagnitude > TauntRadius * TauntRadius)
            {
                return false;
            }

            target = tree.transform;
            return true;
        }

        public void Configure(int stack)
        {
            int next = Mathf.Clamp(stack, 1, 5);
            bool refill = next != _stack;
            _stack = next;
            EnsureCombat();
            ApplyHp(refill: refill || !_dead && _health.CurrentHp <= 0.01f);
            if (!_dead)
            {
                _alive = this;
            }
        }

        public void TickWorld(GenshinLikeCharacter owner)
        {
            if (owner == null)
            {
                return;
            }

            if (!_placed)
            {
                RelocateNear(owner);
            }

            if (_dead)
            {
                if (_respawnAt > 0f && Time.time >= _respawnAt)
                {
                    RelocateNear(owner);
                    Respawn();
                }
            }
        }

        public void RelocateNear(GenshinLikeCharacter owner)
        {
            if (owner == null)
            {
                return;
            }

            Vector3 pos = RougeConstructPlacement.PickRing(
                owner.transform.position,
                SpawnMinRadius,
                SpawnMaxRadius);
            transform.SetPositionAndRotation(pos, Quaternion.identity);
            _placed = true;
        }

        void OnEnable()
        {
            _alive = this;
        }

        void OnDisable()
        {
            if (_alive == this)
            {
                _alive = null;
            }
        }

        void OnDestroy()
        {
            if (_health != null)
            {
                _health.Died -= OnDied;
            }

            if (_alive == this)
            {
                _alive = null;
            }
        }

        void EnsureCombat()
        {
            int playerLayer = CombatLayers.PlayerLayer;
            if (playerLayer >= 0)
            {
                CombatLayers.ApplyLayerRecursively(gameObject, playerLayer);
            }

            var box = GetComponentInChildren<BoxCollider>(true);
            if (box != null)
            {
                box.isTrigger = true;
                box.enabled = true;
            }

            PlayerHurtbox.Ensure(gameObject);

            _health = GetComponent<Health>();
            if (_health == null)
            {
                _health = gameObject.AddComponent<Health>();
            }

            _health.ConfigureDefense(enableIFrames: false, enableHitStun: false);
            _health.Died -= OnDied;
            _health.Died += OnDied;
        }

        void ApplyHp(bool refill)
        {
            if (_health == null)
            {
                return;
            }

            float ratio = refill || _health.MaxHp < 0.01f
                ? 1f
                : _health.CurrentHp / _health.MaxHp;
            _health.Configure(MaxHpForStack(_stack), destroyWhenDead: false);
            if (refill)
            {
                _health.ReviveFull();
            }
            else
            {
                _health.SetCurrentHp(_health.MaxHp * ratio);
            }
        }

        void OnDied()
        {
            _dead = true;
            _respawnAt = Time.time + RespawnDelay;
            if (_alive == this)
            {
                _alive = null;
            }

            SetVisualActive(false);
        }

        void Respawn()
        {
            _dead = false;
            _respawnAt = -1f;
            SetVisualActive(true);
            ApplyHp(refill: true);
            _alive = this;
        }

        void SetVisualActive(bool active)
        {
            var renders = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renders.Length; i++)
            {
                if (renders[i] != null)
                {
                    renders[i].enabled = active;
                }
            }

            var cols = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] != null)
                {
                    cols[i].enabled = active;
                }
            }
        }
    }
}
