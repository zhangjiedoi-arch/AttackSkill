using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Scene = UnityEngine.SceneManagement.Scene;

namespace AttackSkill.Editor.ComponentReferenceFinder
{
    /// <summary>
    /// 在指定文件夹内批量查找使用了某组件的 Prefab / Scene。
    /// </summary>
    public static class ComponentReferenceSearchService
    {
        public class Options
        {
            public string RootFolder = "Assets";
            public bool IncludeSubFolders = true;
            public bool SearchPrefabs = true;
            public bool SearchScenes = true;
            /// <summary>为 true 时，仅统计直接挂在节点上的组件（不含子节点递归计数展示，查找本身仍递归整棵树）。</summary>
            public bool IncludeInactive = true;
        }

        public static List<ComponentReferenceAssetResult> Search(Type componentType, Options options, Action<float, string> onProgress = null)
        {
            var results = new List<ComponentReferenceAssetResult>();
            if (componentType == null || options == null)
            {
                return results;
            }

            if (!typeof(Component).IsAssignableFrom(componentType))
            {
                Debug.LogError($"类型 {componentType.Name} 不是 Component。");
                return results;
            }

            string root = NormalizeFolder(options.RootFolder);
            if (!AssetDatabase.IsValidFolder(root))
            {
                Debug.LogError($"无效文件夹：{root}");
                return results;
            }

            var assetPaths = CollectAssetPaths(root, options);
            for (int i = 0; i < assetPaths.Count; i++)
            {
                string path = assetPaths[i];
                onProgress?.Invoke((float)i / Math.Max(1, assetPaths.Count), path);

                if (path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                {
                    var result = SearchPrefab(path, componentType, options.IncludeInactive);
                    if (result != null && result.Hits.Count > 0)
                    {
                        results.Add(result);
                    }
                }
                else if (path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                {
                    var result = SearchScene(path, componentType, options.IncludeInactive);
                    if (result != null && result.Hits.Count > 0)
                    {
                        results.Add(result);
                    }
                }
            }

            results.Sort((a, b) => string.Compare(a.AssetPath, b.AssetPath, StringComparison.OrdinalIgnoreCase));
            return results;
        }

        public static ComponentReferenceFolderNode BuildFolderTree(string rootFolder, List<ComponentReferenceAssetResult> results)
        {
            string root = NormalizeFolder(rootFolder);
            var rootNode = new ComponentReferenceFolderNode
            {
                FolderPath = root,
                FolderName = GetFolderDisplayName(root)
            };

            if (results == null || results.Count == 0)
            {
                return rootNode;
            }

            var nodeMap = new Dictionary<string, ComponentReferenceFolderNode>(StringComparer.OrdinalIgnoreCase)
            {
                [root] = rootNode
            };

            foreach (var asset in results)
            {
                string folder = NormalizeFolder(asset.FolderPath);
                EnsureFolderNode(root, folder, nodeMap);
                nodeMap[folder].Assets.Add(asset);
            }

            SortTree(rootNode);
            return rootNode;
        }

        private static List<string> CollectAssetPaths(string root, Options options)
        {
            var folders = new[] { root };
            var paths = new List<string>();

            if (options.SearchPrefabs)
            {
                string[] guids = AssetDatabase.FindAssets("t:Prefab", folders);
                AppendFiltered(paths, guids, root, options.IncludeSubFolders, ".prefab");
            }

            if (options.SearchScenes)
            {
                string[] guids = AssetDatabase.FindAssets("t:Scene", folders);
                AppendFiltered(paths, guids, root, options.IncludeSubFolders, ".unity");
            }

            paths.Sort(StringComparer.OrdinalIgnoreCase);
            return paths;
        }

        private static void AppendFiltered(
            List<string> paths,
            string[] guids,
            string root,
            bool includeSubFolders,
            string extension)
        {
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path) || !path.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!includeSubFolders)
                {
                    string dir = NormalizeFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
                    if (!string.Equals(dir, root, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                }

                paths.Add(path);
            }
        }

        private static ComponentReferenceAssetResult SearchPrefab(string path, Type componentType, bool includeInactive)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                return null;
            }

            var result = CreateAssetResult(path);
            CollectHitsFromGameObject(prefab, prefab.name, componentType, includeInactive, result);
            return result;
        }

        private static ComponentReferenceAssetResult SearchScene(string path, Type componentType, bool includeInactive)
        {
            Scene scene;
            bool openedByUs = false;

            // 已打开的场景直接扫，避免重复打开/误关
            if (TryGetLoadedScene(path, out scene))
            {
                openedByUs = false;
            }
            else
            {
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                openedByUs = true;
            }

            try
            {
                var result = CreateAssetResult(path);
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    return result;
                }

                GameObject[] roots = scene.GetRootGameObjects();
                for (int i = 0; i < roots.Length; i++)
                {
                    CollectHitsFromGameObject(roots[i], roots[i].name, componentType, includeInactive, result);
                }

                return result;
            }
            finally
            {
                if (openedByUs && scene.IsValid())
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static bool TryGetLoadedScene(string assetPath, out Scene scene)
        {
            string normalized = assetPath.Replace('\\', '/');
            int count = SceneManager.sceneCount;
            for (int i = 0; i < count; i++)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (s.IsValid() && s.isLoaded &&
                    string.Equals(s.path.Replace('\\', '/'), normalized, StringComparison.OrdinalIgnoreCase))
                {
                    scene = s;
                    return true;
                }
            }

            scene = default;
            return false;
        }

