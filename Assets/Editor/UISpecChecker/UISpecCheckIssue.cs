using UnityEngine;

namespace AttackSkill.Editor.UISpecChecker
{
    public enum UISpecIssueSeverity
    {
        Info,
        Warning,
        Error
    }

    public enum UISpecIssueCategory
    {
        SizeEven,
        NineSliceTile,
        Missing,
        Naming
    }

    /// <summary>
    /// 单条 UI 规范检查结果。
    /// </summary>
    public class UISpecCheckIssue
    {
        public UISpecIssueSeverity Severity;
        public UISpecIssueCategory Category;
        public string Path;
        public string Message;
        public Object Target;
        public Object Context;

        public UISpecCheckIssue(
            UISpecIssueSeverity severity,
            UISpecIssueCategory category,
            string path,
            string message,
            Object target,
            Object context = null)
        {
            Severity = severity;
            Category = category;
            Path = path;
            Message = message;
            Target = target;
            Context = context;
        }
    }
}
