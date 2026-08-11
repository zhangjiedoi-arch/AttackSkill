using UnityEngine;
using AttackSkill.Combat;

namespace AttackSkill.Enemy
{
    /// <summary>
    /// 敌人动画 Event 出伤。须挂在带 Animator 的同一物体上。
    /// 推荐 Event：SkillHit(int)；兼容：Enemy_Hit_Chest_R。
    /// </summary>
    public class EnemyAttackHitRelay : MonoBehaviour
    {
        public const string HitChestRSocketName = "Enemy_Hit_Chest_R";

        [Header("Refs")]
        [SerializeField] Transform ownerRoot;
        [SerializeField] Transform hitChestR;
        [SerializeField] EnemyAgent agent;
        [SerializeField] SkillHitProfile skillHitProfile;
        [Tooltip("留空时用 Player 层")]
        [SerializeField] LayerMask hurtboxMask = ~0;

        [Header("Overrides（≥0 覆盖 Profile / Definition）")]
        [SerializeField] float damageOverride = -1f;
        [SerializeField] float knockbackOverride = -1f;

        [Header("Gizmos")]
        [SerializeField] bool drawHitGizmos = true;

        readonly HitSession _session = new HitSession();
        Collider[] _overlapBuffer = new Collider[24];
        Collider[] _overlapScratch = new Collider[24];

        struct Flash
        {
            public bool Active;
            public Vector3 Center;
            public float Radius;
            public float Until;
        }

        Flash _flash;

        public Transform OwnerRoot =>
            ownerRoot != null ? ownerRoot : (agent != null ? agent.transform : transform.root);

        void Awake()
        {
            if (agent == null)
            {
                agent = GetComponentInParent<EnemyAgent>();
            }

            if (ownerRoot == null && agent != null)
            {
                ownerRoot = agent.transform;
            }

            if (hurtboxMask.value == ~0 || hurtboxMask.value == -1)
            {
                hurtboxMask = CombatLayers.DefaultPlayerHurtboxMask;
            }

            EnsureSockets();
            EnsureSkillHitProfile();
        }

        public void SetSkillHitProfile(SkillHitProfile profile)
        {
            skillHitProfile = profile;
        }

        /// <summary>推荐动画 Event：段下标。</summary>
        public void SkillHit(int segmentIndex)
        {
            EnsureSockets();
            SkillHitProfile profile = EnsureSkillHitProfile();
            if (profile == null || !profile.TryGetSegment(segmentIndex, out SkillHitSegment segment))
            {
                Debug.LogWarning($"[EnemyAttackHit] SkillHit({segmentIndex}) 无有效段。", this);
                return;
            }

            SkillHitSegment runtime = BuildRuntimeSegment(segment);
            Transform socket = HitSocketResolver.Resolve(OwnerRoot, runtime.socket);
            if (socket == null && hitChestR != null && runtime.socket == HitSocketId.Enemy_Hit_Chest_R)
            {
                socket = hitChestR;
            }

            if (socket != null)
            {
                _flash = new Flash
                {
                    Active = true,
                    Center = socket.position,
                    Radius = runtime.radius,
                    Until = Time.time + 0.35f,
                };
            }

            SkillHitExecutor.Execute(runtime, BuildContext());
        }

        /// <summary>兼容旧 Event 名。</summary>
        public void Enemy_Hit_Chest_R()
        {
            SkillHitProfile profile = EnsureSkillHitProfile();
            if (profile == null)
            {
                return;
            }

            int index = profile.FindIndexBySocket(HitSocketId.Enemy_Hit_Chest_R);
            SkillHit(index >= 0 ? index : 0);
        }

        SkillHitSegment BuildRuntimeSegment(SkillHitSegment source)
        {
            return new SkillHitSegment
            {
                id = source.id,
                socket = source.socket,
                shape = source.shape,
                radius = source.radius,
                height = source.height,
                fanAngle = source.fanAngle,
                minHitDistance = source.minHitDistance,
                hitHeight = source.hitHeight,
                damage = ResolveDamage(source),
                knockback = ResolveKnockback(source),
                vfxPrefab = source.vfxPrefab,
                vfxLife = source.vfxLife,
                parentVfxToSocket = source.parentVfxToSocket,
                sfxClip = source.sfxClip,
                sfxVolume = source.sfxVolume,
            };
        }

        float ResolveDamage(SkillHitSegment segment)
        {
            if (damageOverride >= 0f)
            {
                return damageOverride;
            }

            if (agent != null && agent.Definition != null)
            {
                return agent.Definition.attackDamage;
            }

            return segment != null ? segment.damage : 12f;
        }

        float ResolveKnockback(SkillHitSegment segment)
        {
            if (knockbackOverride >= 0f)
            {
                return knockbackOverride;
            }

            if (agent != null && agent.Definition != null)
            {
                return agent.Definition.attackKnockback;
            }

            return segment != null ? segment.knockback : 1.2f;
        }

