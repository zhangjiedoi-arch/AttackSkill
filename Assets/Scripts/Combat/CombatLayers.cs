using UnityEngine;

namespace AttackSkill.Combat
{
    /// <summary>战斗相关层约定（TagManager：Player / Enemy）。</summary>
    public static class CombatLayers
    {
        public const string PlayerLayerName = "Player";
        public const string EnemyLayerName = "Enemy";

        public static int PlayerLayer => LayerMask.NameToLayer(PlayerLayerName);
        public static int EnemyLayer => LayerMask.NameToLayer(EnemyLayerName);

        /// <summary>玩家普攻应检测的 hurtbox 层（Enemy；缺失时回退 Everything）。</summary>
        public static LayerMask DefaultEnemyHurtboxMask
        {
            get
            {
                int enemy = EnemyLayer;
                return enemy >= 0 ? (LayerMask)(1 << enemy) : (LayerMask)~0;
            }
        }

        /// <summary>敌人攻击应检测的 hurtbox 层（Player）。</summary>
        public static LayerMask DefaultPlayerHurtboxMask
        {
            get
            {
                int player = PlayerLayer;
                return player >= 0 ? (LayerMask)(1 << player) : (LayerMask)~0;
            }
        }

        /// <summary>
        /// 玩家攻击检测：Enemy + Default（兼容未改层的旧敌人 Prefab）。
        /// </summary>
        public static LayerMask PlayerOffenseHurtboxMask
        {
            get
            {
                int mask = 0;
                int enemy = EnemyLayer;
                if (enemy >= 0)
                {
                    mask |= 1 << enemy;
                }

                // Default = 0
                mask |= 1 << 0;
                return mask == 0 ? (LayerMask)~0 : (LayerMask)mask;
            }
        }

        /// <summary>相机防穿：排除 UI / 特效 / 玩家自身等。</summary>
        public static LayerMask DefaultCameraCollisionMask
        {
            get
            {
                int mask = ~0;
                Exclude(ref mask, "TransparentFX");
                Exclude(ref mask, "Ignore Raycast");
                Exclude(ref mask, "UI");
                Exclude(ref mask, "Water");
                Exclude(ref mask, "Hidden");
                Exclude(ref mask, PlayerLayerName);
                return mask;
            }
        }

        public static void ApplyLayerRecursively(GameObject root, int layer)
        {
            if (root == null || layer < 0)
            {
                return;
            }

            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                transforms[i].gameObject.layer = layer;
            }
        }

        static void Exclude(ref int mask, string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (layer >= 0)
            {
                mask &= ~(1 << layer);
            }
        }
    }
}
