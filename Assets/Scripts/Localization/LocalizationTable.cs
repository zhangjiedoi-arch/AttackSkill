using System.Collections.Generic;
using UnityEngine;

namespace AttackSkill.Localization
{
    [CreateAssetMenu(menuName = "AttackSkill/Localization/Table", fileName = "LocalizationTable")]
    public class LocalizationTable : ScriptableObject
    {
        public LocalizationTableType tableType = LocalizationTableType.UI;
        public List<LocalizationEntry> entries = new List<LocalizationEntry>();
    }
}
