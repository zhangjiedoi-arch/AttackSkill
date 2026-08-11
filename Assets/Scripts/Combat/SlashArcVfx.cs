using UnityEngine;

namespace AttackSkill.Combat
{
    /// <summary>
    /// 程序化弧形刀光。可不依赖 Prefab，运行时直接 Spawn。
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class SlashArcVfx : MonoBehaviour
    {
        [Header("Shape")]
        [SerializeField] float innerRadius = 0.25f;
        [SerializeField] float outerRadius = 1.15f;
        [SerializeField, Range(30f, 180f)] float arcAngle = 100f;
        [SerializeField, Range(8, 64)] int segments = 32;
        [SerializeField] float bladeHeight = 0.42f;
        [SerializeField] Vector3 localEuler = new Vector3(-12f, 0f, 0f);

        [Header("Motion")]
        [SerializeField] float duration = 0.38f;
        [SerializeField] float fadeStart = 0.4f;
        [SerializeField] AnimationCurve revealCurve = AnimationCurve.EaseInOut(0f, 0.15f, 1f, 1f);
        [SerializeField] float forwardOffset = 0f;
        [SerializeField] float heightOffset = 0f;

        [Header("Look")]
        [SerializeField] Color tint = new Color(1f, 0.88f, 0.42f, 1f);
        [SerializeField] float intensityScale = 1.4f;

        MeshFilter _filter;
        MeshRenderer _renderer;
        Mesh _mesh;
        Material _runtimeMat;
        MaterialPropertyBlock _mpb;
        float _elapsed;
        float _baseIntensity = 4.5f;
        bool _ownsMaterial;

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static Material _sharedFallbackMat;

        /// <summary>世界空间生成一把刀光（Prefab 可空）。</summary>
        public static SlashArcVfx Spawn(Vector3 worldPos, Quaternion worldRot, float radius, float angle, Color color, float life)
        {
            var go = new GameObject("SlashArc_Runtime");
            go.transform.SetPositionAndRotation(worldPos, worldRot);
            go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.sharedMaterial = CreateMaterial();

            var vfx = go.AddComponent<SlashArcVfx>();
            vfx.Configure(radius, angle, color, life);
            return vfx;
        }

        public static Material CreateMaterial()
        {
            var shader = Shader.Find("AttackSkill/VFX/SlashAdditive");
            if (shader != null)
            {
                var mat = new Material(shader)
                {
                    name = "SlashAdditive_Runtime",
                    hideFlags = HideFlags.HideAndDontSave
                };
                mat.SetColor(ColorId, new Color(1f, 0.85f, 0.4f, 1f));
                mat.SetFloat(IntensityId, 5.5f);
                mat.SetFloat("_CoreBoost", 3.2f);
                mat.SetFloat("_SoftEdge", 0.22f);
                return mat;
            }

            // 兜底：内置粒子叠加
            if (_sharedFallbackMat == null)
            {
                shader = Shader.Find("Particles/Additive")
                         ?? Shader.Find("Legacy Shaders/Particles/Additive")
                         ?? Shader.Find("Mobile/Particles/Additive")
                         ?? Shader.Find("Sprites/Default");
                _sharedFallbackMat = new Material(shader != null ? shader : Shader.Find("Hidden/InternalErrorShader"))
                {
                    name = "SlashFallback_Runtime",
                    hideFlags = HideFlags.HideAndDontSave
                };
                if (_sharedFallbackMat.HasProperty("_TintColor"))
                {
                    _sharedFallbackMat.SetColor("_TintColor", new Color(1f, 0.8f, 0.3f, 0.6f));
                }

                if (_sharedFallbackMat.HasProperty("_Color"))
                {
                    _sharedFallbackMat.SetColor("_Color", new Color(1f, 0.85f, 0.4f, 1f));
                }
            }

            return _sharedFallbackMat;
        }

        void Awake()
        {
            _filter = GetComponent<MeshFilter>();
            _renderer = GetComponent<MeshRenderer>();
            _mpb = new MaterialPropertyBlock();
            _mesh = new Mesh { name = "SlashArcRuntime" };
            _mesh.MarkDynamic();
            _filter.sharedMesh = _mesh;

            if (_renderer.sharedMaterial == null)
            {
                _runtimeMat = CreateMaterial();
                _ownsMaterial = _runtimeMat != _sharedFallbackMat;
                _renderer.sharedMaterial = _runtimeMat;
            }

            if (_renderer.sharedMaterial != null && _renderer.sharedMaterial.HasProperty(IntensityId))
            {
                _baseIntensity = _renderer.sharedMaterial.GetFloat(IntensityId);
            }

            transform.localPosition = new Vector3(0f, heightOffset, forwardOffset);
            transform.localRotation = Quaternion.Euler(localEuler);
            Rebuild(0.2f);
            ApplyColor(1f);
        }

        void OnDestroy()
        {
            if (_mesh != null)
            {
                Destroy(_mesh);
            }

            if (_ownsMaterial && _runtimeMat != null)
            {
                Destroy(_runtimeMat);
            }
        }

        void Update()
        {
            _elapsed += Time.deltaTime;
            float t = duration > 0.001f ? Mathf.Clamp01(_elapsed / duration) : 1f;
            float reveal = Mathf.Clamp01(revealCurve.Evaluate(t));
            Rebuild(Mathf.Max(0.12f, reveal));

            float fadeT = Mathf.InverseLerp(fadeStart, 1f, t);
            float alpha = 1f - Mathf.Clamp01(fadeT);
            alpha = alpha * alpha;
            ApplyColor(Mathf.Max(0.05f, alpha));

            if (t >= 1f)
            {
                Destroy(gameObject);
            }
        }

        public void SetSpawnAnchor(float height, float forward)
        {
            heightOffset = height;
            forwardOffset = forward;
            transform.localPosition = new Vector3(0f, heightOffset, forwardOffset);
        }

        public void Configure(float radius, float angle, Color color, float life)
        {
            // 视觉半径与判定半径解耦：贴身弧，不要跟 HitRadius 一样大
            outerRadius = Mathf.Clamp(radius, 0.7f, 1.4f);
            innerRadius = Mathf.Clamp(outerRadius * 0.22f, 0.12f, 0.4f);
            arcAngle = Mathf.Clamp(angle, 30f, 170f);
            tint = color;
            duration = Mathf.Max(0.2f, life);
            forwardOffset = 0f;
            _elapsed = 0f;
            if (transform != null)
            {
                transform.localPosition = new Vector3(0f, heightOffset, forwardOffset);
            }

            Rebuild(0.2f);
            ApplyColor(1f);
        }

        void ApplyColor(float alpha)
        {
            if (_renderer == null)
            {
                return;
            }

            _renderer.GetPropertyBlock(_mpb);
            Color c = tint;
            c.a = alpha;

            if (_renderer.sharedMaterial != null && _renderer.sharedMaterial.HasProperty(ColorId))
            {
                _mpb.SetColor(ColorId, c);
            }

            if (_renderer.sharedMaterial != null && _renderer.sharedMaterial.HasProperty("_TintColor"))
            {
                _mpb.SetColor("_TintColor", new Color(c.r, c.g, c.b, 0.55f * alpha));
            }

            if (_renderer.sharedMaterial != null && _renderer.sharedMaterial.HasProperty(IntensityId))
            {
                _mpb.SetFloat(IntensityId, _baseIntensity * intensityScale * Mathf.Lerp(0.5f, 1.2f, alpha));
            }

            _renderer.SetPropertyBlock(_mpb);
        }

        /// <summary>
        /// 水平扇形薄片（XZ）+ 竖直高度，第三人称从后方能看到面。
        /// </summary>
        void Rebuild(float reveal01)
        {
            int segs = Mathf.Max(6, Mathf.RoundToInt(segments * reveal01));
            float angle = arcAngle * reveal01;
            float start = -angle * 0.5f;
            float step = angle / segs;
            float halfH = bladeHeight * 0.5f;

            // 每段列：内下、内上、外下、外上
            int vertCount = (segs + 1) * 4;
            var verts = new Vector3[vertCount];
            var uvs = new Vector2[vertCount];
            var cols = new Color[vertCount];
            // 每列两片四边形：内-外竖直片 + 上下盖？只用内外竖直环带即可
            var tris = new int[segs * 12];

            for (int i = 0; i <= segs; i++)
            {
                float a = (start + step * i) * Mathf.Deg2Rad;
                float sa = Mathf.Sin(a);
                float ca = Mathf.Cos(a);
                // 0° = +Z（角色前方）
                Vector3 dir = new Vector3(sa, 0f, ca);
                Vector3 inner = dir * innerRadius;
                Vector3 outer = dir * outerRadius;

                int b = i * 4;
                verts[b + 0] = inner + Vector3.up * -halfH;
                verts[b + 1] = inner + Vector3.up * halfH;
                verts[b + 2] = outer + Vector3.up * -halfH;
                verts[b + 3] = outer + Vector3.up * halfH;

                float u = segs > 0 ? i / (float)segs : 0f;
                uvs[b + 0] = new Vector2(u, 0f);
                uvs[b + 1] = new Vector2(u, 0.35f);
                uvs[b + 2] = new Vector2(u, 0.65f);
                uvs[b + 3] = new Vector2(u, 1f);

                float edge = 1f - Mathf.Abs(u * 2f - 1f) * 0.2f;
                cols[b + 0] = new Color(1f, 1f, 1f, 0.45f * edge);
                cols[b + 1] = new Color(1f, 1f, 1f, 0.7f * edge);
                cols[b + 2] = new Color(1f, 1f, 1f, 0.85f * edge);
                cols[b + 3] = new Color(1f, 1f, 1f, edge);
            }

            int t = 0;
            for (int i = 0; i < segs; i++)
            {
                int i0 = i * 4;
                int i1 = (i + 1) * 4;

                // 顶面（外上-内上）— 相机俯视可见
                tris[t++] = i0 + 1;
                tris[t++] = i0 + 3;
                tris[t++] = i1 + 3;
                tris[t++] = i0 + 1;
                tris[t++] = i1 + 3;
                tris[t++] = i1 + 1;

                // 外立面 — 侧后方可见
                tris[t++] = i0 + 2;
                tris[t++] = i0 + 3;
                tris[t++] = i1 + 3;
                tris[t++] = i0 + 2;
                tris[t++] = i1 + 3;
                tris[t++] = i1 + 2;
            }

            _mesh.Clear();
            _mesh.vertices = verts;
            _mesh.uv = uvs;
            _mesh.colors = cols;
            _mesh.triangles = tris;
            _mesh.RecalculateBounds();
            _mesh.RecalculateNormals();
        }
    }
}
