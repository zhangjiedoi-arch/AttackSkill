using System.Collections.Generic;
using AttackSkill.CameraSystem;
using AttackSkill.Character;
using AttackSkill.Character.HSM;
using AttackSkill.Core;
using AttackSkill.Enemy;
using AttackSkill.Game;
using AttackSkill.Localization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AttackSkill.UI
{
    /// <summary>
    /// 全屏大地图：烘焙图拖拽/缩放，玩家与场景标记共用 Placement 表。
    /// </summary>
    [DefaultExecutionOrder(410)]
    public partial class UIWorldMapDialog : UIBase
    {
        const float MinScale = 0.12f;
        const float MaxScale = 3f;
        const float ZoomStep = 1.12f;
        const float PanDuration = 0.4f;
        const float PanSnapSqr = 4f;
        const int PlaceholderExplore = 100;
        const string ExploreNumberColor = "#FFD54A";

        [SerializeField] MapBakeData beachBake;
        [SerializeField] MapBakeData rougeBake;
        [SerializeField] GameObject playerIconPrefab;
        [SerializeField] GameObject markerIconPrefab;

        RectTransform _viewport;
        RectTransform _content;
        Image _textureMap;
        Image _textureMapRouge;
        SmallMapIconView _playerIcon;
        MapBakeData _activeBake;
        Vector2 _worldCenter;
        float _ppm = 4f;
        bool _compositeReady;
        bool _gate;
        bool _built;
        bool _suppressZoomSlider;
        float _scale = 1f;
        PartyPortraitId _playerPortrait = PartyPortraitId.Unknown;
        bool _forceRouge;
        string _viewMapId = "beach";
        bool _showFinishedMarkers = true;
        bool _showCustomMarkers = true;
        Vector3 _originWorld;
        bool _originValid;
        string _focusedMarkerUid;
        RectTransform _palMarkerInfo;
        Text _txtMarkerName;
        Text _txtMarkerDesc;
        bool _panning;
        Vector2 _panFrom;
        Vector2 _panTo;
        float _panT;
        static UIWorldMapDialog _openInstance;

        readonly Dictionary<string, SmallMapIconView> _markerIcons = new Dictionary<string, SmallMapIconView>(16);
        readonly Stack<SmallMapIconView> _markerPool = new Stack<SmallMapIconView>(8);
        readonly List<string> _stale = new List<string>(16);
        readonly HashSet<string> _visible = new HashSet<string>();

        public static void OpenFromHud()
        {
            var ui = UIManager.Instance;
            if (ui == null)
            {
                return;
            }

            if (ui.IsOpen(UIId.WorldMap))
            {
                ui.Close(UIId.WorldMap);
                return;
            }

            if (GameplayInputGate.IsBlocked)
            {
                return;
            }

            ui.EnsureWorldMapRegistered();
            ui.OpenDialog(UIId.WorldMap);
        }

        public override void OnOpen(object args)
        {
            EnsureBuilt();
            EnsureBakes();
            EnsurePrefabs();
            if (!_gate)
            {
                GameplayInputGate.PushSoftBlock(freezeTime: true);
                _gate = true;
            }

            ReleaseCursor();

            var party = PartyController.Instance;
            Vector3 pos = party != null && party.Active != null
                ? party.Active.transform.position
                : Vector3.zero;
            _viewMapId = IsRougeMap(pos) ? "rouge" : "beach";
            _forceRouge = _viewMapId == "rouge";
            _openInstance = this;
            ApplyCompositeMaps();
            CenterOnPlayer();
            CaptureOriginFromPlayer();
            BindChrome();
            ResetOverlays();
            RefreshMainLabels();
            SyncZoomSlider();
        }

        public override void OnClose()
        {
            if (_openInstance == this)
            {
                _openInstance = null;
            }

            UnbindZoomSlider();
            StopPan();
            ResetOverlays();
            RecycleMarkers();
            ReleaseGate();
            RestoreCursor();
        }

        void OnDisable()
        {
            if (_openInstance == this)
            {
                _openInstance = null;
            }

            UnbindZoomSlider();
            StopPan();
            RecycleMarkers();
            ReleaseGate();
            RestoreCursor();
        }

        void RestoreCursor()
        {
            if (GameplayInputGate.IsBlocked && !_gate)
            {
                return;
            }

            var cam = GameServices.ResolveCamera();
            if (cam != null)
            {
                cam.RestoreDesiredCursorLock();
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        void ReleaseCursor()
        {
            var cam = GameServices.ResolveCamera();
            if (cam != null)
            {
                cam.SetCursorLockedTemporary(false);
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        void ReleaseGate()
        {
            if (!_gate)
            {
                return;
            }

            GameplayInputGate.PopSoftBlock();
            _gate = false;
        }

        void LateUpdate()
        {
            if (!IsOpen || _content == null || !_compositeReady)
            {
                return;
            }

            TickPan();
            var party = PartyController.Instance;
            GenshinLikeCharacter player = party != null ? party.Active : null;
            UpdatePlayerIcon(party, player);
            UpdateMarkers(player != null ? player.transform.position : Vector3.zero);
            RaiseFocusedMarker();
            CounterScaleIcons();
        }

        void EnsureBuilt()
        {
            if (_built && _viewport != null && _content != null && _textureMap != null)
            {
                return;
            }

            BindPrefabNodes();
            if (_viewport != null && _content != null && _textureMap != null)
            {
                EnsureDragCatcher();
                _built = true;
                return;
            }

            BuildRuntimeFallback();
            _built = true;
        }

        void BindPrefabNodes()
        {
            if (viewport == null)
            {
                viewport = FindChildComponent<RectTransform>(transform, "viewport");
            }

            if (content == null)
            {
                Transform vp = viewport != null ? viewport : transform;
                content = FindChildComponent<RectTransform>(vp, "content");
            }

            if (textureMap == null)
            {
                Transform host = content != null ? content : transform;
                textureMap = FindChildComponent<Image>(host, "textureMap");
            }

            if (pal_MainSet == null)
            {
                pal_MainSet = FindChildComponent<RectTransform>(transform, "pal_MainSet");
            }

            Transform chrome = pal_MainSet != null ? pal_MainSet : transform;
            if (txtTitle == null)
            {
                txtTitle = FindChildComponent<Text>(chrome, "txtTitle");
            }

            if (txtSearch == null)
            {
                txtSearch = FindChildComponent<Text>(chrome, "txtSearch");
            }

            if (btnSwitch == null)
            {
                btnSwitch = FindChildComponent<Button>(chrome, "btnSwitch");
            }

            if (btnSst == null)
            {
                btnSst = FindChildComponent<Button>(chrome, "btnSst");
            }

            if (sliderZoom == null)
            {
                sliderZoom = FindChildComponent<Slider>(chrome, "sliderZoom");
            }

            if (btnEnlarge == null)
            {
                btnEnlarge = FindChildComponent<Button>(chrome, "btnEnlarge");
            }

            if (btnReduce == null)
            {
                btnReduce = FindChildComponent<Button>(chrome, "btnReduce");
            }

            if (btnClose == null)
            {
                btnClose = FindChildComponent<Button>(chrome, "btnClose");
            }

            if (pal_MapSelect == null)
            {
                pal_MapSelect = FindChildComponent<RectTransform>(transform, "pal_MapSelect");
            }

            if (pal_MapSet == null)
            {
                pal_MapSet = FindChildComponent<RectTransform>(transform, "pal_MapSet");
            }

            _viewport = viewport;
            _content = content;
            _textureMap = textureMap;
            DisableLocalizedOverwrite(txtTitle);
            DisableLocalizedOverwrite(txtSearch);
        }

        void BindChrome()
        {
            BindClick(btnClose, CloseSelf);
            BindClick(btnSwitch, () => ShowChromePanel(pal_MapSelect));
            BindClick(btnSst, () => ShowChromePanel(pal_MapSet));
            BindClick(btnEnlarge, ZoomIn);
            BindClick(btnReduce, ZoomOut);
            BindOverlayClose(pal_MapSelect);
            BindOverlayClose(pal_MapSet);
            BindZoomSlider();
            BindButtonLabels();
        }

        void BindButtonLabels()
        {
            LocalizationService.EnsureInitialized();
            LocalizedText.EnsureOn(ButtonLabel(btnSwitch), "world_map_switch");
            LocalizedText.EnsureOn(ButtonLabel(btnSst), "setting");
            LocalizedText.EnsureOn(ButtonLabel(btnEnlarge), "world_map_zoom_in");
            LocalizedText.EnsureOn(ButtonLabel(btnReduce), "world_map_zoom_out");

            if (pal_MapSelect != null)
            {
                LocalizedText.EnsureOn(
                    FindChildComponent<Text>(pal_MapSelect, "txtTitle"),
                    "world_map_switch");
            }

            if (pal_MapSet != null)
            {
                LocalizedText.EnsureOn(
                    FindChildComponent<Text>(pal_MapSet, "txtTitle"),
                    "world_map_settings");
            }
        }

        static Text ButtonLabel(Button button)
        {
            return button != null ? button.GetComponentInChildren<Text>(true) : null;
        }

        void BindOverlayClose(RectTransform panel)
        {
            if (panel == null)
            {
                return;
            }

            Button close = FindChildComponent<Button>(panel, "btnClose");
            BindClick(close, () => ShowChromePanel(pal_MainSet));
        }

        void BindZoomSlider()
        {
            if (sliderZoom == null)
            {
                return;
            }

            sliderZoom.minValue = MinScale;
            sliderZoom.maxValue = MaxScale;
            sliderZoom.wholeNumbers = false;
            sliderZoom.direction = Slider.Direction.BottomToTop;
            sliderZoom.onValueChanged.RemoveListener(OnZoomSliderChanged);
            sliderZoom.onValueChanged.AddListener(OnZoomSliderChanged);
        }

        void UnbindZoomSlider()
        {
            if (sliderZoom == null)
            {
                return;
            }

            sliderZoom.onValueChanged.RemoveListener(OnZoomSliderChanged);
        }

        void OnZoomSliderChanged(float value)
        {
            if (_suppressZoomSlider)
            {
                return;
            }

            ApplyScale(value, ViewportCenterLocal());
        }

        void ZoomIn()
        {
            ApplyScale(_scale * ZoomStep, ViewportCenterLocal());
        }

        void ZoomOut()
        {
            ApplyScale(_scale / ZoomStep, ViewportCenterLocal());
        }

        void ShowChromePanel(RectTransform target)
        {
            if (target != pal_MainSet)
            {
                HideMarkerPopup(restoreOrigin: true);
            }

            SetOverlayActive(pal_MainSet, target == pal_MainSet);
            SetOverlayActive(pal_MapSelect, target == pal_MapSelect);
            SetOverlayActive(pal_MapSet, target == pal_MapSet);
            AfterChromeShown(target);
        }

        static void SetOverlayActive(RectTransform panel, bool active)
        {
            if (panel != null)
            {
                panel.gameObject.SetActive(active);
            }
        }

        void ResetOverlays()
        {
            HideMarkerPopup(restoreOrigin: false);
            ShowChromePanel(pal_MainSet);
        }

        void RefreshMainLabels()
        {
            if (txtTitle != null)
            {
                txtTitle.text = L(WorldMapTreeCatalog.TitleKeyForMap(_viewMapId));
            }

            if (txtSearch != null)
            {
                txtSearch.supportRichText = true;
                txtSearch.text = LF(
                    "world_map_explore",
                    $"<color={ExploreNumberColor}>{PlaceholderExplore}</color>");
            }
        }

        static void DisableLocalizedOverwrite(Text label)
        {
            if (label == null)
            {
                return;
            }

            var loc = label.GetComponent<LocalizedText>();
            if (loc != null)
            {
                loc.enabled = false;
            }
        }

        void EnsureDragCatcher()
        {
            if (_viewport == null)
            {
                return;
            }

            var catcher = _viewport.GetComponent<WorldMapDragCatcher>();
            if (catcher == null)
            {
                catcher = _viewport.gameObject.AddComponent<WorldMapDragCatcher>();
            }

            catcher.Owner = this;
        }

        Vector2 ViewportCenterLocal()
        {
            return Vector2.zero;
        }

        void ApplyScale(float newScale, Vector2 viewportLocal)
        {
            if (_content == null)
            {
                return;
            }

            StopPan();

            float next = Mathf.Clamp(newScale, MinScale, MaxScale);
            Vector2 before = (viewportLocal - _content.anchoredPosition) / Mathf.Max(0.001f, _scale);
            _scale = next;
            _content.localScale = new Vector3(_scale, _scale, 1f);
            _content.anchoredPosition = viewportLocal - before * _scale;
            SyncZoomSlider();
        }

        void SyncZoomSlider()
        {
            if (sliderZoom == null)
            {
                return;
            }

            _suppressZoomSlider = true;
            sliderZoom.SetValueWithoutNotify(Mathf.Clamp(_scale, MinScale, MaxScale));
            _suppressZoomSlider = false;
        }

        void BuildRuntimeFallback()
        {
            var root = transform as RectTransform;
            if (root != null)
            {
                root.anchorMin = Vector2.zero;
                root.anchorMax = Vector2.one;
                root.offsetMin = Vector2.zero;
                root.offsetMax = Vector2.zero;
            }

            var dim = CreateChild("imgDim", transform);
            Stretch(dim);
            var dimImg = dim.gameObject.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.72f);
            dimImg.raycastTarget = true;

            _viewport = CreateChild("viewport", transform);
            viewport = _viewport;
            Stretch(_viewport);
            _viewport.offsetMin = new Vector2(24f, 72f);
            _viewport.offsetMax = new Vector2(-24f, -72f);
            var vpImg = _viewport.gameObject.AddComponent<Image>();
            vpImg.color = new Color(0.05f, 0.06f, 0.08f, 1f);
            vpImg.raycastTarget = true;
            _viewport.gameObject.AddComponent<RectMask2D>();
            EnsureDragCatcher();

            _content = CreateChild("content", _viewport);
            content = _content;
            _content.anchorMin = _content.anchorMax = new Vector2(0.5f, 0.5f);
            _content.pivot = new Vector2(0.5f, 0.5f);
            _content.sizeDelta = new Vector2(1024f, 1024f);
            _content.anchoredPosition = Vector2.zero;

            var texRt = CreateChild("textureMap", _content);
            Stretch(texRt);
            _textureMap = texRt.gameObject.AddComponent<Image>();
            textureMap = _textureMap;
            _textureMap.raycastTarget = false;
            _textureMap.preserveAspect = false;
            _textureMap.color = Color.white;

            txtTitle = CreateLabel(transform, "txtTitle", 28, new Vector2(0f, 0f), new Vector2(640f, 40f));
            var titleRt = txtTitle.rectTransform;
            titleRt.anchorMin = new Vector2(0.5f, 1f);
            titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -16f);

            btnClose = CreateHudButton(transform, "btnClose", "关闭", new Vector2(-80f, -16f), new Vector2(1f, 1f), new Vector2(1f, 1f));
        }

        void EnsureBakes()
        {
            if (beachBake == null)
            {
                beachBake = LoadBake("SmallMap/MapBakeData_Beach", "Assets/Resources/SmallMap/MapBakeData_Beach.asset");
            }

            if (rougeBake == null)
            {
                rougeBake = LoadBake("SmallMap/MapBakeData_Rouge", "Assets/Resources/SmallMap/MapBakeData_Rouge.asset");
                if (rougeBake == null)
                {
                    rougeBake = LoadBake("SmallMap/MapBakeData", "Assets/Resources/SmallMap/MapBakeData.asset");
                }
            }
        }

        void EnsurePrefabs()
        {
            if (playerIconPrefab == null)
            {
                playerIconPrefab = LoadUiPrefab("PlayerMapIcon");
            }

            if (markerIconPrefab == null)
            {
                markerIconPrefab = LoadUiPrefab("MarketMapIcon");
            }
        }

        static MapBakeData LoadBake(string resourcesPath, string assetPath)
        {
#if UNITY_EDITOR
            var fromAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<MapBakeData>(assetPath);
            if (fromAsset != null)
            {
                return fromAsset;
            }
#endif
            return Resources.Load<MapBakeData>(resourcesPath);
        }

        static GameObject LoadUiPrefab(string name)
        {
            var fromRes = Resources.Load<GameObject>("UI/SmallMap/" + name);
            if (fromRes != null)
            {
                return fromRes;
            }

#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                $"Assets/Prefabs/UI/SmallMap/{name}.prefab");
#else
            return null;
#endif
        }

        static bool IsRougeMap(Vector3 playerPos)
        {
            var progress = GameProgressController.Instance;
            RunPhase phase = progress != null ? progress.Phase : RunPhase.BeachExplore;
            if (phase == RunPhase.RougeCombat)
            {
                return true;
            }

            if (phase == RunPhase.BeachExplore || phase == RunPhase.Booting)
            {
                return false;
            }

            var flow = RouGeLikeFlowController.Instance;
            if (flow != null && flow.HasTeleported)
            {
                return true;
            }

            return playerPos.x >= 500f;
        }

        void ApplyCompositeMaps()
        {
            if (_content == null)
            {
                _compositeReady = false;
                return;
            }

            EnsureBakes();
            EnsureRougeTile();
            _ppm = ResolveSharedPpm(beachBake, rougeBake);
            if (!TryUnionBounds(out float minX, out float maxX, out float minZ, out float maxZ))
            {
                _compositeReady = false;
                return;
            }

            _worldCenter = new Vector2((minX + maxX) * 0.5f, (minZ + maxZ) * 0.5f);
            _content.sizeDelta = new Vector2(
                Mathf.Max(64f, (maxX - minX) * _ppm),
                Mathf.Max(64f, (maxZ - minZ) * _ppm));

            PlaceBakeTile(_textureMap, beachBake);
            PlaceBakeTile(_textureMapRouge, rougeBake);
            if (_textureMap != null)
            {
                _textureMap.transform.SetSiblingIndex(0);
            }

            if (_textureMapRouge != null)
            {
                _textureMapRouge.transform.SetSiblingIndex(1);
            }

            _activeBake = BakeForMap(_viewMapId);
            _compositeReady = true;
            _scale = 1f;
            _content.localScale = Vector3.one;
            RefreshMainLabels();
            SyncZoomSlider();
            RecycleMarkers();
        }

        void EnsureRougeTile()
        {
            if (_textureMapRouge != null || _textureMap == null || _content == null)
            {
                return;
            }

            var go = Instantiate(_textureMap.gameObject, _content, false);
            go.name = "textureMap_Rouge";
            _textureMapRouge = go.GetComponent<Image>();
            if (_textureMapRouge != null)
            {
                _textureMapRouge.raycastTarget = false;
                _textureMapRouge.preserveAspect = false;
            }
        }

        static float ResolveSharedPpm(MapBakeData a, MapBakeData b)
        {
            float ppm = 0f;
            if (a != null)
            {
                ppm = a.pixelsPerMeter;
            }

            if (b != null)
            {
                ppm = Mathf.Max(ppm, b.pixelsPerMeter);
            }

            return ppm > 0.01f ? ppm : 4f;
        }

        bool TryUnionBounds(out float minX, out float maxX, out float minZ, out float maxZ)
        {
            minX = minZ = float.PositiveInfinity;
            maxX = maxZ = float.NegativeInfinity;
            EncapsulateBake(beachBake, ref minX, ref maxX, ref minZ, ref maxZ);
            EncapsulateBake(rougeBake, ref minX, ref maxX, ref minZ, ref maxZ);
            return minX < maxX && minZ < maxZ;
        }

        static void EncapsulateBake(
            MapBakeData bake,
            ref float minX,
            ref float maxX,
            ref float minZ,
            ref float maxZ)
        {
            if (bake == null || bake.extentX < 0.01f || bake.extentZ < 0.01f)
            {
                return;
            }

            minX = Mathf.Min(minX, bake.MinX);
            maxX = Mathf.Max(maxX, bake.MaxX);
            minZ = Mathf.Min(minZ, bake.MinZ);
            maxZ = Mathf.Max(maxZ, bake.MaxZ);
        }

        void PlaceBakeTile(Image image, MapBakeData bake)
        {
            if (image == null)
            {
                return;
            }

            if (bake == null)
            {
                image.enabled = false;
                return;
            }

            Sprite sp = ResolveBakeSprite(bake);
            image.sprite = sp;
            image.enabled = sp != null;
            image.color = Color.white;
            var rt = image.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(bake.extentX * _ppm, bake.extentZ * _ppm);
            rt.anchoredPosition = WorldToContent(bake.origin);
            rt.localScale = bake.flipY ? new Vector3(1f, -1f, 1f) : Vector3.one;
        }

        static Sprite ResolveBakeSprite(MapBakeData bake)
        {
            if (bake == null)
            {
                return null;
            }

            Sprite sp = bake.ResolveSprite();
            if (sp != null)
            {
                return sp;
            }

#if UNITY_EDITOR
            bool rouge = bake.name != null &&
                         bake.name.IndexOf("Rouge", System.StringComparison.OrdinalIgnoreCase) >= 0;
            string path = rouge
                ? "Assets/Sprites/SmallMap/MapBakeData_Rouge.png"
                : "Assets/Sprites/SmallMap/MapBakeData_Beach.png";
            sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
#endif
            return sp;
        }

        public void OnDragMap(PointerEventData eventData)
        {
            if (_content == null)
            {
                return;
            }

            StopPan();
            _content.anchoredPosition += eventData.delta;
        }

        public void OnScrollMap(PointerEventData eventData)
        {
            if (_content == null || _viewport == null)
            {
                return;
            }

            float delta = eventData.scrollDelta.y;
            if (Mathf.Abs(delta) < 0.01f)
            {
                return;
            }

            Camera cam = eventData.pressEventCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _viewport,
                eventData.position,
                cam,
                out Vector2 local);
            ApplyScale(_scale * (delta > 0f ? ZoomStep : 1f / ZoomStep), local);
        }

        void CenterOnPlayer()
        {
            var party = PartyController.Instance;
            if (party == null || party.Active == null)
            {
                return;
            }

            CenterContentOnWorld(party.Active.transform.position);
        }

        void CaptureOriginFromPlayer()
        {
            var party = PartyController.Instance;
            if (party != null && party.Active != null)
            {
                CaptureOriginWorld(party.Active.transform.position);
                return;
            }

            if (_activeBake != null)
            {
                CaptureOriginWorld(_activeBake.origin);
            }
        }

        void CaptureOriginWorld(Vector3 world)
        {
            _originWorld = world;
            _originValid = true;
        }

        void CenterContentOnWorld(Vector3 world, bool ease = false)
        {
            if (_content == null || !TryWorldToContent(world, out Vector2 p))
            {
                return;
            }

            Vector2 target = -p * _scale;
            if (!ease)
            {
                StopPan();
                _content.anchoredPosition = target;
                return;
            }

            BeginPan(target);
        }

        void BeginPan(Vector2 target)
        {
            _panFrom = _content.anchoredPosition;
            _panTo = target;
            if ((_panTo - _panFrom).sqrMagnitude <= PanSnapSqr)
            {
                StopPan();
                _content.anchoredPosition = _panTo;
                return;
            }

            _panT = 0f;
            _panning = true;
        }

        void TickPan()
        {
            if (!_panning || _content == null)
            {
                return;
            }

            _panT += Time.unscaledDeltaTime / PanDuration;
            if (_panT >= 1f)
            {
                _content.anchoredPosition = _panTo;
                StopPan();
                return;
            }

            float t = 1f - Mathf.Pow(1f - _panT, 3f);
            _content.anchoredPosition = Vector2.LerpUnclamped(_panFrom, _panTo, t);
        }

        void StopPan()
        {
            _panning = false;
            _panT = 0f;
        }

        void RestoreOriginView()
        {
            if (_originValid)
            {
                CenterContentOnWorld(_originWorld, ease: true);
            }
        }

        public void OnMarkerClicked(string uid)
        {
            if (string.IsNullOrEmpty(uid) || !MapMarkerCatalog.TryGetWorld(uid, out Vector3 world))
            {
                return;
            }

            if ((pal_MapSelect != null && pal_MapSelect.gameObject.activeSelf) ||
                (pal_MapSet != null && pal_MapSet.gameObject.activeSelf))
            {
                ShowChromePanel(pal_MainSet);
            }

            _focusedMarkerUid = uid;
            CenterContentOnWorld(world, ease: true);
            ShowMarkerPopup(uid);
        }

        bool TryWorldToContent(Vector3 world, out Vector2 pos)
        {
            pos = default;
            if (!_compositeReady || _content == null || _ppm < 0.01f)
            {
                return false;
            }

            pos = WorldToContent(world);
            return true;
        }

        Vector2 WorldToContent(Vector3 world)
        {
            return new Vector2(
                (world.x - _worldCenter.x) * _ppm,
                (world.z - _worldCenter.y) * _ppm);
        }

        void CenterOnBake(MapBakeData bake)
        {
            if (bake == null || _content == null || !_compositeReady)
            {
                return;
            }

            CenterContentOnWorld(bake.origin, ease: true);
            CaptureOriginWorld(bake.origin);
        }

        void UpdatePlayerIcon(PartyController party, GenshinLikeCharacter player)
        {
            if (player == null || playerIconPrefab == null || _content == null)
            {
                if (_playerIcon != null)
                {
                    _playerIcon.gameObject.SetActive(false);
                }

                return;
            }

            if (_playerIcon == null)
            {
                var go = Instantiate(playerIconPrefab, _content, false);
                go.name = "PlayerMapIcon";
                _playerIcon = go.GetComponent<SmallMapIconView>() ?? go.AddComponent<SmallMapIconView>();
                _playerIcon.EnsureBound();
            }

            _playerIcon.gameObject.SetActive(true);
            if (party != null)
            {
                PartyPortraitId portrait = party.GetPortraitId(party.ActiveIndex);
                if (portrait != _playerPortrait || !_playerIcon.HasIcon)
                {
                    Sprite icon = SmallMapIconCatalog.ForPlayer(portrait);
                    _playerIcon.BindPlayer(icon);
                    if (icon != null && _playerIcon.HasIcon)
                    {
                        _playerPortrait = portrait;
                    }
                }
            }

            if (!TryWorldToContent(player.transform.position, out Vector2 pos))
            {
                _playerIcon.gameObject.SetActive(false);
                return;
            }

            float yaw = 0f;
            var orbit = GameServices.ResolveCamera();
            if (orbit != null && orbit.YawTransform != null)
            {
                yaw = orbit.YawTransform.eulerAngles.y;
            }

            _playerIcon.Place(pos, -yaw);
            _playerIcon.transform.SetAsLastSibling();
        }

        void UpdateMarkers(Vector3 playerPos)
        {
            if (markerIconPrefab == null || _content == null || !_compositeReady)
            {
                return;
            }

            MapMarkerCatalog.EnsureLoaded();
            _visible.Clear();

            IReadOnlyList<MapMarkerPlacementRow> places = MapMarkerCatalog.Placements;
            for (int i = 0; i < places.Count; i++)
            {
                MapMarkerPlacementRow row = places[i];
                if (row == null || !row.enabled || string.IsNullOrEmpty(row.uid))
                {
                    continue;
                }

                ShowMarker(
                    row.uid,
                    row.typeId,
                    new Vector3(row.x, 0f, row.z),
                    MapMarkerCatalog.IsFinished(row.uid, row.finished));
            }

            IReadOnlyList<MapMarkerBinder> live = MapMarkerRegistry.Live;
            for (int i = 0; i < live.Count; i++)
            {
                MapMarkerBinder binder = live[i];
                if (binder == null || !binder.isActiveAndEnabled)
                {
                    continue;
                }

                ShowMarker(
                    binder.Uid,
                    binder.TypeId,
                    binder.transform.position,
                    MapMarkerCatalog.IsFinished(binder.Uid, binder.Finished));
            }

            _stale.Clear();
            foreach (var kv in _markerIcons)
            {
                if (!_visible.Contains(kv.Key))
                {
                    _stale.Add(kv.Key);
                }
            }

            for (int i = 0; i < _stale.Count; i++)
            {
                string key = _stale[i];
                if (_markerIcons.TryGetValue(key, out SmallMapIconView view))
                {
                    view.Recycle();
                    _markerPool.Push(view);
                }

                _markerIcons.Remove(key);
            }
        }

        void ShowMarker(string uid, string typeId, Vector3 world, bool tableFinished)
        {
            MapMarkerTypeRow type = MapMarkerCatalog.GetType(typeId);
            if (!MapMarkerCatalog.IsTypeEnabled(type))
            {
                return;
            }

            if (!PassesMarkerFilter(type))
            {
                return;
            }

            if (!TryWorldToContent(world, out Vector2 pos))
            {
                return;
            }

            _visible.Add(uid);
            if (!_markerIcons.TryGetValue(uid, out SmallMapIconView view) || view == null)
            {
                view = RentMarker();
                _markerIcons[uid] = view;
            }

            view.gameObject.SetActive(true);
            view.BindMarker(
                SmallMapIconCatalog.ForMarker(type.icon),
                type.canFinish && tableFinished && _showFinishedMarkers);
            view.SetPixelSize(type.size);
            view.Place(pos, 0f);
            EnsureMarkerClickable(view, uid);
            if (_playerIcon != null)
            {
                _playerIcon.transform.SetAsLastSibling();
            }
        }

        void EnsureMarkerClickable(SmallMapIconView view, string uid)
        {
            if (view == null)
            {
                return;
            }

            view.SetIconRaycast(false);
            var hit = view.GetComponent<Image>();
            if (hit == null)
            {
                hit = view.gameObject.AddComponent<Image>();
                hit.color = new Color(1f, 1f, 1f, 0f);
            }

            hit.raycastTarget = true;
            var btn = view.GetComponent<Button>();
            if (btn != null)
            {
                btn.transition = Selectable.Transition.None;
                btn.onClick.RemoveAllListeners();
                btn.targetGraphic = hit;
                btn.interactable = false;
            }

            var pointer = view.GetComponent<WorldMapMarkerPointer>();
            if (pointer == null)
            {
                pointer = view.gameObject.AddComponent<WorldMapMarkerPointer>();
            }

            pointer.Owner = this;
            pointer.Uid = uid;
        }

        void RaiseFocusedMarker()
        {
            if (string.IsNullOrEmpty(_focusedMarkerUid))
            {
                return;
            }

            if (_markerIcons.TryGetValue(_focusedMarkerUid, out SmallMapIconView view) && view != null)
            {
                view.transform.SetAsLastSibling();
            }
        }

        SmallMapIconView RentMarker()
        {
            SmallMapIconView view = null;
            while (_markerPool.Count > 0 && view == null)
            {
                view = _markerPool.Pop();
            }

            if (view == null)
            {
                var go = Instantiate(markerIconPrefab, _content, false);
                go.name = "MarketMapIcon";
                view = go.GetComponent<SmallMapIconView>() ?? go.AddComponent<SmallMapIconView>();
                view.EnsureBound();
            }

            view.transform.SetParent(_content, false);
            return view;
        }

        void RecycleMarkers()
        {
            foreach (var kv in _markerIcons)
            {
                if (kv.Value != null)
                {
                    kv.Value.Recycle();
                    _markerPool.Push(kv.Value);
                }
            }

            _markerIcons.Clear();
            _visible.Clear();
        }

        void CounterScaleIcons()
        {
            float inv = 1f / Mathf.Max(0.001f, _scale);
            Vector3 s = new Vector3(inv, inv, 1f);
            if (_playerIcon != null)
            {
                _playerIcon.transform.localScale = s;
            }

            foreach (var kv in _markerIcons)
            {
                if (kv.Value != null)
                {
                    kv.Value.transform.localScale = s;
                }
            }
        }

        static T FindChildComponent<T>(Transform root, string name) where T : Component
        {
            if (root == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            var all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null || all[i].name != name)
                {
                    continue;
                }

                var c = all[i].GetComponent<T>();
                if (c != null)
                {
                    return c;
                }
            }

            return null;
        }

        static RectTransform CreateChild(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static Text CreateLabel(Transform parent, string name, int size, Vector2 pos, Vector2 sizeDelta)
        {
            var rt = CreateChild(name, parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = sizeDelta;
            var label = rt.gameObject.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (label.font == null)
            {
                label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            label.text = string.Empty;
            label.fontSize = size;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;
            return label;
        }

        static Button CreateHudButton(
            Transform parent,
            string name,
            string caption,
            Vector2 pos,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            var rt = CreateChild(name, parent);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = anchorMin;
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(120f, 40f);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0.18f, 0.2f, 0.26f, 0.92f);
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var label = CreateLabel(rt, "Label", 22, Vector2.zero, new Vector2(110f, 36f));
            label.text = caption;
            return btn;
        }

        public static GameObject CreateRuntimeTemplate()
        {
            var root = new GameObject("UI_WorldMap_Dialog", typeof(RectTransform));
            var view = root.AddComponent<UIWorldMapDialog>();
            view.EnsureBuilt();
            root.SetActive(false);
            return root;
        }
    }

    sealed class WorldMapDragCatcher : MonoBehaviour, IBeginDragHandler, IDragHandler, IScrollHandler
    {
        public UIWorldMapDialog Owner;

        public void OnBeginDrag(PointerEventData eventData)
        {
        }

        public void OnDrag(PointerEventData eventData)
        {
            Owner?.OnDragMap(eventData);
        }

        public void OnScroll(PointerEventData eventData)
        {
            Owner?.OnScrollMap(eventData);
        }
    }

    sealed class WorldMapMarkerPointer :
        MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler,
        IBeginDragHandler,
        IDragHandler,
        IScrollHandler
    {
        const float ClickSlopSqr = 256f;

        public UIWorldMapDialog Owner;
        public string Uid;

        Vector2 _press;
        bool _dragged;

        public void OnPointerDown(PointerEventData eventData)
        {
            _press = eventData.position;
            _dragged = false;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
        }

        public void OnDrag(PointerEventData eventData)
        {
            if ((eventData.position - _press).sqrMagnitude > ClickSlopSqr)
            {
                _dragged = true;
            }

            Owner?.OnDragMap(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_dragged || (eventData.position - _press).sqrMagnitude > ClickSlopSqr)
            {
                return;
            }

            if (!string.IsNullOrEmpty(Uid))
            {
                Owner?.OnMarkerClicked(Uid);
            }
        }

        public void OnScroll(PointerEventData eventData)
        {
            Owner?.OnScrollMap(eventData);
        }
    }
}
