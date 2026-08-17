using AttackSkill.Character.Exploration;
using AttackSkill.Combat;
using UnityEngine;

namespace AttackSkill.Character
{
    /// <summary>
    /// 运行时装配默认资源（放 Resources/CharacterRuntimeSettings）。
    /// 包体只从这里取 Prefab，禁止运行时 AssetDatabase。
    /// </summary>
    [CreateAssetMenu(menuName = "AttackSkill/Character/Runtime Settings", fileName = "CharacterRuntimeSettings")]
    public class CharacterRuntimeSettings : ScriptableObject
    {
        public const string ResourcesPath = "CharacterRuntimeSettings";

        [Header("Skill / VFX")]
        public GameObject skillTimelinePrefab;
        public GameObject skillCameraPrefab;
        public GameObject circleVfxPrefab;
        public GameObject slashVfxPrefab;
        [Tooltip("Assets/Prefabs/VFX/Snow hit.prefab — Hit_Chest_L/R")]
        public GameObject snowHitVfxPrefab;
        [Tooltip("Assets/Prefabs/VFX/Ground AOE explosion.prefab — Hit_Root")]
        public GameObject groundAoeExplosionVfxPrefab;
        [Tooltip("玩家 E 技能多段出伤表；空则用运行时默认（legacy，优先 TimedHit）")]
        public SkillHitProfile playerSkillHitProfile;

        [Header("Timed Hit Profiles（普攻/E/R，按角色）")]
        [Tooltip("Assets/HitProfile/HitProfile_漂泊者")]
        public TimedHitProfile timedHitWanderer;
        [Tooltip("Assets/HitProfile/HitProfile_千咲")]
        public TimedHitProfile timedHitQianxiao;
        [Tooltip("Assets/HitProfile/HitProfile_柯莱塔")]
        public TimedHitProfile timedHitColetta;

        [Header("Skill Hit SFX")]
        [Tooltip("Assets/Audio/FatKick_R.wav — Hit_Chest_R")]
        public AudioClip skillHitFatKickR;
        [Tooltip("Assets/Audio/FatKick_L.wav — Hit_Chest_L")]
        public AudioClip skillHitFatKickL;
        [Tooltip("Assets/Audio/Hit_Root_Land.wav — Hit_Root")]
        public AudioClip skillHitRootLand;

        [Header("World UI")]
        [Tooltip("Assets/Prefabs/UI/WorldUI/DamageNumber.prefab")]
        public GameObject damageNumberPrefab;
        [Tooltip("Assets/Prefabs/UI/WorldUI/Enemy_blood.prefab")]
        public GameObject enemyBloodPrefab;
        [Tooltip("Assets/Prefabs/UI/WorldUI/ObtainRemains.prefab — 声骸 F 提示")]
        public GameObject obtainRemainsPrefab;
        [Tooltip("怪物血条相对玩家的显示距离（米）")]
        public float enemyBloodVisibleRange = 20f;

        [Header("Party Roster")]
        public GameObject maleWandererPrefab;
        public GameObject femaleWandererPrefab;
        public GameObject qianxiaoPrefab;
        public GameObject colettaPrefab;

        [Header("Tools (挂点装备)")]
        [Tooltip("挂到 Motorcycle_pos：Prefabs/Tools/摩托")]
        public GameObject motorcyclePrefab;
        [Tooltip("挂到 Sword_pos：Prefabs/Tools/脆刃")]
        public GameObject swordPrefab;
        [Tooltip("挂到 wings_pos：Prefabs/Tools/哥伦比亚的翅膀")]
        public GameObject wingsPrefab;

        [Header("Skill R")]
        [Tooltip("Assets/Prefabs/VFX/AoE slash orange.prefab — 挂到 R_Hit_Root")]
        public GameObject skillRAoeVfxPrefab;

        [Header("Flight Airflow Vfx")]
        [Tooltip("Prefabs/VFX/Sparks blue — 翅膀/御剑飞行气流")]
        public GameObject flightAirflowVfxPrefab;
        [Tooltip("挂点本地偏移；Y 会叠在 wings_pos / Sword_pos 高度上。无 Avatar 覆盖时用此值。")]
        public Vector3 flightAirflowLocalOffset = new Vector3(0f, 0f, -0.5f);
        [Tooltip("挂点缺失时的本地 Y 回退")]
        public float flightAirflowFallbackLocalY = 1.05f;

        [Header("Exploration Tools")]
        [Tooltip("可选：覆盖 Resources/ExplorationToolCatalog")]
        public ExplorationToolCatalog explorationToolCatalog;

