using System.Collections.Generic;

namespace AttackSkill.Character.HSM
{
    /// <summary>
    /// 层级状态基类。子状态通过 Parent 挂到父状态上。
    /// </summary>
    public abstract class HState
    {
        public string Name { get; }
        public HState Parent { get; private set; }
        public HStateMachine Machine { get; private set; }

        protected HState(string name)
        {
            Name = name;
        }

        internal void Bind(HStateMachine machine, HState parent)
        {
            Machine = machine;
            Parent = parent;
        }

        public virtual void OnEnter() { }
        public virtual void OnExit() { }
        public virtual void OnUpdate(float deltaTime) { }
        public virtual void OnFixedUpdate(float deltaTime) { }

        public List<HState> GetPathFromRoot()
        {
            var path = new List<HState>(8);
            GetPathFromRootNonAlloc(path);
            return path;
        }

        public void GetPathFromRootNonAlloc(List<HState> buffer)
        {
            buffer.Clear();
            for (HState s = this; s != null; s = s.Parent)
            {
                buffer.Add(s);
            }

            buffer.Reverse();
        }

        public bool IsInHierarchy(HState ancestor)
        {
            for (HState s = this; s != null; s = s.Parent)
            {
                if (s == ancestor)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
