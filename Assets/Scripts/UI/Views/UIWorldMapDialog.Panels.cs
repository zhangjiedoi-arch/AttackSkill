using System.Collections.Generic;
using AttackSkill.CameraSystem;
using AttackSkill.Localization;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AttackSkill.UI
{
    public partial class UIWorldMapDialog
    {
        const float RegionHeadHeight = 80f;
        const float SubRowHeight = 60f;
        const float MapRowHeight = 80f;
        const float RowGap = 4f;

        static readonly Color SelectedName = new Color(1f, 0.835f, 0.29f, 1f);
        static readonly Color NormalName = Color.white;

        RectTransform _regionContent;
        RectTransform _mapContent;
        RectTransform _regionTemplate;
        RectTransform _subTemplate;
        RectTransform _mapTemplate;
        Scrollbar _barCustom;
        Scrollbar _barFinished;
        bool _selectBuilt;
        bool _suppressMapToggle;
        bool _suppressFilterBar;
        string _leafId;

        readonly List<RectTransform> _regionClones = new List<RectTransform>(8);
        readonly List<RectTransform> _mapClones = new List<RectTransform>(8);
        readonly Dictionary<string, bool> _expanded = new Dictionary<string, bool>(8);

        public static bool TryHandleEscape()
        {
            return _openInstance != null && _openInstance.TryCloseSubPanel();
        }

        bool TryCloseSubPanel()
        {
            if (IsMarkerPopupOpen())
            {
                HideMarkerPopup(restoreOrigin: true);
                return true;
            }

            if ((pal_MapSelect != null && pal_MapSelect.gameObject.activeSelf) ||
                (pal_MapSet != null && pal_MapSet.gameObject.activeSelf))
            {
                ShowChromePanel(pal_MainSet);
                return true;
            }

            return false;
        }

        bool IsMarkerPopupOpen()
        {
            return _palMarkerInfo != null && _palMarkerInfo.gameObject.activeSelf;
        }

        void ShowMarkerPopup(string uid)
        {
            EnsureMarkerPopup();
            MapMarkerCatalog.ResolveLabels(uid, MapMarkerCatalog.GetTypeId(uid), out string titleKey, out string descKey);
            if (_txtMarkerName != null)
            {
                _txtMarkerName.text = L(titleKey);
            }

            if (_txtMarkerDesc != null)
            {
                _txtMarkerDesc.text = L(descKey);
            }

            if (_palMarkerInfo != null)
            {
                _palMarkerInfo.gameObject.SetActive(true);
                _palMarkerInfo.SetAsLastSibling();
            }
        }

        void HideMarkerPopup(bool restoreOrigin)
        {
            bool wasOpen = IsMarkerPopupOpen();
            _focusedMarkerUid = null;
            if (_palMarkerInfo != null && _palMarkerInfo.gameObject.activeSelf)
            {
                _palMarkerInfo.gameObject.SetActive(false);
            }

            if (restoreOrigin && wasOpen)
            {
                RestoreOriginView();
            }
        }

        void EnsureMarkerPopup()
        {
            if (_palMarkerInfo != null)
            {
                return;
            }

            _palMarkerInfo = CreateChild("pal_MarkerInfo", transform);
            Stretch(_palMarkerInfo);
            var rootImg = _palMarkerInfo.gameObject.AddComponent<Image>();
            rootImg.color = new Color(0f, 0f, 0f, 0f);
            rootImg.raycastTarget = false;

            var card = CreateChild("palCard", _palMarkerInfo);
            card.anchorMin = card.anchorMax = new Vector2(1f, 0.5f);
            card.pivot = new Vector2(1f, 0.5f);
            card.anchoredPosition = new Vector2(-36f, 0f);
            card.sizeDelta = new Vector2(420f, 280f);
            var cardImg = card.gameObject.AddComponent<Image>();
            cardImg.color = new Color(0.1f, 0.12f, 0.16f, 0.94f);
            cardImg.raycastTarget = true;

            _txtMarkerName = CreateLabel(card, "txtName", 26, Vector2.zero, new Vector2(380f, 40f));
            _txtMarkerName.alignment = TextAnchor.MiddleLeft;
            var nameRt = _txtMarkerName.rectTransform;
            nameRt.anchorMin = nameRt.anchorMax = new Vector2(0.5f, 1f);
            nameRt.pivot = new Vector2(0.5f, 1f);
            nameRt.anchoredPosition = new Vector2(0f, -16f);
            DisableLocalizedOverwrite(_txtMarkerName);

            _txtMarkerDesc = CreateLabel(card, "txtDesc", 20, Vector2.zero, new Vector2(380f, 150f));
            _txtMarkerDesc.alignment = TextAnchor.UpperLeft;
            _txtMarkerDesc.horizontalOverflow = HorizontalWrapMode.Wrap;
            _txtMarkerDesc.verticalOverflow = VerticalWrapMode.Overflow;
            var descRt = _txtMarkerDesc.rectTransform;
            descRt.anchorMin = descRt.anchorMax = new Vector2(0.5f, 1f);
            descRt.pivot = new Vector2(0.5f, 1f);
            descRt.anchoredPosition = new Vector2(0f, -64f);
            DisableLocalizedOverwrite(_txtMarkerDesc);

            Button close = CreateHudButton(
                card,
                "btnClose",
                L("world_map_poi_close"),
                new Vector2(-16f, 16f),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f));
            BindClick(close, () => HideMarkerPopup(restoreOrigin: true));
            LocalizedText.EnsureOn(ButtonLabel(close), "world_map_poi_close");
            _palMarkerInfo.gameObject.SetActive(false);
        }

        void AfterChromeShown(RectTransform target)
        {
            if (target == pal_MapSelect)
            {
                RefreshMapSelect();
            }
            else if (target == pal_MapSet)
            {
                BindMapSet();
            }
        }

        void CachePanelTemplates()
        {
            if (_selectBuilt)
            {
                return;
            }

            if (pal_MapSelect != null)
            {
                var palRegion = FindChildComponent<RectTransform>(pal_MapSelect, "palRegion");
                var palMapList = FindChildComponent<RectTransform>(pal_MapSelect, "palMapList");
                _regionContent = palRegion != null
                    ? FindChildComponent<RectTransform>(palRegion, "Content")
                    : null;
                _mapContent = palMapList != null
                    ? FindChildComponent<RectTransform>(palMapList, "Content")
                    : null;
                _regionTemplate = _regionContent != null
                    ? FindChildComponent<RectTransform>(_regionContent, "itemRegion")
                    : null;
                if (_regionTemplate != null)
                {
                    _subTemplate = FindChildComponent<RectTransform>(_regionTemplate, "itemSub");
                    ParkTemplate(_regionTemplate, pal_MapSelect);
                }

                if (_subTemplate != null)
                {
                    ParkTemplate(_subTemplate, pal_MapSelect);
                }

                _mapTemplate = _mapContent != null
                    ? FindChildComponent<RectTransform>(_mapContent, "itemRegion")
                    : null;
                if (_mapTemplate != null)
                {
                    ParkTemplate(_mapTemplate, pal_MapSelect);
                }
            }

            if (pal_MapSet != null)
            {
                _barCustom = FindChildComponent<Scrollbar>(pal_MapSet, "srlBarCustom");
                _barFinished = FindChildComponent<Scrollbar>(pal_MapSet, "srlBarFinished");
            }

            _selectBuilt = true;
        }

        static void ParkTemplate(RectTransform template, Transform park)
        {
            template.SetParent(park, false);
            template.gameObject.SetActive(false);
        }

        void RefreshMapSelect()
        {
            CachePanelTemplates();
            WorldMapTreeCatalog.EnsureLoaded();
            IReadOnlyList<WorldMapRegionRow> regions = WorldMapTreeCatalog.Regions;
            ClearClones(_regionClones, _regionContent);
            if (_regionTemplate == null || _regionContent == null)
            {
                return;
            }

            float y = 0f;
            for (int i = 0; i < regions.Count; i++)
            {
                WorldMapRegionRow region = regions[i];
                if (region == null)
                {
                    continue;
                }

                bool hasChildren = WorldMapTreeCatalog.RegionHasChildren(region);
                bool expanded = hasChildren && IsExpanded(region.id);
                var row = Instantiate(_regionTemplate, _regionContent, false);
                row.gameObject.SetActive(true);
                row.name = "itemRegion_" + region.id;
                _regionClones.Add(row);

                var txtName = FindChildComponent<Text>(row, "txtName");
                if (txtName != null)
                {
                    LocalizedText.EnsureOn(txtName, region.nameKey);
                    txtName.text = L(region.nameKey);
                }

                var arrow = FindChildComponent<Image>(row, "imgArrow");
                if (arrow != null)
                {
                    arrow.gameObject.SetActive(hasChildren);
                    arrow.rectTransform.localEulerAngles = expanded
                        ? new Vector3(0f, 0f, -90f)
                        : Vector3.zero;
                }

                var palChildren = FindChildComponent<RectTransform>(row, "palChildren");
                int childCount = hasChildren ? region.children.Length : 0;
                float extra = expanded ? childCount * SubRowHeight : 0f;
                if (palChildren != null)
                {
                    palChildren.gameObject.SetActive(expanded);
                    palChildren.anchoredPosition = new Vector2(0f, -RegionHeadHeight);
                    palChildren.sizeDelta = new Vector2(-10f, extra);
                    if (expanded)
                    {
                        FillSubRows(palChildren, region);
                    }
                }

                float height = RegionHeadHeight + extra;
                PlaceStackRow(row, y, height);
                y += height + RowGap;

                var headBtn = FindChildComponent<Button>(row, "btnSelect");
                string regionId = region.id;
                BindClick(headBtn, () => OnClickRegion(regionId));
            }

            _regionContent.sizeDelta = new Vector2(0f, Mathf.Max(y, 8f));
            RefreshMapList();
        }

        void FillSubRows(RectTransform palChildren, WorldMapRegionRow region)
        {
            if (_subTemplate == null || palChildren == null || region.children == null)
            {
                return;
            }

            for (int i = palChildren.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(palChildren.GetChild(i).gameObject);
            }

            for (int i = 0; i < region.children.Length; i++)
            {
                WorldMapSubRegionRow sub = region.children[i];
                if (sub == null)
                {
                    continue;
                }

                var row = Instantiate(_subTemplate, palChildren, false);
                row.gameObject.SetActive(true);
                row.name = "itemSub_" + sub.id;
                PlaceStackRow(row, i * SubRowHeight, SubRowHeight);

                var txtName = FindChildComponent<Text>(row, "txtName");
                if (txtName != null)
                {
                    LocalizedText.EnsureOn(txtName, sub.nameKey);
                    txtName.text = L(sub.nameKey);
                    txtName.color = _leafId == sub.id ? SelectedName : NormalName;
                }

                var btn = row.GetComponent<Button>() ?? row.GetComponentInChildren<Button>(true);
                string subId = sub.id;
                string parentId = region.id;
                BindClick(btn, () => OnClickSub(parentId, subId));
            }
        }

        void RefreshMapList()
        {
            ClearClones(_mapClones, _mapContent);
            if (_mapTemplate == null || _mapContent == null)
            {
                return;
            }

            ResolveLeaf(out WorldMapRegionRow region, out WorldMapSubRegionRow sub);
            string[] maps = WorldMapTreeCatalog.MapsOf(region, sub);
            float y = 0f;
            for (int i = 0; i < maps.Length; i++)
            {
                string mapId = maps[i];
                if (string.IsNullOrEmpty(mapId))
                {
                    continue;
                }

                var row = Instantiate(_mapTemplate, _mapContent, false);
                row.gameObject.SetActive(true);
                row.name = "itemMap_" + mapId;
                _mapClones.Add(row);
                PlaceStackRow(row, y, MapRowHeight);
                y += MapRowHeight + RowGap;

                string titleKey = WorldMapTreeCatalog.TitleKeyForMap(mapId);
                var txtName = FindChildComponent<Text>(row, "txtName");
                if (txtName != null)
                {
                    LocalizedText.EnsureOn(txtName, titleKey);
                    txtName.text = L(titleKey);
                    txtName.color = mapId == _viewMapId ? SelectedName : NormalName;
                }

                var toggle = FindChildComponent<Toggle>(row, "Toggle");
                _suppressMapToggle = true;
                if (toggle != null)
                {
                    toggle.group = null;
                    toggle.isOn = mapId == _viewMapId;
                    toggle.onValueChanged.RemoveAllListeners();
                    string captured = mapId;
                    toggle.onValueChanged.AddListener(on =>
                    {
                        if (_suppressMapToggle)
                        {
                            return;
                        }

                        if (!on)
                        {
                            if (captured == _viewMapId)
                            {
                                _suppressMapToggle = true;
                                toggle.isOn = true;
                                _suppressMapToggle = false;
                            }

                            return;
                        }

                        SelectViewMap(captured);
                    });
                }

                _suppressMapToggle = false;

                var btn = FindChildComponent<Button>(row, "btnSelect");
                string capturedMap = mapId;
                BindClick(btn, () => SelectViewMap(capturedMap));
            }

            _mapContent.sizeDelta = new Vector2(0f, Mathf.Max(y, 8f));
        }

        void OnClickRegion(string regionId)
        {
            WorldMapRegionRow region = FindRegion(regionId);
            if (region == null)
            {
                return;
            }

            if (WorldMapTreeCatalog.RegionHasChildren(region))
            {
                _expanded[regionId] = !IsExpanded(regionId);
                if (IsExpanded(regionId) && region.children != null && region.children.Length > 0)
                {
                    _leafId = region.children[0].id;
                }
            }
            else
            {
                _leafId = regionId;
            }

            RefreshMapSelect();
        }

        void OnClickSub(string regionId, string subId)
        {
            _expanded[regionId] = true;
            _leafId = subId;
            RefreshMapSelect();
        }

        void SelectViewMap(string mapId)
        {
            HideMarkerPopup(restoreOrigin: false);
            _viewMapId = mapId;
            _forceRouge = mapId == "rouge";
            _activeBake = BakeForMap(mapId);
            CenterOnBake(_activeBake);
            RefreshMainLabels();
            RefreshMapList();
        }

        MapBakeData BakeForMap(string mapId)
        {
            if (mapId == "rouge")
            {
                return rougeBake != null ? rougeBake : beachBake;
            }

            return beachBake != null ? beachBake : rougeBake;
        }

        bool IsExpanded(string regionId)
        {
            return !string.IsNullOrEmpty(regionId) &&
                   _expanded.TryGetValue(regionId, out bool on) &&
                   on;
        }

        void ResolveLeaf(out WorldMapRegionRow region, out WorldMapSubRegionRow sub)
        {
            region = null;
            sub = null;
            IReadOnlyList<WorldMapRegionRow> regions = WorldMapTreeCatalog.Regions;
            if (regions.Count == 0)
            {
                return;
            }

            for (int i = 0; i < regions.Count; i++)
            {
                WorldMapRegionRow row = regions[i];
                if (row == null)
                {
                    continue;
                }

                if (row.id == _leafId)
                {
                    region = row;
                    return;
                }

                if (row.children == null)
                {
                    continue;
                }

                for (int c = 0; c < row.children.Length; c++)
                {
                    if (row.children[c] != null && row.children[c].id == _leafId)
                    {
                        region = row;
                        sub = row.children[c];
                        return;
                    }
                }
            }

            region = regions[0];
            if (WorldMapTreeCatalog.RegionHasChildren(region))
            {
                sub = region.children[0];
                _leafId = sub.id;
                _expanded[region.id] = true;
            }
            else
            {
                _leafId = region.id;
            }
        }

        static WorldMapRegionRow FindRegion(string id)
        {
            IReadOnlyList<WorldMapRegionRow> regions = WorldMapTreeCatalog.Regions;
            for (int i = 0; i < regions.Count; i++)
            {
                if (regions[i] != null && regions[i].id == id)
                {
                    return regions[i];
                }
            }

            return null;
        }

        static void PlaceStackRow(RectTransform rt, float yFromTop, float height)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -yFromTop);
            rt.sizeDelta = new Vector2(0f, height);
        }

        static void ClearClones(List<RectTransform> list, RectTransform parent)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null)
                {
                    DestroyImmediate(list[i].gameObject);
                }
            }

            list.Clear();
            if (parent == null)
            {
                return;
            }

            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (child != null && child.gameObject.activeSelf)
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        void BindMapSet()
        {
            CachePanelTemplates();
            if (pal_MapSet != null)
            {
                LocalizedText.EnsureOn(
                    FindChildComponent<Text>(pal_MapSet, "txtTip"),
                    "world_map_marker_tip");
                LocalizedText.EnsureOn(
                    FindChildComponent<Text>(pal_MapSet, "txtCustom"),
                    "world_map_marker_custom");
                LocalizedText.EnsureOn(
                    FindChildComponent<Text>(pal_MapSet, "txtFinished"),
                    "world_map_marker_finished");
            }

            BindFilterBar(_barCustom, _showCustomMarkers, OnCustomBar);
            BindFilterBar(_barFinished, _showFinishedMarkers, OnFinishedBar);
        }

        void BindFilterBar(Scrollbar bar, bool on, UnityAction<float> handler)
        {
            if (bar == null)
            {
                return;
            }

            bar.numberOfSteps = 2;
            bar.size = 0.38f;
            bar.onValueChanged.RemoveListener(handler);
            _suppressFilterBar = true;
            bar.value = on ? 1f : 0f;
            _suppressFilterBar = false;
            bar.onValueChanged.AddListener(handler);
        }

        void OnCustomBar(float value)
        {
            if (_suppressFilterBar)
            {
                return;
            }

            ApplyCustomVisible(value >= 0.5f);
        }

        void OnFinishedBar(float value)
        {
            if (_suppressFilterBar)
            {
                return;
            }

            ApplyFinishedVisible(value >= 0.5f);
        }

        void ApplyCustomVisible(bool on)
        {
            _showCustomMarkers = on;
            SnapBar(_barCustom, on);
        }

        void ApplyFinishedVisible(bool on)
        {
            _showFinishedMarkers = on;
            SnapBar(_barFinished, on);
        }

        void SnapBar(Scrollbar bar, bool on)
        {
            if (bar == null)
            {
                return;
            }

            float snapped = on ? 1f : 0f;
            if (Mathf.Abs(bar.value - snapped) < 0.01f)
            {
                return;
            }

            _suppressFilterBar = true;
            bar.value = snapped;
            _suppressFilterBar = false;
        }

        bool PassesMarkerFilter(MapMarkerTypeRow type)
        {
            if (type == null)
            {
                return false;
            }

            bool custom = type.layer == "custom" || type.id == "custom";
            if (custom && !_showCustomMarkers)
            {
                return false;
            }

            return true;
        }
    }
}
