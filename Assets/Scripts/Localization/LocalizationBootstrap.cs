using UnityEngine;
using AttackSkill.Core;

namespace AttackSkill.Localization
{
    /// <summary>
    /// 进游戏加载语言表；F8 循环切语言并通知所有 LocalizedText。
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class LocalizationBootstrap : MonoBehaviour
    {
        public static LocalizationBootstrap Instance { get; private set; }

        [SerializeField] bool dontDestroyOnLoad = true;
        [SerializeField] bool loadOnAwake = true;
        [SerializeField] KeyCode cycleLocaleKey = KeyCode.F8;
        [SerializeField] bool drawHud = false;

        void Awake()
        {
            if (!SceneSingleton.ShouldKeep(this, Instance))
            {
                return;
            }

            Instance = this;
            SceneSingleton.ApplyDontDestroyOnLoad(this, dontDestroyOnLoad);

            if (loadOnAwake)
            {
                LocalizationService.Rebuild();
            }
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        void Update()
        {
            if (GameInput.GetKeyDown(cycleLocaleKey))
            {
                LocalizationService.CycleLocale();
            }
        }

        void OnGUI()
        {
            if (!drawHud || !LocalizationService.IsInitialized)
            {
                return;
            }

            GUI.Label(
                new Rect(12, 84, 900, 22),
                LocalizationService.Format(
                    LocalizationTableType.Common,
                    "locale_hud",
                    LocalizationService.CurrentLocale,
                    LocalizationService.LocaleDisplayName(LocalizationService.CurrentLocale),
                    cycleLocaleKey));
        }
    }
}
