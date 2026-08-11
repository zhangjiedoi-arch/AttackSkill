using UnityEditor;
using UnityEngine;

namespace AttackSkill.Editor
{
    /// <summary>
    /// 给海岛/场景模型正确挂静态 MeshCollider。
    /// 常见失败原因：挂在无 MeshFilter 的根节点上，sharedMesh 为空。
    /// </summary>
    public static class BeachSceneColliderMenu
    {
        [MenuItem("Tools/AttackSkill/海滩场景/给选中物体挂MeshCollider（子网格）")]
        public static void AddMeshCollidersOnChildren()
        {
            var roots = Selection.gameObjects;
            if (roots == null || roots.Length == 0)
            {
                EditorUtility.DisplayDialog("海滩场景", "请先在 Hierarchy 选中海岛根物体。", "OK");
                return;
            }

            var added = 0;
            var updated = 0;
            var skipped = 0;

            foreach (var root in roots)
            {
                if (root == null)
                    continue;

                Undo.RegisterFullObjectHierarchyUndo(root, "Add Mesh Colliders");

                // 根上若误挂了空 MeshCollider，先清掉以免误导
                var rootMc = root.GetComponent<MeshCollider>();
                if (rootMc != null && root.GetComponent<MeshFilter>() == null)
                {
                    Undo.DestroyObjectImmediate(rootMc);
                }

                var filters = root.GetComponentsInChildren<MeshFilter>(true);
                foreach (var filter in filters)
                {
                    if (filter == null || filter.sharedMesh == null)
                    {
                        skipped++;
                        continue;
                    }

                    var n = filter.gameObject.name.ToLowerInvariant();
                    if (n.Contains("sky") || n.Contains("water"))
                    {
                        skipped++;
                        continue;
                    }

                    var mc = filter.GetComponent<MeshCollider>();
                    if (mc == null)
                    {
                        mc = Undo.AddComponent<MeshCollider>(filter.gameObject);
                        added++;
                    }
                    else
                    {
                        updated++;
                    }

                    mc.sharedMesh = filter.sharedMesh;
                    mc.convex = false;
                    mc.isTrigger = false;
                }
            }

            EditorUtility.DisplayDialog(
                "海滩场景",
                $"MeshCollider 新增 {added}，更新 {updated}，跳过 {skipped}\n\n" +
                "接着检查：\n" +
                "1. Scene 开 Gizmos，能看到绿色碰撞线框\n" +
                "2. Convex / Is Trigger 都不要勾\n" +
                "3. 角色放到海岛表面上方再 Play\n" +
                "4. 若仍穿透：在岛面位置临时放一个拉扁的 Cube 作地板测试",
                "OK");
        }

        [MenuItem("Tools/AttackSkill/海滩场景/给选中物体挂MeshCollider（子网格）", true)]
        public static bool Validate()
        {
            return Selection.gameObjects != null && Selection.gameObjects.Length > 0;
        }
    }
}
