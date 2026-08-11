using UnityEngine;

namespace AttackSkill.Core
{
    /// <summary>进游戏最早把 <see cref="GameInput"/> 切到 Input System 后端。</summary>
    public static class GameInputBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            GameInput.UseInputSystem();
        }
    }
}
