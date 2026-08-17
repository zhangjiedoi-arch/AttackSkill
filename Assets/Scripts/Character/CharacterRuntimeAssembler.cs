using UnityEngine;
using AttackSkill.CameraSystem;
using AttackSkill.Character.HSM;
using AttackSkill.Combat;
using AttackSkill.Core;

namespace AttackSkill.Character
{
    /// <summary>
    /// 运行时把 Avatar（表现）装配成可操控 Actor（逻辑根 + 玩法组件）。
    /// 兼容：已绑齐组件的旧 Prefab；仅挂 CharacterAvatar 的新 Prefab。
    /// </summary>
    public static class CharacterRuntimeAssembler
    {
        /// <summary>
        /// 生成角色：Avatar-only → 建 Actor 壳再挂玩法；旧完整 Prefab → 就地补齐并接线。
        /// </summary>
        public static GenshinLikeCharacter Spawn(GameObject prefab, Vector3 position, Quaternion rotation, string instanceName = null)
        {
            if (prefab == null)
            {
                Debug.LogError("[CharacterRuntimeAssembler] prefab 为空。");
                return null;
            }

            bool avatarOnly = prefab.GetComponent<GenshinLikeCharacter>() == null &&
                              prefab.GetComponentInChildren<GenshinLikeCharacter>(true) == null;

            GameObject actorRoot;
            CharacterAvatar avatar;

            if (avatarOnly)
            {
                actorRoot = new GameObject(string.IsNullOrEmpty(instanceName) ? prefab.name + "_Actor" : instanceName);
                actorRoot.transform.SetPositionAndRotation(position, rotation);
                actorRoot.transform.localScale = Vector3.one;

                var avatarGo = Object.Instantiate(prefab, actorRoot.transform, false);
                avatarGo.name = prefab.name;
                avatarGo.transform.localPosition = Vector3.zero;
                avatarGo.transform.localRotation = Quaternion.identity;

                avatar = avatarGo.GetComponent<CharacterAvatar>();
                if (avatar == null)
                {
                    avatar = avatarGo.AddComponent<CharacterAvatar>();
                }

                avatar.AutoBind();
            }
            else
            {
                actorRoot = Object.Instantiate(prefab, position, rotation);
                if (!string.IsNullOrEmpty(instanceName))
                {
                    actorRoot.name = instanceName;
                }

                avatar = actorRoot.GetComponent<CharacterAvatar>();
                if (avatar == null)
                {
                    avatar = actorRoot.GetComponentInChildren<CharacterAvatar>(true);
                }

                if (avatar == null)
                {
                    avatar = actorRoot.AddComponent<CharacterAvatar>();
                }

                avatar.AutoBind();
            }

            var character = EnsureGameplay(actorRoot, avatar);
            if (character != null)
            {
                // 装配过程中启用/添加 CharacterController 可能冲掉 Instantiate 坐标
                character.TeleportTo(position, rotation);
            }

            return character;
        }

        public static GenshinLikeCharacter EnsureGameplay(GameObject actorRoot, CharacterAvatar avatar)
        {
            if (actorRoot == null)
            {
                return null;
            }

            if (avatar == null)
            {
                avatar = actorRoot.GetComponentInChildren<CharacterAvatar>(true);
            }

            avatar?.AutoBind();

            int playerLayer = CombatLayers.PlayerLayer;
            if (playerLayer >= 0)
            {
                CombatLayers.ApplyLayerRecursively(actorRoot, playerLayer);
            }

            var cc = actorRoot.GetComponent<CharacterController>();
            if (cc == null)
            {
                cc = actorRoot.AddComponent<CharacterController>();
                FitCharacterController(cc);
            }

            if (playerLayer >= 0)
            {
                cc.excludeLayers = 1 << playerLayer;
            }

            if (actorRoot.GetComponent<AudioSource>() == null)
            {
                var src = actorRoot.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 1f;
            }

            if (actorRoot.GetComponent<CharacterAudio>() == null)
            {
                actorRoot.AddComponent<CharacterAudio>();
            }

            var health = actorRoot.GetComponent<Health>();
            if (health == null)
            {
                health = actorRoot.AddComponent<Health>();
            }

            health.ConfigureDefense(enableIFrames: true, iFrames: 0.5f, stun: 0.18f, enableHitStun: true);
            ApplyDefaultCombatStats(actorRoot, avatar);

            Animator animator = avatar != null ? avatar.Animator : actorRoot.GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                animator.applyRootMotion = false;
            }

            GameObject hitHost = animator != null ? animator.gameObject : actorRoot;
            var hitRelay = hitHost.GetComponent<AttackHitRelay>();
            if (hitRelay == null)
            {
                hitRelay = hitHost.AddComponent<AttackHitRelay>();
            }

            WireHitRelay(hitRelay, actorRoot.transform, avatar);

            var skill = actorRoot.GetComponent<CharacterSkillPlayer>();
            if (skill == null)
            {
                skill = actorRoot.AddComponent<CharacterSkillPlayer>();
            }

            skill.ConfigureRuntime(
                animator,
                null,
                null,
                null,
                GameServices.ResolveCamera());

            var character = actorRoot.GetComponent<GenshinLikeCharacter>();
            if (character == null)
            {
                character = actorRoot.AddComponent<GenshinLikeCharacter>();
            }

            character.BindPresentation(avatar, animator);

            // 放在最终装配之后：避免被其它组件 Awake 干扰，且保证挂在 Actor 根上
            PlayerHurtbox.Ensure(actorRoot);
            return character;
        }

