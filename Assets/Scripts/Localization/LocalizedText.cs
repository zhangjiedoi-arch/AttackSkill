using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace AttackSkill.Localization
{
    /// <summary>
    /// UI 文本本地化：Prefab 填 key 或直写；OnEnable / 切语言时赋值一次。
    /// 可挂在带 Text 或 TextMeshProUGUI 的物体上。
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public class LocalizedText : MonoBehaviour
    {
        [Header("Lookup")]
        [SerializeField] LocalizationTableType table = LocalizationTableType.UI;
        [SerializeField] string key;
        [Tooltip("Prefab 直写预览；Key 为空时 Auto/DirectToKey 会用其反查")]
        [TextArea(1, 3)]
        [SerializeField] string directText;
        [SerializeField] LocalizedBindMode bindMode = LocalizedBindMode.Auto;

        [Header("Locale")]
        [Tooltip("勾选后该控件强制使用下方语言，否则跟随全局")]
        [SerializeField] bool overrideLocale;
        [SerializeField] GameLocale localeOverride = GameLocale.ZhHans;

        [Header("Bind")]
        [SerializeField] bool bindOnEnable = true;
        [SerializeField] bool previewInEditMode = true;

        Text _uiText;
        TMP_Text _tmpText;
        bool _cached;

        public LocalizationTableType Table
        {
            get => table;
            set => table = value;
        }

        public string Key
        {
            get => key;
            set => key = value;
        }

        public string DirectText
        {
            get => directText;
            set => directText = value;
        }

        public LocalizedBindMode BindMode
        {
            get => bindMode;
            set => bindMode = value;
        }

        /// <summary>代码动态 UI：挂/配 LocalizedText 后 Apply。</summary>
        public void Configure(LocalizationTableType tableType, string locKey, LocalizedBindMode mode = LocalizedBindMode.Key)
        {
            table = tableType;
            key = locKey ?? string.Empty;
            bindMode = mode;
            directText = string.Empty;
            _cached = false;
            CacheTargets();
            if (isActiveAndEnabled && Application.isPlaying)
            {
                Apply();
            }
        }

        /// <summary>在 Text/TMP 物体上确保有 LocalizedText 并绑定 key。</summary>
        public static LocalizedText EnsureOn(Component textHost, string locKey, LocalizationTableType tableType = LocalizationTableType.UI)
        {
            if (textHost == null || string.IsNullOrEmpty(locKey))
            {
                return null;
            }

            var loc = textHost.GetComponent<LocalizedText>();
            if (loc == null)
            {
                loc = textHost.gameObject.AddComponent<LocalizedText>();
            }

            loc.Configure(tableType, locKey, LocalizedBindMode.Key);
            return loc;
        }

        void Awake()
        {
            CacheTargets();
        }

        void OnEnable()
        {
            CacheTargets();
            if (Application.isPlaying)
            {
                LocalizationService.LocaleChanged += OnLocaleChanged;
                if (bindOnEnable)
                {
                    Apply();
                }
            }
            else if (previewInEditMode)
            {
                ApplyEditPreview();
            }
        }

        void OnDisable()
        {
            if (Application.isPlaying)
            {
                LocalizationService.LocaleChanged -= OnLocaleChanged;
            }
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (!isActiveAndEnabled || Application.isPlaying || !previewInEditMode)
            {
                return;
            }

            CacheTargets();
            ApplyEditPreview();
        }
#endif

        void OnLocaleChanged(GameLocale _)
        {
            Apply();
        }

        public void Apply()
        {
            CacheTargets();
            LocalizationService.EnsureInitialized();
            GameLocale? loc = overrideLocale ? localeOverride : (GameLocale?)null;
            string value = LocalizationService.Resolve(table, key, directText, bindMode, loc);
            SetText(value);

            // 反查成功时回写 key，后续切语言不再依赖直写
            if (Application.isPlaying &&
                string.IsNullOrEmpty(key) &&
                !string.IsNullOrEmpty(directText) &&
                (bindMode == LocalizedBindMode.Auto || bindMode == LocalizedBindMode.DirectToKey) &&
                LocalizationService.TryFindKey(table, directText, out string found))
            {
                key = found;
            }
        }

        void ApplyEditPreview()
        {
            // 编辑器预览：优先显示直写，否则用表内简中
            if (!string.IsNullOrEmpty(directText))
            {
                SetText(directText);
                return;
            }

            if (!string.IsNullOrEmpty(key))
            {
                LocalizationService.EnsureInitialized();
                SetText(LocalizationService.Get(table, key, GameLocale.ZhHans));
            }
        }

        void CacheTargets()
        {
            if (_cached)
            {
                return;
            }

            _tmpText = GetComponent<TMP_Text>();
            _uiText = GetComponent<Text>();
            _cached = true;
        }

        void SetText(string value)
        {
            if (_tmpText != null)
            {
                _tmpText.text = value;
            }

            if (_uiText != null)
            {
                _uiText.text = value;
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Apply Localization Now")]
        void ContextApply()
        {
            _cached = false;
            CacheTargets();
            if (Application.isPlaying)
            {
                Apply();
            }
            else
            {
                ApplyEditPreview();
            }
        }
#endif
    }
}
