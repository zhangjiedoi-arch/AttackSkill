using AttackSkill.Character.Exploration;
using AttackSkill.Combat;
using UnityEngine;

namespace AttackSkill.Character
{
    /// <summary>
    /// 运行时装配默认资源（放 Resources/CharacterRuntimeSettings）。
    /// 包体只从这里取 Prefab，禁止运行时 AssetDatabase。
    /// 战斗出伤/特效走 TimedHitProfile，不在此配置。
    /// </summary>
    [CreateAssetMenu(menuName = "AttackSkill/Character/Runtime Settings", fileName = "CharacterRuntimeSettings")]
    public class CharacterRuntimeSettings : ScriptableObject
    {
        public const string ResourcesPath = "CharacterRuntimeSettings";

        [Header("Timed Hit Profiles（普攻/E/R，按角色）")]
        [Tooltip("Assets/HitProfile/HitProfile_漂泊者")]
        public TimedHitProfile timedHitWanderer;
        [Tooltip("Assets/HitProfile/HitProfile_千咲")]
        public TimedHitProfile timedHitQianxiao;
        [Tooltip("Assets/HitProfile/HitProfile_柯莱塔")]
        public TimedHitProfile timedHitColetta;

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
        [Tooltip("肉鸽区域 BGM（Assets/Audio/drone.mp3）")]
        public AudioClip droneBgm;

        [Header("Enemy Drops")]
        [Tooltip("Prefabs/VFX/Healing circle")]
        public GameObject healingCirclePrefab;
        [Tooltip("Prefabs/VFX/Healing — 挂玩家 Hit_Root")]
        public GameObject healingAuraPrefab;
        [Range(0f, 1f)] public float enemyHealDropChance = 0.3f;
        public float healingCircleRadius = 3f;
        public float healingCircleHealPerSecond = 100f;
        public float healingCircleLifetime = 20f;
        [Tooltip("Prefabs/Tools/Exp — 经验球")]
        public GameObject expOrbPrefab;

        [Header("Rouge Orbit Weapons")]
        [Tooltip("Prefabs/Weapon/火之刃")]
        public GameObject fireOrbitBladePrefab;
        [Tooltip("Prefabs/Weapon/风之刃")]
        public GameObject windOrbitBladePrefab;

        [Header("Rouge Constructs")]
        [Tooltip("Prefabs/Weapon/冰之哀伤")]
        public GameObject iceSorrowPrefab;
        [Tooltip("Prefabs/Weapon/火之高兴")]
        public GameObject fireJoyPrefab;
        [Tooltip("Prefabs/Weapon/雪之哀霜")]
        public GameObject snowFrostPrefab;
        [Tooltip("Prefabs/Weapon/诱敌之树")]
        public GameObject decoyTreePrefab;

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

        public GameObject GetFlightAirflowVfx() => flightAirflowVfxPrefab;
        public GameObject GetDamageNumberPrefab() => damageNumberPrefab;
        public GameObject GetEnemyBloodPrefab() => enemyBloodPrefab;
        public GameObject GetObtainRemainsPrefab() => obtainRemainsPrefab;

        public GameObject GetExpOrbPrefab()
        {
            if (expOrbPrefab == null)
            {
                expOrbPrefab = Resources.Load<GameObject>("Rouge/Exp");
            }

#if UNITY_EDITOR
            if (expOrbPrefab == null)
            {
                expOrbPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/Tools/Exp.prefab");
            }
#endif
            return expOrbPrefab;
        }

        public GameObject GetFireOrbitBladePrefab()
        {
            if (fireOrbitBladePrefab == null)
            {
                fireOrbitBladePrefab = LoadWeaponPrefab("火之刃");
            }

            return fireOrbitBladePrefab;
        }

        public GameObject GetWindOrbitBladePrefab()
        {
            if (windOrbitBladePrefab == null)
            {
                windOrbitBladePrefab = LoadWeaponPrefab("风之刃");
            }

            return windOrbitBladePrefab;
        }

        public GameObject GetIceSorrowPrefab()
        {
            if (iceSorrowPrefab == null)
            {
                iceSorrowPrefab = LoadWeaponPrefab("冰之哀伤");
            }

            return iceSorrowPrefab;
        }

        public GameObject GetFireJoyPrefab()
        {
            if (fireJoyPrefab == null)
            {
                fireJoyPrefab = LoadWeaponPrefab("火之高兴");
            }

            return fireJoyPrefab;
        }

        public GameObject GetSnowFrostPrefab()
        {
            if (snowFrostPrefab == null)
            {
                snowFrostPrefab = LoadWeaponPrefab("雪之哀霜");
            }

            return snowFrostPrefab;
        }

        public GameObject GetDecoyTreePrefab()
        {
            if (decoyTreePrefab == null)
            {
                decoyTreePrefab = LoadWeaponPrefab("诱敌之树");
            }

            return decoyTreePrefab;
        }

        static GameObject LoadWeaponPrefab(string fileName)
        {
            var fromResources = Resources.Load<GameObject>("Weapon/" + fileName);
            if (fromResources != null)
            {
                return fromResources;
            }

#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                $"Assets/Prefabs/Weapon/{fileName}.prefab");
#else
            return null;
#endif
        }

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

            if (healingCirclePrefab == null)
            {
                healingCirclePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/VFX/Healing circle.prefab");
            }

            if (healingAuraPrefab == null)
            {
                healingAuraPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/VFX/Healing.prefab");
            }

            if (expOrbPrefab == null)
            {
                expOrbPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Resources/Rouge/Exp.prefab");
            }

            if (expOrbPrefab == null)
            {
                expOrbPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/Tools/Exp.prefab");
            }

            if (fireOrbitBladePrefab == null)
            {
                fireOrbitBladePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/Weapon/火之刃.prefab");
            }

            if (windOrbitBladePrefab == null)
            {
                windOrbitBladePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/Weapon/风之刃.prefab");
            }

            if (iceSorrowPrefab == null)
            {
                iceSorrowPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/Weapon/冰之哀伤.prefab");
            }

            if (fireJoyPrefab == null)
            {
                fireJoyPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/Weapon/火之高兴.prefab");
            }

            if (snowFrostPrefab == null)
            {
                snowFrostPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/Weapon/雪之哀霜.prefab");
            }

            if (decoyTreePrefab == null)
            {
                decoyTreePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/Weapon/诱敌之树.prefab");
            }

            if (droneBgm == null)
            {
                droneBgm = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(
                    "Assets/Audio/drone.mp3");
            }
        }
#endif
    }
}
