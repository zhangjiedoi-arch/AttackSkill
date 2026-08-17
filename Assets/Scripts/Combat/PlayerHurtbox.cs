using UnityEngine;

namespace AttackSkill.Combat
{
    /// <summary>
    /// 玩家受击盒：OverlapSphere 扫不到纯 CharacterController，必须有命名 Trigger Capsule。
    /// </summary>
    public static class PlayerHurtbox
    {
        public const string ObjectName = "PlayerHurtbox";

        /// <summary>保证 root 下存在可用的 <see cref="ObjectName"/>（不因其它 Collider 而跳过）。</summary>
        public static void Ensure(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            var cc = root.GetComponent<CharacterController>();
            Transform existing = FindHurtbox(root.transform);
            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
            }
            else
            {
                go = new GameObject(ObjectName);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
            }

            go.layer = root.layer;
            if (!go.activeSelf)
            {
                go.SetActive(true);
            }

            var capsule = go.GetComponent<CapsuleCollider>();
            if (capsule == null)
            {
                capsule = go.AddComponent<CapsuleCollider>();
            }

            capsule.enabled = true;
            capsule.isTrigger = true;
            if (cc != null)
            {
                capsule.height = Mathf.Max(0.5f, cc.height);
                capsule.radius = Mathf.Max(0.2f, cc.radius);
                capsule.center = cc.center;
            }
            else
            {
                capsule.height = 1.8f;
                capsule.radius = 0.4f;
                capsule.center = new Vector3(0f, 0.9f, 0f);
            }
        }

        static Transform FindHurtbox(Transform root)
        {
            if (root == null)
            {
                return null;
            }

            Transform direct = root.Find(ObjectName);
            if (direct != null)
            {
                return direct;
            }

            // 兼容曾挂在子层级的情况
            var all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == ObjectName)
                {
                    return all[i];
                }
            }

            return null;
        }
    }
}
