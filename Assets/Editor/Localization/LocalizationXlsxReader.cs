using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using AttackSkill.Localization;

namespace AttackSkill.Editor.Localization
{
    /// <summary>轻量读取 .xlsx（OpenXML ZIP），无第三方库。</summary>
    public static class LocalizationXlsxReader
    {
        static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        static readonly XNamespace RelPkg = "http://schemas.openxmlformats.org/package/2006/relationships";
        static readonly XNamespace RelDoc = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

        public static Dictionary<string, List<LocalizationEntry>> ReadWorkbook(string absolutePath, out List<string> errors)
        {
            errors = new List<string>();
            var result = new Dictionary<string, List<LocalizationEntry>>(StringComparer.OrdinalIgnoreCase);

            if (!File.Exists(absolutePath))
            {
                errors.Add("文件不存在：" + absolutePath);
                return result;
            }

            using (var stream = File.Open(absolutePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                var shared = ReadSharedStrings(zip);
                var sheetMap = ReadSheetTargets(zip, errors);
                foreach (var kv in sheetMap)
                {
                    string sheetName = kv.Key;
                    string entryPath = kv.Value;
                    var entry = zip.GetEntry(entryPath);
                    if (entry == null)
                    {
                        errors.Add($"找不到 Sheet 文件：{sheetName} → {entryPath}");
                        continue;
                    }

                    using (var sheetStream = entry.Open())
                    {
                        var doc = XDocument.Load(sheetStream);
                        var rows = ParseSheetRows(doc, shared);
                        result[sheetName] = RowsToEntries(sheetName, rows, errors);
                    }
                }
            }

            return result;
        }

        static Dictionary<string, string> ReadSheetTargets(ZipArchive zip, List<string> errors)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var wbEntry = zip.GetEntry("xl/workbook.xml");
            var relsEntry = zip.GetEntry("xl/_rels/workbook.xml.rels");
            if (wbEntry == null || relsEntry == null)
            {
                errors.Add("xlsx 缺少 workbook.xml 或 rels");
                return map;
            }

            XDocument wb;
            XDocument rels;
            using (var s = wbEntry.Open())
            {
                wb = XDocument.Load(s);
            }

            using (var s = relsEntry.Open())
            {
                rels = XDocument.Load(s);
            }

            var idToTarget = rels.Root?
                .Elements(RelPkg + "Relationship")
                .ToDictionary(e => (string)e.Attribute("Id"), e => (string)e.Attribute("Target"))
                ?? new Dictionary<string, string>();

            foreach (var sheet in wb.Root?.Element(Main + "sheets")?.Elements(Main + "sheet") ?? Enumerable.Empty<XElement>())
            {
                string name = (string)sheet.Attribute("name");
                string rid = (string)sheet.Attribute(RelDoc + "id");
                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(rid) || !idToTarget.TryGetValue(rid, out string target))
                {
                    continue;
                }

                target = target.Replace('\\', '/');
                if (target.StartsWith("/"))
                {
                    target = target.TrimStart('/');
                }

                if (!target.StartsWith("xl/", StringComparison.OrdinalIgnoreCase))
                {
                    target = "xl/" + target;
                }

                map[name] = target;
            }

            return map;
        }

        static List<string> ReadSharedStrings(ZipArchive zip)
        {
            var list = new List<string>();
            var entry = zip.GetEntry("xl/sharedStrings.xml");
            if (entry == null)
            {
                return list;
            }

            using (var s = entry.Open())
            {
                var doc = XDocument.Load(s);
                foreach (var si in doc.Root?.Elements(Main + "si") ?? Enumerable.Empty<XElement>())
                {
                    var sb = new StringBuilder();
                    foreach (var t in si.Descendants(Main + "t"))
                    {
                        sb.Append(t.Value);
                    }

                    list.Add(sb.ToString());
                }
            }

            return list;
        }

        static List<List<string>> ParseSheetRows(XDocument doc, List<string> shared)
        {
            var rows = new List<List<string>>();
            var sheetData = doc.Root?.Element(Main + "sheetData");
            if (sheetData == null)
            {
                return rows;
            }

            foreach (var row in sheetData.Elements(Main + "row"))
            {
                var cells = row.Elements(Main + "c").ToList();
                if (cells.Count == 0)
                {
                    continue;
                }

                int maxCol = 0;
                var sparse = new Dictionary<int, string>();
                foreach (var c in cells)
                {
                    string r = (string)c.Attribute("r");
                    int col = ColumnIndex(r);
                    sparse[col] = ReadCellValue(c, shared);
                    if (col > maxCol)
                    {
                        maxCol = col;
                    }
                }

                var line = new List<string>(maxCol + 1);
                for (int i = 0; i <= maxCol; i++)
                {
                    line.Add(sparse.TryGetValue(i, out string v) ? v : string.Empty);
                }

                rows.Add(line);
            }

            return rows;
        }

        static string ReadCellValue(XElement cell, List<string> shared)
        {
            string t = (string)cell.Attribute("t");
            if (t == "inlineStr")
            {
                var sb = new StringBuilder();
                foreach (var node in cell.Descendants(Main + "t"))
                {
                    sb.Append(node.Value);
                }

                return sb.ToString();
            }

            var v = cell.Element(Main + "v");
            if (v == null)
            {
                return string.Empty;
            }

            string raw = v.Value ?? string.Empty;
            if (t == "s" && int.TryParse(raw, out int idx) && idx >= 0 && idx < shared.Count)
            {
                return shared[idx];
            }

            return raw;
        }

        static List<LocalizationEntry> RowsToEntries(string sheetName, List<List<string>> rows, List<string> errors)
        {
            var list = new List<LocalizationEntry>();
            if (rows.Count == 0)
            {
                return list;
            }

            var header = rows[0];
            int keyCol = FindCol(header, "key");
            int zhCol = FindCol(header, "zhHans", "zh", "zh-cn", "zh_cn", "cn");
            int enCol = FindCol(header, "en", "english");
            int jaCol = FindCol(header, "ja", "jp", "japanese");

            if (keyCol < 0)
            {
                errors.Add($"[{sheetName}] 缺少 key 列");
                return list;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 1; i < rows.Count; i++)
            {
                var row = rows[i];
                string key = Get(row, keyCol).Trim();
                if (string.IsNullOrEmpty(key) || key.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!seen.Add(key))
                {
                    errors.Add($"[{sheetName}] 重复 key：{key}（行 {i + 1}）");
                    continue;
                }

                list.Add(new LocalizationEntry
                {
                    key = key,
                    zhHans = Get(row, zhCol),
                    en = Get(row, enCol),
                    ja = Get(row, jaCol)
                });
            }

            return list;
        }

        static int FindCol(List<string> header, params string[] names)
        {
            for (int i = 0; i < header.Count; i++)
            {
                string h = (header[i] ?? string.Empty).Trim();
                for (int n = 0; n < names.Length; n++)
                {
                    if (h.Equals(names[n], StringComparison.OrdinalIgnoreCase))
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        static string Get(List<string> row, int col)
        {
            if (col < 0 || col >= row.Count)
            {
                return string.Empty;
            }

            return row[col] ?? string.Empty;
        }

        static int ColumnIndex(string cellRef)
        {
            if (string.IsNullOrEmpty(cellRef))
            {
                return 0;
            }

            int i = 0;
            int col = 0;
            while (i < cellRef.Length && char.IsLetter(cellRef[i]))
            {
                col = col * 26 + (char.ToUpperInvariant(cellRef[i]) - 'A' + 1);
                i++;
            }

            return Math.Max(0, col - 1);
        }
    }
}
