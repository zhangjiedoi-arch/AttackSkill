namespace AttackSkill.Game
{
    /// <summary>跨场景启动意图：开场「连接」= 新开局；主菜单继续 = Continue。</summary>
    public enum GameBootIntent
    {
        /// <summary>未指定：直接进 GameScene 时，由 GameProgress.loadSaveOnStart 决定是否读档。</summary>
        Unspecified = 0,
        /// <summary>新开一局：不加载进度档（磁盘档保留）。</summary>
        NewGame = 1,
        /// <summary>继续：加载进度档并 PendingRestore。</summary>
        Continue = 2
    }

    /// <summary>开场 / 进度系统共享的一次性 Boot 意图。</summary>
    public static class GameBoot
    {
        static GameBootIntent _intent = GameBootIntent.Unspecified;

        public static GameBootIntent Intent => _intent;

        public static void SetIntent(GameBootIntent intent)
        {
            _intent = intent;
        }

        /// <summary>读取并重置为 Unspecified。</summary>
        public static GameBootIntent ConsumeIntent()
        {
            var i = _intent;
            _intent = GameBootIntent.Unspecified;
            return i;
        }

        public static void ClearIntent()
        {
            _intent = GameBootIntent.Unspecified;
        }
    }
}
