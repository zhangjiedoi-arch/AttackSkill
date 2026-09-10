using System;
using System.Collections.Generic;
using UnityEngine;

namespace AttackSkill.UI
{
    [Serializable]
    public class MapMarkerTypeRow
    {
        public string id;
        public string icon;
        public string nameKey;
        public string descKey;
        public string layer;
        public int size = 32;
        public bool rotateWithView;
        public bool showOffscreen;
        public int priority;
        public float visibleRadius;
        public bool canFinish;
        public bool enabled = true;
    }

    [Serializable]
    public class MapMarkerTypeTable
    {
        public MapMarkerTypeRow[] types;
    }

    [Serializable]
    public class MapMarkerPlacementRow
    {
        public string uid;
        public string typeId;
        public string mapId;
        public float x;
        public float z;
        public float yaw;
        public string questFlag;
        public string titleKey;
        public string descKey;
        public bool finished;
        public bool enabled = true;
    }

    [Serializable]
    public class MapMarkerPlacementTable
    {
        public MapMarkerPlacementRow[] markers;
    }

    public static class MapMarkerCatalog
    {
        const string TypePath = "SmallMap/MapMarkerTypeTable";
        const string PlacePath = "SmallMap/MapMarkerPlacementTable";

        static MapMarkerTypeTable _types;
        static MapMarkerPlacementTable _places;
        static Dictionary<string, MapMarkerTypeRow> _typeById;
        static readonly Dictionary<string, bool> FinishedOverride = new Dictionary<string, bool>(16);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _types = null;
            _places = null;
            _typeById = null;
            FinishedOverride.Clear();
        }

        public static IReadOnlyList<MapMarkerPlacementRow> Placements
        {
            get
            {
                EnsureLoaded();
                return _places != null && _places.markers != null
                    ? _places.markers
                    : Array.Empty<MapMarkerPlacementRow>();
            }
        }

        public static void EnsureLoaded()
        {
            if (_types != null && _places != null && _typeById != null)
            {
                return;
            }

            var typeAsset = Resources.Load<TextAsset>(TypePath);
            _types = typeAsset != null
                ? JsonUtility.FromJson<MapMarkerTypeTable>(typeAsset.text)
                : new MapMarkerTypeTable { types = Array.Empty<MapMarkerTypeRow>() };

            var placeAsset = Resources.Load<TextAsset>(PlacePath);
            _places = placeAsset != null
                ? JsonUtility.FromJson<MapMarkerPlacementTable>(placeAsset.text)
                : new MapMarkerPlacementTable { markers = Array.Empty<MapMarkerPlacementRow>() };

            _typeById = new Dictionary<string, MapMarkerTypeRow>(16);
            if (_types.types != null)
            {
                for (int i = 0; i < _types.types.Length; i++)
                {
                    MapMarkerTypeRow row = _types.types[i];
                    if (row == null || string.IsNullOrEmpty(row.id))
                    {
                        continue;
                    }

                    _typeById[row.id] = row;
                }
            }
        }

        public static MapMarkerTypeRow GetType(string typeId)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(typeId) || _typeById == null)
            {
                return null;
            }

            return _typeById.TryGetValue(typeId, out MapMarkerTypeRow row) ? row : null;
        }

        public static MapMarkerPlacementRow GetPlacement(string uid)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(uid) || _places?.markers == null)
            {
                return null;
            }

            for (int i = 0; i < _places.markers.Length; i++)
            {
                MapMarkerPlacementRow row = _places.markers[i];
                if (row != null && row.uid == uid)
                {
                    return row;
                }
            }

            return null;
        }

        public static bool TryGetWorld(string uid, out Vector3 world)
        {
            world = default;
            MapMarkerPlacementRow place = GetPlacement(uid);
            if (place != null)
            {
                world = new Vector3(place.x, 0f, place.z);
                return true;
            }

            return TryGetLiveBinder(uid, out MapMarkerBinder binder, out world) && binder != null;
        }

        public static string GetTypeId(string uid)
        {
            MapMarkerPlacementRow place = GetPlacement(uid);
            if (place != null && !string.IsNullOrEmpty(place.typeId))
            {
                return place.typeId;
            }

            if (TryGetLiveBinder(uid, out MapMarkerBinder binder, out _) && binder != null)
            {
                return binder.TypeId;
            }

            return string.Empty;
        }

        static bool TryGetLiveBinder(string uid, out MapMarkerBinder binder, out Vector3 world)
        {
            binder = null;
            world = default;
            if (string.IsNullOrEmpty(uid))
            {
                return false;
            }

            IReadOnlyList<MapMarkerBinder> live = MapMarkerRegistry.Live;
            for (int i = 0; i < live.Count; i++)
            {
                MapMarkerBinder row = live[i];
                if (row != null && row.Uid == uid)
                {
                    binder = row;
                    world = row.transform.position;
                    return true;
                }
            }

            return false;
        }

        public static void ResolveLabels(string uid, string typeId, out string titleKey, out string descKey)
        {
            MapMarkerPlacementRow place = GetPlacement(uid);
            MapMarkerTypeRow type = GetType(
                !string.IsNullOrEmpty(typeId) ? typeId : place != null ? place.typeId : null);
            titleKey = place != null && !string.IsNullOrEmpty(place.titleKey)
                ? place.titleKey
                : type != null ? type.nameKey : string.Empty;
            descKey = place != null && !string.IsNullOrEmpty(place.descKey)
                ? place.descKey
                : type != null ? type.descKey : string.Empty;
        }

        public static bool IsTypeEnabled(MapMarkerTypeRow row)
        {
            return row != null && row.enabled && !string.IsNullOrEmpty(row.id);
        }

        public static void SetFinished(string uid, bool finished)
        {
            if (string.IsNullOrEmpty(uid))
            {
                return;
            }

            FinishedOverride[uid] = finished;
        }

        public static bool IsFinished(string uid, bool tableDefault)
        {
            if (!string.IsNullOrEmpty(uid) && FinishedOverride.TryGetValue(uid, out bool value))
            {
                return value;
            }

            return tableDefault;
        }
    }
}
