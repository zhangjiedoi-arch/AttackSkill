using UnityEngine;

namespace AttackSkill.Localization
{
    /// <summary>语言总表索引，放 Resources/Localization/LocalizationCatalog。</summary>
    [CreateAssetMenu(menuName = "AttackSkill/Localization/Catalog", fileName = "LocalizationCatalog")]
    public class LocalizationCatalog : ScriptableObject
    {
        public const string ResourcesPath = "Localization/LocalizationCatalog";

        public GameLocale defaultLocale = GameLocale.ZhHans;
        [Tooltip("反查 key 时使用的源语言（Prefab 直写通常用简中）")]
        public GameLocale authoringLocale = GameLocale.ZhHans;
        public LocalizationTable[] tables;

        static LocalizationCatalog _cached;

        public static LocalizationCatalog Get()
        {
            if (_cached == null)
            {
                _cached = Resources.Load<LocalizationCatalog>(ResourcesPath);
            }

            return _cached;
        }

#if UNITY_EDITOR
        public static void ClearCache()
        {
            _cached = null;
        }
#endif
    }
}
