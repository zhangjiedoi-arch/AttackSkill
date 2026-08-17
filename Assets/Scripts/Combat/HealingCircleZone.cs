using AttackSkill.Character;
using AttackSkill.Character.HSM;
using AttackSkill.Enemy;
using AttackSkill.Rouge;
using UnityEngine;

namespace AttackSkill.Combat
{
    /// <summary>
    /// 地面治疗圈：范围内 Active 玩家每秒回血；在玩家 Hit_Root 挂 Healing 特效（对象池）。
    /// </summary>
    public class HealingCircleZone : MonoBehaviour
    {
        [SerializeField] float radius = 3f;
        [SerializeField] float healPerSecond = 100f;
        [SerializeField] float lifetime = 20f;
        [SerializeField] GameObject healingAuraPrefab;

        float _expireAt;
        bool _playerInside;
        GameObject _auraInstance;
        bool _ownedByPool;

        public static HealingCircleZone Spawn(
            Vector3 worldPos,
            GameObject circlePrefab,
            GameObject auraPrefab,
            float radius = 3f,
            float healPerSecond = 100f,
            float lifetime = 20f)
        {
            if (circlePrefab == null)
            {
                return null;
            }

            GameObject go = VfxObjectPool.Spawn(circlePrefab, worldPos, Quaternion.identity);
            if (go == null)
            {
                go = Object.Instantiate(circlePrefab, worldPos, Quaternion.identity);
            }

            var zone = go.GetComponent<HealingCircleZone>();
            if (zone == null)
            {
                zone = go.AddComponent<HealingCircleZone>();
            }

            zone.Configure(auraPrefab, radius, healPerSecond, lifetime, ownedByPool: go.GetComponent<VfxPoolMember>() != null);
            return zone;
        }

        public void Configure(
            GameObject auraPrefab,
            float healRadius,
            float healRate,
            float life,
            bool ownedByPool)
        {
            healingAuraPrefab = auraPrefab;
            radius = Mathf.Max(0.5f, healRadius);
            healPerSecond = Mathf.Max(0f, healRate);
            lifetime = Mathf.Max(1f, life);
            _ownedByPool = ownedByPool;
            _expireAt = Time.time + lifetime;
            _playerInside = false;
            ClearAura();

            if (healingAuraPrefab != null)
            {
                VfxObjectPool.Prewarm(healingAuraPrefab, 2);
            }
        }

        void Update()
        {
            if (Time.time >= _expireAt)
            {
                DespawnSelf();
                return;
            }

            Transform player = PlayerTargetLocator.GetActivePlayerTransform();
            if (player == null)
            {
                SetPlayerInside(false, null);
                return;
            }

            Vector3 delta = player.position - transform.position;
            delta.y = 0f;
            bool inside = delta.sqrMagnitude <= radius * radius;
            SetPlayerInside(inside, player);

            if (!inside)
            {
                return;
            }

            var health = player.GetComponentInParent<Health>();
            if (health != null && health.IsAlive)
            {
                float rate = healPerSecond + RougePassiveEffects.HealCircleBonusPerSecond;
                health.Heal(Mathf.Max(0f, rate) * Time.deltaTime);
            }
        }

        void SetPlayerInside(bool inside, Transform player)
        {
            if (inside == _playerInside)
            {
                if (inside)
                {
                    EnsureAuraFollows(player);
                }

                return;
            }

            _playerInside = inside;
            if (inside)
            {
                AttachAura(player);
            }
            else
            {
                ClearAura();
            }
        }

        void AttachAura(Transform player)
        {
            if (healingAuraPrefab == null || player == null)
            {
                return;
            }

            Transform hitRoot = ResolveHitRoot(player);
            if (hitRoot == null)
            {
                hitRoot = player;
            }

            ClearAura();
            _auraInstance = VfxObjectPool.Spawn(healingAuraPrefab, hitRoot.position, hitRoot.rotation);
            if (_auraInstance == null)
            {
                return;
            }

            _auraInstance.transform.SetParent(hitRoot, false);
            _auraInstance.transform.localPosition = Vector3.zero;
            _auraInstance.transform.localRotation = Quaternion.identity;
        }

        void EnsureAuraFollows(Transform player)
        {
            if (_auraInstance == null)
            {
                AttachAura(player);
                return;
            }

            Transform hitRoot = ResolveHitRoot(player);
            if (hitRoot != null && _auraInstance.transform.parent != hitRoot)
            {
                _auraInstance.transform.SetParent(hitRoot, false);
                _auraInstance.transform.localPosition = Vector3.zero;
                _auraInstance.transform.localRotation = Quaternion.identity;
            }
        }

        void ClearAura()
        {
            if (_auraInstance == null)
            {
                return;
            }

            VfxObjectPool.RecycleNow(_auraInstance);
            _auraInstance = null;
        }

        void DespawnSelf()
        {
            ClearAura();
            if (_ownedByPool)
            {
                VfxObjectPool.RecycleNow(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        void OnDisable()
        {
            ClearAura();
            _playerInside = false;
        }

        static Transform ResolveHitRoot(Transform player)
        {
            if (player == null)
            {
                return null;
            }

            var avatar = player.GetComponentInParent<CharacterAvatar>();
            if (avatar == null)
            {
                avatar = player.GetComponentInChildren<CharacterAvatar>(true);
            }

            if (avatar != null)
            {
                if (avatar.Hits == null || avatar.Hits.Root == null)
                {
                    avatar.AutoBind();
                }

                if (avatar.Hits != null && avatar.Hits.Root != null)
                {
                    return avatar.Hits.Root;
                }
            }

            return FindChildExact(player.root != null ? player.root : player, CharacterAvatar.HitRootName);
        }

        static Transform FindChildExact(Transform root, string exactName)
        {
            if (root == null || string.IsNullOrEmpty(exactName))
            {
                return null;
            }

            var all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == exactName)
                {
                    return all[i];
                }
            }

            return null;
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 1f, 0.45f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
#endif
    }
}
