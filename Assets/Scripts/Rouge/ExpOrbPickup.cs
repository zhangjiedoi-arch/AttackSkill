using System.Collections.Generic;
using AttackSkill.Combat;
using AttackSkill.Enemy;
using UnityEngine;

namespace AttackSkill.Rouge
{
    /// <summary>经验球：靠近 Active 玩家自动拾取。</summary>
    public class ExpOrbPickup : MonoBehaviour
    {
        [SerializeField] int expAmount = 10;
        [SerializeField] float lifetime = 20f;
        [SerializeField] float pickRadius = 3f;

        float _expireAt;
        bool _collected;
        bool _configured;

        static readonly List<ExpOrbPickup> Live = new List<ExpOrbPickup>(64);

        public static void ClearAll()
        {
            if (Live.Count == 0)
            {
                return;
            }

            var snap = Live.ToArray();
            Live.Clear();
            for (int i = 0; i < snap.Length; i++)
            {
                if (snap[i] != null)
                {
                    Destroy(snap[i].gameObject);
                }
            }
        }

        public void Configure(int exp, float life, float radius)
        {
            expAmount = Mathf.Max(1, exp);
            lifetime = Mathf.Max(1f, life);
            pickRadius = Mathf.Max(0.5f, radius);
            _expireAt = Time.time + lifetime;
            _collected = false;
            _configured = true;
        }

        void OnEnable()
        {
            if (!_configured)
            {
                var cfg = RougeCatalog.ExpOrb;
                Configure(cfg.expAmount, cfg.lifetime, cfg.pickRadius);
            }

            if (!Live.Contains(this))
            {
                Live.Add(this);
            }
        }

        void OnDisable()
        {
            Live.Remove(this);
        }

        void Update()
        {
            if (_collected)
            {
                return;
            }

            if (Time.time >= _expireAt)
            {
                Destroy(gameObject);
                return;
            }

            transform.Rotate(Vector3.up, 90f * Time.deltaTime, Space.World);

            Transform player = PlayerTargetLocator.GetActivePlayerTransform();
            if (player == null)
            {
                return;
            }

            Vector3 delta = player.position - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude > pickRadius * pickRadius)
            {
                return;
            }

            Collect();
        }

        void Collect()
        {
            if (_collected)
            {
                return;
            }

            _collected = true;
            PartyRougeProgress.AddExp(expAmount);
            Destroy(gameObject);
        }

        public static ExpOrbPickup Spawn(Vector3 worldPos, GameObject prefab)
        {
            if (prefab == null)
            {
                return SpawnFallback(worldPos);
            }

            var cfg = RougeCatalog.ExpOrb;
            Vector3 pos = SnapToGround(worldPos);
            var go = Object.Instantiate(prefab, pos, Quaternion.identity);
            go.name = "ExpOrb";
            go.SetActive(true);
            return FinalizeOrb(go, cfg);
        }

        public static ExpOrbPickup SpawnFallback(Vector3 worldPos)
        {
            var cfg = RougeCatalog.ExpOrb;
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "ExpOrb";
            go.transform.position = SnapToGround(worldPos);
            go.transform.localScale = Vector3.one * 0.45f;

            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
            }

            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Standard"))
                {
                    color = new Color(0.25f, 0.95f, 1f, 0.95f)
                };
            }

            return FinalizeOrb(go, cfg);
        }

        static ExpOrbPickup FinalizeOrb(GameObject go, RougeExpOrbConfig cfg)
        {
            if (go == null)
            {
                return null;
            }

            var orb = go.GetComponent<ExpOrbPickup>();
            if (orb == null)
            {
                orb = go.AddComponent<ExpOrbPickup>();
            }

            var col = go.GetComponent<SphereCollider>();
            if (col == null)
            {
                col = go.AddComponent<SphereCollider>();
            }

            col.isTrigger = true;
            col.radius = Mathf.Max(col.radius, 0.35f);
            orb.Configure(cfg.expAmount, cfg.lifetime, cfg.pickRadius);
            return orb;
        }

        const float GroundHover = 0.28f;
        const float GroundRayUp = 2f;
        const float GroundRayDown = 24f;

        static Vector3 SnapToGround(Vector3 pos)
        {
            int mask = CombatLayers.DefaultCameraCollisionMask;
            int enemy = CombatLayers.EnemyLayer;
            if (enemy >= 0)
            {
                mask &= ~(1 << enemy);
            }

            Vector3 origin = pos + Vector3.up * GroundRayUp;
            if (Physics.Raycast(
                    origin,
                    Vector3.down,
                    out RaycastHit hit,
                    GroundRayUp + GroundRayDown,
                    mask,
                    QueryTriggerInteraction.Ignore))
            {
                Vector3 grounded = hit.point;
                grounded.y += GroundHover;
                return grounded;
            }

            pos.y += GroundHover;
            return pos;
        }
    }
}
