using System;
using System.Collections.Generic;
using UnityEngine;

namespace AttackSkill.Localization
{
    /// <summary>
    /// 启动/切语言时整表载入 Dictionary；运行时只 Get，不每帧查表。
    /// 运行时文案唯一来源：Resources/Localization/Json/LocalizationBundle。
    /// </summary>
    public static class LocalizationService
    {
        const string PrefsKey = "AttackSkill.Locale";
        const string BundleResourcesPath = "Localization/Json/LocalizationBundle";

        /// <summary>当前产品启用的语言循环（与设置页下拉一致；Ja 表有占位译但仍不进 Cycle）。</summary>
        public static readonly GameLocale[] ActiveLocales =
        {
            GameLocale.ZhHans,
            GameLocale.En
        };

        static bool _initialized;
        static GameLocale _authoringLocale = GameLocale.ZhHans;
        static readonly Dictionary<string, LocalizationEntry> Entries = new Dictionary<string, LocalizationEntry>(256);
        static readonly Dictionary<string, string> ReverseLookup = new Dictionary<string, string>(256);

        public static bool IsInitialized => _initialized;
        public static GameLocale CurrentLocale { get; private set; } = GameLocale.ZhHans;
        public static GameLocale AuthoringLocale => _authoringLocale;

        public static event Action<GameLocale> LocaleChanged;

        public static void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            Rebuild();
        }

        public static void Rebuild()
        {
            Entries.Clear();
            ReverseLookup.Clear();

            var catalog = LocalizationCatalog.Get();
            GameLocale bootLocale = GameLocale.ZhHans;
            if (catalog != null)
            {
                _authoringLocale = catalog.authoringLocale;
                bootLocale = catalog.defaultLocale;
            }

            // 运行时唯一源：Bundle。缺失时才回退 Catalog SO + 分表 JSON。
            if (!TryRegisterBundle())
            {
                Debug.LogWarning(
                    "[Localization] 未找到 LocalizationBundle，回退 Catalog/分表 JSON（请用「从 Excel 导出 JSON」）。");
                RegisterCatalogTables(catalog);
                RegisterJsonTableAsset("Localization/Json/Common");
                RegisterJsonTableAsset("Localization/Json/UI");
                RegisterJsonTableAsset("Localization/Json/Story");
            }

            RegisterBuiltinSeed();

            if (PlayerPrefs.HasKey(PrefsKey))
            {
                CurrentLocale = (GameLocale)PlayerPrefs.GetInt(PrefsKey, (int)bootLocale);
            }
            else
            {
                CurrentLocale = bootLocale;
            }

            if (!IsActiveLocale(CurrentLocale))
            {
                CurrentLocale = ActiveLocales.Length > 0 ? ActiveLocales[0] : GameLocale.ZhHans;
            }

            _initialized = true;
        }

        static void RegisterCatalogTables(LocalizationCatalog catalog)
        {
            if (catalog?.tables == null)
            {
                return;
            }

            for (int i = 0; i < catalog.tables.Length; i++)
            {
                RegisterTable(catalog.tables[i]);
            }
        }

        static void RegisterTable(LocalizationTable table)
        {
            if (table == null || table.entries == null)
            {
                return;
            }

            for (int i = 0; i < table.entries.Count; i++)
            {
                RegisterEntry(table.tableType, table.entries[i]);
            }
        }