        private static void CollectHitsFromGameObject(
            GameObject root,
            string hierarchyPath,
            Type componentType,
            bool includeInactive,
            ComponentReferenceAssetResult result)
        {
            if (includeInactive || root.activeSelf)
            {
                var comps = root.GetComponents(componentType);
                if (comps != null && comps.Length > 0)
                {
                    int valid = 0;
                    for (int i = 0; i < comps.Length; i++)
                    {
                        if (comps[i] != null)
                        {
                            valid++;
                        }
                    }

                    if (valid > 0)
                    {
                        result.Hits.Add(new ComponentReferenceHit(
                            result.AssetPath,
                            hierarchyPath,
                            componentType.Name,
                            valid));
                    }
                }
            }

            var t = root.transform;
            for (int i = 0; i < t.childCount; i++)
            {
                var child = t.GetChild(i).gameObject;
                if (!includeInactive && !child.activeSelf)
                {
                    continue;
                }

                CollectHitsFromGameObject(child, hierarchyPath + "/" + child.name, componentType, includeInactive, result);
            }
        }

        private static ComponentReferenceAssetResult CreateAssetResult(string path)
        {
            string folder = NormalizeFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
            return new ComponentReferenceAssetResult
            {
                AssetPath = path,
                AssetName = Path.GetFileNameWithoutExtension(path),
                FolderPath = folder
            };
        }

        private static void EnsureFolderNode(
            string root,
            string folder,
            Dictionary<string, ComponentReferenceFolderNode> nodeMap)
        {
            if (nodeMap.ContainsKey(folder))
            {
                return;
            }

            // 从 root 逐级创建中间文件夹节点
            string relative = folder;
            if (folder.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                relative = folder.Length == root.Length
                    ? string.Empty
                    : folder.Substring(root.Length).TrimStart('/');
            }

            string current = root;
            if (!string.IsNullOrEmpty(relative))
            {
                string[] parts = relative.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string part in parts)
                {
                    string parent = current;
                    current = current + "/" + part;
                    if (nodeMap.ContainsKey(current))
                    {
                        continue;
                    }

                    var node = new ComponentReferenceFolderNode
                    {
                        FolderPath = current,
                        FolderName = part
                    };
                    nodeMap[current] = node;
                    nodeMap[parent].Children.Add(node);
                }
            }

            if (!nodeMap.ContainsKey(folder))
            {
                // folder 在 root 之外时挂到 root 下
                var orphan = new ComponentReferenceFolderNode
                {
                    FolderPath = folder,
                    FolderName = GetFolderDisplayName(folder)
                };
                nodeMap[folder] = orphan;
                nodeMap[root].Children.Add(orphan);
            }
        }

        private static void SortTree(ComponentReferenceFolderNode node)
        {
            node.Children = node.Children
                .OrderBy(c => c.FolderName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            node.Assets = node.Assets
                .OrderBy(a => a.AssetName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            for (int i = 0; i < node.Children.Count; i++)
            {
                SortTree(node.Children[i]);
            }
        }

        public static string NormalizeFolder(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return "Assets";
            }

            path = path.Replace('\\', '/').TrimEnd('/');
            return string.IsNullOrEmpty(path) ? "Assets" : path;
        }

        private static string GetFolderDisplayName(string folder)
        {
            int idx = folder.LastIndexOf('/');
            return idx >= 0 && idx < folder.Length - 1 ? folder.Substring(idx + 1) : folder;
        }

        /// <summary>
        /// 导出纯文本报告，便于特殊处理时对照。
        /// </summary>
        public static string BuildReport(Type componentType, string rootFolder, List<ComponentReferenceAssetResult> results)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"组件引用查找报告");
            sb.AppendLine($"组件：{(componentType != null ? componentType.FullName : "未知")}");
            sb.AppendLine($"范围：{NormalizeFolder(rootFolder)}");
            sb.AppendLine($"命中资源：{(results != null ? results.Count : 0)}");
            sb.AppendLine(new string('-', 48));

            if (results == null)
            {
                return sb.ToString();
            }

            foreach (var asset in results)
            {
                sb.AppendLine($"{asset.AssetPath}  （{asset.TotalCount}）");
                foreach (var hit in asset.Hits)
                {
                    sb.AppendLine($"  - {hit.HierarchyPath}");
                }
            }

            return sb.ToString();
        }
    }
}
