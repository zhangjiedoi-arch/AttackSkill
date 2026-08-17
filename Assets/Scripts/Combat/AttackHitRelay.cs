using System.Collections.Generic;
using UnityEngine;
using AttackSkill.Character;

namespace AttackSkill.Combat
{    [System.Serializable]
    public class AttackSwingConfig
    {
        [Tooltip("本段伤害倍率%（100=100%攻击力）；无 TimedHitProfile 时以 Profile 为准")]
        public float Damage = 100f;
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
    /// 普攻/技能出伤：HSM 驱动 TimedHitProfile（normalizedTime），经形状检测与 HitResolver。
    /// 挂在带 Animator 的同一物体上以便读动画进度。
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

        [Header("Combo 0/1/2 fallback（无 TimedHit 段时）")]
        [SerializeField] AttackSwingConfig[] swings =
        {
            new AttackSwingConfig { Damage = 100f, HitRadius = 2.0f, FanAngle = 80f, Knockback = 1.2f },
            new AttackSwingConfig { Damage = 120f, HitRadius = 2.3f, FanAngle = 100f, Knockback = 1.6f },
            new AttackSwingConfig { Damage = 150f, HitRadius = 2.6f, FanAngle = 120f, Knockback = 2.4f }
        };

        [Header("Timed Hit Profile（normalizedTime，无动画 Event）")]
        [SerializeField] TimedHitProfile timedHitProfile;

        [Header("Skill Hit Profile (legacy / Gizmo 兜底)")]
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
        readonly HashSet<int> _timedFiredKeys = new HashSet<int>();
        int _activeCombo = -1;
        int _swingSerial;
        Collider[] _overlapBuffer = new Collider[32];
        Collider[] _overlapScratch = new Collider[32];
        bool _suppressAnimHits;
        bool _socketVfxPrewarmed;
        bool _timedActive;
        int _timedCombo = -1;
        string _timedPhaseId;
        bool _isBasicAttackPhase;
        float _prevNormalized;
        bool _hasTimedSample;
        Animator _animator;

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

        /// <summary>当前是否处于普攻连段出伤（锋刃等被动用）。</summary>
        public bool IsBasicAttackActive => _isBasicAttackPhase;

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

        public void SetTimedHitProfile(TimedHitProfile profile)
        {
            timedHitProfile = profile;
        }

        public TimedHitProfile TimedHitProfile => timedHitProfile;

