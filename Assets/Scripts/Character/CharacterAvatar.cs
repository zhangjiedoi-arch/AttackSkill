using UnityEngine;

namespace AttackSkill.Character
{
    /// <summary>
    /// 角色表现层（Avatar）：模型 / Animator / 挂点。
    /// 不含移动、战斗、技能等玩法逻辑；由 <see cref="CharacterRuntimeAssembler"/> 在生成时装配。
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterAvatar : MonoBehaviour
    {
        static readonly string[] WeaponNameKeys =
        {
            "大剑", "武器", "Weapon", "weapon", "Sword", "sword", "Blade", "blade", "Katana", "katana"
        };

        public const string MotorcycleSocketName = "Motorcycle_pos";
        public const string SwordSocketName = "Sword_pos";
        public const string WingsSocketName = "wings_pos";
        public const string HitChestRName = "Hit_Chest_R";
        public const string HitChestLName = "Hit_Chest_L";
        public const string HitRootName = "Hit_Root";
        public const string SkillRHitRootName = "R_Hit_Root";
        public const string WeaponPosName = "Weapon_Pos";

        [System.Serializable]
        public class ToolSockets
        {
            [Tooltip("摩托挂点（Motorcycle_pos）")]
            public Transform Motorcycle;
            [Tooltip("御剑挂点（Sword_pos）")]
            public Transform Sword;
            [Tooltip("翅膀挂点（wings_pos）")]
            public Transform Wings;
        }

        [System.Serializable]
        public class HitSockets
        {
            [Tooltip("右胸出伤/特效挂点")]
            public Transform ChestR;
            [Tooltip("左胸出伤/特效挂点")]
            public Transform ChestL;
            [Tooltip("根部 AOE 挂点")]
            public Transform Root;
        }

        [System.Serializable]
        public class SkillRSockets
        {
            [Tooltip("R 技能 AOE 挂点（R_Hit_Root）")]
            public Transform RHitRoot;
            [Tooltip("R 技能武器挂点（Weapon_Pos）；普攻期间显示，平时隐藏")]
            public Transform WeaponPos;
        }

        [Header("Presentation")]
        [SerializeField] Animator animator;
        [SerializeField] Transform weapon;
        [Tooltip("刀光世界坐标取样点；为空则用 weapon")]
        [SerializeField] Transform vfxSocket;
        [Tooltip("扇形伤害原点；为空则用 weapon")]
        [SerializeField] Transform hitOrigin;

        [Header("Tool Sockets")]
        [SerializeField] ToolSockets toolSockets = new ToolSockets();

        [Header("Hit Sockets")]
        [SerializeField] HitSockets hitSockets = new HitSockets();

        [Header("Skill R Sockets（全角色预留；漂泊者已接线）")]
        [SerializeField] SkillRSockets skillRSockets = new SkillRSockets();

        [Header("Flight Airflow Vfx")]
        [Tooltip("勾选后用下方偏移覆盖 CharacterRuntimeSettings 全局值")]
        [SerializeField] bool overrideAirflowOffset;
        [Tooltip("叠在挂点高度上的本地偏移（默认 Z=-0.5）")]
        [SerializeField] Vector3 airflowLocalOffset = new Vector3(0f, 0f, -0.5f);
        [Tooltip("wings_pos / Sword_pos 缺失时的本地 Y")]
        [SerializeField] float airflowFallbackLocalY = 1.05f;

        [Header("Optional")]
        [SerializeField] string displayName;

        public Animator Animator => animator;
        public Transform Weapon => weapon;
        public Transform VfxSocket => vfxSocket != null ? vfxSocket : weapon;
        public Transform HitOrigin => hitOrigin != null ? hitOrigin : (weapon != null ? weapon : transform);
        public ToolSockets Tools => toolSockets;
        public HitSockets Hits => hitSockets;
        public SkillRSockets SkillR => skillRSockets;
        public bool OverrideAirflowOffset => overrideAirflowOffset;
        public Vector3 AirflowLocalOffset => airflowLocalOffset;
        public float AirflowFallbackLocalY => airflowFallbackLocalY;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;

        void Reset()
        {
            AutoBind();
        }

        void OnValidate()
        {
            if (animator == null || weapon == null ||
                toolSockets == null ||
                toolSockets.Motorcycle == null ||
                toolSockets.Sword == null ||
                toolSockets.Wings == null ||
                hitSockets == null ||
                hitSockets.ChestR == null ||
                hitSockets.ChestL == null ||
                hitSockets.Root == null ||
                skillRSockets == null ||
                skillRSockets.RHitRoot == null ||
                skillRSockets.WeaponPos == null)
            {
                AutoBind();
            }
        }

        /// <summary>按子层级自动查找 Animator / 武器 / 工具挂点（仅填空引用）。</summary>
        public void AutoBind()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>(true);
            }

            if (weapon == null)
            {
                weapon = FindChildExactOrContains(transform, SwordSocketName, WeaponNameKeys);
            }

            if (vfxSocket == null)
            {
                vfxSocket = weapon;
            }

            if (hitOrigin == null)
            {
                hitOrigin = weapon != null ? weapon : transform;
            }

            if (toolSockets == null)
            {
                toolSockets = new ToolSockets();
            }

            if (toolSockets.Motorcycle == null)
            {
                toolSockets.Motorcycle = FindChildExact(transform, MotorcycleSocketName);
            }

            if (toolSockets.Sword == null)
            {
                toolSockets.Sword = FindChildExact(transform, SwordSocketName);
                if (toolSockets.Sword == null && weapon != null && weapon.name == SwordSocketName)
                {
                    toolSockets.Sword = weapon;
                }
            }

            if (toolSockets.Wings == null)
            {
                toolSockets.Wings = FindChildExact(transform, WingsSocketName);
            }

            if (hitSockets == null)
            {
                hitSockets = new HitSockets();
            }

            if (hitSockets.ChestR == null)
            {
                hitSockets.ChestR = FindChildExact(transform, HitChestRName);
            }

            if (hitSockets.ChestL == null)
            {
                hitSockets.ChestL = FindChildExact(transform, HitChestLName);
            }

            if (hitSockets.Root == null)
            {
                hitSockets.Root = FindChildExact(transform, HitRootName);
            }

            if (skillRSockets == null)
            {
                skillRSockets = new SkillRSockets();
            }

            if (skillRSockets.RHitRoot == null)
            {
                skillRSockets.RHitRoot = FindChildExact(transform, SkillRHitRootName);
            }

            if (skillRSockets.WeaponPos == null)
            {
                skillRSockets.WeaponPos = FindChildExact(transform, WeaponPosName);
            }

            if (animator != null)
            {
                animator.applyRootMotion = false;
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

        static Transform FindChildExactOrContains(Transform root, string exactPreferred, string[] containsKeys)
        {
            Transform exact = FindChildExact(root, exactPreferred);
            if (exact != null)
            {
                return exact;
            }

            if (root == null || containsKeys == null)
            {
                return null;
            }

            var all = root.GetComponentsInChildren<Transform>(true);
            for (int k = 0; k < containsKeys.Length; k++)
            {
                string key = containsKeys[k];
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] != null &&
                        all[i].name.IndexOf(key, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return all[i];
                    }
                }
            }

            return null;
        }
    }
}
