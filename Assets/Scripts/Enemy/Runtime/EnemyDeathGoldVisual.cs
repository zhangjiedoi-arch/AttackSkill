using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AttackSkill.Enemy
{
    /// <summary>
    /// 敌人死亡金色透明残影。成功返回 true；失败时勿开启 F 交互。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyDeathGoldVisual : MonoBehaviour
    {
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int RimColorId = Shader.PropertyToID("_RimColor");
        static readonly int RimPowerId = Shader.PropertyToID("_RimPower");
        static readonly int RimIntensityId = Shader.PropertyToID("_RimIntensity");
        static readonly int EmissionId = Shader.PropertyToID("_Emission");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");

        [SerializeField] Color goldColor = new Color(0.92f, 0.72f, 0.28f, 1f);
        [SerializeField] Color rimColor = new Color(1f, 0.88f, 0.5f, 1f);
        [SerializeField] float rimPower = 3.2f;
        [SerializeField] float rimIntensity = 0.85f;
        [SerializeField] float emission = 0.28f;
        [SerializeField] float restAlpha = 0.26f;
        [SerializeField] float fadeInDuration = 0.45f;
        [SerializeField] Material deathGoldTemplate;

        struct Slot
        {
            public Renderer Renderer;
            public Material[] OriginalShared;
            public ShadowCastingMode OriginalShadow;
            public bool OriginalReceive;
        }

        readonly List<Slot> _slots = new List<Slot>(8);
        readonly List<Material> _owned = new List<Material>(16);
        readonly List<Renderer> _rendererBuffer = new List<Renderer>(8);

        Material _template;
        bool _playing;
        bool _swapped;
        float _elapsed;
        float _currentAlpha;

        public bool IsPlaying => _playing;
        public bool IsSwapped => _swapped;

        void OnDestroy()
        {
            DestroyOwnedMaterials();
        }

        /// <summary>换金色透明材质并淡入。失败返回 false。</summary>
        public bool Play()
        {
            if (_playing && _swapped)
            {
                return true;
            }

            if (!EnsureTemplate())
            {
                Debug.LogWarning("[EnemyDeathGoldVisual] 无法加载 DeathGold 材质/Shader。", this);
                return false;
            }

            if (!_swapped && !CaptureAndSwap())
            {
                return false;
            }

            EnemyDeathVisualUtil.DisableBlockingColliders(gameObject, includeTriggers: true);

            _playing = true;
            _elapsed = 0f;
            _currentAlpha = 0.05f;
            ApplyAlpha(_currentAlpha);
            enabled = true;
            return true;
        }

        public void Restore()
        {
            _playing = false;
            _elapsed = 0f;
            _currentAlpha = 1f;

            for (int i = 0; i < _slots.Count; i++)
            {
                Slot s = _slots[i];
                if (s.Renderer == null)
                {
                    continue;
                }

                if (s.OriginalShared != null)
                {
                    s.Renderer.sharedMaterials = s.OriginalShared;
                }

                s.Renderer.shadowCastingMode = s.OriginalShadow;
                s.Renderer.receiveShadows = s.OriginalReceive;
            }

            DestroyOwnedMaterials();
            _slots.Clear();
            _swapped = false;
            enabled = false;
        }

        void Update()
        {
            if (!_playing || !_swapped)
            {
                return;
            }

            _elapsed += Time.deltaTime;
            float t = fadeInDuration <= 0.01f ? 1f : Mathf.Clamp01(_elapsed / fadeInDuration);
            float peak = Mathf.Min(0.45f, restAlpha + 0.06f);
            float shaped = t < 0.7f
                ? Mathf.SmoothStep(0.04f, peak, t / 0.7f)
                : Mathf.SmoothStep(peak, restAlpha, (t - 0.7f) / 0.3f);

            _currentAlpha = shaped;
            ApplyAlpha(_currentAlpha);

            if (t >= 1f)
            {
                _currentAlpha = restAlpha;
                ApplyAlpha(_currentAlpha);
                _playing = false;
                enabled = false;
            }
        }

        bool EnsureTemplate()
        {
            if (EnemyDeathVisualUtil.IsUsableMaterial(deathGoldTemplate, EnemyDeathVisualUtil.GoldShaderName))
            {
                _template = deathGoldTemplate;
                return true;
            }

            if (EnemyDeathVisualUtil.IsUsableMaterial(_template, EnemyDeathVisualUtil.GoldShaderName))
            {
                return true;
            }

            _template = EnemyDeathVisualUtil.LoadOrCreateTemplate(
                EnemyDeathVisualUtil.GoldMatResourcesPath,
                EnemyDeathVisualUtil.GoldShaderName);
            return EnemyDeathVisualUtil.IsUsableMaterial(_template, EnemyDeathVisualUtil.GoldShaderName);
        }

        bool CaptureAndSwap()
        {
            DestroyOwnedMaterials();
            _slots.Clear();
            EnemyDeathVisualUtil.CollectMeshRenderers(gameObject, _rendererBuffer);

            for (int i = 0; i < _rendererBuffer.Count; i++)
            {
                Renderer r = _rendererBuffer[i];
                Material[] original = r.sharedMaterials;
                if (original == null || original.Length == 0)
                {
                    continue;
                }

                var deathMats = new Material[original.Length];
                for (int m = 0; m < original.Length; m++)
                {
                    deathMats[m] = CreateDeathMaterial(original[m]);
                    _owned.Add(deathMats[m]);
                }

                _slots.Add(new Slot
                {
                    Renderer = r,
                    OriginalShared = original,
                    OriginalShadow = r.shadowCastingMode,
                    OriginalReceive = r.receiveShadows
                });

                r.sharedMaterials = deathMats;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }

            _swapped = _slots.Count > 0;
            if (!_swapped)
            {
                Debug.LogWarning("[EnemyDeathGoldVisual] 未找到可替换的 Mesh/SkinnedMeshRenderer。", this);
            }

            return _swapped;
        }

        Material CreateDeathMaterial(Material source)
        {
            var mat = new Material(_template)
            {
                name = "DeathGold_Runtime"
            };
            mat.SetColor(ColorId, goldColor);
            mat.SetColor(RimColorId, rimColor);
            mat.SetFloat(RimPowerId, rimPower);
            mat.SetFloat(RimIntensityId, rimIntensity);
            mat.SetFloat(EmissionId, emission);
            mat.SetFloat(AlphaId, 0.05f);
            EnemyDeathVisualUtil.CopyAlbedoFrom(source, mat);
            return mat;
        }

        void ApplyAlpha(float alpha)
        {
            for (int i = 0; i < _owned.Count; i++)
            {
                if (_owned[i] != null)
                {
                    _owned[i].SetFloat(AlphaId, alpha);
                }
            }
        }

        void DestroyOwnedMaterials()
        {
            for (int i = 0; i < _owned.Count; i++)
            {
                if (_owned[i] != null)
                {
                    Destroy(_owned[i]);
                }
            }

            _owned.Clear();
        }
    }
}
