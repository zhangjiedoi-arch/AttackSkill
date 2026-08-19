using System.Collections.Generic;
using AttackSkill.Localization;
using AttackSkill.Rouge;

namespace AttackSkill.UI
{
    public sealed class SkillSelectArgs
    {
        public List<RougePassiveDefData> options;
    }

    public static class RougePassiveText
    {
        public static string Name(RougePassiveDefData def)
        {
            return Resolve(def, def != null ? def.nameKey : null, "_name");
        }

        public static string Desc(RougePassiveDefData def)
        {
            return Resolve(def, def != null ? def.descKey : null, "_desc");
        }

        static string Resolve(RougePassiveDefData def, string key, string suffix)
        {
            if (def == null)
            {
                return string.Empty;
            }

            if (string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(def.id))
            {
                key = "rouge_passive_" + def.id + suffix;
            }

            if (string.IsNullOrEmpty(key))
            {
                return string.Empty;
            }

            return LocalizationService.Get(LocalizationTableType.Story, key);
        }
    }
}
