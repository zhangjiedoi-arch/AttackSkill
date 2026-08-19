using System.Collections.Generic;
using AttackSkill.Character.HSM;
using AttackSkill.Rouge;
using UnityEngine;

namespace AttackSkill.Combat
{
    /// <summary>运行时战斗属性；由角色/怪物 SO 注入，并同步 Health.MaxHp。</summary>
    [DisallowMultipleComponent]
    public class CombatStats : MonoBehaviour
    {
        const float DefaultSkillECooldown = 5f;
        const float DefaultSkillRCooldown = 10f;

        [SerializeField] CombatStatBlock runtime = CombatStatBlock.DefaultCharacter(CombatElement.Light);
        [SerializeField] CharacterCombatStatsDefinition characterSource;
        [SerializeField] EnemyCombatStatsDefinition enemySource;
        [SerializeField] float skillECooldown = DefaultSkillECooldown;
        [SerializeField] float skillRCooldown = DefaultSkillRCooldown;

        Health _health;
        GenshinLikeCharacter _player;
        float _skillEReadyAt;
        float _skillRReadyAt;

        static readonly List<CombatStats> LiveInstances = new List<CombatStats>(64);

        /// <summary>随肉鸽等级缩放后的攻击（玩家与敌人相同系数）。</summary>
        public float Attack => runtime.attack * LevelStatMul;
        /// <summary>随肉鸽等级缩放后的防御（玩家与敌人相同系数）。</summary>
        public float Defense => runtime.defense * LevelStatMul;
        public float CritRate => runtime.critRate;
        public float CritDamage => runtime.critDamage;
        /// <summary>随肉鸽等级缩放后的最大生命（不含深渊契约等被动）。</summary>
        public float MaxHp => runtime.maxHp * LevelStatMul;
        public CombatElement Element => runtime.element;
        public CombatStatBlock Runtime => runtime;

        public float SkillECooldown => Mathf.Max(0f, skillECooldown);
        public float SkillRCooldown => Mathf.Max(0f, skillRCooldown);

        public bool IsSkillEReady => Time.time >= _skillEReadyAt;
        public bool IsSkillRReady => Time.time >= _skillRReadyAt;

        public float SkillERemaining => Mathf.Max(0f, _skillEReadyAt - Time.time);
        public float SkillRRemaining => Mathf.Max(0f, _skillRReadyAt - Time.time);

        /// <summary>fillAmount：0=刚进 CD，1=可用。</summary>
        public float SkillEFillAmount
        {
            get
            {
                float d = SkillECooldown;
                return d <= 0.01f ? 1f : Mathf.Clamp01(1f - SkillERemaining / d);
            }
        }

        public float SkillRFillAmount
        {
            get
            {
                float d = SkillRCooldown;
                return d <= 0.01f ? 1f : Mathf.Clamp01(1f - SkillRRemaining / d);
            }
        }

        public CharacterCombatStatsDefinition CharacterSource => characterSource;
        public EnemyCombatStatsDefinition EnemySource => enemySource;

        float LevelStatMul => RougePassiveEffects.LevelStatMul;

        /// <summary>
        /// 装配时 CombatStats 早于 GenshinLikeCharacter 加入，不能在 Awake 里把「不是玩家」缓存死。
        /// </summary>
        bool IsPlayerUnit
        {
            get
            {
                if (_player != null)
                {
                    return true;
                }

                if (enemySource != null)
                {
                    return false;
                }

                _player = GetComponent<GenshinLikeCharacter>();
                if (_player == null)
                {
                    _player = GetComponentInParent<GenshinLikeCharacter>();
                }

                return _player != null || characterSource != null;
            }
        }

        void Awake()
        {
            _health = GetComponent<Health>();
            _player = GetComponent<GenshinLikeCharacter>()
                      ?? GetComponentInParent<GenshinLikeCharacter>();
        }

        void OnEnable()
        {
            if (!LiveInstances.Contains(this))
            {
                LiveInstances.Add(this);
            }
        }

        void OnDisable()
        {
            LiveInstances.Remove(this);
        }

        /// <summary>玩家升级后重算场上单位 Health（攻防读 getter，血量需同步）。</summary>
        public static void RefreshAllHealthForRougeLevel()
        {
            for (int i = LiveInstances.Count - 1; i >= 0; i--)
            {
                CombatStats stats = LiveInstances[i];
                if (stats == null)
                {
                    LiveInstances.RemoveAt(i);
                    continue;
                }

                stats.RefreshHealth(refillHp: false);
            }
        }

        public void ApplyCharacterDefinition(CharacterCombatStatsDefinition def, bool refillHp = true)
        {
            characterSource = def;
            enemySource = null;
            if (def != null)
            {
                runtime = def.stats;
                skillECooldown = def.skillECooldown > 0f ? def.skillECooldown : DefaultSkillECooldown;
                skillRCooldown = def.skillRCooldown > 0f ? def.skillRCooldown : DefaultSkillRCooldown;
            }
            else
            {
                skillECooldown = DefaultSkillECooldown;
                skillRCooldown = DefaultSkillRCooldown;
            }

            SyncHealth(refillHp);
        }

        public void ApplyEnemyDefinition(EnemyCombatStatsDefinition def, bool refillHp = true)
        {
            enemySource = def;
            characterSource = null;
            if (def != null)
            {
                runtime = def.stats;
            }
            else
            {
                runtime = CombatStatBlock.DefaultEnemyThunder();
            }

            SyncHealth(refillHp);
        }

        public void ApplyBlock(in CombatStatBlock block, bool refillHp = true)
        {
            runtime = block;
            if (characterSource == null)
            {
                skillECooldown = DefaultSkillECooldown;
                skillRCooldown = DefaultSkillRCooldown;
            }

            SyncHealth(refillHp);
        }

        public void BeginSkillECooldown()
        {
            _skillEReadyAt = Time.time + SkillECooldown;
        }

        public void BeginSkillRCooldown()
        {
            _skillRReadyAt = Time.time + SkillRCooldown;
        }

        public static CombatStats Ensure(GameObject root)
        {
            if (root == null)
            {
                return null;
            }

            var stats = root.GetComponent<CombatStats>();
            if (stats == null)
            {
                stats = root.AddComponent<CombatStats>();
            }

            return stats;
        }

        public static CombatStats Find(Component host)
        {
            if (host == null)
            {
                return null;
            }

            return host.GetComponentInParent<CombatStats>();
        }

        public static CombatStats Find(GameObject host)
        {
            if (host == null)
            {
                return null;
            }

            return host.GetComponentInParent<CombatStats>();
        }

        /// <summary>按当前等级/被动重算 Health（升级时保留血量比例）。</summary>
        public void RefreshHealth(bool refillHp = false)
        {
            SyncHealth(refillHp);
        }

        void SyncHealth(bool refillHp)
        {
            if (_health == null)
            {
                _health = GetComponent<Health>();
            }

            if (_health == null)
            {
                return;
            }

            float hp = Mathf.Max(1f, MaxHp);
            if (IsPlayerUnit)
            {
                hp = Mathf.Max(1f, hp * RougePassiveEffects.MaxHpMul);
            }

            if (refillHp)
            {
                _health.Configure(hp, destroyWhenDead: false);
            }
            else
            {
                float ratio = _health.MaxHp > 0.01f ? _health.CurrentHp / _health.MaxHp : 1f;
                _health.Configure(hp, destroyWhenDead: false);
                _health.SetCurrentHp(hp * ratio);
            }
        }
    }
}