        [Header("Locomotion Audio")]
        [Tooltip("Assets/Audio/Jump_Land.wav")]
        public AudioClip jumpLand;
        [Tooltip("Assets/Audio/Jump_Loop.wav — Fall 下落循环")]
        public AudioClip jumpLoop;

        [Header("Exploration Audio")]
        public AudioClip flyingLoop;
        public AudioClip flyingTakeOff;
        public AudioClip flyingGoUp;
        public AudioClip flyingDown;
        public AudioClip swordFlyingLoop;
        public AudioClip swordFlyingGoOn;
        public AudioClip motorcycleLoop;
        public AudioClip motorcycleDownSpeed;
        public AudioClip motorcycleGoOn;
        public AudioClip motorcycleJump;

        [Header("Scene BGM")]
        [Tooltip("GameScene 海滩 BGM")]
        public AudioClip seaBgm;

        [Header("Party Portraits (Battle HUD)")]
        [Tooltip("IconPlayerFemale")]
        public Sprite iconPlayerFemale;
        [Tooltip("IconPlayerMale")]
        public Sprite iconPlayerMale;
        [Tooltip("IconQianxiao")]
        public Sprite iconQianxiao;
        [Tooltip("IconKelaita")]
        public Sprite iconKelaita;

        static CharacterRuntimeSettings _cached;

        public static CharacterRuntimeSettings Get()
        {
            if (_cached == null)
            {
                _cached = Resources.Load<CharacterRuntimeSettings>(ResourcesPath);
            }

            return _cached;
        }

        public GameObject GetSkillTimeline() => skillTimelinePrefab;
        public GameObject GetSkillCamera() => skillCameraPrefab;
        public GameObject GetCircleVfx() => circleVfxPrefab;
        public GameObject GetSlashVfx() => slashVfxPrefab;
        public GameObject GetSnowHitVfx() => snowHitVfxPrefab;
        public GameObject GetGroundAoeExplosionVfx() => groundAoeExplosionVfxPrefab;
        public GameObject GetFlightAirflowVfx() => flightAirflowVfxPrefab;
        public AudioClip GetSkillHitFatKickR() => skillHitFatKickR;
        public AudioClip GetSkillHitFatKickL() => skillHitFatKickL;
        public AudioClip GetSkillHitRootLand() => skillHitRootLand;
        public GameObject GetDamageNumberPrefab() => damageNumberPrefab;
        public GameObject GetEnemyBloodPrefab() => enemyBloodPrefab;
        public GameObject GetObtainRemainsPrefab() => obtainRemainsPrefab;
        public GameObject GetSkillRAoeVfx() => skillRAoeVfxPrefab;

        public TimedHitProfile GetTimedHitProfile(PartyPortraitId portraitId)
        {
            EnsureTimedHitProfilesLoaded();
            switch (portraitId)
            {
                case PartyPortraitId.Qianxiao:
                    return timedHitQianxiao != null ? timedHitQianxiao : timedHitWanderer;
                case PartyPortraitId.Coletta:
                    return timedHitColetta != null ? timedHitColetta : timedHitWanderer;
                case PartyPortraitId.WandererMale:
                case PartyPortraitId.WandererFemale:
                default:
                    return timedHitWanderer;
            }
        }

        void EnsureTimedHitProfilesLoaded()
        {
            if (timedHitWanderer == null)
            {
                timedHitWanderer = Resources.Load<TimedHitProfile>("HitProfile/HitProfile_漂泊者");
            }

            if (timedHitQianxiao == null)
            {
                timedHitQianxiao = Resources.Load<TimedHitProfile>("HitProfile/HitProfile_千咲");
            }

            if (timedHitColetta == null)
            {
                timedHitColetta = Resources.Load<TimedHitProfile>("HitProfile/HitProfile_柯莱塔");
            }
        }

        public SkillHitProfile GetPlayerSkillHitProfile()
        {
            if (playerSkillHitProfile == null)
            {
                playerSkillHitProfile = Resources.Load<SkillHitProfile>("Combat/SkillHit_Player_E");
            }

            if (playerSkillHitProfile != null)
            {
                FillSkillHitSfxIfEmpty(playerSkillHitProfile);
                return playerSkillHitProfile;
            }

            return SkillHitProfileDefaults.PlayerE(
                snowHitVfxPrefab,
                groundAoeExplosionVfxPrefab,
                skillHitFatKickR,
                skillHitFatKickL,
                skillHitRootLand);
        }

