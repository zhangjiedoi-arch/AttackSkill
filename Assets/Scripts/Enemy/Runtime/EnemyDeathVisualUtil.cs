using System.Collections.Generic;
using UnityEngine;

namespace AttackSkill.Enemy
{
    /// <summary>死亡表现共用：材质加载、贴图拷贝、碰撞关闭。</summary>
    public static class EnemyDeathVisualUtil
    {
        public const string GoldShaderName = "AttackSkill/Enemy/DeathGold";
        public const string DissolveShaderName = "AttackSkill/Enemy/DeathDissolve";
        public const string GoldMatResourcesPath = "Enemy/Mat_EnemyDeathGold";
        public const string DissolveMatResourcesPath = "Enemy/Mat_EnemyDeathDissolve";

        static readonly string[] AlbedoPropertyNames =
        {
            "_MainTex",
            "_BaseMap",
            "_BaseColorMap",
            "_Diffuse",
            "_Albedo"
        };

        public static bool IsUsableMaterial(Material mat, string expectedShaderName)
        {
            if (mat == null || mat.shader == null)
            {
                return false;
            }

            string name = mat.shader.name;
            if (string.IsNullOrEmpty(name)
                || name == "Hidden/InternalErrorShader"
                || name.IndexOf("InternalError", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            if (!mat.shader.isSupported)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(expectedShaderName) && name != expectedShaderName)
            {
                return false;
            }

            return true;
        }

        public static Shader ResolveShader(string shaderName)
        {
            var refs = Resources.Load<EnemyDeathShaderRefs>(EnemyDeathShaderRefs.ResourcesPath);
            if (refs != null)
            {
                Shader fromRefs = refs.Resolve(shaderName);
                if (fromRefs != null)
                {
                    return fromRefs;
                }
            }

            return Shader.Find(shaderName);
        }

        public static Material LoadOrCreateTemplate(string resourcesPath, string shaderName)
        {
            // 优先解析可用 Shader，拒绝粉红/错误材质
            Shader shader = ResolveShader(shaderName);
            if (shader != null && shader.isSupported)
            {
                var fromResources = Resources.Load<Material>(resourcesPath);
                if (IsUsableMaterial(fromResources, shaderName))
                {
                    return fromResources;
                }

                return new Material(shader)
                {
                    name = "Runtime_" + shaderName.Replace('/', '_'),
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            var mat = Resources.Load<Material>(resourcesPath);
            if (IsUsableMaterial(mat, shaderName))
            {
                return mat;
            }

            Debug.LogError(
                $"[EnemyDeathVisualUtil] Shader 不可用: '{shaderName}' (Find={(shader != null ? shader.name : "null")}). " +
                "请确认 Assets/Shaders 已导入，并执行菜单：工具/敌人/重建死亡特效材质。");
            return null;
        }

        public static void CopyAlbedoFrom(Material source, Material dest)
        {
            if (source == null || dest == null || !dest.HasProperty("_MainTex"))
            {
                return;
            }

            for (int i = 0; i < AlbedoPropertyNames.Length; i++)
            {
                string prop = AlbedoPropertyNames[i];
                if (!source.HasProperty(prop))
                {
                    continue;
                }

                Texture tex = source.GetTexture(prop);
                if (tex == null)
                {
                    continue;
                }

                dest.SetTexture("_MainTex", tex);
                dest.SetTextureScale("_MainTex", source.GetTextureScale(prop));
                dest.SetTextureOffset("_MainTex", source.GetTextureOffset(prop));
                return;
            }
        }

        /// <summary>关闭碰撞（保留 Trigger 可选）；死亡声骸/溶解不再挡路。</summary>
        public static void DisableBlockingColliders(GameObject root, bool includeTriggers = true)
        {
            if (root == null)
            {
                return;
            }

            var cc = root.GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.enabled = false;
            }

            var cols = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
            {
                Collider c = cols[i];
                if (c == null)
                {
                    continue;
                }

                if (!includeTriggers && c.isTrigger)
                {
                    continue;
                }

                c.enabled = false;
            }
        }

        /// <summary>对象池复用前重新打开碰撞。</summary>
        public static void EnableBlockingColliders(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            var cc = root.GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.enabled = true;
            }

            var cols = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] != null)
                {
                    cols[i].enabled = true;
                }
            }
        }

        public static void CollectMeshRenderers(GameObject root, List<Renderer> dst)
        {
            dst.Clear();
            if (root == null)
            {
                return;
            }

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r is MeshRenderer || r is SkinnedMeshRenderer)
                {
                    dst.Add(r);
                }
            }
        }
    }
}
