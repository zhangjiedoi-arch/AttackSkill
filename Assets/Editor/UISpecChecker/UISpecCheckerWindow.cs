using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AttackSkill.Editor.UISpecChecker
{
    /// <summary>
    /// UI 拼接规范检查窗口：偶数尺寸 / 九宫平铺 / Missing / 命名规范。
    /// 菜单：工具 / 资源检查 / UI拼接规范检查
    /// </summary>
    public class UISpecCheckerWindow : EditorWindow
    {
        private enum ScanScope
        {
            Selection,
            OpenScenes,
            PrefabFolder
        }

        private ScanScope _scope = ScanScope.Selection;
        private DefaultAsset _prefabFolder;
        private UISpecCheckService.Options _options = new UISpecCheckService.Options();

        private Vector2 _scroll;
        private List<UISpecCheckIssue> _issues = new List<UISpecCheckIssue>();
        private string _status = "就绪。选择检查范围后点击「开始检查」。";
        private int _filterFlags = -1; // 全选类别
        private UISpecIssueSeverity? _severityFilter;

        private static readonly string[] CategoryLabels =
        {
            "偶数宽高",
            "九宫平铺",
            "资源缺失",
            "命名规范"
        };

        [MenuItem("工具/资源检查/UI拼接规范检查")]
        public static void Open()
        {
            var window = GetWindow<UISpecCheckerWindow>();
            window.titleContent = new GUIContent("UI拼接规范检查");
            window.minSize = new Vector2(720, 480);
            window.Show();
        }

        private void OnGUI()
        {
            DrawToolbar();
            EditorGUILayout.Space(4);
            DrawOptions();
            EditorGUILayout.Space(6);
            DrawActions();
            EditorGUILayout.Space(4);
            DrawFilterBar();
            EditorGUILayout.Space(2);
            DrawResults();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.LabelField("UI 拼接规范检查", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "检查项：\n" +
                "1. 界面宽高是否均为偶数（避免拼接缝隙）\n" +
                "2. 九宫/平铺图片是否平铺次数过多（影响性能）\n" +
                "3. 资源/脚本是否 Missing\n" +
                "4. 组件命名是否符合「前缀+驼峰」：btnAdd、imgIcon、txtTitle 等",
                MessageType.Info);
        }

        private void DrawOptions()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("检查范围", EditorStyles.boldLabel);
                _scope = (ScanScope)EditorGUILayout.EnumPopup("范围", _scope);

                if (_scope == ScanScope.PrefabFolder)
                {
                    _prefabFolder = (DefaultAsset)EditorGUILayout.ObjectField(
                        "Prefab 文件夹",
                        _prefabFolder,
                        typeof(DefaultAsset),
                        false);

                    if (_prefabFolder != null)
                    {
                        string path = AssetDatabase.GetAssetPath(_prefabFolder);
                        if (!AssetDatabase.IsValidFolder(path))
                        {
                            EditorGUILayout.HelpBox("请选择 Project 中的文件夹。", MessageType.Warning);
                        }
                    }
                }

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("检查开关", EditorStyles.boldLabel);
                _options.CheckSizeEven = EditorGUILayout.ToggleLeft("宽高均为偶数", _options.CheckSizeEven);
                _options.CheckNineSlice = EditorGUILayout.ToggleLeft("九宫/平铺次数", _options.CheckNineSlice);
                _options.CheckMissing = EditorGUILayout.ToggleLeft("资源缺失 (Missing)", _options.CheckMissing);
                _options.CheckNaming = EditorGUILayout.ToggleLeft("组件命名规范", _options.CheckNaming);

                using (new EditorGUI.DisabledScope(!_options.CheckNineSlice))
                {
                    _options.MaxNineSliceTiles = EditorGUILayout.Slider(
                        new GUIContent("九宫平铺上限", "中心区域在单方向上的平铺次数超过该值则告警"),
                        _options.MaxNineSliceTiles,
                        2f,
                        32f);
                }
            }
        }

        private void DrawActions()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.backgroundColor = new Color(0.45f, 0.85f, 0.55f);
                if (GUILayout.Button("开始检查", GUILayout.Height(28)))
                {
                    RunCheck();
                }

                GUI.backgroundColor = Color.white;
                if (GUILayout.Button("清空结果", GUILayout.Width(100), GUILayout.Height(28)))
                {
                    _issues.Clear();
                    _status = "结果已清空。";
                }
            }

            EditorGUILayout.LabelField(_status, EditorStyles.miniLabel);
        }

        private void DrawFilterBar()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("筛选", GUILayout.Width(36));
                _filterFlags = EditorGUILayout.MaskField(_filterFlags, CategoryLabels, GUILayout.Width(220));

                EditorGUILayout.LabelField("级别", GUILayout.Width(36));
                string[] sev = { "全部", "Error", "Warning", "Info" };
                int sevIndex = 0;
                if (_severityFilter == UISpecIssueSeverity.Error) sevIndex = 1;
                else if (_severityFilter == UISpecIssueSeverity.Warning) sevIndex = 2;
                else if (_severityFilter == UISpecIssueSeverity.Info) sevIndex = 3;
                int newSev = EditorGUILayout.Popup(sevIndex, sev, GUILayout.Width(100));
                _severityFilter = newSev switch
                {
                    1 => UISpecIssueSeverity.Error,
                    2 => UISpecIssueSeverity.Warning,
                    3 => UISpecIssueSeverity.Info,
                    _ => (UISpecIssueSeverity?)null
                };

                GUILayout.FlexibleSpace();
                int shown = GetFilteredIssues().Count();
                EditorGUILayout.LabelField($"显示 {shown} / {_issues.Count}", GUILayout.Width(120));
            }
        }

        private void DrawResults()
        {
            using (var scroll = new EditorGUILayout.ScrollViewScope(_scroll))
            {
                _scroll = scroll.scrollPosition;

                var filtered = GetFilteredIssues().ToList();
                if (filtered.Count == 0)
                {
                    EditorGUILayout.LabelField(_issues.Count == 0 ? "暂无检查结果。" : "当前筛选下无结果。");
                    return;
                }

                foreach (var issue in filtered)
                {
                    DrawIssueRow(issue);
                }
            }
        }

        private void DrawIssueRow(UISpecCheckIssue issue)
        {
            Color bg = GUI.backgroundColor;
            GUI.backgroundColor = issue.Severity switch
            {
                UISpecIssueSeverity.Error => new Color(1f, 0.55f, 0.55f),
                UISpecIssueSeverity.Warning => new Color(1f, 0.9f, 0.55f),
                _ => new Color(0.75f, 0.85f, 1f)
            };

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUI.backgroundColor = bg;

                using (new EditorGUILayout.HorizontalScope())
                {
                    string tag = $"[{SeverityLabel(issue.Severity)}] [{CategoryLabel(issue.Category)}]";
                    EditorGUILayout.LabelField(tag, EditorStyles.boldLabel, GUILayout.Width(160));

                    if (GUILayout.Button("定位", GUILayout.Width(48)))
                    {
                        PingTarget(issue);
                    }

                    if (GUILayout.Button("选中", GUILayout.Width(48)))
                    {
                        SelectTarget(issue);
                    }
                }

                EditorGUILayout.LabelField(issue.Path, EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.LabelField(issue.Message, EditorStyles.wordWrappedLabel);
            }
        }

        private IEnumerable<UISpecCheckIssue> GetFilteredIssues()
        {
            return _issues.Where(issue =>
            {
                int bit = 1 << (int)issue.Category;
                if (_filterFlags != -1 && (_filterFlags & bit) == 0)
                {
                    return false;
                }

                if (_severityFilter.HasValue && issue.Severity != _severityFilter.Value)
                {
                    return false;
                }

                return true;
            });
        }

        private void RunCheck()
        {
            var roots = CollectRoots();
            if (roots.Count == 0)
            {
                _issues.Clear();
                _status = "未找到可检查的 UI 对象。请确认范围选择是否正确。";
                ShowNotification(new GUIContent("未找到检查目标"));
                return;
            }

            try
            {
                EditorUtility.DisplayProgressBar("UI拼接规范检查", "正在检查…", 0.35f);
                _issues = UISpecCheckService.CheckRoots(roots, _options);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            int err = _issues.Count(i => i.Severity == UISpecIssueSeverity.Error);
            int warn = _issues.Count(i => i.Severity == UISpecIssueSeverity.Warning);
            _status = $"检查完成：根节点 {roots.Count} 个，问题 {_issues.Count} 条（Error {err} / Warning {warn}）。";
            ShowNotification(new GUIContent(_issues.Count == 0 ? "全部通过" : $"发现 {_issues.Count} 条问题"));
            Repaint();
        }

        private List<GameObject> CollectRoots()
        {
            switch (_scope)
            {
                case ScanScope.Selection:
                    return CollectFromSelection();
                case ScanScope.OpenScenes:
                    return CollectFromOpenScenes();
                case ScanScope.PrefabFolder:
                    return CollectFromPrefabFolder();
                default:
                    return new List<GameObject>();
            }
        }

        private static List<GameObject> CollectFromSelection()
        {
            var list = new List<GameObject>();
            foreach (var obj in Selection.objects)
            {
                if (obj is GameObject go)
                {
                    list.Add(go);
                    continue;
                }

                string path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    list.Add(prefab);
                }
            }

            return list.Distinct().ToList();
        }

        private static List<GameObject> CollectFromOpenScenes()
        {
            var list = new List<GameObject>();
            int sceneCount = UnityEngine.SceneManagement.SceneManager.sceneCount;
            for (int s = 0; s < sceneCount; s++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(s);
                if (!scene.isLoaded)
                {
                    continue;
                }

                foreach (var root in scene.GetRootGameObjects())
                {
                    // 只检查带 Canvas / RectTransform 的 UI 树，避免扫到无关 3D 物体
                    if (root.GetComponentInChildren<Canvas>(true) != null ||
                        root.GetComponentInChildren<RectTransform>(true) != null)
                    {
                        list.Add(root);
                    }
                }
            }

            return list;
        }

        private List<GameObject> CollectFromPrefabFolder()
        {
            var list = new List<GameObject>();
            if (_prefabFolder == null)
            {
                return list;
            }

            string folder = AssetDatabase.GetAssetPath(_prefabFolder);
            if (!AssetDatabase.IsValidFolder(folder))
            {
                return list;
            }

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (EditorUtility.DisplayCancelableProgressBar(
                        "收集 Prefab",
                        path,
                        (float)i / Math.Max(1, guids.Length)))
                {
                    break;
                }

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    continue;
                }

                if (prefab.GetComponentInChildren<RectTransform>(true) == null &&
                    prefab.GetComponentInChildren<Canvas>(true) == null)
                {
                    continue;
                }

                list.Add(prefab);
            }

            EditorUtility.ClearProgressBar();
            return list;
        }

        private static void PingTarget(UISpecCheckIssue issue)
        {
            UnityEngine.Object target = issue.Context != null ? issue.Context : issue.Target;
            if (target == null)
            {
                return;
            }

            EditorGUIUtility.PingObject(target);
            Selection.activeObject = issue.Target;
        }

        private static void SelectTarget(UISpecCheckIssue issue)
        {
            if (issue.Target == null)
            {
                return;
            }

            Selection.activeObject = issue.Target;
            EditorGUIUtility.PingObject(issue.Target);

            // Prefab 资源：尝试打开 Prefab 编辑模式并高亮
            string assetPath = AssetDatabase.GetAssetPath(issue.Target);
            if (!string.IsNullOrEmpty(assetPath) && assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                AssetDatabase.OpenAsset(issue.Target);
            }
        }

        private static string SeverityLabel(UISpecIssueSeverity severity)
        {
            return severity switch
            {
                UISpecIssueSeverity.Error => "错误",
                UISpecIssueSeverity.Warning => "警告",
                _ => "信息"
            };
        }

        private static string CategoryLabel(UISpecIssueCategory category)
        {
            return category switch
            {
                UISpecIssueCategory.SizeEven => "偶数宽高",
                UISpecIssueCategory.NineSliceTile => "九宫平铺",
                UISpecIssueCategory.Missing => "资源缺失",
                UISpecIssueCategory.Naming => "命名规范",
                _ => category.ToString()
            };
        }
    }
}
