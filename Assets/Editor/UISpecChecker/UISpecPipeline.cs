using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AttackSkill.Editor.UIBinding;
using AttackSkill.UI;
using UnityEditor;
using UnityEngine;

namespace AttackSkill.Editor.UISpecChecker
{
    /// <summary>
    /// UISpec + 绑定完整性流水线。支持菜单与 -executeMethod 批处理。
    /// </summary>
    public static class UISpecPipeline
    {
        const string PrefabFolder = "Assets/Prefabs/UI";

        [MenuItem("工具/UI/UISpec 流水线检查", false, 55)]
        public static void ValidateFromMenu()
        {
            bool ok = ValidateAll(logToConsole: true, showDialog: true);
            if (!ok)
            {
                Debug.LogError("[UISpecPipeline] 检查未通过（存在 Error）。CI 请使用 ValidateAllBatch。");
            }
        }

        /// <summary>
        /// 批处理入口：
        /// Unity.exe -batchmode -projectPath ... -executeMethod AttackSkill.Editor.UISpecChecker.UISpecPipeline.ValidateAllBatch
        /// </summary>
        public static void ValidateAllBatch()
        {
            bool ok = ValidateAll(logToConsole: true, showDialog: false);
            EditorApplication.Exit(ok ? 0 : 1);
        }

        public static bool ValidateAll(bool logToConsole, bool showDialog)
        {
            var options = new UISpecCheckService.Options
            {
                CheckSizeEven = true,
                CheckNineSlice = true,
                CheckMissing = true,
                CheckNaming = true
            };

            var roots = new List<GameObject>();
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null)
                {
                    roots.Add(go);
                }
            }

            var issues = UISpecCheckService.CheckRoots(roots, options);
            issues.AddRange(CheckGeneratedBindings());

            int err = issues.Count(i => i.Severity == UISpecIssueSeverity.Error);
            int warn = issues.Count(i => i.Severity == UISpecIssueSeverity.Warning);

            var sb = new StringBuilder();
            sb.AppendLine($"[UISpecPipeline] Prefab={roots.Count} Error={err} Warning={warn}");
            foreach (var issue in issues.OrderByDescending(i => i.Severity))
            {
                sb.AppendLine($"{issue.Severity} | {issue.Category} | {issue.Path} | {issue.Message}");
            }

            if (logToConsole)
            {
                if (err > 0)
                {
                    Debug.LogError(sb.ToString());
                }
                else
                {
                    Debug.Log(sb.ToString());
                }
            }

            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "UISpec 流水线",
                    $"检查 {roots.Count} 个 UI Prefab。\nError={err}  Warning={warn}\n详见 Console。",
                    "确定");
            }

            return err == 0;
        }

        static List<UISpecCheckIssue> CheckGeneratedBindings()
        {
            var list = new List<UISpecCheckIssue>();
            foreach (var spec in UIBindingSpecCatalog.All)
            {
                string path = $"{PrefabFolder}/{spec.PrefabName}.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    list.Add(new UISpecCheckIssue(
                        UISpecIssueSeverity.Error,
                        UISpecIssueCategory.Missing,
                        path,
                        $"缺少 Prefab：{spec.PrefabName}",
                        null));
                    continue;
                }

                var view = prefab.GetComponent(spec.ClassName);
                if (view == null)
                {
                    var mbs = prefab.GetComponents<MonoBehaviour>();
                    for (int i = 0; i < mbs.Length; i++)
                    {
                        if (mbs[i] != null && mbs[i].GetType().Name == spec.ClassName)
                        {
                            view = mbs[i];
                            break;
                        }
                    }
                }

                if (view == null)
                {
                    list.Add(new UISpecCheckIssue(
                        UISpecIssueSeverity.Error,
                        UISpecIssueCategory.Missing,
                        path,
                        $"Prefab 未挂 {spec.ClassName}",
                        prefab));
                    continue;
                }

                var so = new SerializedObject(view);
                foreach (var field in spec.Fields)
                {
                    var prop = so.FindProperty(field.FieldName);
                    if (prop == null)
                    {
                        list.Add(new UISpecCheckIssue(
                            UISpecIssueSeverity.Error,
                            UISpecIssueCategory.Missing,
                            path + "/" + field.FieldName,
                            $"缺少生成字段 {field.FieldName}（请运行「工具/UI/生成绑定代码」）",
                            view));
                        continue;
                    }

                    if (field.CollectAll)
                    {
                        if (!prop.isArray || prop.arraySize == 0)
                        {
                            list.Add(new UISpecCheckIssue(
                                UISpecIssueSeverity.Error,
                                UISpecIssueCategory.Missing,
                                path + "/" + field.FieldName,
                                $"绑定数组 {field.FieldName} 为空（请「同步绑定到 Prefab」）",
                                view));
                        }

                        continue;
                    }

                    if (prop.objectReferenceValue == null)
                    {
                        list.Add(new UISpecCheckIssue(
                            UISpecIssueSeverity.Error,
                            UISpecIssueCategory.Missing,
                            path + "/" + field.NodeName,
                            $"绑定未赋值：{field.FieldName} ← {field.NodeName}",
                            view));
                    }
                }
            }

            return list;
        }
    }
}
