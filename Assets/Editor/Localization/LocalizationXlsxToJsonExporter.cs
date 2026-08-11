using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AttackSkill.Localization;
using UnityEditor;
using UnityEngine;

namespace AttackSkill.Editor.Localization
{
    /// <summary>
    /// Localization.xlsx → JSON 导表。
    /// 运行时唯一源：Resources/Localization/Json/LocalizationBundle.json
    /// 分表 {Common,UI,Story}.json 仅供 diff/审阅，不会被运行时加载（Bundle 存在时）。
    /// </summary>
    public static class LocalizationXlsxToJsonExporter
    {
        public const string DefaultXlsxAssetPath = "Assets/LocalizationSource/Localization.xlsx";
        public const string JsonOutputRoot = "Assets/Resources/Localization/Json";
        public const string BundleFileName = "LocalizationBundle.json";

        [MenuItem("工具/多语言/从 Excel 导出 JSON", false, 44)]
        public static void ExportMenu()
        {
            string xlsx = DefaultXlsxAssetPath;
            if (!File.Exists(ToAbsolute(xlsx)))
            {
                xlsx = EditorUtility.OpenFilePanel("选择 Localization.xlsx", Application.dataPath, "xlsx");
                if (string.IsNullOrEmpty(xlsx))
                {
                    return;
                }
            }
            else
            {
                xlsx = ToAbsolute(xlsx);
            }

            if (!Export(xlsx, out string message))
            {
                EditorUtility.DisplayDialog("导表失败", message, "OK");
                return;
            }

            EditorUtility.DisplayDialog("导表完成", message, "OK");
        }

        [MenuItem("工具/多语言/校验 Bundle（空翻译 / key 规范）", false, 45)]
        public static void ValidateBundleMenu()
        {
            string path = $"{JsonOutputRoot}/{BundleFileName}";
            string abs = ToAbsolute(path);
            if (!File.Exists(abs))
            {
                EditorUtility.DisplayDialog("校验失败", $"未找到 {path}", "OK");
                return;
            }

            var bundle = JsonUtility.FromJson<LocalizationBundleJsonData>(File.ReadAllText(abs, Encoding.UTF8));
            int emptyJa = 0;
            int emptyEn = 0;
            int badKey = 0;
            var badKeySamples = new List<string>();
            int total = 0;

            if (bundle?.tables != null)
            {
                for (int t = 0; t < bundle.tables.Length; t++)
                {
                    var table = bundle.tables[t];
                    if (table?.entries == null)
                    {
                        continue;
                    }

                    for (int i = 0; i < table.entries.Length; i++)
                    {
                        var e = table.entries[i];
                        if (e == null || string.IsNullOrEmpty(e.key))
                        {
                            continue;
                        }

                        total++;
                        if (string.IsNullOrWhiteSpace(e.en))
                        {
                            emptyEn++;
                        }

                        if (string.IsNullOrWhiteSpace(e.ja))
                        {
                            emptyJa++;
                        }

                        if (!IsValidKey(e.key))
                        {
                            badKey++;
                            if (badKeySamples.Count < 8)
                            {
                                badKeySamples.Add($"{table.tableType}/{e.key}");
                            }
                        }
                    }
                }
            }

            string samples = badKeySamples.Count > 0
                ? "\n坏 key 样例：\n- " + string.Join("\n- ", badKeySamples)
                : string.Empty;

            string msg =
                $"Bundle：{path}\n" +
                $"条目：{total}\n" +
                $"空 en：{emptyEn}\n" +
                $"空 ja：{emptyJa}\n" +
                $"不合规 key（须小写+数字+下划线）：{badKey}" +
                samples +
                "\n\n规范示例：tools_network_test\n" +
                "运行时仅加载 Bundle；ActiveLocales=简中/英（F8/设置）。";

            Debug.Log("[Localization] " + msg.Replace("\n", " | "));
            EditorUtility.DisplayDialog("语言表校验", msg, "OK");
        }

        /// <summary>key 规范：小写字母/数字/下划线，如 tools_network_test。</summary>
        public static bool IsValidKey(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            for (int i = 0; i < key.Length; i++)
            {
                char c = key[i];
                bool ok = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_';
                if (!ok)
                {
                    return false;
                }
            }

            return true;
        }

