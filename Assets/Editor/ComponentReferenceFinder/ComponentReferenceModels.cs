using System;
using System.Collections.Generic;
using UnityEngine;

namespace AttackSkill.Editor.ComponentReferenceFinder
{
    /// <summary>
    /// 单条组件引用命中：某个资源内的某个节点。
    /// </summary>
    public class ComponentReferenceHit
    {
        public string AssetPath;
        public string HierarchyPath;
        public string ComponentTypeName;
        public int InstanceCountOnNode;

        public ComponentReferenceHit(string assetPath, string hierarchyPath, string componentTypeName, int instanceCountOnNode = 1)
        {
            AssetPath = assetPath;
            HierarchyPath = hierarchyPath;
            ComponentTypeName = componentTypeName;
            InstanceCountOnNode = instanceCountOnNode;
        }
    }

    /// <summary>
    /// 某个资源（Prefab / Scene）的引用汇总。
    /// </summary>
    public class ComponentReferenceAssetResult
    {
        public string AssetPath;
        public string AssetName;
        public string FolderPath;
        public List<ComponentReferenceHit> Hits = new List<ComponentReferenceHit>();

        public int TotalCount
        {
            get
            {
                int sum = 0;
                for (int i = 0; i < Hits.Count; i++)
                {
                    sum += Hits[i].InstanceCountOnNode;
                }

                return sum;
            }
        }
    }

    /// <summary>
    /// 按文件夹聚合的结果节点，支持逐级展开。
    /// </summary>
    public class ComponentReferenceFolderNode
    {
        public string FolderPath;
        public string FolderName;
        public List<ComponentReferenceFolderNode> Children = new List<ComponentReferenceFolderNode>();
        public List<ComponentReferenceAssetResult> Assets = new List<ComponentReferenceAssetResult>();

        public int AssetCount
        {
            get
            {
                int count = Assets.Count;
                for (int i = 0; i < Children.Count; i++)
                {
                    count += Children[i].AssetCount;
                }

                return count;
            }
        }

        public int HitCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < Assets.Count; i++)
                {
                    count += Assets[i].TotalCount;
                }

                for (int i = 0; i < Children.Count; i++)
                {
                    count += Children[i].HitCount;
                }

                return count;
            }
        }
    }
}