        public void FillSkillHitSfxIfEmpty(SkillHitProfile profile)
        {
            if (profile?.segments == null)
            {
                return;
            }

            for (int i = 0; i < profile.segments.Length; i++)
            {
                var seg = profile.segments[i];
                if (seg == null || seg.sfxClip != null)
                {
                    continue;
                }

                switch (seg.socket)
                {
                    case HitSocketId.Hit_Chest_R:
                        seg.sfxClip = skillHitFatKickR;
                        break;
                    case HitSocketId.Hit_Chest_L:
                        seg.sfxClip = skillHitFatKickL;
                        break;
                    case HitSocketId.Hit_Root:
                        seg.sfxClip = skillHitRootLand;
                        break;
                }
            }
        }

        public bool TryGetPartyRoster(
            out GameObject male,
            out GameObject female,
            out GameObject qianxiao,
            out GameObject coletta)
        {
            male = maleWandererPrefab;
            female = femaleWandererPrefab;
            qianxiao = qianxiaoPrefab;
            coletta = colettaPrefab;
            return male != null && female != null && qianxiao != null && coletta != null;
        }

        public Sprite GetPartyPortrait(PartyPortraitId id)
        {
            switch (id)
            {
                case PartyPortraitId.WandererFemale:
                    return iconPlayerFemale;
                case PartyPortraitId.WandererMale:
                    return iconPlayerMale;
                case PartyPortraitId.Qianxiao:
                    return iconQianxiao;
                case PartyPortraitId.Coletta:
                    return iconKelaita;
                default:
                    return null;
            }
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            // 删除 Resources/Tools 后，自动补齐 Prefabs/Tools 引用
            if (motorcyclePrefab == null)
            {
                motorcyclePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/Tools/摩托.prefab");
            }

            if (swordPrefab == null)
            {
                swordPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/Tools/脆刃.prefab");
            }

            if (wingsPrefab == null)
            {
                wingsPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/Tools/哥伦比亚的翅膀.prefab");
            }

            if (skillRAoeVfxPrefab == null)
            {
                skillRAoeVfxPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/VFX/AoE slash orange.prefab");
            }

            if (snowHitVfxPrefab == null)
            {
                snowHitVfxPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/VFX/Snow hit.prefab");
            }

            if (groundAoeExplosionVfxPrefab == null)
            {
                groundAoeExplosionVfxPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/VFX/Ground AOE explosion.prefab");
            }

            if (playerSkillHitProfile == null)
            {
                playerSkillHitProfile = UnityEditor.AssetDatabase.LoadAssetAtPath<SkillHitProfile>(
                    "Assets/Resources/Combat/SkillHit_Player_E.asset");
            }

            if (timedHitWanderer == null)
            {
                timedHitWanderer = UnityEditor.AssetDatabase.LoadAssetAtPath<TimedHitProfile>(
                    "Assets/HitProfile/HitProfile_漂泊者.asset");
            }

            if (timedHitQianxiao == null)
            {
                timedHitQianxiao = UnityEditor.AssetDatabase.LoadAssetAtPath<TimedHitProfile>(
                    "Assets/HitProfile/HitProfile_千咲.asset");
            }

            if (timedHitColetta == null)
            {
                timedHitColetta = UnityEditor.AssetDatabase.LoadAssetAtPath<TimedHitProfile>(
                    "Assets/HitProfile/HitProfile_柯莱塔.asset");
            }

            if (flightAirflowVfxPrefab == null)
            {
                flightAirflowVfxPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/VFX/Sparks blue.prefab");
            }

            if (skillHitFatKickR == null)
            {
                skillHitFatKickR = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(
                    "Assets/Audio/FatKick_R.wav");
            }

            if (skillHitFatKickL == null)
            {
                skillHitFatKickL = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(
                    "Assets/Audio/FatKick_L.wav");
            }

            if (skillHitRootLand == null)
            {
                skillHitRootLand = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(
                    "Assets/Audio/Hit_Root_Land.wav");
            }

            if (damageNumberPrefab == null)
            {
                damageNumberPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/UI/WorldUI/DamageNumber.prefab");
            }

            if (enemyBloodPrefab == null)
            {
                enemyBloodPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/UI/WorldUI/Enemy_blood.prefab");
            }

            if (obtainRemainsPrefab == null)
            {
                obtainRemainsPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/UI/WorldUI/ObtainRemains.prefab");
            }

            if (playerSkillHitProfile != null)
            {
                FillSkillHitSfxIfEmpty(playerSkillHitProfile);
            }
        }
#endif
    }
}
