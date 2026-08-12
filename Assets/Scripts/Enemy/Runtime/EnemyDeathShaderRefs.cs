using UnityEngine;

namespace AttackSkill.Enemy
{
    /// <summary>
    /// 挂在 Resources 里强制打进包，避免 Shader.Find 在 Player 里找不到死亡特效 Shader。
    /// </summary>
    [CreateAssetMenu(menuName = "AttackSkill/Enemy/Death Shader Refs", fileName = "EnemyDeathShaderRefs")]
    public sealed class EnemyDeathShaderRefs : ScriptableObject
    {
        public const string ResourcesPath = "Enemy/EnemyDeathShaderRefs";

        public Shader gold;
        public Shader dissolve;

        public Shader Resolve(string shaderName)
        {
            if (shaderName == EnemyDeathVisualUtil.GoldShaderName && gold != null && gold.isSupported)
            {
                return gold;
            }

            if (shaderName == EnemyDeathVisualUtil.DissolveShaderName && dissolve != null && dissolve.isSupported)
            {
                return dissolve;
            }

            return null;
        }
    }
}
