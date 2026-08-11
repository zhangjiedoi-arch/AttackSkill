using System.IO;
using System.Xml;
using UnityEditor;
using UnityEngine;

namespace AttackSkill.Editor
{
    /// <summary>
    /// 按海滩场景 1.xml 的 textureID 映射，把 tex/*.dds 赋到 Materials 的 _MainTex。
    /// 团结引擎 meta 里是编码 GUID，不能手写进 .mat，必须经 AssetDatabase 赋值。
    /// </summary>
    public static class BeachSceneTextureAssignMenu
    {
        const string SceneRoot = "Assets/Model/海滩场景";
        const string XmlPath = SceneRoot + "/1.xml";
        const string MatFolder = SceneRoot + "/Materials";
        const string TexFolder = SceneRoot + "/tex";

        [MenuItem("Tools/AttackSkill/海滩场景/按XML挂贴图")]
        public static void AssignTexturesMenu()
        {
            AssignTextures(showDialog: true);
        }

        public static void AssignTextures(bool showDialog)
        {
            if (!File.Exists(XmlPath))
            {
                if (showDialog)
                    EditorUtility.DisplayDialog("海滩场景", "找不到 1.xml", "OK");
                return;
            }

            var doc = new XmlDocument();
            doc.Load(XmlPath);

            var textureNodes = doc.SelectNodes("/MMDModel/textureList/Texture/fileName");
            if (textureNodes == null || textureNodes.Count == 0)
            {
                if (showDialog)
                    EditorUtility.DisplayDialog("海滩场景", "1.xml 无 textureList", "OK");
                return;
            }

            var textures = new string[textureNodes.Count];
            for (var i = 0; i < textureNodes.Count; i++)
                textures[i] = Path.GetFileName(textureNodes[i].InnerText.Trim().Replace('\\', '/'));

            var matNodes = doc.SelectNodes("/MMDModel/materialList/Material");
            if (matNodes == null)
            {
                if (showDialog)
                    EditorUtility.DisplayDialog("海滩场景", "1.xml 无 materialList", "OK");
                return;
            }

            var assigned = 0;
            var skipped = 0;
            var failed = 0;

            AssetDatabase.StartAssetEditing();
            try
            {
                for (var i = 0; i < matNodes.Count; i++)
                {
                    var matNode = matNodes[i];
                    var matName = matNode.SelectSingleNode("materialName")?.InnerText?.Trim();
                    var tidText = matNode.SelectSingleNode("textureID")?.InnerText?.Trim();
                    if (string.IsNullOrEmpty(matName) || string.IsNullOrEmpty(tidText))
                    {
                        failed++;
                        continue;
                    }

                    if (!int.TryParse(tidText, out var tid) || tid < 0)
                    {
                        skipped++;
                        continue;
                    }

                    if (tid >= textures.Length)
                    {
                        Debug.LogWarning($"[BeachScene] {matName} textureID={tid} 越界");
                        failed++;
                        continue;
                    }

                    var texName = textures[tid];
                    if (!texName.EndsWith(".dds"))
                    {
                        skipped++;
                        continue;
                    }

                    var matPath = $"{MatFolder}/{matName}.mat";
                    var texPath = $"{TexFolder}/{texName}";
                    var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                    var tex = AssetDatabase.LoadAssetAtPath<Texture>(texPath);
                    if (mat == null)
                    {
                        Debug.LogWarning($"[BeachScene] 找不到材质: {matPath}");
                        failed++;
                        continue;
                    }

                    if (tex == null)
                    {
                        Debug.LogWarning($"[BeachScene] 找不到贴图: {texPath}");
                        failed++;
                        continue;
                    }

                    if (mat.HasProperty("_MainTex"))
                        mat.SetTexture("_MainTex", tex);
                    else if (mat.HasProperty("_BaseMap"))
                        mat.SetTexture("_BaseMap", tex);
                    else
                    {
                        Debug.LogWarning($"[BeachScene] {matName} 无 _MainTex/_BaseMap");
                        failed++;
                        continue;
                    }

                    EditorUtility.SetDirty(mat);
                    assigned++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            var msg = $"挂贴图完成\n成功: {assigned}\n跳过: {skipped}\n失败: {failed}";
            Debug.Log($"[BeachScene] AssignTextures done assigned={assigned} skipped={skipped} failed={failed}");
            if (showDialog)
                EditorUtility.DisplayDialog("海滩场景", msg, "OK");
        }
    }
}
