using System;

namespace AttackSkill.Localization
{
    /// <summary>导表 JSON 结构（供 JsonUtility / 运行时加载）。</summary>
    [Serializable]
    public class LocalizationTableJsonData
    {
        public string tableType;
        public LocalizationEntry[] entries;
    }

    [Serializable]
    public class LocalizationBundleJsonData
    {
        public int version = 1;
        public LocalizationTableJsonData[] tables;
    }
}