        SkillHitExecutor.Context BuildContext()
        {
            Transform root = OwnerRoot != null ? OwnerRoot : transform;
            Vector3 forward = root.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = Vector3.forward;
            }

            return new SkillHitExecutor.Context
            {
                OwnerRoot = root,
                Attacker = agent != null ? agent.gameObject : root.gameObject,
                Mask = hurtboxMask,
                Flags = HitResolver.DefaultEnemyOffense,
                Session = _session,
                Buffer = _overlapBuffer,
                Scratch = _overlapScratch,
                PlanarForward = forward.normalized,
                ComboIndex = 0,
                ClearSession = true,
                DrawDebug = false,
                LogContext = this,
            };
        }

        SkillHitProfile EnsureSkillHitProfile()
        {
            if (skillHitProfile == null && agent != null && agent.Definition != null)
            {
                skillHitProfile = agent.Definition.skillHitProfile;
            }

            if (skillHitProfile == null)
            {
                skillHitProfile = Resources.Load<SkillHitProfile>("Combat/SkillHit_Enemy_Basic");
            }

            if (skillHitProfile == null)
            {
                float dmg = agent != null && agent.Definition != null ? agent.Definition.attackDamage : 12f;
                float kb = agent != null && agent.Definition != null ? agent.Definition.attackKnockback : 1.2f;
                skillHitProfile = SkillHitProfileDefaults.EnemyBasic(dmg, kb, 1.5f);
            }

            return skillHitProfile;
        }

        void EnsureSockets()
        {
            Transform root = OwnerRoot;
            if (root == null)
            {
                return;
            }

            if (hitChestR == null)
            {
                hitChestR = HitSocketResolver.Resolve(root, HitSocketId.Enemy_Hit_Chest_R);
            }

            if (hitChestR == null)
            {
                hitChestR = FindChildExact(root, HitChestRSocketName);
            }
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

        void LateUpdate()
        {
            if (!drawHitGizmos || !Application.isPlaying)
            {
                return;
            }

            EnsureSockets();
            SkillHitSegment seg = GetPrimarySegment();
            float radius = seg != null ? seg.radius : 1.5f;
            if (hitChestR != null)
            {
                DrawDebugSphere(hitChestR.position, radius, new Color(1f, 0.35f, 0.2f, 1f));
            }

            if (_flash.Active && Time.time <= _flash.Until)
            {
                DrawDebugSphere(_flash.Center, _flash.Radius, new Color(1f, 0.1f, 0.05f, 1f));
            }
            else if (_flash.Active)
            {
                _flash.Active = false;
            }
        }

        SkillHitSegment GetPrimarySegment()
        {
            SkillHitProfile profile = EnsureSkillHitProfile();
            if (profile == null)
            {
                return null;
            }

            int index = profile.FindIndexBySocket(HitSocketId.Enemy_Hit_Chest_R);
            if (index < 0)
            {
                index = 0;
            }

            return profile.TryGetSegment(index, out SkillHitSegment seg) ? seg : null;
        }

        static void DrawDebugSphere(Vector3 center, float radius, Color color)
        {
            const int segments = 24;
            float r = Mathf.Max(0.05f, radius);
            DrawDebugCircle(center, Vector3.up, r, color, segments);
            DrawDebugCircle(center, Vector3.right, r, color, segments);
            DrawDebugCircle(center, Vector3.forward, r, color, segments);
        }

        static void DrawDebugCircle(Vector3 center, Vector3 normal, float radius, Color color, int segments)
        {
            Vector3 axis = normal.normalized;
            Vector3 tangent = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.99f ? Vector3.up : Vector3.right).normalized;
            Vector3 bitangent = Vector3.Cross(axis, tangent);
            Vector3 prev = center + tangent * radius;
            for (int i = 1; i <= segments; i++)
            {
                float ang = (i / (float)segments) * Mathf.PI * 2f;
                Vector3 next = center + (tangent * Mathf.Cos(ang) + bitangent * Mathf.Sin(ang)) * radius;
                Debug.DrawLine(prev, next, color, 0f, false);
                prev = next;
            }
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (!drawHitGizmos)
            {
                return;
            }

            EnsureSockets();
            if (hitChestR == null)
            {
                return;
            }

            SkillHitSegment seg = GetPrimarySegment();
            float radius = seg != null ? seg.radius : 1.5f;
            Gizmos.color = new Color(1f, 0.4f, 0.15f, 0.95f);
            Gizmos.DrawWireSphere(hitChestR.position, radius);
            UnityEditor.Handles.color = Gizmos.color;
            UnityEditor.Handles.Label(
                hitChestR.position + Vector3.up * (radius + 0.1f),
                $"Enemy_Hit_Chest_R r={radius}");

            if (_flash.Active && (!Application.isPlaying || Time.time <= _flash.Until))
            {
                Gizmos.color = new Color(1f, 0.15f, 0.05f, 0.35f);
                Gizmos.DrawSphere(_flash.Center, _flash.Radius);
            }
        }
#endif
    }
}
