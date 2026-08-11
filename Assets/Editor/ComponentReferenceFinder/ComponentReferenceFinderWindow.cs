using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AttackSkill.Editor.ComponentReferenceFinder
{
    /// <summary>
    /// 批量查找某组件在资源中的引用，按文件夹逐级展示。
    /// 菜单：工具 / 资源检查 / 组件引用查找
    /// </summary>
    public class ComponentReferenceFinderWindow : EditorWindow
    {
        private MonoScript _monoScript;
        private string _typeSearch = string.Empty;
        private Type _selectedType;
        private List<Type> _typeCandidates = new List<Type>();
        private Vector2 _typeListScroll;

        private DefaultAsset _folderAsset;
        private string _folderPath = "Assets";
        private bool _includeSubFolders = true;
        private bool _searchPrefabs = true;
        private bool _searchScenes = false;
        private bool _includeInactive = true;

        private ComponentReferenceFolderNode _tree;
        private List<ComponentReferenceAssetResult> _flatResults = new List<ComponentReferenceAssetResult>();
        private readonly HashSet<string> _expandedFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _expandedAssets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private Vector2 _resultScroll;
        private string _status = "选择组件与文件夹后点击「开始查找」。";
        private string _nameFilter = string.Empty;

        [MenuItem("工具/资源检查/组件引用查找")]
        public static void Open()
        {
            var window = GetWindow<ComponentReferenceFinderWindow>();
            window.titleContent = new GUIContent("组件引用查找");
            window.minSize = new Vector2(760, 520);
            window.Show();
        }

        private void OnEnable()
        {
            if (_folderAsset == null && AssetDatabase.IsValidFolder("Assets"))
            {
                _folderAsset = AssetDatabase.LoadAssetAtPath<DefaultAsset>("Assets");
            }
        }

        private void OnGUI()
        {
            DrawHeader();
            EditorGUILayout.Space(4);
            DrawComponentPicker();
            EditorGUILayout.Space(4);
            DrawFolderAndOptions();
            EditorGUILayout.Space(6);
            DrawActions();
            EditorGUILayout.Space(4);
            DrawResults();
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("组件引用查找", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "批量查找指定组件在 Prefab / Scene 中的引用。\n" +
                "结果按文件夹逐级分组，便于定位所有使用该组件的界面，方便做特殊处理。",
                MessageType.Info);
        }

        private void DrawComponentPicker()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("目标组件", EditorStyles.boldLabel);

                EditorGUI.BeginChangeCheck();
                _monoScript = (MonoScript)EditorGUILayout.ObjectField("脚本资源", _monoScript, typeof(MonoScript), false);
                if (EditorGUI.EndChangeCheck() && _monoScript != null)
                {
                    Type t = _monoScript.GetClass();
                    if (t != null && typeof(Component).IsAssignableFrom(t))
                    {
                        _selectedType = t;
                        _typeSearch = t.Name;
                        _typeCandidates.Clear();
                    }
                    else
                    {
                        _selectedType = null;
                        ShowNotification(new GUIContent("请选择继承 Component / MonoBehaviour 的脚本"));
                    }
                }

                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("或按类型名搜索（含 UGUI 内置组件）", EditorStyles.miniLabel);

                EditorGUI.BeginChangeCheck();
                _typeSearch = EditorGUILayout.TextField("类型关键字", _typeSearch);
                if (EditorGUI.EndChangeCheck())
                {
                    RefreshTypeCandidates();
                }

                if (_typeCandidates.Count > 0 && _selectedType == null)
                {
                    using (var scroll = new EditorGUILayout.ScrollViewScope(_typeListScroll, GUILayout.MaxHeight(120)))
                    {
                        _typeListScroll = scroll.scrollPosition;
                        int showCount = Math.Min(_typeCandidates.Count, 40);
                        for (int i = 0; i < showCount; i++)
                        {
                            Type t = _typeCandidates[i];
                            if (GUILayout.Button($"{t.Name}  ({t.Namespace})", EditorStyles.miniButtonLeft))
                            {
                                _selectedType = t;
                                _typeSearch = t.Name;
                                _typeCandidates.Clear();
                                // 同步 MonoScript（若有）
                                string scriptPath = FindMonoScriptPath(t);
                                _monoScript = string.IsNullOrEmpty(scriptPath)
                                    ? null
                                    : AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
                            }
                        }

                        if (_typeCandidates.Count > showCount)
                        {
                            EditorGUILayout.LabelField($"… 还有 {_typeCandidates.Count - showCount} 项，请缩小关键字", EditorStyles.miniLabel);
                        }
                    }
                }

                string current = _selectedType != null
                    ? $"{_selectedType.FullName}"
                    : "未选择";
                EditorGUILayout.LabelField("当前组件", current);
            }
        }

        private void DrawFolderAndOptions()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("查找范围", EditorStyles.boldLabel);

                EditorGUI.BeginChangeCheck();
                _folderAsset = (DefaultAsset)EditorGUILayout.ObjectField("根文件夹", _folderAsset, typeof(DefaultAsset), false);
                if (EditorGUI.EndChangeCheck() && _folderAsset != null)
                {
                    string path = AssetDatabase.GetAssetPath(_folderAsset);
                    if (AssetDatabase.IsValidFolder(path))
                    {
                        _folderPath = path;
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("请选择 Project 中的文件夹。", MessageType.Warning);
                    }
                }

                EditorGUILayout.LabelField("路径", _folderPath, EditorStyles.miniLabel);

                _includeSubFolders = EditorGUILayout.ToggleLeft("包含子文件夹（逐级递归）", _includeSubFolders);
                _searchPrefabs = EditorGUILayout.ToggleLeft("查找 Prefab", _searchPrefabs);
                _searchScenes = EditorGUILayout.ToggleLeft("查找 Scene（只读打开，较大时较慢）", _searchScenes);
                _includeInactive = EditorGUILayout.ToggleLeft("包含未激活节点", _includeInactive);
            }
        }

        private void DrawActions()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.backgroundColor = new Color(0.45f, 0.85f, 0.55f);
                if (GUILayout.Button("开始查找", GUILayout.Height(28)))
                {
                    RunSearch();
                }

                GUI.backgroundColor = Color.white;

                using (new EditorGUI.DisabledScope(_flatResults.Count == 0))
                {
                    if (GUILayout.Button("复制报告", GUILayout.Width(90), GUILayout.Height(28)))
                    {
                        string report = ComponentReferenceSearchService.BuildReport(_selectedType, _folderPath, _flatResults);
                        EditorGUIUtility.systemCopyBuffer = report;
                        ShowNotification(new GUIContent("已复制到剪贴板"));
                    }

                    if (GUILayout.Button("导出 TXT", GUILayout.Width(90), GUILayout.Height(28)))
                    {
                        ExportReport();
                    }

                    if (GUILayout.Button("展开全部", GUILayout.Width(80), GUILayout.Height(28)))
                    {
                        ExpandAll(_tree, true);
                    }

                    if (GUILayout.Button("折叠全部", GUILayout.Width(80), GUILayout.Height(28)))
                    {
                        _expandedFolders.Clear();
                        _expandedAssets.Clear();
                    }
                }
            }

            EditorGUILayout.LabelField(_status, EditorStyles.miniLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("结果过滤", GUILayout.Width(56));
                _nameFilter = EditorGUILayout.TextField(_nameFilter);
            }
        }

        private void DrawResults()
        {
            using (var scroll = new EditorGUILayout.ScrollViewScope(_resultScroll))
            {
                _resultScroll = scroll.scrollPosition;

                if (_tree == null)
                {
                    EditorGUILayout.LabelField("暂无结果。");
                    return;
                }

                if (_flatResults.Count == 0)
                {
                    EditorGUILayout.LabelField("未找到引用。");
                    return;
                }

                DrawFolderNode(_tree, 0);
            }
        }

        private void DrawFolderNode(ComponentReferenceFolderNode node, int depth)
        {
            if (node == null)
            {
                return;
            }

            // 过滤：若本节点及子树在过滤后无资源，则跳过
            if (!FolderMatchesFilter(node))
            {
                return;
            }

            bool isRoot = depth == 0;
            bool expanded = isRoot || _expandedFolders.Contains(node.FolderPath);
            string foldLabel = $"{node.FolderName}   [{node.AssetCount} 资源 / {node.HitCount} 处]";

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(depth * 14);
                if (!isRoot)
                {
                    bool newExpanded = EditorGUILayout.Foldout(expanded, foldLabel, true);
                    if (newExpanded != expanded)
                    {
                        if (newExpanded)
                        {
                            _expandedFolders.Add(node.FolderPath);
                        }
                        else
                        {
                            _expandedFolders.Remove(node.FolderPath);
                        }
                    }
                }
                else
                {
                    EditorGUILayout.LabelField(foldLabel, EditorStyles.boldLabel);
                    expanded = true;
                }

                if (!isRoot && GUILayout.Button("以此为根重查", GUILayout.Width(96)))
                {
                    ReSearchFromFolder(node.FolderPath);
                }
            }

            if (!expanded && !isRoot)
            {
                return;
            }

            foreach (var child in node.Children)
            {
                DrawFolderNode(child, depth + 1);
            }

            foreach (var asset in node.Assets)
            {
                if (!AssetMatchesFilter(asset))
                {
                    continue;
                }

                DrawAssetRow(asset, depth + 1);
            }
        }

        private void DrawAssetRow(ComponentReferenceAssetResult asset, int depth)
        {
            bool expanded = _expandedAssets.Contains(asset.AssetPath);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(depth * 14);
                    bool newExpanded = EditorGUILayout.Foldout(
                        expanded,
                        $"{asset.AssetName}  （{asset.TotalCount}）",
                        true);

                    if (newExpanded != expanded)
                    {
                        if (newExpanded)
                        {
                            _expandedAssets.Add(asset.AssetPath);
                        }
                        else
                        {
                            _expandedAssets.Remove(asset.AssetPath);
                        }
                    }

                    EditorGUILayout.LabelField(asset.AssetPath, EditorStyles.miniLabel);

                    if (GUILayout.Button("定位", GUILayout.Width(48)))
                    {
                        var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(asset.AssetPath);
                        if (obj != null)
                        {
                            EditorGUIUtility.PingObject(obj);
                            Selection.activeObject = obj;
                        }
                    }

                    if (GUILayout.Button("打开", GUILayout.Width(48)))
                    {
                        var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(asset.AssetPath);
                        if (obj != null)
                        {
                            AssetDatabase.OpenAsset(obj);
                        }
                    }
                }

                if (!_expandedAssets.Contains(asset.AssetPath))
                {
                    return;
                }

                foreach (var hit in asset.Hits)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Space((depth + 1) * 14);
                        EditorGUILayout.LabelField($"• {hit.HierarchyPath}", EditorStyles.wordWrappedMiniLabel);
                    }
                }
            }
        }

        private void RunSearch()
        {
            if (_selectedType == null)
            {
                _status = "请先选择要查找的组件类型。";
                ShowNotification(new GUIContent("未选择组件"));
                return;
            }

            if (!_searchPrefabs && !_searchScenes)
            {
                _status = "请至少勾选 Prefab 或 Scene。";
                return;
            }

            if (!AssetDatabase.IsValidFolder(_folderPath))
            {
                _status = $"无效文件夹：{_folderPath}";
                return;
            }

            var options = new ComponentReferenceSearchService.Options
            {
                RootFolder = _folderPath,
                IncludeSubFolders = _includeSubFolders,
                SearchPrefabs = _searchPrefabs,
                SearchScenes = _searchScenes,
                IncludeInactive = _includeInactive
            };

            try
            {
                _flatResults = ComponentReferenceSearchService.Search(
                    _selectedType,
                    options,
                    (progress, path) =>
                    {
                        if (EditorUtility.DisplayCancelableProgressBar("组件引用查找", path, progress))
                        {
                            throw new OperationCanceledException();
                        }
                    });
            }
            catch (OperationCanceledException)
            {
                _status = "查找已取消。";
                EditorUtility.ClearProgressBar();
                return;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            _tree = ComponentReferenceSearchService.BuildFolderTree(_folderPath, _flatResults);
            _expandedFolders.Clear();
            _expandedAssets.Clear();
            // 默认展开根下第一层文件夹
            if (_tree != null)
            {
                foreach (var child in _tree.Children)
                {
                    _expandedFolders.Add(child.FolderPath);
                }
            }

            int hits = _flatResults.Sum(r => r.TotalCount);
            _status = $"完成：组件 {_selectedType.Name}，命中资源 {_flatResults.Count} 个，组件实例 {hits} 处。范围 {_folderPath}";
            ShowNotification(new GUIContent(_flatResults.Count == 0 ? "未找到引用" : $"找到 {_flatResults.Count} 个资源"));
            Repaint();
        }

        private void ReSearchFromFolder(string folderPath)
        {
            _folderPath = ComponentReferenceSearchService.NormalizeFolder(folderPath);
            _folderAsset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(_folderPath);
            RunSearch();
        }

        private void ExportReport()
        {
            string report = ComponentReferenceSearchService.BuildReport(_selectedType, _folderPath, _flatResults);
            string defaultName = _selectedType != null
                ? $"ComponentRefs_{_selectedType.Name}.txt"
                : "ComponentRefs.txt";
            string savePath = EditorUtility.SaveFilePanel("导出组件引用报告", Application.dataPath, defaultName, "txt");
            if (string.IsNullOrEmpty(savePath))
            {
                return;
            }

            System.IO.File.WriteAllText(savePath, report, System.Text.Encoding.UTF8);
            ShowNotification(new GUIContent("导出成功"));
            EditorUtility.RevealInFinder(savePath);
        }

        private void ExpandAll(ComponentReferenceFolderNode node, bool expandAssets)
        {
            if (node == null)
            {
                return;
            }

            _expandedFolders.Add(node.FolderPath);
            if (expandAssets)
            {
                foreach (var asset in node.Assets)
                {
                    _expandedAssets.Add(asset.AssetPath);
                }
            }

            foreach (var child in node.Children)
            {
                ExpandAll(child, expandAssets);
            }
        }

        private bool FolderMatchesFilter(ComponentReferenceFolderNode node)
        {
            if (string.IsNullOrEmpty(_nameFilter))
            {
                return true;
            }

            foreach (var asset in node.Assets)
            {
                if (AssetMatchesFilter(asset))
                {
                    return true;
                }
            }

            foreach (var child in node.Children)
            {
                if (FolderMatchesFilter(child))
                {
                    return true;
                }
            }

            return false;
        }

        private bool AssetMatchesFilter(ComponentReferenceAssetResult asset)
        {
            if (string.IsNullOrEmpty(_nameFilter))
            {
                return true;
            }

            return asset.AssetPath.IndexOf(_nameFilter, StringComparison.OrdinalIgnoreCase) >= 0
                   || asset.AssetName.IndexOf(_nameFilter, StringComparison.OrdinalIgnoreCase) >= 0
                   || asset.Hits.Any(h => h.HierarchyPath.IndexOf(_nameFilter, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private void RefreshTypeCandidates()
        {
            _typeCandidates.Clear();
            _selectedType = null;

            if (string.IsNullOrWhiteSpace(_typeSearch) || _typeSearch.Length < 2)
            {
                return;
            }

            string key = _typeSearch.Trim();
            var types = TypeCache.GetTypesDerivedFrom<Component>();
            foreach (var t in types)
            {
                if (t == null || t.IsAbstract)
                {
                    continue;
                }

                if (t.Name.IndexOf(key, StringComparison.OrdinalIgnoreCase) < 0
                    && (t.FullName == null || t.FullName.IndexOf(key, StringComparison.OrdinalIgnoreCase) < 0))
                {
                    continue;
                }

                _typeCandidates.Add(t);
            }

            _typeCandidates = _typeCandidates
                .OrderBy(t => t.Name.Equals(key, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .Take(80)
                .ToList();

            // 精确匹配时直接选中
            var exact = _typeCandidates.FirstOrDefault(t => t.Name.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (exact != null && _typeCandidates.Count == 1)
            {
                _selectedType = exact;
            }
        }

        private static string FindMonoScriptPath(Type type)
        {
            if (type == null)
            {
                return null;
            }

            string[] guids = AssetDatabase.FindAssets($"t:MonoScript {type.Name}");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script != null && script.GetClass() == type)
                {
                    return path;
                }
            }

            return null;
        }
    }
}
