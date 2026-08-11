using UnityEngine;
using AttackSkill.Character;

namespace AttackSkill.Combat
{    [System.Serializable]
    public class AttackSwingConfig
    {
        [Tooltip("本段伤害")]
        public float Damage = 10f;
        [Tooltip("扇形半径")]
        public float HitRadius = 2.2f;
        [Tooltip("扇形全角（度），以角色朝向为中心")]
        [Range(1f, 360f)]
        public float FanAngle = 90f;
        [Tooltip("扇形原点相对角色脚底的高度")]
        public float HitHeight = 0.9f;
        [Tooltip("内圈忽略距离，避免打到贴身碰撞")]
        public float MinHitDistance = 0.15f;
        [Tooltip("击退强度")]
        public float Knockback = 1.5f;
        [Tooltip("出刀特效（刀光等）")]
        public GameObject SlashVfxPrefab;
        [Tooltip("命中特效")]
        public GameObject HitVfxPrefab;
        [Tooltip("特效存活时间")]
        public float VfxLifeTime = 1.2f;
    }

    /// <summary>
    /// 接收动画事件，负责出刀特效与扇形伤害检测。
    /// 必须挂在带 Animator 的同一物体上（Animation Event 才能调到）。
    /// </summary>
    public class AttackHitRelay : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] Transform ownerRoot;
        [SerializeField] Transform hitOrigin;
        [Tooltip("刀光取此挂点的世界坐标/旋转，并挂到场景根节点")]
        [SerializeField] Transform vfxSocket;
        [Tooltip("刀剑 Transform；为空则按名称自动查找（大剑/Sword/Weapon 等）")]
        [SerializeField] Transform weapon;
        [Tooltip("留空/Everything 时运行时自动改为 Enemy 层")]
        [SerializeField] LayerMask hurtboxMask = ~0;
        [SerializeField] bool drawDebug = false;

        [Header("Combo 0/1/2 = attack1/2/3")]
        [SerializeField] AttackSwingConfig[] swings =
        {
            new AttackSwingConfig { Damage = 10f, HitRadius = 2.0f, FanAngle = 80f, Knockback = 1.2f },
            new AttackSwingConfig { Damage = 14f, HitRadius = 2.3f, FanAngle = 100f, Knockback = 1.6f },
            new AttackSwingConfig { Damage = 22f, HitRadius = 2.6f, FanAngle = 120f, Knockback = 2.4f }
        };

        [Header("Skill Hit Profile (E 技能等)")]
        [SerializeField] SkillHitProfile skillHitProfile;
        [SerializeField] Transform hitChestR;
        [SerializeField] Transform hitChestL;
        [SerializeField] Transform hitRoot;
        [Tooltip("Legacy 回填；优先用 Profile.segments[].vfxPrefab")]
        [SerializeField] GameObject snowHitVfxPrefab;
        [SerializeField] GameObject groundAoeExplosionVfxPrefab;
        [Tooltip("Scene 中绘制挂点球体/圆柱 Hitbox")]
        [SerializeField] bool drawSocketHitGizmos = true;
        [Tooltip("出伤瞬间加粗显示时长（秒）")]
        [SerializeField] float socketHitGizmoFlashSeconds = 0.35f;

        static readonly string[] WeaponNameKeys =
        {
            "大剑", "武器", "Weapon", "weapon", "Sword", "sword", "Blade", "blade", "Katana", "katana"
        };

        readonly HitSession _hitSession = new HitSession();
        int _activeCombo = -1;
        int _swingSerial;
        Collider[] _overlapBuffer = new Collider[32];
        Collider[] _overlapScratch = new Collider[32];
        bool _suppressAnimHits;
        bool _socketVfxPrewarmed;

        struct SocketGizmoFlash
        {
            public bool Active;
            public bool IsCylinder;
            public Vector3 CenterOrBottom;
            public float Radius;
            public float Height;
            public float Until;
        }

        SocketGizmoFlash _socketFlash;

        public Transform OwnerRoot => ownerRoot != null ? ownerRoot : transform.root;

        /// <summary>大招由 SkillHitWindow 出伤时打开，避免动画 Event 重复结算。</summary>
        public bool SuppressAnimHits
        {
            get => _suppressAnimHits;
            set => _suppressAnimHits = value;
        }

        /// <summary>运行时装配：绑定角色根与武器/刀光挂点，并可选填三段刀光 Prefab。</summary>
        public void ConfigurePresentation(
            Transform root,
            Transform weaponTransform,
            Transform vfxSocketTransform,
            Transform hitOriginTransform,
            GameObject slashVfxPrefab = null)
        {
            ConfigurePresentation(
                root,
                weaponTransform,
                vfxSocketTransform,
                hitOriginTransform,
                slashVfxPrefab,
                null,
                null,
                null,
                null,
                null);
        }

        public void ConfigurePresentation(
            Transform root,
            Transform weaponTransform,
            Transform vfxSocketTransform,
            Transform hitOriginTransform,
            GameObject slashVfxPrefab,
            Transform hitChestRTransform,
            Transform hitChestLTransform,
            Transform hitRootTransform,
            GameObject snowHitVfx,
            GameObject groundAoeVfx,
            SkillHitProfile profile = null)
        {
            if (root != null)
            {
                ownerRoot = root;
            }

            if (weaponTransform != null)
            {
                weapon = weaponTransform;
            }

            if (vfxSocketTransform != null)
            {
                vfxSocket = vfxSocketTransform;
            }

            if (hitOriginTransform != null)
            {
                hitOrigin = hitOriginTransform;
            }

            if (hitChestRTransform != null)
            {
                hitChestR = hitChestRTransform;
            }

            if (hitChestLTransform != null)
            {
                hitChestL = hitChestLTransform;
            }

            if (hitRootTransform != null)
            {
                hitRoot = hitRootTransform;
            }

            if (snowHitVfx != null)
            {
                snowHitVfxPrefab = snowHitVfx;
            }

            if (groundAoeVfx != null)
            {
                groundAoeExplosionVfxPrefab = groundAoeVfx;
            }

            if (profile != null)
            {
                skillHitProfile = profile;
            }

            if (hurtboxMask.value == ~0 || hurtboxMask.value == -1)
            {
                hurtboxMask = CombatLayers.PlayerOffenseHurtboxMask;
            }

            if (slashVfxPrefab != null && swings != null)
            {
                for (int i = 0; i < swings.Length; i++)
                {
                    if (swings[i] != null && swings[i].SlashVfxPrefab == null)
                    {
                        swings[i].SlashVfxPrefab = slashVfxPrefab;
                    }
                }
            }

            EnsureHitSockets();
            EnsureSkillHitProfile();
        }

        public void SetSkillHitProfile(SkillHitProfile profile)
        {
            skillHitProfile = profile;
            EnsureSkillHitProfile();
        }

        void Awake()
        {
            if (ownerRoot == null)
            {
                ownerRoot = transform.root;
            }

            // Everything / 未指定 → 玩家进攻用层（含 Default，避免只扫 Enemy 漏检）
            if (hurtboxMask.value == ~0 || hurtboxMask.value == -1)
            {
                hurtboxMask = CombatLayers.PlayerOffenseHurtboxMask;
            }

            if (weapon == null)
            {
                weapon = FindWeaponTransform(OwnerRoot);
            }

            if (vfxSocket == null)
            {
                vfxSocket = weapon;
            }

            if (hitOrigin == null)
            {
                hitOrigin = weapon != null ? weapon : transform;
            }

            EnsureHitSockets();
            EnsureSkillHitProfile();

            if (drawDebug && weapon != null)
            {
                Debug.Log($"[AttackHit] weapon={weapon.name} parent={(weapon.parent != null ? weapon.parent.name : "null")}", weapon);
            }

            // 大剑默认隐藏，仅普攻三段期间显示
            SetWeaponVisible(false);
        }

        /// <summary>
        /// 推荐动画 Event：只传段下标（与 SkillHitProfile.segments 对齐）。
        /// </summary>
        public void SkillHit(int segmentIndex)
        {
            if (_suppressAnimHits)
            {
                return;
            }

            EnsureHitSockets();
            SkillHitProfile profile = EnsureSkillHitProfile();
            if (profile == null || !profile.TryGetSegment(segmentIndex, out SkillHitSegment segment))
            {
                Debug.LogWarning($"[AttackHit] SkillHit({segmentIndex}) 无有效段配置。", this);
                return;
            }

            PulseSegmentGizmo(segment);
            int hit = SkillHitExecutor.Execute(segment, BuildSkillHitContext(comboIndex: 100 + segmentIndex));
            if (drawDebug)
            {
                Debug.Log(
                    $"[AttackHit] SkillHit({segmentIndex}) id={segment.id} socket={segment.socket} applied={hit}",
                    this);
            }
        }

        /// <summary>兼容旧 Event 名：按挂点查段表。</summary>
        public void Hit_Chest_R() => SkillHitBySocket(HitSocketId.Hit_Chest_R);

        public void Hit_Chest_L() => SkillHitBySocket(HitSocketId.Hit_Chest_L);

        public void Hit_Root() => SkillHitBySocket(HitSocketId.Hit_Root);

        void SkillHitBySocket(HitSocketId socket)
        {
            SkillHitProfile profile = EnsureSkillHitProfile();
            if (profile == null)
            {
                return;
            }

            int index = profile.FindIndexBySocket(socket);
            if (index < 0)
            {
                Debug.LogWarning($"[AttackHit] Profile 中无 socket={socket} 的段。", this);
                return;
            }

            SkillHit(index);
        }

        SkillHitExecutor.Context BuildSkillHitContext(int comboIndex)
        {
            Transform root = OwnerRoot != null ? OwnerRoot : transform;
            return new SkillHitExecutor.Context
            {
                OwnerRoot = root,
                Attacker = root.gameObject,
                Mask = hurtboxMask,
                Flags = HitResolver.DefaultPlayerOffense,
                Session = _hitSession,
                Buffer = _overlapBuffer,
                Scratch = _overlapScratch,
                PlanarForward = GetPlanarForward(),
                ComboIndex = comboIndex,
                ClearSession = true,
                DrawDebug = drawDebug,
                LogContext = this,
            };
        }

        void PulseSegmentGizmo(SkillHitSegment segment)
        {
            if (segment == null)
            {
                return;
            }

            Transform socket = HitSocketResolver.Resolve(OwnerRoot, segment.socket);
            if (socket == null)
            {
                return;
            }

            if (segment.shape == HitShapeType.Cylinder)
            {
                PulseSocketGizmoCylinder(socket.position, segment.radius, segment.height);
            }
            else
            {
                PulseSocketGizmoSphere(socket.position, segment.radius);
            }
        }

        void PulseSocketGizmoSphere(Vector3 center, float radius)
        {
            _socketFlash = new SocketGizmoFlash
            {
                Active = true,
                IsCylinder = false,
                CenterOrBottom = center,
                Radius = radius,
                Height = 0f,
                Until = Time.time + Mathf.Max(0.05f, socketHitGizmoFlashSeconds),
            };
        }

        void PulseSocketGizmoCylinder(Vector3 bottom, float radius, float height)
        {
            _socketFlash = new SocketGizmoFlash
            {
                Active = true,
                IsCylinder = true,
                CenterOrBottom = bottom,
                Radius = radius,
                Height = height,
                Until = Time.time + Mathf.Max(0.05f, socketHitGizmoFlashSeconds),
            };
        }

        void EnsureHitSockets()
        {
            Transform root = OwnerRoot;
            if (root == null)
            {
                return;
            }

            var avatar = root.GetComponent<CharacterAvatar>();
            if (avatar == null)
            {
                avatar = root.GetComponentInChildren<CharacterAvatar>(true);
            }

            if (avatar != null)
            {
                if (avatar.Hits == null ||
                    avatar.Hits.ChestR == null ||
                    avatar.Hits.ChestL == null ||
                    avatar.Hits.Root == null)
                {
                    avatar.AutoBind();
                }

                if (avatar.Hits != null)
                {
                    if (hitChestR == null)
                    {
                        hitChestR = avatar.Hits.ChestR;
                    }

                    if (hitChestL == null)
                    {
                        hitChestL = avatar.Hits.ChestL;
                    }

                    if (hitRoot == null)
                    {
                        hitRoot = avatar.Hits.Root;
                    }
                }
            }

            if (hitChestR == null)
            {
                hitChestR = FindChildExact(root, CharacterAvatar.HitChestRName);
            }

            if (hitChestL == null)
            {
                hitChestL = FindChildExact(root, CharacterAvatar.HitChestLName);
            }

            if (hitRoot == null)
            {
                hitRoot = FindChildExact(root, CharacterAvatar.HitRootName);
            }
        }

        SkillHitProfile EnsureSkillHitProfile()
        {
            var settings = CharacterRuntimeSettings.Get();
            if (skillHitProfile == null && settings != null)
            {
                skillHitProfile = settings.GetPlayerSkillHitProfile();
            }

            if (skillHitProfile == null)
            {
                skillHitProfile = SkillHitProfileDefaults.PlayerE(
                    snowHitVfxPrefab,
                    groundAoeExplosionVfxPrefab);
            }

            if (snowHitVfxPrefab == null || groundAoeExplosionVfxPrefab == null)
            {
                if (settings != null)
                {
                    if (snowHitVfxPrefab == null)
                    {
                        snowHitVfxPrefab = settings.GetSnowHitVfx();
                    }

                    if (groundAoeExplosionVfxPrefab == null)
                    {
                        groundAoeExplosionVfxPrefab = settings.GetGroundAoeExplosionVfx();
                    }
                }
            }

            FillSegmentVfxFromLegacy(skillHitProfile);
            if (settings != null)
            {
                settings.FillSkillHitSfxIfEmpty(skillHitProfile);
            }

            if (!_socketVfxPrewarmed)
            {
                _socketVfxPrewarmed = true;
                PrewarmProfileVfx(skillHitProfile);
            }

            return skillHitProfile;
        }

        void FillSegmentVfxFromLegacy(SkillHitProfile profile)
        {
            if (profile?.segments == null)
            {
                return;
            }

            for (int i = 0; i < profile.segments.Length; i++)
            {
                var seg = profile.segments[i];
                if (seg == null || seg.vfxPrefab != null)
                {
                    continue;
                }

                if (seg.socket == HitSocketId.Hit_Root)
                {
                    seg.vfxPrefab = groundAoeExplosionVfxPrefab;
                }
                else if (seg.socket == HitSocketId.Hit_Chest_R || seg.socket == HitSocketId.Hit_Chest_L)
                {
                    seg.vfxPrefab = snowHitVfxPrefab;
                }
            }
        }

        static void PrewarmProfileVfx(SkillHitProfile profile)
        {
            if (profile?.segments == null)
            {
                return;
            }

            for (int i = 0; i < profile.segments.Length; i++)
            {
                var seg = profile.segments[i];
                if (seg?.vfxPrefab != null)
                {
                    VfxObjectPool.Prewarm(seg.vfxPrefab, 2);
                }
            }
        }

        void LateUpdate()
        {
            if (!drawSocketHitGizmos || !Application.isPlaying)
            {
                return;
            }

            EnsureHitSockets();
            SkillHitProfile profile = EnsureSkillHitProfile();
            SkillHitSegment rootSeg = FindSegment(profile, HitSocketId.Hit_Root);
            Transform rootSocket = hitRoot != null
                ? hitRoot
                : HitSocketResolver.Resolve(OwnerRoot, HitSocketId.Hit_Root);
            if (rootSocket == null || rootSeg == null)
            {
                return;
            }

            DrawDebugCylinder(
                rootSocket.position,
                rootSeg.radius,
                rootSeg.height,
                new Color(1f, 0.45f, 0.12f, 1f));

            if (_socketFlash.Active && _socketFlash.IsCylinder && Time.time <= _socketFlash.Until)
            {
                DrawDebugCylinder(
                    _socketFlash.CenterOrBottom,
                    _socketFlash.Radius,
                    _socketFlash.Height,
                    new Color(1f, 0.2f, 0.05f, 1f));
            }
        }

        static SkillHitSegment FindSegment(SkillHitProfile profile, HitSocketId socket)
        {
            if (profile == null)
            {
                return null;
            }

            int index = profile.FindIndexBySocket(socket);
            return index >= 0 ? profile.segments[index] : null;
        }

        static void DrawDebugCylinder(Vector3 bottom, float radius, float height, Color color)
        {
            float h = Mathf.Max(0.05f, height);
            float r = Mathf.Max(0.05f, radius);
            Vector3 top = bottom + Vector3.up * h;
            const int segments = 32;
            Vector3 prevB = bottom + new Vector3(r, 0f, 0f);
            Vector3 prevT = top + new Vector3(r, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float ang = (i / (float)segments) * Mathf.PI * 2f;
                Vector3 offset = new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
                Vector3 nextB = bottom + offset;
                Vector3 nextT = top + offset;
                Debug.DrawLine(prevB, nextB, color, 0f, false);
                Debug.DrawLine(prevT, nextT, color, 0f, false);
                if (i % 2 == 0)
                {
                    Debug.DrawLine(nextB, nextT, color, 0f, false);
                }

                prevB = nextB;
                prevT = nextT;
            }
        }

        public int PerformSphereHit(
            Vector3 center,
            float radius,
            float damage,
            float knockback,
            int comboIndex = -1,
            bool clearHitIds = true)
        {
            if (clearHitIds)
            {
                _hitSession.Begin();
            }

            Transform root = OwnerRoot != null ? OwnerRoot : transform;
            GameObject attackerGo = root.gameObject;
            Vector3 forward = GetPlanarForward();

            int count = ShapeHitDetector.OverlapSphere(center, radius, hurtboxMask, _overlapBuffer);
            int applied = 0;
            for (int i = 0; i < count; i++)
            {
                Collider col = _overlapBuffer[i];
                if (!ShapeHitDetector.TryPassSphereFilter(col, center, radius, out Vector3 hitPoint, out Vector3 toHit))
                {
                    continue;
                }

                IDamageable damageable = FanHitDetector.ResolveDamageable(col);
                if (damageable == null)
                {
                    continue;
                }

                Vector3 dir = toHit.sqrMagnitude > 0.0001f ? toHit.normalized : forward;
                var info = new DamageInfo(damage, hitPoint, dir, knockback, comboIndex, attackerGo);
                if (HitResolver.TryApply(HitRequest.Create(
                        info,
                        damageable,
                        col,
                        HitResolver.DefaultPlayerOffense,
                        _hitSession,
                        root)))
                {
                    applied++;
                }
            }

            if (drawDebug)
            {
                Debug.Log($"[AttackHit] sphere r={radius} scanned={count} hit={applied} dmg={damage}", this);
            }

            return applied;
        }

        public int PerformCylinderHit(
            Vector3 bottomCenter,
            float radius,
            float height,
            float damage,
            float knockback,
            int comboIndex = -1,
            bool clearHitIds = true)
        {
            if (clearHitIds)
            {
                _hitSession.Begin();
            }

            Transform root = OwnerRoot != null ? OwnerRoot : transform;
            GameObject attackerGo = root.gameObject;
            Vector3 forward = GetPlanarForward();

            int count = ShapeHitDetector.OverlapCylinder(
                bottomCenter,
                radius,
                height,
                hurtboxMask,
                _overlapBuffer,
                _overlapScratch);

            int applied = 0;
            for (int i = 0; i < count; i++)
            {
                Collider col = _overlapBuffer[i];
                if (!ShapeHitDetector.TryPassCylinderFilter(
                        col,
                        bottomCenter,
                        radius,
                        height,
                        out Vector3 hitPoint,
                        out Vector3 planarToHit))
                {
                    continue;
                }

                IDamageable damageable = FanHitDetector.ResolveDamageable(col);
                if (damageable == null)
                {
                    continue;
                }

                Vector3 dir = planarToHit.sqrMagnitude > 0.0001f ? planarToHit.normalized : forward;
                var info = new DamageInfo(damage, hitPoint, dir, knockback, comboIndex, attackerGo);
                if (HitResolver.TryApply(HitRequest.Create(
                        info,
                        damageable,
                        col,
                        HitResolver.DefaultPlayerOffense,
                        _hitSession,
                        root)))
                {
                    applied++;
                }
            }

            if (drawDebug)
            {
                Debug.Log(
                    $"[AttackHit] cylinder r={radius} h={height} scanned={count} hit={applied} dmg={damage}",
                    this);
            }

            return applied;
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

        /// <summary>显示/隐藏大剑（及其渲染物体）。</summary>
        public void SetWeaponVisible(bool visible)
        {
            if (weapon == null)
            {
                weapon = FindWeaponTransform(OwnerRoot);
            }

            if (weapon == null)
            {
                return;
            }

            if (weapon.gameObject.activeSelf != visible)
            {
                weapon.gameObject.SetActive(visible);
            }
        }

        /// <summary>HSM 每段攻击开始时调用，清空本段命中记录。</summary>
        public void BeginSwing(int comboIndex)
        {
            _activeCombo = Mathf.Clamp(comboIndex, 0, Mathf.Max(0, swings.Length - 1));
            _hitSession.Begin();
            _swingSerial++;
            SetWeaponVisible(true);
        }

        public void EndCombat()
        {
            _activeCombo = -1;
            _hitSession.Clear();
            SetWeaponVisible(false);
        }

        /// <summary>
        /// 动画 Event：出刀帧调用。可无参（用当前 BeginSwing 的 combo），或传 0/1/2。
        /// </summary>
        public void OnAttackHit()
        {
            if (_suppressAnimHits)
            {
                return;
            }

            DoHit(_activeCombo >= 0 ? _activeCombo : 0);
        }

        public void OnAttackHit(int comboIndex)
        {
            if (_suppressAnimHits)
            {
                return;
            }

            DoHit(comboIndex);
        }

        /// <summary>
        /// 动画 Event：出刀特效。可无参或传 comboIndex。
        /// </summary>
        public void OnAttackVfx()
        {
            if (_suppressAnimHits)
            {
                return;
            }

            int combo = _activeCombo >= 0 ? _activeCombo : 0;
            PlaySwingSfx(combo <= 0 ? 0 : 1);
            SpawnSlashVfx(_activeCombo >= 0 ? _activeCombo : 0);
        }

        public void OnAttackVfx(int comboIndex)
        {
            if (_suppressAnimHits)
            {
                return;
            }

            PlaySwingSfx(comboIndex);
            SpawnSlashVfx(comboIndex);
        }

        /// <summary>脚本主动播刀光（不受 SuppressAnimHits 影响）。</summary>
        public void PlaySlashVfx(int comboIndex)
        {
            SpawnSlashVfx(comboIndex);
        }

        /// <summary>同帧出伤+特效（动画只挂一个 Event 时用）。</summary>
        public void OnAttackStrike()
        {
            if (_suppressAnimHits)
            {
                return;
            }

            int combo = _activeCombo >= 0 ? _activeCombo : 0;
            PlaySwingSfx(combo <= 0 ? 0 : 1);
            SpawnSlashVfx(combo);
            DoHit(combo);
        }

        /// <summary>
        /// 动画 Event：int 参数 0=swinging，1=large-sword-swing；伤害/刀光仍用当前连段。
        /// </summary>
        public void OnAttackStrike(int swingType)
        {
            if (_suppressAnimHits)
            {
                return;
            }

            PlaySwingSfx(swingType);
            int combo = _activeCombo >= 0 ? _activeCombo : 0;
            SpawnSlashVfx(combo);
            DoHit(combo);
        }

        void PlaySwingSfx(int swingType)
        {
            var audio = OwnerRoot != null
                ? OwnerRoot.GetComponent<CharacterAudio>()
                : null;
            if (audio == null)
            {
                audio = GetComponentInParent<CharacterAudio>();
            }

            audio?.PlaySwing(swingType);
        }

        void DoHit(int comboIndex)
        {
            AttackSwingConfig cfg = GetConfig(comboIndex);
            PerformFanHit(
                cfg.Damage,
                cfg.HitRadius,
                cfg.FanAngle,
                cfg.Knockback,
                cfg.MinHitDistance,
                cfg.HitHeight,
                comboIndex,
                clearHitIds: false,
                hitVfxPrefab: cfg.HitVfxPrefab,
                vfxLife: cfg.VfxLifeTime);
        }

        /// <summary>大招/自定义窗口：扇形伤害（可单独清空命中表）。经 <see cref="HitResolver"/> 结算。</summary>
        public int PerformFanHit(
            float damage,
            float hitRadius,
            float fanAngle,
            float knockback,
            float minHitDistance = 0.15f,
            float hitHeight = 0.9f,
            int comboIndex = -1,
            bool clearHitIds = true,
            GameObject hitVfxPrefab = null,
            float vfxLife = 1.2f)
        {
            if (clearHitIds)
            {
                _hitSession.Begin();
            }

            Transform root = OwnerRoot != null ? OwnerRoot : transform;
            Vector3 origin = root.position + Vector3.up * hitHeight;
            if (hitOrigin != null && hitOrigin != transform)
            {
                origin = hitOrigin.position;
                origin.y = root.position.y + hitHeight;
            }

            Vector3 forward = GetPlanarForward();
            GameObject attackerGo = OwnerRoot != null ? OwnerRoot.gameObject : gameObject;

            int count = FanHitDetector.Overlap(
                origin,
                forward,
                hitRadius,
                fanAngle,
                minHitDistance,
                hurtboxMask,
                _overlapBuffer);

            int applied = 0;
            for (int i = 0; i < count; i++)
            {
                Collider col = _overlapBuffer[i];
                if (col == null)
                {
                    continue;
                }

                if (!FanHitDetector.TryPassFanFilter(
                        col,
                        origin,
                        forward,
                        hitRadius,
                        fanAngle,
                        minHitDistance,
                        out Vector3 hitPoint,
                        out _))
                {
                    continue;
                }

                IDamageable damageable = FanHitDetector.ResolveDamageable(col);
                if (damageable == null)
                {
                    continue;
                }

                var info = new DamageInfo(
                    damage,
                    hitPoint,
                    forward,
                    knockback,
                    comboIndex,
                    attackerGo);

                if (HitResolver.TryApply(HitRequest.Create(
                        info,
                        damageable,
                        col,
                        HitResolver.DefaultPlayerOffense,
                        _hitSession,
                        root,
                        hitVfxPrefab,
                        vfxLife)))
                {
                    applied++;
                }
            }

            if (drawDebug)
            {
                Debug.Log($"[AttackHit] fan angle={fanAngle} scanned={count} hit={applied} dmg={damage}", this);
            }

            return applied;
        }

        void SpawnSlashVfx(int comboIndex)
        {
            AttackSwingConfig cfg = GetConfig(comboIndex);
            if (weapon == null)
            {
                weapon = FindWeaponTransform(OwnerRoot);
            }

            if (vfxSocket == null)
            {
                vfxSocket = weapon;
            }

            // 世界坐标取 vfxSocket；父节点为场景根（不跟随角色）
            Vector3 worldPos;
            Quaternion worldRot;
            if (vfxSocket != null)
            {
                worldPos = vfxSocket.position;
                worldRot = vfxSocket.rotation;
            }
            else
            {
                Transform root = OwnerRoot != null ? OwnerRoot : transform;
                worldPos = root.position + Vector3.up * 0.95f;
                worldRot = Quaternion.LookRotation(GetPlanarForward(), Vector3.up);
            }

            float life = Mathf.Max(0.1f, cfg.VfxLifeTime);
            GameObject fxGo;

            if (cfg.SlashVfxPrefab != null)
            {
                fxGo = Instantiate(cfg.SlashVfxPrefab, worldPos, worldRot);
                fxGo.transform.SetParent(null, true);
                fxGo.transform.SetPositionAndRotation(worldPos, worldRot);
                fxGo.transform.localScale = Vector3.one;

                var slash = fxGo.GetComponent<SlashArcVfx>();
                if (slash != null)
                {
                    Color c = comboIndex == 0
                        ? new Color(1f, 0.9f, 0.45f, 1f)
                        : comboIndex == 1
                            ? new Color(1f, 0.7f, 0.32f, 1f)
                            : new Color(1f, 0.42f, 0.22f, 1f);
                    float radius = Mathf.Clamp(cfg.HitRadius * 0.5f, 0.9f, 1.25f);
                    float angle = Mathf.Clamp(cfg.FanAngle * 0.85f, 60f, 120f);
                    slash.SetSpawnAnchor(0f, 0f);
                    slash.Configure(radius, angle, c, life);
                }
                else
                {
                    Destroy(fxGo, life);
                }
            }
            else
            {
                var slash = SlashArcVfx.Spawn(
                    worldPos,
                    worldRot,
                    Mathf.Clamp(cfg.HitRadius * 0.5f, 0.9f, 1.25f),
                    Mathf.Clamp(cfg.FanAngle * 0.85f, 60f, 120f),
                    comboIndex == 0
                        ? new Color(1f, 0.9f, 0.45f, 1f)
                        : comboIndex == 1
                            ? new Color(1f, 0.7f, 0.32f, 1f)
                            : new Color(1f, 0.42f, 0.22f, 1f),
                    Mathf.Clamp(life, 0.28f, 0.6f));
                fxGo = slash.gameObject;
                fxGo.transform.SetParent(null, true);
                fxGo.transform.SetPositionAndRotation(worldPos, worldRot);
                fxGo.transform.localScale = Vector3.one;
            }

            if (drawDebug)
            {
                string prefabName = cfg.SlashVfxPrefab != null ? cfg.SlashVfxPrefab.name : "SlashArc_Runtime";
                Debug.Log($"[AttackVfx] combo={comboIndex} prefab={prefabName} socket={vfxSocket?.name} worldRoot", fxGo);
            }
        }

        Transform FindWeaponTransform(Transform root)
        {
            if (root == null)
            {
                return null;
            }

            // 1) 名称匹配刀剑
            var all = root.GetComponentsInChildren<Transform>(true);
            for (int k = 0; k < WeaponNameKeys.Length; k++)
            {
                string key = WeaponNameKeys[k];
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] != null && all[i].name.IndexOf(key, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return all[i];
                    }
                }
            }

            // 2) 材质名含大剑
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null)
                {
                    continue;
                }

                var mats = r.sharedMaterials;
                for (int m = 0; m < mats.Length; m++)
                {
                    if (mats[m] != null && mats[m].name.IndexOf("大剑", System.StringComparison.Ordinal) >= 0)
                    {
                        return r.transform;
                    }
                }
            }

            // 3) Humanoid 右手
            var anim = root.GetComponentInChildren<Animator>();
            if (anim != null && anim.isHuman)
            {
                Transform hand = anim.GetBoneTransform(HumanBodyBones.RightHand);
                if (hand != null)
                {
                    return hand;
                }
            }

            return null;
        }

        void SpawnHitVfx(AttackSwingConfig cfg, Vector3 point)
        {
            if (cfg.HitVfxPrefab == null)
            {
                return;
            }

            var fx = Instantiate(cfg.HitVfxPrefab, point, Quaternion.identity);
            Destroy(fx, Mathf.Max(0.1f, cfg.VfxLifeTime));
        }

        AttackSwingConfig GetConfig(int comboIndex)
        {
            if (swings == null || swings.Length == 0)
            {
                return new AttackSwingConfig();
            }

            int i = Mathf.Clamp(comboIndex, 0, swings.Length - 1);
            return swings[i] ?? new AttackSwingConfig();
        }

        Vector3 GetFanOrigin(AttackSwingConfig cfg)
        {
            Transform root = OwnerRoot;
            if (hitOrigin != null && hitOrigin != transform)
            {
                Vector3 p = hitOrigin.position;
                p.y = root.position.y + cfg.HitHeight;
                return p;
            }

            return root.position + Vector3.up * cfg.HitHeight;
        }

        Vector3 GetPlanarForward()
        {
            Vector3 dir = OwnerRoot.forward;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f)
            {
                dir = Vector3.forward;
            }

            return dir.normalized;
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (!drawSocketHitGizmos)
            {
                return;
            }

            EnsureHitSockets();
            DrawSocketHitGizmos(false);
            DrawSocketFlashGizmo();
        }

        void OnDrawGizmosSelected()
        {
            EnsureHitSockets();

            if (swings != null && swings.Length > 0)
            {
                int idx = _activeCombo >= 0 ? _activeCombo : 0;
                DrawFanGizmo(GetConfig(idx));
            }

            if (drawSocketHitGizmos)
            {
                DrawSocketHitGizmos(true);
            }
        }

        void DrawSocketFlashGizmo()
        {
            if (!_socketFlash.Active)
            {
                return;
            }

            if (Application.isPlaying && Time.time > _socketFlash.Until)
            {
                _socketFlash.Active = false;
                return;
            }

            if (_socketFlash.IsCylinder)
            {
                DrawWireCylinder(
                    _socketFlash.CenterOrBottom,
                    _socketFlash.Radius,
                    _socketFlash.Height,
                    new Color(1f, 0.15f, 0.05f, 1f),
                    fill: true,
                    bold: true);
            }
            else
            {
                Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.85f);
                Gizmos.DrawSphere(_socketFlash.CenterOrBottom, _socketFlash.Radius);
                Gizmos.color = new Color(0.1f, 0.55f, 1f, 1f);
                Gizmos.DrawWireSphere(_socketFlash.CenterOrBottom, _socketFlash.Radius);
            }
        }

        void DrawSocketHitGizmos(bool selected)
        {
            SkillHitProfile profile = skillHitProfile;
            if (profile == null)
            {
                profile = EnsureSkillHitProfile();
            }

            float alpha = selected ? 1f : 0.9f;
            DrawSocketSegmentGizmo(profile, HitSocketId.Hit_Chest_R, hitChestR, new Color(0.25f, 0.75f, 1f, alpha), selected);
            DrawSocketSegmentGizmo(profile, HitSocketId.Hit_Chest_L, hitChestL, new Color(0.35f, 0.95f, 0.75f, alpha), selected);

            Transform rootSocket = hitRoot;
            if (rootSocket == null && OwnerRoot != null)
            {
                rootSocket = HitSocketResolver.Resolve(OwnerRoot, HitSocketId.Hit_Root);
            }

            SkillHitSegment rootSeg = FindSegment(profile, HitSocketId.Hit_Root);
            if (rootSocket != null && rootSeg != null)
            {
                DrawWireCylinder(
                    rootSocket.position,
                    rootSeg.radius,
                    rootSeg.height,
                    new Color(1f, 0.45f, 0.12f, 1f),
                    fill: selected,
                    bold: true);

#if UNITY_EDITOR
                UnityEditor.Handles.color = new Color(1f, 0.45f, 0.12f, 1f);
                UnityEditor.Handles.Label(
                    rootSocket.position + Vector3.up * (rootSeg.height + 0.15f),
                    $"Hit_Root r={rootSeg.radius} h={rootSeg.height}");
#endif
            }
#if UNITY_EDITOR
            else if (selected)
            {
                UnityEditor.Handles.color = Color.red;
                Vector3 p = OwnerRoot != null ? OwnerRoot.position : transform.position;
                UnityEditor.Handles.Label(p + Vector3.up * 2f, "Hit_Root 挂点未找到");
            }
#endif
        }

        void DrawSocketSegmentGizmo(
            SkillHitProfile profile,
            HitSocketId socketId,
            Transform cached,
            Color color,
            bool selected)
        {
            Transform socket = cached != null
                ? cached
                : HitSocketResolver.Resolve(OwnerRoot, socketId);
            SkillHitSegment seg = FindSegment(profile, socketId);
            if (socket == null || seg == null)
            {
                return;
            }

            Gizmos.color = color;
            Gizmos.DrawWireSphere(socket.position, seg.radius);
            if (selected)
            {
                Color fill = color;
                fill.a = 0.12f;
                Gizmos.color = fill;
                Gizmos.DrawSphere(socket.position, seg.radius);
            }
        }

        static void DrawWireCylinder(
            Vector3 bottom,
            float radius,
            float height,
            Color color,
            bool fill,
            bool bold = false)
        {
            float h = Mathf.Max(0.05f, height);
            float r = Mathf.Max(0.05f, radius);
            Vector3 top = bottom + Vector3.up * h;
            Vector3 mid = bottom + Vector3.up * (h * 0.5f);
            int segments = bold ? 48 : 32;

#if UNITY_EDITOR
            // Handles 圆盘在 Scene 里比 Gizmos 线更稳、更明显
            UnityEditor.Handles.color = color;
            UnityEditor.Handles.DrawWireDisc(bottom, Vector3.up, r);
            UnityEditor.Handles.DrawWireDisc(top, Vector3.up, r);
            UnityEditor.Handles.DrawWireDisc(mid, Vector3.up, r);
            if (bold)
            {
                UnityEditor.Handles.DrawWireDisc(bottom, Vector3.up, r * 0.98f);
                UnityEditor.Handles.DrawWireDisc(top, Vector3.up, r * 0.98f);
            }
#endif

            Gizmos.color = color;
            Vector3 prevBottom = bottom + new Vector3(r, 0f, 0f);
            Vector3 prevTop = top + new Vector3(r, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float ang = (i / (float)segments) * Mathf.PI * 2f;
                Vector3 offset = new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
                Vector3 nextBottom = bottom + offset;
                Vector3 nextTop = top + offset;
                Gizmos.DrawLine(prevBottom, nextBottom);
                Gizmos.DrawLine(prevTop, nextTop);
                if (i % 2 == 0 || bold)
                {
                    Gizmos.DrawLine(nextBottom, nextTop);
                }

                prevBottom = nextBottom;
                prevTop = nextTop;
            }

            Gizmos.DrawLine(bottom, top);
            Gizmos.DrawLine(bottom + Vector3.right * r, top + Vector3.right * r);
            Gizmos.DrawLine(bottom - Vector3.right * r, top - Vector3.right * r);
            Gizmos.DrawLine(bottom + Vector3.forward * r, top + Vector3.forward * r);
            Gizmos.DrawLine(bottom - Vector3.forward * r, top - Vector3.forward * r);

            if (fill)
            {
                Color fillColor = color;
                fillColor.a = 0.12f;
                Gizmos.color = fillColor;
                Gizmos.matrix = Matrix4x4.TRS(mid, Quaternion.identity, new Vector3(r * 2f, h, r * 2f));
                Gizmos.DrawSphere(Vector3.zero, 0.5f);
                Gizmos.matrix = Matrix4x4.identity;
            }
        }

        void DrawFanGizmo(AttackSwingConfig cfg)
        {
            Vector3 origin = GetFanOrigin(cfg);
            Vector3 forward = GetPlanarForward();
            float half = cfg.FanAngle * 0.5f;
            int segments = Mathf.Clamp(Mathf.RoundToInt(cfg.FanAngle / 6f), 8, 48);

            Gizmos.color = new Color(1f, 0.35f, 0.2f, 0.9f);
            Vector3 left = Quaternion.AngleAxis(-half, Vector3.up) * forward;
            Vector3 right = Quaternion.AngleAxis(half, Vector3.up) * forward;
            Gizmos.DrawLine(origin, origin + left * cfg.HitRadius);
            Gizmos.DrawLine(origin, origin + right * cfg.HitRadius);

            Vector3 prev = origin + left * cfg.HitRadius;
            for (int i = 1; i <= segments; i++)
            {
                float t = Mathf.Lerp(-half, half, i / (float)segments);
                Vector3 dir = Quaternion.AngleAxis(t, Vector3.up) * forward;
                Vector3 next = origin + dir * cfg.HitRadius;
                Gizmos.DrawLine(prev, next);
                prev = next;
            }

            if (cfg.MinHitDistance > 0.01f)
            {
                Gizmos.color = new Color(1f, 0.7f, 0.2f, 0.6f);
                prev = origin + left * cfg.MinHitDistance;
                for (int i = 1; i <= segments; i++)
                {
                    float t = Mathf.Lerp(-half, half, i / (float)segments);
                    Vector3 dir = Quaternion.AngleAxis(t, Vector3.up) * forward;
                    Vector3 next = origin + dir * cfg.MinHitDistance;
                    Gizmos.DrawLine(prev, next);
                    prev = next;
                }
            }
        }
#endif
    }
}
