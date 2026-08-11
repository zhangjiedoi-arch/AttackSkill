using System;
using UnityEngine;

namespace AttackSkill.Localization
{
    [Serializable]
    public class LocalizationEntry
    {
        public string key;
        [TextArea(1, 3)] public string zhHans;
        [TextArea(1, 3)] public string en;
        [TextArea(1, 3)] public string ja;

        public string Get(GameLocale locale)
        {
            switch (locale)
            {
                case GameLocale.En:
                    return string.IsNullOrEmpty(en) ? Fallback() : en;
                case GameLocale.Ja:
                    return string.IsNullOrEmpty(ja) ? Fallback() : ja;
                default:
                    return string.IsNullOrEmpty(zhHans) ? Fallback() : zhHans;
            }
        }

        string Fallback()
        {
            if (!string.IsNullOrEmpty(zhHans))
            {
                return zhHans;
            }

            if (!string.IsNullOrEmpty(en))
            {
                return en;
            }

            return ja ?? string.Empty;
        }
    }
}
