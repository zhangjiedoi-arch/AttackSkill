using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AttackSkill.Character
{
    /// <summary>
    /// 相机过近时角色淡出：MMD 不透明材质临时切 Transparent 并调 _Color.a，过近则停绘。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterCameraProximityFade : MonoBehaviour
    {
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        struct RendererState
        {
            public Renderer Renderer;
            public Material[] SharedMaterials;
            public Material[] FadeMaterials;
            public ShadowCastingMode ShadowMode;
            public bool SupportsColorAlpha;
        }

        readonly List<RendererState> _states = new List<RendererState>(16);

        float _visibility = 1f;
        bool _cacheDirty = true;
        bool _usingFadeMaterials;

        public float Visibility => _visibility;

        void OnDisable()
        {
            RestoreFullVisibility();
        }

        void OnDestroy()
        {
            DestroyFadeMaterials();
        }

        /// <summary>工具/挂件显隐后可调用，下次淡出时重建缓存。</summary>
        public void InvalidateCache()
        {
            if (_usingFadeMaterials)
            {
                RestoreSharedMaterials();
            }

            DestroyFadeMaterials();
            _states.Clear();
            _cacheDirty = true;
        }

        /// <summary>1=完全可见，0=完全隐藏。</summary>
        public void SetVisibility(float visibility)
        {
            visibility = Mathf.Clamp01(visibility);
            bool enteringFade = visibility < 0.999f && _visibility >= 0.999f;
            if (!_cacheDirty && !enteringFade && Mathf.Abs(visibility - _visibility) < 0.001f)
            {
                return;
            }

            // 刚进入淡出时重建缓存，带上刚显示的摩托/翅膀等挂件
            if (enteringFade)
            {
                _cacheDirty = true;
            }

            _visibility = visibility;
            EnsureCache();

            if (visibility >= 0.999f)
            {
                RestoreFullVisibility();
                return;
            }

            if (visibility <= 0.02f)
            {
                ApplyHidden();
                return;
            }

            ApplyAlpha(visibility);
        }

        public void RestoreFullVisibility()
        {
            _visibility = 1f;
            if (_states.Count == 0 && _cacheDirty)
            {
                return;
            }

            EnsureCache();
            RestoreSharedMaterials();
            for (int i = 0; i < _states.Count; i++)
            {
                RendererState s = _states[i];
                if (s.Renderer == null)
                {
                    continue;
                }

                s.Renderer.forceRenderingOff = false;
                s.Renderer.shadowCastingMode = s.ShadowMode;
                s.Renderer.SetPropertyBlock(null);
            }

            _usingFadeMaterials = false;
        }

        void ApplyHidden()
        {
            EnsureCache();
            for (int i = 0; i < _states.Count; i++)
            {
                RendererState s = _states[i];
                if (s.Renderer == null)
                {
                    continue;
                }

                s.Renderer.forceRenderingOff = true;
                s.Renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        void ApplyAlpha(float alpha)
        {
            EnsureCache();
            EnsureFadeMaterials();

            for (int i = 0; i < _states.Count; i++)
            {
                RendererState s = _states[i];
                if (s.Renderer == null)
                {
                    continue;
                }

                s.Renderer.forceRenderingOff = false;
                s.Renderer.shadowCastingMode = ShadowCastingMode.Off;

                if (!s.SupportsColorAlpha || s.FadeMaterials == null)
                {
                    // 无法透明的材质：半透明区间按阈值闪隐，贴近时由 ApplyHidden 处理
                    s.Renderer.forceRenderingOff = alpha < 0.55f;
                    continue;
                }

                ApplyColorAlpha(s.FadeMaterials, alpha);
            }
        }

        void ApplyColorAlpha(Material[] mats, float alpha)
        {
            if (mats == null)
            {
                return;
            }

            for (int i = 0; i < mats.Length; i++)
            {
                Material m = mats[i];
                if (m == null)
                {
                    continue;
                }

                if (m.HasProperty(ColorId))
                {
                    Color c = m.GetColor(ColorId);
                    c.a = alpha;
                    m.SetColor(ColorId, c);
                }
                else if (m.HasProperty(BaseColorId))
                {
                    Color c = m.GetColor(BaseColorId);
                    c.a = alpha;
                    m.SetColor(BaseColorId, c);
                }
            }
        }

        void EnsureCache()
        {
            if (!_cacheDirty && _states.Count > 0)
            {
                return;
            }

            DestroyFadeMaterials();
            _states.Clear();

            var renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer)
                {
                    continue;
                }

                // 跳过仅用于特效/UI 的空材质
                Material[] shared = r.sharedMaterials;
                if (shared == null || shared.Length == 0)
                {
                    continue;
                }

                _states.Add(new RendererState
                {
                    Renderer = r,
                    SharedMaterials = shared,
                    FadeMaterials = null,
                    ShadowMode = r.shadowCastingMode,
                    SupportsColorAlpha = false
                });
            }

            _cacheDirty = false;
            _usingFadeMaterials = false;
        }

        void EnsureFadeMaterials()
        {
            if (_usingFadeMaterials)
            {
                return;
            }

            for (int i = 0; i < _states.Count; i++)
            {
                RendererState s = _states[i];
                if (s.Renderer == null || s.SharedMaterials == null)
                {
                    continue;
                }

                var fadeMats = new Material[s.SharedMaterials.Length];
                bool anyAlpha = false;
                for (int m = 0; m < s.SharedMaterials.Length; m++)
                {
                    Material src = s.SharedMaterials[m];
                    if (src == null)
                    {
                        continue;
                    }

                    Material clone = new Material(src);
                    if (TryMakeTransparent(clone))
                    {
                        anyAlpha = true;
                    }

                    fadeMats[m] = clone;
                }

                s.FadeMaterials = fadeMats;
                s.SupportsColorAlpha = anyAlpha;
                _states[i] = s;

                if (anyAlpha)
                {
                    s.Renderer.sharedMaterials = fadeMats;
                }
            }

            _usingFadeMaterials = true;
        }

        void RestoreSharedMaterials()
        {
            for (int i = 0; i < _states.Count; i++)
            {
                RendererState s = _states[i];
                if (s.Renderer == null || s.SharedMaterials == null)
                {
                    continue;
                }

                s.Renderer.sharedMaterials = s.SharedMaterials;
            }
        }

        void DestroyFadeMaterials()
        {
            for (int i = 0; i < _states.Count; i++)
            {
                RendererState s = _states[i];
                if (s.FadeMaterials == null)
                {
                    continue;
                }

                for (int m = 0; m < s.FadeMaterials.Length; m++)
                {
                    if (s.FadeMaterials[m] != null)
                    {
                        Destroy(s.FadeMaterials[m]);
                    }
                }

                s.FadeMaterials = null;
                _states[i] = s;
            }

            _usingFadeMaterials = false;
        }

        static bool TryMakeTransparent(Material mat)
        {
            if (mat == null || mat.shader == null)
            {
                return false;
            }

            string shaderName = mat.shader.name;
            if (string.IsNullOrEmpty(shaderName))
            {
                return mat.HasProperty(ColorId) || mat.HasProperty(BaseColorId);
            }

            if (shaderName.IndexOf("Transparent", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return mat.HasProperty(ColorId) || mat.HasProperty(BaseColorId);
            }

            string transparentName = ToTransparentShaderName(shaderName);
            if (transparentName != null && transparentName != shaderName)
            {
                Shader transparent = Shader.Find(transparentName);
                if (transparent != null)
                {
                    mat.shader = transparent;
                    return mat.HasProperty(ColorId) || mat.HasProperty(BaseColorId);
                }
            }

            // URP Lit 等：尝试开透明模式
            if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.renderQueue = (int)RenderQueue.Transparent;
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }

            return mat.HasProperty(ColorId) || mat.HasProperty(BaseColorId);
        }

        /// <summary>
        /// MMDLit / MMDLit-Edge / MMDLit-BothFaces-Edge → 对应 Transparent 变体。
        /// </summary>
        static string ToTransparentShaderName(string shaderName)
        {
            if (!shaderName.StartsWith("MMD4Mecanim/", System.StringComparison.Ordinal))
            {
                return null;
            }

            if (shaderName.EndsWith("-Edge", System.StringComparison.Ordinal))
            {
                string withoutEdge = shaderName.Substring(0, shaderName.Length - "-Edge".Length);
                if (withoutEdge.EndsWith("-Transparent", System.StringComparison.Ordinal))
                {
                    return shaderName;
                }

                return withoutEdge + "-Transparent-Edge";
            }

            if (shaderName.EndsWith("-Transparent", System.StringComparison.Ordinal))
            {
                return shaderName;
            }

            return shaderName + "-Transparent";
        }
    }
}