        /// <summary>按 Prefab/Avatar 名猜肖像并注入属性；Party 切人后会再按槽位覆盖。</summary>
        public static void ApplyCombatStatsForPortrait(GameObject actorRoot, PartyPortraitId portraitId)
        {
            if (actorRoot == null)
            {
                return;
            }

            CombatStats stats = CombatStats.Ensure(actorRoot);
            CharacterCombatStatsDefinition def = CombatStatsCatalog.LoadCharacter(portraitId);
            if (def != null)
            {
                stats.ApplyCharacterDefinition(def, refillHp: true);
                return;
            }

            CombatElement element = ElementForPortrait(portraitId);
            stats.ApplyBlock(CombatStatBlock.DefaultCharacter(element), refillHp: true);
        }

        static void ApplyDefaultCombatStats(GameObject actorRoot, CharacterAvatar avatar)
        {
            PartyPortraitId portrait = PartyPortraitId.Unknown;
            if (avatar != null)
            {
                portrait = ResolvePortraitFromName(avatar.DisplayName);
            }

            if (portrait == PartyPortraitId.Unknown && actorRoot != null)
            {
                portrait = ResolvePortraitFromName(actorRoot.name);
            }

            ApplyCombatStatsForPortrait(actorRoot, portrait != PartyPortraitId.Unknown
                ? portrait
                : PartyPortraitId.WandererFemale);
        }

        static PartyPortraitId ResolvePortraitFromName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return PartyPortraitId.Unknown;
            }

            string n = name;
            if (ContainsIgnoreCase(n, "Qianxiao") || n.Contains("千咲"))
            {
                return PartyPortraitId.Qianxiao;
            }

            if (ContainsIgnoreCase(n, "Coletta") || ContainsIgnoreCase(n, "Kelaita") || n.Contains("柯莱塔"))
            {
                return PartyPortraitId.Coletta;
            }

            if (ContainsIgnoreCase(n, "Female") || n.Contains("女"))
            {
                return PartyPortraitId.WandererFemale;
            }

            if (ContainsIgnoreCase(n, "Male") || n.Contains("男") || ContainsIgnoreCase(n, "Wanderer") ||
                n.Contains("漂泊"))
            {
                return PartyPortraitId.WandererMale;
            }

            return PartyPortraitId.Unknown;
        }

        static CombatElement ElementForPortrait(PartyPortraitId id)
        {
            switch (id)
            {
                case PartyPortraitId.Qianxiao:
                    return CombatElement.Dark;
                case PartyPortraitId.Coletta:
                    return CombatElement.Ice;
                case PartyPortraitId.WandererMale:
                case PartyPortraitId.WandererFemale:
                default:
                    return CombatElement.Light;
            }
        }

        static bool ContainsIgnoreCase(string source, string value)
        {
            return source.IndexOf(value, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static void WireHitRelay(AttackHitRelay relay, Transform ownerRoot, CharacterAvatar avatar)
        {
            if (relay == null)
            {
                return;
            }

            Transform weapon = avatar != null ? avatar.Weapon : null;
            Transform vfx = avatar != null ? avatar.VfxSocket : weapon;
            Transform hit = avatar != null ? avatar.HitOrigin : weapon;
            Transform chestR = avatar != null ? avatar.Hits?.ChestR : null;
            Transform chestL = avatar != null ? avatar.Hits?.ChestL : null;
            Transform hitRoot = avatar != null ? avatar.Hits?.Root : null;

            relay.ConfigurePresentation(
                ownerRoot,
                weapon,
                vfx,
                hit,
                null,
                chestR,
                chestL,
                hitRoot,
                null,
                null,
                null);

            var settings = CharacterRuntimeSettings.Get();
            if (settings == null)
            {
                return;
            }

            PartyPortraitId portrait = PartyPortraitId.WandererFemale;
            if (avatar != null)
            {
                portrait = ResolvePortraitFromName(avatar.DisplayName);
            }

            if (portrait == PartyPortraitId.Unknown && ownerRoot != null)
            {
                portrait = ResolvePortraitFromName(ownerRoot.name);
            }

            if (portrait == PartyPortraitId.Unknown)
            {
                portrait = PartyPortraitId.WandererFemale;
            }

            TimedHitProfile timedProfile = settings.GetTimedHitProfile(portrait);
            if (timedProfile != null)
            {
                relay.SetTimedHitProfile(timedProfile);
            }
        }

        static void FitCharacterController(CharacterController cc)
        {
            const float worldHeight = 1.8f;
            const float worldRadius = 0.35f;
            const float worldStep = 0.3f;

            Vector3 lossy = cc.transform.lossyScale;
            float sx = Mathf.Max(0.0001f, Mathf.Abs(lossy.x));
            float sy = Mathf.Max(0.0001f, Mathf.Abs(lossy.y));
            float sz = Mathf.Max(0.0001f, Mathf.Abs(lossy.z));
            float sRadius = Mathf.Max(sx, sz);

            cc.height = worldHeight / sy;
            cc.radius = worldRadius / sRadius;
            cc.center = new Vector3(0f, (worldHeight * 0.5f) / sy, 0f);
            cc.slopeLimit = 45f;
            cc.skinWidth = 0.08f;
            cc.minMoveDistance = 0f;

            float maxStep = worldHeight + worldRadius * 2f;
            cc.stepOffset = Mathf.Min(worldStep, maxStep - 0.01f);
        }
    }
}