        void Awake()
        {
            if (ownerRoot == null)
            {
                ownerRoot = transform.root;
            }

            _animator = GetComponent<Animator>();
            if (_animator == null)
            {
                _animator = GetComponentInParent<Animator>();
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

            // 大剑 / Weapon_Pos 默认隐藏，仅普攻期间显示 Weapon_Pos
            SetWeaponVisible(false);
        }

        void Update()
        {
            TickTimedHits();
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
                ClearSession = false,
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
            if (skillHitProfile == null)
            {
                skillHitProfile = SkillHitProfileDefaults.PlayerE(
                    snowHitVfxPrefab,
                    groundAoeExplosionVfxPrefab);
            }

            FillSegmentVfxFromLegacy(skillHitProfile);

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

        /// <summary>显示/隐藏 <c>Weapon_Pos</c>（普攻期间显示，结束隐藏）。</summary>
        public void SetWeaponVisible(bool visible)
        {
            Transform weaponPos = ResolveWeaponPos();
            if (weaponPos == null)
            {
                return;
            }

            if (weaponPos.gameObject.activeSelf != visible)
            {
                weaponPos.gameObject.SetActive(visible);
            }
        }

        Transform ResolveWeaponPos()
        {
            Transform root = OwnerRoot != null ? OwnerRoot : transform;
            var avatar = root.GetComponent<CharacterAvatar>();
            if (avatar == null)
            {
                avatar = root.GetComponentInChildren<CharacterAvatar>(true);
            }

            if (avatar != null)
            {
                if (avatar.SkillR == null || avatar.SkillR.WeaponPos == null)
                {
                    avatar.AutoBind();
                }

                if (avatar.SkillR?.WeaponPos != null)
                {
                    return avatar.SkillR.WeaponPos;
                }
            }

            return FindChildExact(root, CharacterAvatar.WeaponPosName);
        }

        /// <summary>HSM 每段普攻开始：清命中表并按 attack1/2/3 启动 TimedHit。</summary>
        public void BeginSwing(int comboIndex)
        {
            _activeCombo = Mathf.Clamp(comboIndex, 0, 2);
            _swingSerial++;
            SetWeaponVisible(true);

            string phaseId = "attack1";
            if (_activeCombo == 1)
            {
                phaseId = "attack2";
            }
            else if (_activeCombo == 2)
            {
                phaseId = "attack3";
            }

            BeginTimedPhase(phaseId, _activeCombo);
        }

        /// <summary>HSM 进入 skill / Skill_R 等：按 phase id 启动 TimedHit。</summary>
        public void BeginTimedPhase(string phaseId, int comboIndex = -1)
        {
            if (string.IsNullOrEmpty(phaseId))
            {
                EndTimedPhase();
                return;
            }

            _hitSession.Begin();
            _timedPhaseId = phaseId;
            _timedCombo = comboIndex;
            _isBasicAttackPhase = phaseId.StartsWith("attack", System.StringComparison.OrdinalIgnoreCase);
            _prevNormalized = 0f;
            _hasTimedSample = false;
            _timedFiredKeys.Clear();
            _timedActive = timedHitProfile != null;
            if (drawDebug && !_timedActive)
            {
                Debug.LogWarning($"[AttackHit] BeginTimedPhase({phaseId}) 但 TimedHitProfile 为空。", this);
            }
        }

        public void EndTimedPhase()
        {
            _timedActive = false;
            _timedPhaseId = null;
            _timedCombo = -1;
            _isBasicAttackPhase = false;
            _hasTimedSample = false;
            _prevNormalized = 0f;
            _timedFiredKeys.Clear();
        }

        public void EndCombat()
        {
            _activeCombo = -1;
            _hitSession.Clear();
            SetWeaponVisible(false);
            EndTimedPhase();
        }

        /// <summary>脚本主动播刀光（不受 SuppressAnimHits 影响）。</summary>
        public void PlaySlashVfx(int comboIndex)
        {
            SpawnSlashVfx(comboIndex);
        }

        void TickTimedHits()
        {
            if (!_timedActive || timedHitProfile == null || _suppressAnimHits)
            {
                return;
            }

            if (_animator == null)
            {
                _animator = GetComponent<Animator>();
                if (_animator == null)
                {
                    return;
                }
            }

            if (!timedHitProfile.TryGetPhaseById(_timedPhaseId, out TimedAttackPhase phase) || phase == null)
            {
                return;
            }

            // 必须等 Animator 真正进入本 phase 状态（或正在过渡进去），
            // 否则会误用上一状态的 normalizedTime，E 等技能会双播特效/出伤。
            if (!TryGetPhaseNormalized(phase, out float curr))
            {
                return;
            }

            float prev = _hasTimedSample ? _prevNormalized : 0f;
            if (curr + 0.0001f < prev)
            {
                // 过渡结束切 Current 时进度可能回跳：只同步进度，不从 0 重放 cue
                _prevNormalized = curr;
                _hasTimedSample = true;
                return;
            }

            if (phase.cues != null)
            {
                for (int i = 0; i < phase.cues.Length; i++)
                {
                    TimedHitCue cue = phase.cues[i];
                    if (cue == null)
                    {
                        continue;
                    }

                    bool wantPres = cue.ShouldPlayPresentation(prev, curr);
                    bool wantHit = cue.ShouldSampleHit(prev, curr);
                    if (!wantPres && !wantHit)
                    {
                        continue;
                    }

                    // 一次性 cue 防重入（进度回跳 / 双源采样）
                    bool playPres = wantPres && TryMarkTimedFire(i, isPresentation: true);
                    bool sampleHit = wantHit &&
                                     (cue.continuousHitSampling || TryMarkTimedFire(i, isPresentation: false));
                    if (!playPres && !sampleHit)
                    {
                        continue;
                    }

                    SkillHitExecuteFlags flags = SkillHitExecuteFlags.None;
                    if (playPres)
                    {
                        flags |= SkillHitExecuteFlags.Presentation;
                    }

                    if (sampleHit)
                    {
                        flags |= SkillHitExecuteFlags.Hit;
                    }

                    if (cue.segment != null)
                    {
                        PulseSegmentGizmo(cue.segment);
                        int combo = _timedCombo >= 0 ? _timedCombo : phase.comboIndex;
                        SkillHitExecutor.Execute(cue.segment, BuildSkillHitContext(combo), flags);
                    }
                }
            }

            _prevNormalized = curr;
            _hasTimedSample = true;
        }

        bool TryGetPhaseNormalized(TimedAttackPhase phase, out float normalized)
        {
            normalized = 0f;
            AnimatorStateInfo current = _animator.GetCurrentAnimatorStateInfo(0);
            bool inTransition = _animator.IsInTransition(0);
            AnimatorStateInfo next = inTransition
                ? _animator.GetNextAnimatorStateInfo(0)
                : default;

            string stateName = !string.IsNullOrEmpty(phase.animatorStateName)
                ? phase.animatorStateName
                : phase.id;

            if (!string.IsNullOrEmpty(stateName))
            {
                if (inTransition && next.fullPathHash != 0 && next.IsName(stateName))
                {
                    normalized = Mathf.Clamp01(next.normalizedTime);
                    return true;
                }

                if (current.IsName(stateName))
                {
                    normalized = Mathf.Clamp01(current.normalizedTime);
                    return true;
                }

                // 尚未进入目标状态：不采样
                return false;
            }

            // 未配置状态名：退回当前（过渡中优先 Next）
            if (inTransition && next.fullPathHash != 0)
            {
                normalized = Mathf.Clamp01(next.normalizedTime);
                return true;
            }

            normalized = Mathf.Clamp01(current.normalizedTime);
            return true;
        }

        bool TryMarkTimedFire(int cueIndex, bool isPresentation)
        {
            int key = isPresentation ? cueIndex : cueIndex + 10000;
            if (_timedFiredKeys.Contains(key))
            {
                return false;
            }

            _timedFiredKeys.Add(key);
            return true;
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
