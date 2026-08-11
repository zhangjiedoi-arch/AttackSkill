using UnityEngine;

namespace AttackSkill.Environment
{
    /// <summary>
    /// 假海浪：滚动材质 MainTex UV。可不定时切换速度与方向。
    /// </summary>
    public sealed class MaterialUvScroll : MonoBehaviour
    {
        [SerializeField] string materialNameContains = "2.Material.001d02";
        [SerializeField] Vector2 scrollSpeed = new Vector2(0.035f, 0.02f);
        [SerializeField] string textureProperty = "_MainTex";
        [SerializeField] bool useMaterialInstance = true;
        [SerializeField] Material targetMaterial;

        [Header("不定时变速/变向")]
        [SerializeField] bool varySpeedAndDirection = true;
        [Tooltip("两次切换之间的最短间隔（秒）")]
        [SerializeField] float changeIntervalMin = 2.5f;
        [Tooltip("两次切换之间的最长间隔（秒）")]
        [SerializeField] float changeIntervalMax = 7f;
        [Tooltip("速度大小下限")]
        [SerializeField] float speedMin = 0.015f;
        [Tooltip("速度大小上限")]
        [SerializeField] float speedMax = 0.06f;
        [Tooltip("切换到新速度时的平滑时间（秒）")]
        [SerializeField] float blendDuration = 1.5f;
        [Tooltip("是否允许完全反向")]
        [SerializeField] bool allowReverse = true;

        Renderer[] _renderers;
        Material _runtimeMat;
        Vector2 _offset;
        bool _ownsMaterial;

        Vector2 _currentSpeed;
        Vector2 _fromSpeed;
        Vector2 _toSpeed;
        float _changeTimer;
        float _blendTimer;
        bool _blending;

        void Awake()
        {
            ResolveMaterial();
            _currentSpeed = scrollSpeed;
            _fromSpeed = scrollSpeed;
            _toSpeed = scrollSpeed;
            ScheduleNextChange();
        }

        void Update()
        {
            if (_runtimeMat == null)
            {
                return;
            }

            if (varySpeedAndDirection)
            {
                TickVariation(Time.deltaTime);
                _offset += _currentSpeed * Time.deltaTime;
            }
            else
            {
                _offset += scrollSpeed * Time.deltaTime;
            }

            _runtimeMat.SetTextureOffset(textureProperty, _offset);
        }

        void TickVariation(float dt)
        {
            if (_blending)
            {
                _blendTimer += dt;
                float t = blendDuration <= 0.0001f ? 1f : Mathf.Clamp01(_blendTimer / blendDuration);
                // Smoothstep，换向更自然
                t = t * t * (3f - 2f * t);
                _currentSpeed = Vector2.Lerp(_fromSpeed, _toSpeed, t);
                if (t >= 1f)
                {
                    _blending = false;
                    _currentSpeed = _toSpeed;
                }

                return;
            }

            _changeTimer -= dt;
            if (_changeTimer > 0f)
            {
                return;
            }

            BeginNewTargetSpeed();
            ScheduleNextChange();
        }

        void BeginNewTargetSpeed()
        {
            float mag = Random.Range(Mathf.Min(speedMin, speedMax), Mathf.Max(speedMin, speedMax));
            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            // 略偏向初始 scrollSpeed 的主方向，避免完全乱飘
            Vector2 bias = scrollSpeed.sqrMagnitude > 0.000001f ? scrollSpeed.normalized : Vector2.right;
            dir = (dir + bias * 0.35f).normalized;

            if (!allowReverse && Vector2.Dot(dir, bias) < 0f)
            {
                dir = -dir;
            }

            _fromSpeed = _currentSpeed;
            _toSpeed = dir * mag;
            _blendTimer = 0f;
            _blending = true;
        }

        void ScheduleNextChange()
        {
            float min = Mathf.Min(changeIntervalMin, changeIntervalMax);
            float max = Mathf.Max(changeIntervalMin, changeIntervalMax);
            _changeTimer = Random.Range(min, max);
        }

        void OnDestroy()
        {
            if (_ownsMaterial && _runtimeMat != null)
            {
                Destroy(_runtimeMat);
            }
        }

        public void ResolveMaterial()
        {
            _ownsMaterial = false;
            if (targetMaterial != null)
            {
                if (useMaterialInstance)
                {
                    _runtimeMat = Instantiate(targetMaterial);
                    _runtimeMat.name = targetMaterial.name + " (WaterScroll)";
                    _ownsMaterial = true;
                }
                else
                {
                    _runtimeMat = targetMaterial;
                }

                ApplyInstanceToMatchingRenderers();
                return;
            }

            _renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < _renderers.Length; i++)
            {
                var mats = _renderers[i].sharedMaterials;
                for (int m = 0; m < mats.Length; m++)
                {
                    var mat = mats[m];
                    if (mat == null)
                    {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(materialNameContains) &&
                        !mat.name.Contains(materialNameContains))
                    {
                        continue;
                    }

                    if (useMaterialInstance)
                    {
                        _runtimeMat = _renderers[i].materials[m];
                    }
                    else
                    {
                        _runtimeMat = mat;
                    }

                    return;
                }
            }
        }

        void ApplyInstanceToMatchingRenderers()
        {
            if (_runtimeMat == null)
            {
                return;
            }

            _renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < _renderers.Length; i++)
            {
                var mats = _renderers[i].sharedMaterials;
                bool dirty = false;
                for (int m = 0; m < mats.Length; m++)
                {
                    var mat = mats[m];
                    if (mat == null)
                    {
                        continue;
                    }

                    bool match = targetMaterial != null
                        ? mat == targetMaterial || mat.name.StartsWith(targetMaterial.name)
                        : mat.name.Contains(materialNameContains);
                    if (!match)
                    {
                        continue;
                    }

                    mats[m] = _runtimeMat;
                    dirty = true;
                }

                if (dirty)
                {
                    _renderers[i].sharedMaterials = mats;
                }
            }
        }
    }
}
