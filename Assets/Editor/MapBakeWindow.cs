using System.IO;
using AttackSkill.CameraSystem;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace AttackSkill.Editor
{
    /// <summary>正交俯视相机拍一帧，ReadPixels 存 PNG，并写入 MapBakeData 标定。</summary>
    public sealed class MapBakeWindow : EditorWindow
    {
        const string DefaultPng = "Assets/Sprites/SmallMap/BakedWorldMap.png";
        const string DefaultData = "Assets/Resources/SmallMap/MapBakeData.asset";
        const int MaxTex = 2048;

        Vector3 _origin;
        float _extentX = 80f;
        float _extentZ = 80f;
        float _cameraHeight = 80f;
        float _pixelsPerMeter = 4f;
        bool _flipY;
        Color _clearColor = new Color(0.15f, 0.18f, 0.22f, 1f);
        LayerMask _cullingMask = ~0;
        string _pngPath = DefaultPng;
        MapBakeData _bakeData;
        string _status = "选中地形根节点，点「从选中物体计算包围盒」，再烘焙。";

        [MenuItem("工具/地图/烘焙 PNG", false, 70)]
        public static void Open()
        {
            var window = GetWindow<MapBakeWindow>();
            window.titleContent = new GUIContent("地图烘焙");
            window.minSize = new Vector2(420, 460);
            window.Show();
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("正交俯视烘焙", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "用临时正交相机朝下拍一帧，ReadPixels 存 PNG。\n" +
                "Culling Mask 去掉角色 / UI / 特效。北 = 世界 +Z。",
                MessageType.Info);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("范围（世界 XZ）", EditorStyles.boldLabel);
                _origin = EditorGUILayout.Vector3Field("中心", _origin);
                _extentX = Mathf.Max(1f, EditorGUILayout.FloatField("X 全宽（米）", _extentX));
                _extentZ = Mathf.Max(1f, EditorGUILayout.FloatField("Z 全深（米）", _extentZ));
                _cameraHeight = Mathf.Max(5f, EditorGUILayout.FloatField("相机离地高度", _cameraHeight));
                if (GUILayout.Button("从选中物体计算包围盒"))
                {
                    CaptureBoundsFromSelection();
                }
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("输出", EditorStyles.boldLabel);
                _pixelsPerMeter = Mathf.Clamp(EditorGUILayout.FloatField("每米像素", _pixelsPerMeter), 0.25f, 32f);
                _flipY = EditorGUILayout.Toggle("垂直翻转 PNG", _flipY);
                _clearColor = EditorGUILayout.ColorField("清屏色", _clearColor);
                _cullingMask = LayerMaskField("Culling Mask", _cullingMask);
                _pngPath = EditorGUILayout.TextField("PNG 路径", _pngPath);
                _bakeData = (MapBakeData)EditorGUILayout.ObjectField(
                    "MapBakeData",
                    _bakeData,
                    typeof(MapBakeData),
                    false);

                Vector2Int texSize = ResolveTexSize();
                EditorGUILayout.LabelField("贴图尺寸", $"{texSize.x} × {texSize.y}（上限 {MaxTex}）");
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("仅烘焙 PNG", GUILayout.Height(28)))
                {
                    Bake(writeData: false);
                }

                if (GUILayout.Button("烘焙 PNG 并写入 MapBakeData", GUILayout.Height(28)))
                {
                    Bake(writeData: true);
                }
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(_status, MessageType.None);
        }

        void CaptureBoundsFromSelection()
        {
            var objs = Selection.gameObjects;
            if (objs == null || objs.Length == 0)
            {
                _status = "请先在 Hierarchy 选中地形 / 平面根物体。";
                return;
            }

            bool any = false;
            Bounds b = new Bounds();
            for (int i = 0; i < objs.Length; i++)
            {
                EncapsulateHierarchy(objs[i].transform, ref b, ref any);
            }

            if (!any)
            {
                _status = "选中物体下没有 Renderer / Terrain / Collider。";
                return;
            }

            _origin = b.center;
            _extentX = Mathf.Max(1f, b.size.x);
            _extentZ = Mathf.Max(1f, b.size.z);
            _cameraHeight = Mathf.Max(20f, b.extents.y + 30f);
            _status = $"包围盒中心 ({_origin.x:F1}, {_origin.y:F1}, {_origin.z:F1})，{_extentX:F1} × {_extentZ:F1} m。";
        }

        static void EncapsulateHierarchy(Transform root, ref Bounds bounds, ref bool any)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null || !renderers[i].enabled)
                {
                    continue;
                }

                Absorb(renderers[i].bounds, ref bounds, ref any);
            }

            var terrains = root.GetComponentsInChildren<Terrain>(true);
            for (int i = 0; i < terrains.Length; i++)
            {
                Terrain terrain = terrains[i];
                if (terrain == null || terrain.terrainData == null)
                {
                    continue;
                }

                Vector3 size = terrain.terrainData.size;
                Vector3 center = terrain.transform.position + size * 0.5f;
                Absorb(new Bounds(center, size), ref bounds, ref any);
            }

            var colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] == null || !colliders[i].enabled)
                {
                    continue;
                }

                Absorb(colliders[i].bounds, ref bounds, ref any);
            }
        }

        static void Absorb(Bounds add, ref Bounds bounds, ref bool any)
        {
            if (add.size.sqrMagnitude < 0.0001f)
            {
                return;
            }

            if (!any)
            {
                bounds = add;
                any = true;
                return;
            }

            bounds.Encapsulate(add);
        }

        Vector2Int ResolveTexSize()
        {
            int w = Mathf.Clamp(Mathf.RoundToInt(_extentX * _pixelsPerMeter), 32, MaxTex);
            int h = Mathf.Clamp(Mathf.RoundToInt(_extentZ * _pixelsPerMeter), 32, MaxTex);
            return new Vector2Int(w, h);
        }

        void Bake(bool writeData)
        {
            if (string.IsNullOrWhiteSpace(_pngPath) || !_pngPath.StartsWith("Assets/"))
            {
                _status = "PNG 路径必须在 Assets/ 下。";
                return;
            }

            Vector2Int texSize = ResolveTexSize();
            RenderTexture rt = null;
            Camera cam = null;
            Texture2D cpu = null;
            RenderTexture prevActive = RenderTexture.active;

            try
            {
                rt = new RenderTexture(texSize.x, texSize.y, 24, RenderTextureFormat.ARGB32)
                {
                    antiAliasing = 1,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                rt.Create();

                var camGo = new GameObject("MapBakeCamera_Temp")
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = _extentZ * 0.5f;
                cam.aspect = _extentX / _extentZ;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = Mathf.Max(200f, _cameraHeight * 4f);
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = _clearColor;
                cam.cullingMask = _cullingMask;
                cam.targetTexture = rt;
                cam.enabled = false;
                cam.transform.position = new Vector3(_origin.x, _origin.y + _cameraHeight, _origin.z);
                cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                cam.Render();

                RenderTexture.active = rt;
                cpu = new Texture2D(texSize.x, texSize.y, TextureFormat.RGBA32, false, false);
                cpu.ReadPixels(new Rect(0, 0, texSize.x, texSize.y), 0, 0);
                cpu.Apply();

                if (_flipY)
                {
                    FlipVertical(cpu);
                }

                byte[] png = cpu.EncodeToPNG();
                string abs = Path.GetFullPath(_pngPath);
                Directory.CreateDirectory(Path.GetDirectoryName(abs) ?? "Assets");
                File.WriteAllBytes(abs, png);
                AssetDatabase.ImportAsset(_pngPath, ImportAssetOptions.ForceUpdate);
                ConfigureTextureImporter(_pngPath);

                if (writeData)
                {
                    WriteBakeData(_pngPath);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                _status = writeData
                    ? $"已写入 {_pngPath} 与 MapBakeData。"
                    : $"已写入 {_pngPath}。";
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Texture2D>(_pngPath));
            }
            catch (System.Exception e)
            {
                _status = "烘焙失败：" + e.Message;
                Debug.LogException(e);
            }
            finally
            {
                RenderTexture.active = prevActive;
                if (cpu != null)
                {
                    DestroyImmediate(cpu);
                }

                if (cam != null)
                {
                    DestroyImmediate(cam.gameObject);
                }

                if (rt != null)
                {
                    rt.Release();
                    DestroyImmediate(rt);
                }
            }
        }

        void WriteBakeData(string pngAssetPath)
        {
            if (_bakeData == null)
            {
                string dir = Path.GetDirectoryName(DefaultData)?.Replace('\\', '/');
                if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
                {
                    EnsureFolder(dir);
                }

                _bakeData = AssetDatabase.LoadAssetAtPath<MapBakeData>(DefaultData);
                if (_bakeData == null)
                {
                    _bakeData = CreateInstance<MapBakeData>();
                    AssetDatabase.CreateAsset(_bakeData, DefaultData);
                }
            }

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(pngAssetPath);
            _bakeData.origin = new Vector3(_origin.x, 0f, _origin.z);
            _bakeData.extentX = _extentX;
            _bakeData.extentZ = _extentZ;
            _bakeData.flipY = _flipY;
            _bakeData.pixelsPerMeter = _pixelsPerMeter;
            _bakeData.texture = tex;
            _bakeData.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(pngAssetPath);
            EditorUtility.SetDirty(_bakeData);
        }

        static void EnsureFolder(string assetFolder)
        {
            string[] parts = assetFolder.Split('/');
            string cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(cur, parts[i]);
                }

                cur = next;
            }
        }

        static void ConfigureTextureImporter(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }

        static void FlipVertical(Texture2D tex)
        {
            Color[] pixels = tex.GetPixels();
            int w = tex.width;
            int h = tex.height;
            for (int y = 0; y < h / 2; y++)
            {
                int top = y * w;
                int bot = (h - 1 - y) * w;
                for (int x = 0; x < w; x++)
                {
                    Color t = pixels[top + x];
                    pixels[top + x] = pixels[bot + x];
                    pixels[bot + x] = t;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
        }

        static LayerMask LayerMaskField(string label, LayerMask selected)
        {
            int concat = InternalEditorUtility.LayerMaskToConcatenatedLayersMask(selected);
            concat = EditorGUILayout.MaskField(label, concat, InternalEditorUtility.layers);
            return InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(concat);
        }
    }
}
