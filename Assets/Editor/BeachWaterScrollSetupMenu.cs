using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using AttackSkill.Environment;

namespace AttackSkill.Editor
{
    /// <summary>
    /// 把 2.Material.001d02 配成半透明水面 + UV 假滚动。
    /// </summary>
    public static class BeachWaterScrollSetupMenu
    {
        const string MatPath = "Assets/Model/海滩场景/Materials/2.Material.001d02.mat";
        const string TexPath = "Assets/Model/海滩场景/tex/sea_mz_water.dds";

        [MenuItem("Tools/AttackSkill/海滩场景/配置水面假滚动（2.Material.001d02）")]
        public static void Setup()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            var tex = AssetDatabase.LoadAssetAtPath<Texture>(TexPath);
            if (mat == null)
            {
                EditorUtility.DisplayDialog("海滩场景", $"找不到材质:\n{MatPath}", "OK");
                return;
            }

            if (tex == null)
            {
                EditorUtility.DisplayDialog("海滩场景", $"找不到水面贴图:\n{TexPath}", "OK");
                return;
            }

            Undo.RecordObject(mat, "Setup Beach Water Material");

            // 尽量保证贴图可平铺
            var texImporter = AssetImporter.GetAtPath(TexPath) as TextureImporter;
            if (texImporter != null)
            {
                texImporter.wrapMode = TextureWrapMode.Repeat;
                texImporter.SaveAndReimport();
                tex = AssetDatabase.LoadAssetAtPath<Texture>(TexPath);
            }

            mat.SetTexture("_MainTex", tex);
            mat.SetTextureScale("_MainTex", new Vector2(4f, 4f));
            mat.SetTextureOffset("_MainTex", Vector2.zero);
            mat.color = new Color(0.35f, 0.62f, 0.82f, 0.72f);
            mat.SetFloat("_Metallic", 0.05f);
            mat.SetFloat("_Glossiness", 0.85f);
            SetStandardTransparent(mat);
            EditorUtility.SetDirty(mat);

            var go = Selection.activeGameObject;
            if (go == null)
            {
                AssetDatabase.SaveAssets();
                EditorUtility.DisplayDialog(
                    "海滩场景",
                    "材质已配成半透明水面。\n请再选中海岛根物体后重新执行一次，以挂上 UV 滚动脚本。",
                    "OK");
                return;
            }

            var scroll = go.GetComponent<MaterialUvScroll>();
            if (scroll == null)
            {
                scroll = Undo.AddComponent<MaterialUvScroll>(go);
            }

            var so = new SerializedObject(scroll);
            so.FindProperty("materialNameContains").stringValue = "2.Material.001d02";
            so.FindProperty("targetMaterial").objectReferenceValue = mat;
            so.FindProperty("scrollSpeed").vector2Value = new Vector2(0.035f, 0.02f);
            so.FindProperty("useMaterialInstance").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(go);

            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog(
                "海滩场景",
                "已完成：\n1) 2.Material.001d02 → 半透明 + sea_mz_water\n2) 已在选中物体上挂 MaterialUvScroll\n\n进入 Play 即可看到假滚动。",
                "OK");
        }

        static void SetStandardTransparent(Material mat)
        {
            // Standard Rendering Mode = Transparent
            mat.SetFloat("_Mode", 3f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)RenderQueue.Transparent;
        }
    }
}
