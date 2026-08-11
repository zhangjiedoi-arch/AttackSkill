# -*- coding: utf-8 -*-
"""Standalone helper: Localization.xlsx -> Resources JSON (same shape as Unity menu)."""
import json
import zipfile
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
XLSX = ROOT / "Assets/LocalizationSource/Localization.xlsx"
OUT = ROOT / "Assets/Resources/Localization/Json"
NS_R = "{http://schemas.openxmlformats.org/officeDocument/2006/relationships}id"
MAIN = "{http://schemas.openxmlformats.org/spreadsheetml/2006/main}"


def col_index(ref: str) -> int:
    i = 0
    col = 0
    while i < len(ref) and ref[i].isalpha():
        col = col * 26 + (ord(ref[i].upper()) - 64)
        i += 1
    return max(0, col - 1)


def find_col(header, names):
    names = {n.lower() for n in names}
    for i, h in enumerate(header):
        if (h or "").strip().lower() in names:
            return i
    return -1


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(XLSX) as z:
        ss = []
        if "xl/sharedStrings.xml" in z.namelist():
            root = ET.fromstring(z.read("xl/sharedStrings.xml"))
            for si in root.findall(f".//{MAIN}si"):
                ss.append("".join((t.text or "") for t in si.findall(f".//{MAIN}t")))

        wb = ET.fromstring(z.read("xl/workbook.xml"))
        rels = ET.fromstring(z.read("xl/_rels/workbook.xml.rels"))
        id2t = {el.get("Id"): el.get("Target") for el in rels}
        sheets = []
        for s in wb.findall(f".//{MAIN}sheet"):
            sheets.append((s.get("name"), id2t[s.get(NS_R)]))

        def read_sheet(target: str):
            target = "xl/" + target.lstrip("/")
            if target.startswith("xl/xl/"):
                target = target[3:]
            root = ET.fromstring(z.read(target))
            rows = []
            for row in root.findall(f".//{MAIN}row"):
                sparse = {}
                maxc = 0
                for c in row.findall(f"{MAIN}c"):
                    ref = c.get("r")
                    t = c.get("t")
                    v = c.find(f"{MAIN}v")
                    val = v.text if v is not None else ""
                    if t == "s" and val.isdigit():
                        val = ss[int(val)]
                    ci = col_index(ref)
                    sparse[ci] = val
                    maxc = max(maxc, ci)
                rows.append([sparse.get(i, "") for i in range(maxc + 1)])
            return rows

        type_map = {"common": "Common", "ui": "UI", "story": "Story"}
        bundle = {"version": 1, "tables": []}
        for name, target in sheets:
            key = (name or "").lower()
            if key not in type_map:
                print("skip sheet", name)
                continue
            rows = read_sheet(target)
            if not rows:
                continue
            header = rows[0]
            kc = find_col(header, ["key"])
            zc = find_col(header, ["zhhans", "zh", "zh-cn", "zh_cn", "cn"])
            ec = find_col(header, ["en", "english"])
            jc = find_col(header, ["ja", "jp", "japanese"])
            entries = []
            seen = set()
            for row in rows[1:]:
                k = (row[kc] if kc >= 0 and kc < len(row) else "").strip()
                if not k or k.startswith("#"):
                    continue
                if k in seen:
                    continue
                seen.add(k)

                def g(c):
                    return row[c] if c >= 0 and c < len(row) else ""

                entries.append({"key": k, "zhHans": g(zc), "en": g(ec), "ja": g(jc)})

            table = {"tableType": type_map[key], "entries": entries}
            path = OUT / f"{table['tableType']}.json"
            path.write_text(json.dumps(table, ensure_ascii=False, indent=2), encoding="utf-8")
            bundle["tables"].append(table)
            print(f"{name}: {len(entries)} -> {path}")

        bundle_path = OUT / "LocalizationBundle.json"
        bundle_path.write_text(json.dumps(bundle, ensure_ascii=False, indent=2), encoding="utf-8")
        print("bundle ->", bundle_path)


if __name__ == "__main__":
    main()
