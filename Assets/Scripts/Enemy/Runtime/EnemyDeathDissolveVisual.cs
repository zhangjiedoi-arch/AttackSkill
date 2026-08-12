using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AttackSkill.Enemy
{
    /// <summary>敌人死亡飘散：Dissolve + 上浮；成功返回 true。</summary>
    [DisallowMultipleComponent]
    public sealed class EnemyDeathDissolveVisual : MonoBehaviour
    {
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int EdgeColorId = Shader.PropertyToID("_EdgeColor");
        static readonly int DissolveId = Shader.PropertyToID("_Dissolve");
        static readonly int EdgeWidthId = Shader.PropertyToID("_EdgeWidth");
        static readonly int NoiseScaleId = Shader.PropertyToID("_NoiseScale");
        static readonly int HeightBiasId = Shader.PropertyToID("_HeightBias");
        static readonly int EmissionId = Shader.PropertyToID("_Emission");

        [SerializeField] Color tint = new Color(1f, 0.82f, 0.35f, 1f);
        [SerializeField] Color edgeColor = new Color(1f, 0.9f, 0.4f, 1f);
        [SerializeField] float duration = 1.55f;
        [SerializeField] float riseDistance = 0.55f;
        [SerializeField] float edgeWidth = 0.08f;
        [SerializeField] float noiseScale = 3.5f;
        [SerializeField] float heightBias = 0.35f;
        [SerializeField] float edgeEmission = 2.5f;
        [SerializeField] Material dissolveTemplate;
        [SerializeField] GameObject dustVfxPrefab;

        struct Slot
        {
            public Renderer Renderer;
            public Material[] OriginalShared;
            public ShadowCastingMode OriginalShadow;
            public bool OriginalReceive;
            public bool OriginalEnabled;
        }

        readonly List<Slot> _slots = new List<Slot>(8);
        readonly List<Material> _owned = new List<Material>(16);
        readonly List<Renderer> _rendererBuffer = new List<Renderer>(8);

        Material _template;
        bool _playing;
        bool _swapped;
        float _elapsed;
        Vector3 _startPos;
        Action _onCompleted;

        public bool IsPlaying => _playing;
        public float Duration => Mathf.Max(0.2f, duration);

        void OnDestroy()
        {
            DestroyOwnedMaterials();
        }

        public bool Play(Action onCompleted = null)
        {
            if (_playing)
            {
                return true;
            }

            if (!EnsureTemplate())
            {
                Debug.LogWarning("[EnemyDeathDissolveVisual] 无法加载 DeathDissolve 材质/Shader。", this);
                onCompleted?.Invoke();
                return false;
            }

            if (!CaptureAndSwap())
            {
                onCompleted?.Invoke();
                return false;
            }

            EnemyDeathVisualUtil.DisableBlockingColliders(gameObject, includeTriggers: true);

            _onCompleted = onCompleted;
            _playing = true;
            _elapsed = 0f;
            _startPos = transform.position;
            ApplyDissolve(0f);
            TrySpawnDust();
            enabled = true;
            return true;
        }

        public void Restore()
        {
            _playing = false;
            _onCompleted = null;
            _elapsed = 0f;

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
                s.Renderer.enabled = s.OriginalEnabled;
            }

            DestroyOwnedMaterials();
            _slots.Clear();
            _swapped = false;
            enabled = false;
        }

        void Update()
        {
            if (!_playing)
            {
                return;
            }

            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / Duration);
            ApplyDissolve(Mathf.SmoothStep(0f, 1f, t));
            transform.position = _startPos + Vector3.up * (riseDistance * Mathf.SmoothStep(0f, 1f, t));

            if (t < 1f)
            {
                return;
            }

            _playing = false;
            HideRenderers();
            enabled = false;
            var cb = _onCompleted;
            _onCompleted = null;
            cb?.Invoke();
        }

        bool EnsureTemplate()
        {
            if (EnemyDeathVisualUtil.IsUsableMaterial(dissolveTemplate, EnemyDeathVisualUtil.DissolveShaderName))
            {
                _template = dissolveTemplate;
                return true;
            }

            if (EnemyDeathVisualUtil.IsUsableMaterial(_template, EnemyDeathVisualUtil.DissolveShaderName))
            {
                return true;
            }

            _template = EnemyDeathVisualUtil.LoadOrCreateTemplate(
                EnemyDeathVisualUtil.DissolveMatResourcesPath,
                EnemyDeathVisualUtil.DissolveShaderName);
            return EnemyDeathVisualUtil.IsUsableMaterial(_template, EnemyDeathVisualUtil.DissolveShaderName);
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

                var mats = new Material[original.Length];
                for (int m = 0; m < original.Length; m++)
                {
                    mats[m] = CreateDissolveMaterial(original[m]);
                    _owned.Add(mats[m]);
                }

                _slots.Add(new Slot
                {
                    Renderer = r,
                    OriginalShared = original,
                    OriginalShadow = r.shadowCastingMode,
                    OriginalReceive = r.receiveShadows,
                    OriginalEnabled = r.enabled
                });

                r.sharedMaterials = mats;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.enabled = true;
            }

            _swapped = _slots.Count > 0;
            return _swapped;
        }

        Material CreateDissolveMaterial(Material source)
        {
            var mat = new Material(_template)
            {
                name = "DeathDissolve_Runtime"
            };
            mat.SetColor(ColorId, tint);
            mat.SetColor(EdgeColorId, edgeColor);
            mat.SetFloat(EdgeWidthId, edgeWidth);
            mat.SetFloat(NoiseScaleId, noiseScale);
            mat.SetFloat(HeightBiasId, heightBias);
            mat.SetFloat(EmissionId, edgeEmission);
            mat.SetFloat(DissolveId, 0f);
            EnemyDeathVisualUtil.CopyAlbedoFrom(source, mat);
            return mat;
        }

        void ApplyDissolve(float amount)
        {
            for (int i = 0; i < _owned.Count; i++)
            {
                if (_owned[i] != null)
                {
                    _owned[i].SetFloat(DissolveId, amount);
                }
            }
        }

        void HideRenderers()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].Renderer != null)
                {
                    _slots[i].Renderer.enabled = false;
                }
            }
        }

        void TrySpawnDust()
        {
            if (dustVfxPrefab == null)
            {
                return;
            }

            Vector3 pos = transform.position + Vector3.up * 1f;
            var fx = Instantiate(dustVfxPrefab, pos, Quaternion.identity);
            Destroy(fx, Duration + 0.5f);
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

        public void ConfigureFromDefinition(EnemyDefinition def)
        {
            if (def == null)
            {
                return;
            }

            duration = Mathf.Max(0.2f, def.dissolveDuration);
            riseDistance = def.dissolveRiseDistance;
            if (def.dissolveDustVfx != null)
            {
                dustVfxPrefab = def.dissolveDustVfx;
            }
        }
    }
}