        public static bool Export(string absoluteXlsxPath, out string message)
        {
            var sheets = LocalizationXlsxReader.ReadWorkbook(absoluteXlsxPath, out List<string> errors);
            if (sheets.Count == 0)
            {
                message = "未读到任何 Sheet。\n" + string.Join("\n", errors);
                return false;
            }

            EnsureJsonFolder();

            var bundle = new LocalizationBundleJsonData
            {
                version = 1,
                tables = new LocalizationTableJsonData[0]
            };
            var tableList = new List<LocalizationTableJsonData>();
            int entryCount = 0;
            int filledJa = 0;
            int badKeys = 0;

            foreach (var kv in sheets)
            {
                if (!TryMapTableType(kv.Key, out LocalizationTableType type))
                {
                    Debug.LogWarning($"[Localization] 跳过未识别 Sheet：{kv.Key}（请用 Common/UI/Story）");
                    continue;
                }

                var entries = kv.Value ?? new List<LocalizationEntry>();
                for (int i = 0; i < entries.Count; i++)
                {
                    var e = entries[i];
                    if (e == null)
                    {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(e.key) && e.key.IndexOf('.') >= 0)
                    {
                        e.key = e.key.Replace('.', '_');
                    }

                    if (!IsValidKey(e.key))
                    {
                        badKeys++;
                        Debug.LogWarning($"[Localization] 不合规 key：{type}/{e.key}");
                    }

                    // 空日文用英文（再缺用简中）占位，避免切 Ja 时大片中文
                    if (string.IsNullOrWhiteSpace(e.ja))
                    {
                        e.ja = !string.IsNullOrWhiteSpace(e.en) ? e.en : (e.zhHans ?? string.Empty);
                        filledJa++;
                    }
                }

                var tableData = new LocalizationTableJsonData
                {
                    tableType = type.ToString(),
                    entries = entries.ToArray()
                };
                tableList.Add(tableData);
                entryCount += entries.Count;

                // 分表仅供审阅/diff
                string perTablePath = $"{JsonOutputRoot}/{type}.json";
                File.WriteAllText(ToAbsolute(perTablePath), ToPrettyJson(tableData), new UTF8Encoding(false));
            }

            bundle.tables = tableList.ToArray();
            string bundlePath = $"{JsonOutputRoot}/{BundleFileName}";
            File.WriteAllText(ToAbsolute(bundlePath), ToPrettyJson(bundle), new UTF8Encoding(false));

            AssetDatabase.Refresh();
            LocalizationCatalog.ClearCache();
            LocalizationService.Rebuild();

            for (int i = 0; i < errors.Count; i++)
            {
                Debug.LogWarning("[Localization] " + errors[i]);
            }

            message =
                $"已导出（运行时源 = {BundleFileName}）\n" +
                $"源：{absoluteXlsxPath}\n" +
                $"表：{tableList.Count}  条目：{entryCount}\n" +
                $"空 ja 已占位：{filledJa}\n" +
                $"不合规 key：{badKeys}\n" +
                $"分表 JSON 仅审阅用\n" +
                (errors.Count > 0 ? $"警告：{errors.Count} 条（见 Console）" : "无警告");
            Debug.Log("[Localization] " + message.Replace('\n', ' '));
            return tableList.Count > 0;
        }

        static bool TryMapTableType(string sheetName, out LocalizationTableType type)
        {
            switch ((sheetName ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "common":
                    type = LocalizationTableType.Common;
                    return true;
                case "ui":
                    type = LocalizationTableType.UI;
                    return true;
                case "story":
                    type = LocalizationTableType.Story;
                    return true;
                default:
                    type = default;
                    return false;
            }
        }

        static void EnsureJsonFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }

            if (!AssetDatabase.IsValidFolder("Assets/Resources/Localization"))
            {
                AssetDatabase.CreateFolder("Assets/Resources", "Localization");
            }

            if (!AssetDatabase.IsValidFolder(JsonOutputRoot))
            {
                AssetDatabase.CreateFolder("Assets/Resources/Localization", "Json");
            }
        }

        static string ToAbsolute(string assetOrAbs)
        {
            if (Path.IsPathRooted(assetOrAbs))
            {
                return assetOrAbs;
            }

            string project = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.GetFullPath(Path.Combine(project, assetOrAbs.Replace('/', Path.DirectorySeparatorChar)));
        }

        /// <summary>JsonUtility 无缩进；导表输出可读 JSON。</summary>
        static string ToPrettyJson(object obj)
        {
            string compact = JsonUtility.ToJson(obj);
            return IndentJson(compact);
        }

        static string IndentJson(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return json;
            }

            var sb = new StringBuilder(json.Length * 2);
            int indent = 0;
            bool inString = false;
            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '\"' && (i == 0 || json[i - 1] != '\\'))
                {
                    inString = !inString;
                    sb.Append(c);
                    continue;
                }

                if (inString)
                {
                    sb.Append(c);
                    continue;
                }

                switch (c)
                {
                    case '{':
                    case '[':
                        sb.Append(c);
                        sb.Append('\n');
                        indent++;
                        sb.Append(' ', indent * 2);
                        break;
                    case '}':
                    case ']':
                        sb.Append('\n');
                        indent = Math.Max(0, indent - 1);
                        sb.Append(' ', indent * 2);
                        sb.Append(c);
                        break;
                    case ',':
                        sb.Append(c);
                        sb.Append('\n');
                        sb.Append(' ', indent * 2);
                        break;
                    case ':':
                        sb.Append(": ");
                        break;
                    default:
                        sb.Append(c);
                        break;
                }
            }

            return sb.ToString();
        }
    }
}
