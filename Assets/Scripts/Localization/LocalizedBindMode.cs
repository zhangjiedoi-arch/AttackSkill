namespace AttackSkill.Localization
{
    /// <summary>Prefab 绑定策略。</summary>
    public enum LocalizedBindMode
    {
        /// <summary>有 key 用 key；否则用直写文案在源语言反查 key；再否则显示直写。</summary>
        Auto = 0,
        /// <summary>只用 key 查表。</summary>
        Key = 1,
        /// <summary>用直写在源语言反查 key，再取当前语言。</summary>
        DirectToKey = 2,
        /// <summary>不查表，直接使用直写（临时文案）。</summary>
        DirectOnly = 3
    }
}
