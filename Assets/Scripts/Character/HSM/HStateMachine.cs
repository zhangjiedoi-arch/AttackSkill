using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace AttackSkill.Character.HSM
{
    /// <summary>
    /// 层级状态机：切换时只 Exit/Enter 与 LCA 不同的路径。
    /// </summary>
    public class HStateMachine
    {
        readonly List<HState> _updatePath = new List<HState>(8);
        readonly List<HState> _fixedPath = new List<HState>(8);
        readonly List<HState> _fromPath = new List<HState>(8);
        readonly List<HState> _toPath = new List<HState>(8);
        readonly List<HState> _pathScratch = new List<HState>(8);
        readonly StringBuilder _pathBuilder = new StringBuilder(64);
        string _cachedPath = "<null>";
        bool _pathDirty = true;

        public HState Current { get; private set; }

        public string CurrentPath
        {
            get
            {
                if (!_pathDirty)
                {
                    return _cachedPath;
                }

                if (Current == null)
                {
                    _cachedPath = "<null>";
                    _pathDirty = false;
                    return _cachedPath;
                }

                Current.GetPathFromRootNonAlloc(_pathScratch);
                _pathBuilder.Length = 0;
                for (int i = 0; i < _pathScratch.Count; i++)
                {
                    if (i > 0)
                    {
                        _pathBuilder.Append('/');
                    }

                    _pathBuilder.Append(_pathScratch[i].Name);
                }

                _cachedPath = _pathBuilder.ToString();
                _pathDirty = false;
                return _cachedPath;
            }
        }

        public void Start(HState initial)
        {
            Current = initial;
            _pathDirty = true;
            initial.GetPathFromRootNonAlloc(_toPath);
            for (int i = 0; i < _toPath.Count; i++)
            {
                _toPath[i].OnEnter();
            }
        }

        public void ChangeState(HState next, bool allowReenter = false)
        {
            if (next == null)
            {
                return;
            }

            if (next == Current)
            {
                if (!allowReenter)
                {
                    return;
                }

                next.OnExit();
                next.OnEnter();
                _pathDirty = true;
                return;
            }

            Current.GetPathFromRootNonAlloc(_fromPath);
            next.GetPathFromRootNonAlloc(_toPath);

            int lca = 0;
            int max = Mathf.Min(_fromPath.Count, _toPath.Count);
            while (lca < max && _fromPath[lca] == _toPath[lca])
            {
                lca++;
            }

            for (int i = _fromPath.Count - 1; i >= lca; i--)
            {
                _fromPath[i].OnExit();
            }

            Current = next;
            _pathDirty = true;

            for (int i = lca; i < _toPath.Count; i++)
            {
                _toPath[i].OnEnter();
            }
        }

        public void Update(float deltaTime)
        {
            if (Current == null)
            {
                return;
            }

            HState leaf = Current;
            leaf.GetPathFromRootNonAlloc(_updatePath);
            for (int i = 0; i < _updatePath.Count; i++)
            {
                _updatePath[i].OnUpdate(deltaTime);
                if (Current != leaf)
                {
                    return;
                }
            }
        }

        public void FixedUpdate(float deltaTime)
        {
            if (Current == null)
            {
                return;
            }

            HState leaf = Current;
            leaf.GetPathFromRootNonAlloc(_fixedPath);
            for (int i = 0; i < _fixedPath.Count; i++)
            {
                _fixedPath[i].OnFixedUpdate(deltaTime);
                if (Current != leaf)
                {
                    return;
                }
            }
        }
    }
}
