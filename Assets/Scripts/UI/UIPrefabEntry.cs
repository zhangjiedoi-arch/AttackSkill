using System;
using UnityEngine;

namespace AttackSkill.UI
{
    [Serializable]
    public class UIPrefabEntry
    {
        public UIId id;
        public UILayer layer = UILayer.Panel;
        public GameObject prefab;
        [Tooltip("同一 Id 只保留一个实例，关闭时隐藏复用")]
        public bool singleton = true;
        [Tooltip("为 false 时保留 Prefab 自身锚点与尺寸（战斗 HUD 用）")]
        public bool stretchToParent = true;
        [Tooltip("为 false 时打开本 Panel 不关闭其它 Panel（多块 HUD 并存）")]
        public bool closesOtherPanels = true;
    }
}
