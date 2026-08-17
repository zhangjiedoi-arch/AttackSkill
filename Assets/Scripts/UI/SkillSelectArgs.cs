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
            if (def == null)
            {
                return string.Empty;
            }

            return LocalizationService.CurrentLocale == GameLocale.En
                ? (string.IsNullOrEmpty(def.nameEn) ? def.nameZh : def.nameEn)
                : (string.IsNullOrEmpty(def.nameZh) ? def.nameEn : def.nameZh);
        }

        public static string Desc(RougePassiveDefData def)
        {
            if (def == null)
            {
                return string.Empty;
            }

            return LocalizationService.CurrentLocale == GameLocale.En
                ? (string.IsNullOrEmpty(def.descEn) ? def.descZh : def.descEn)
                : (string.IsNullOrEmpty(def.descZh) ? def.descEn : def.descZh);
        }
    }
}