        static bool TryRegisterBundle()
        {
            var bundle = Resources.Load<TextAsset>(BundleResourcesPath);
            if (bundle == null || string.IsNullOrEmpty(bundle.text))
            {
                return false;
            }

            try
            {
                var data = JsonUtility.FromJson<LocalizationBundleJsonData>(bundle.text);
                if (data?.tables == null || data.tables.Length == 0)
                {
                    return false;
                }

                for (int i = 0; i < data.tables.Length; i++)
                {
                    RegisterJsonTable(data.tables[i]);
                }

                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Localization] Bundle JSON 解析失败：{e.Message}");
                return false;
            }
        }

        static void RegisterJsonTableAsset(string resourcesPath)
        {
            var asset = Resources.Load<TextAsset>(resourcesPath);
            if (asset == null || string.IsNullOrEmpty(asset.text))
            {
                return;
            }

            try
            {
                RegisterJsonTable(JsonUtility.FromJson<LocalizationTableJsonData>(asset.text));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Localization] JSON 解析失败 {resourcesPath}：{e.Message}");
            }
        }

        static void RegisterJsonTable(LocalizationTableJsonData data)
        {
            if (data?.entries == null || string.IsNullOrEmpty(data.tableType))
            {
                return;
            }

            if (!Enum.TryParse(data.tableType, true, out LocalizationTableType type))
            {
                Debug.LogWarning($"[Localization] 未知 tableType：{data.tableType}");
                return;
            }

            for (int i = 0; i < data.entries.Length; i++)
            {
                RegisterEntry(type, data.entries[i], overwrite: true);
            }
        }

        static void RegisterEntry(LocalizationTableType tableType, LocalizationEntry entry, bool overwrite = true)
        {
            if (entry == null || string.IsNullOrEmpty(entry.key))
            {
                return;
            }

            string mapKey = MakeKey(tableType, entry.key);
            if (!overwrite && Entries.ContainsKey(mapKey))
            {
                return;
            }

            Entries[mapKey] = entry;

            string authoring = entry.Get(_authoringLocale);
            if (!string.IsNullOrEmpty(authoring))
            {
                string reverseKey = MakeReverseKey(tableType, authoring);
                if (!ReverseLookup.ContainsKey(reverseKey))
                {
                    ReverseLookup[reverseKey] = entry.key;
                }
            }
        }

        static void RegisterBuiltinSeed()
        {
            void Add(LocalizationTableType type, string key, string zh, string en, string ja)
            {
                RegisterEntry(type, new LocalizationEntry
                {
                    key = key,
                    zhHans = zh,
                    en = en,
                    ja = ja
                }, overwrite: false);
            }

            // key 规范：小写 + 下划线（如 tools_network_test）
            Add(LocalizationTableType.UI, "pause_title", "已暂停", "Paused", "一時停止");
            Add(LocalizationTableType.UI, "pause_hint", "再按 {0} 继续", "Press {0} to resume", "{0} でもう一度再開");
            Add(LocalizationTableType.UI, "pause_reset", "返回海滩", "Return to Beach", "海岸に戻る");
            Add(LocalizationTableType.UI, "locale_label", "语言", "Language", "言語");
            Add(LocalizationTableType.UI, "locale_zh", "简体中文", "Chinese", "中国語");
            Add(LocalizationTableType.UI, "locale_en", "英语", "English", "英語");
            Add(LocalizationTableType.UI, "locale_ja", "日语", "Japanese", "日本語");
            Add(LocalizationTableType.UI, "echo_obtain_wip",
                "获取功能尚未实现",
                "Obtain feature not yet implemented",
                "取得機能は未実装です");
            Add(LocalizationTableType.UI, "task_camp",
                "击败海边的敌人",
                "Defeat the enemies by the sea",
                "海岸の敵を倒せ");
            Add(LocalizationTableType.UI, "task_abyss",
                "在深渊的怒火中活下去",
                "Survive the wrath of the Abyss",
                "深淵の怒りを生き抜け");
            Add(LocalizationTableType.UI, "world_map_beach", "海滩", "Beach", "ビーチ");
            Add(LocalizationTableType.UI, "world_map_rouge", "肉鸽平面", "Rogue Plane", "ローグ平面");
            Add(LocalizationTableType.UI, "world_map_explore", "探索度 {0}", "Exploration {0}", "探索度 {0}");
            Add(LocalizationTableType.UI, "world_map_switch", "切换地图", "Switch Map", "マップ切替");
            Add(LocalizationTableType.UI, "world_map_settings", "地图设置", "Map Settings", "マップ設定");
            Add(LocalizationTableType.UI, "world_map_zoom_in", "+", "+", "+");
            Add(LocalizationTableType.UI, "world_map_zoom_out", "-", "-", "-");
            Add(LocalizationTableType.UI, "world_map_region_qiqiu", "七丘", "Septimont", "セブンヒルズ");
            Add(LocalizationTableType.UI, "world_map_region_black_shores", "黑海岸", "Black Shores", "黒海岸");
            Add(LocalizationTableType.UI, "world_map_marker_tip", "标记", "Markers", "マーカー");
            Add(LocalizationTableType.UI, "world_map_marker_custom", "自定义标记", "Custom Markers", "カスタムマーカー");
            Add(LocalizationTableType.UI, "world_map_marker_finished", "已完成挑战标记", "Completed Challenges", "完了した挑戦マーカー");
            Add(LocalizationTableType.UI, "map_marker_chest_desc", "散落在野外的补给箱，打开可获得物资。", "A supply chest. Open it for materials.", "野外の補給箱。開けると物資が入手できる。");
            Add(LocalizationTableType.UI, "map_marker_teleporter_desc", "解锁后可在大地图传送至此。", "Unlock to teleport here from the map.", "解除後、マップからここに転送できる。");
            Add(LocalizationTableType.UI, "map_marker_grocery_store_desc", "采购日常用品的杂货铺。", "A shop for everyday supplies.", "日用品を扱う雑貨店。");
            Add(LocalizationTableType.UI, "map_marker_canteen_desc", "可以休息、补给的食堂。", "A canteen to rest and resupply.", "休憩と補給ができる食堂。");
            Add(LocalizationTableType.UI, "map_marker_observation_tower_desc", "登塔可揭示附近区域。", "Climb to reveal the nearby area.", "登ると周辺エリアが解放される。");
            Add(LocalizationTableType.UI, "world_map_poi_close", "关闭", "Close", "閉じる");
            Add(LocalizationTableType.UI, "map_poi_beach_chest_01", "半埋在沙滩里的木箱，潮水拍打时会发出轻响。", "A crate half-buried in the sand, clicking as the tide hits it.", "砂に半分埋まった木箱。潮が当たると軽く鳴る。");
            Add(LocalizationTableType.UI, "map_poi_beach_grocery_01", "海岸边的小铺，货架上摆着晒干的海货。", "A shore stall stacked with dried seafood.", "海岸の小さな店。干した海産物が並ぶ。");
            Add(LocalizationTableType.UI, "map_poi_beach_canteen_01", "渔民歇脚的食堂，炉子整天温着汤。", "A fishermen's canteen. The pot stays warm all day.", "漁師が休む食堂。鍋は一日中温かい。");
            Add(LocalizationTableType.UI, "map_poi_beach_portal_01", "海滩锚点。解锁后可从大地图回到此处。", "Beach anchor. Unlock to return here from the map.", "ビーチの転送点。解除後マップから戻れる。");
            Add(LocalizationTableType.UI, "map_poi_beach_tower_01", "已点亮的瞭望塔，附近海滩已收入视野。", "A lit watchtower. The nearby beach is already revealed.", "点灯済みの展望塔。近くの海岸は見えている。");
            Add(LocalizationTableType.UI, "map_poi_rouge_chest_01", "深渊边缘的铁箱，锁扣还带着余温。", "An iron chest at the abyss edge. The latch is still warm.", "深淵の縁の鉄箱。留め具はまだ温かい。");
            Add(LocalizationTableType.UI, "map_poi_rouge_grocery_01", "不知谁留下的补给摊，货物已经所剩无几。", "An abandoned stall with little stock left.", "誰かが残した補給屋。品物はもう少ない。");
            Add(LocalizationTableType.UI, "map_poi_rouge_canteen_01", "临时搭起的伙房，桌上还有没喝完的热水。", "A makeshift kitchen. A cup of hot water still sits on the table.", "仮設の炊事場。テーブルに湯が残っている。");
            Add(LocalizationTableType.UI, "map_poi_rouge_portal_01", "肉鸽平面锚点。解锁后可在大地图传送至此。", "Rogue-plane anchor. Unlock to teleport here from the map.", "ローグ平面の転送点。解除後マップから移動できる。");
            Add(LocalizationTableType.UI, "map_poi_rouge_tower_01", "俯瞰深渊入口的塔。登顶可看清刷怪区轮廓。", "A tower over the abyss gate. The spawn field is visible from the top.", "深淵の入口を見下ろす塔。頂上から出現域が見える。");
            Add(LocalizationTableType.Common, "ok", "确定", "OK", "OK");
            Add(LocalizationTableType.Common, "cancel", "取消", "Cancel", "キャンセル");
            Add(LocalizationTableType.Common, "confirm", "确认", "Confirm", "確認");
            Add(LocalizationTableType.Story, "sample_line",
                "风从庭院吹过。",
                "Wind passes through the courtyard.",
                "庭を風が通り抜ける。");
        }

        public static string Get(LocalizationTableType table, string key, GameLocale? locale = null)
        {
            EnsureInitialized();
            if (string.IsNullOrEmpty(key))
            {
                return string.Empty;
            }

            if (Entries.TryGetValue(MakeKey(table, key), out LocalizationEntry entry))
            {
                return entry.Get(locale ?? CurrentLocale);
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return $"#{table}/{key}";
#else
            return key;
#endif
        }

        public static string Format(LocalizationTableType table, string key, params object[] args)
        {
            string raw = Get(table, key);
            if (args == null || args.Length == 0)
            {
                return raw;
            }

            try
            {
                return string.Format(raw, args);
            }
            catch (FormatException)
            {
                return raw;
            }
        }

        public static bool TryFindKey(LocalizationTableType table, string directText, out string key)
        {
            EnsureInitialized();
            key = null;
            if (string.IsNullOrEmpty(directText))
            {
                return false;
            }

            return ReverseLookup.TryGetValue(MakeReverseKey(table, directText), out key);
        }

        public static string Resolve(
            LocalizationTableType table,
            string key,
            string directText,
            LocalizedBindMode mode,
            GameLocale? locale = null)
        {
            EnsureInitialized();
            GameLocale loc = locale ?? CurrentLocale;

            switch (mode)
            {
                case LocalizedBindMode.Key:
                    return Get(table, key, loc);

                case LocalizedBindMode.DirectOnly:
                    return directText ?? string.Empty;

                case LocalizedBindMode.DirectToKey:
                    if (TryFindKey(table, directText, out string found) ||
                        (!string.IsNullOrEmpty(key) && Entries.ContainsKey(MakeKey(table, key))))
                    {
                        string useKey = !string.IsNullOrEmpty(found) ? found : key;
                        return Get(table, useKey, loc);
                    }

                    return directText ?? string.Empty;

                default: // Auto
                    if (!string.IsNullOrEmpty(key))
                    {
                        return Get(table, key, loc);
                    }

                    if (TryFindKey(table, directText, out string autoKey))
                    {
                        return Get(table, autoKey, loc);
                    }

                    return directText ?? string.Empty;
            }
        }

        public static void SetLocale(GameLocale locale)
        {
            EnsureInitialized();
            if (!IsActiveLocale(locale))
            {
                Debug.LogWarning($"[Localization] 语言 {locale} 未启用，忽略 SetLocale。");
                return;
            }

            if (CurrentLocale == locale)
            {
                return;
            }

            CurrentLocale = locale;
            PlayerPrefs.SetInt(PrefsKey, (int)locale);
            PlayerPrefs.Save();
            LocaleChanged?.Invoke(locale);
        }

        public static void CycleLocale()
        {
            EnsureInitialized();
            if (ActiveLocales == null || ActiveLocales.Length == 0)
            {
                return;
            }

            int idx = 0;
            for (int i = 0; i < ActiveLocales.Length; i++)
            {
                if (ActiveLocales[i] == CurrentLocale)
                {
                    idx = i;
                    break;
                }
            }

            int next = (idx + 1) % ActiveLocales.Length;
            SetLocale(ActiveLocales[next]);
        }

        public static bool IsActiveLocale(GameLocale locale)
        {
            if (ActiveLocales == null)
            {
                return false;
            }

            for (int i = 0; i < ActiveLocales.Length; i++)
            {
                if (ActiveLocales[i] == locale)
                {
                    return true;
                }
            }

            return false;
        }

        public static string LocaleDisplayName(GameLocale locale)
        {
            switch (locale)
            {
                case GameLocale.En:
                    return Get(LocalizationTableType.UI, "locale_en");
                case GameLocale.Ja:
                    return Get(LocalizationTableType.UI, "locale_ja");
                default:
                    return Get(LocalizationTableType.UI, "locale_zh");
            }
        }

        static string MakeKey(LocalizationTableType table, string key)
        {
            return ((int)table) + "|" + key;
        }

        static string MakeReverseKey(LocalizationTableType table, string text)
        {
            return ((int)table) + "|" + text;
        }
    }
}
