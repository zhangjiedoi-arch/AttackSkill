using UnityEngine;

namespace AttackSkill.CameraSystem
{
    /// <summary>正交俯视烘焙图的世界标定。小地图 / 大地图共用。</summary>
    [CreateAssetMenu(menuName = "AttackSkill/Map/Bake Data", fileName = "MapBakeData")]
    public class MapBakeData : ScriptableObject
    {
        [Tooltip("烘焙范围中心（世界 XZ，Y 忽略）")]
        public Vector3 origin;

        [Tooltip("X 方向全宽（米）")]
        public float extentX = 100f;

        [Tooltip("Z 方向全深（米）")]
        public float extentZ = 100f;

        [Tooltip("贴图 UV.y 是否相对世界 +Z 翻转")]
        public bool flipY;

        [Tooltip("每米像素（仅记录；小地图显示半径另算）")]
        public float pixelsPerMeter = 4f;

        public Texture2D texture;
        public Sprite sprite;

        public Sprite ResolveSprite()
        {
            if (sprite != null)
            {
                return sprite;
            }

#if UNITY_EDITOR
            if (texture != null)
            {
                string path = UnityEditor.AssetDatabase.GetAssetPath(texture);
                sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
#endif
            return sprite;
        }

        public float MinX => origin.x - extentX * 0.5f;
        public float MaxX => origin.x + extentX * 0.5f;
        public float MinZ => origin.z - extentZ * 0.5f;
        public float MaxZ => origin.z + extentZ * 0.5f;

        public bool ContainsXZ(Vector3 world, float padMeters = 0f)
        {
            if (extentX < 0.01f || extentZ < 0.01f)
            {
                return false;
            }

            float dx = world.x - origin.x;
            float dz = world.z - origin.z;
            float hx = extentX * 0.5f + padMeters;
            float hz = extentZ * 0.5f + padMeters;
            return Mathf.Abs(dx) <= hx && Mathf.Abs(dz) <= hz;
        }

        public bool TryWorldToUv(Vector3 world, out Vector2 uv)
        {
            uv = default;
            if (extentX < 0.01f || extentZ < 0.01f)
            {
                return false;
            }

            uv.x = (world.x - origin.x) / extentX + 0.5f;
            uv.y = (world.z - origin.z) / extentZ + 0.5f;
            if (flipY)
            {
                uv.y = 1f - uv.y;
            }

            return true;
        }
    }
}
