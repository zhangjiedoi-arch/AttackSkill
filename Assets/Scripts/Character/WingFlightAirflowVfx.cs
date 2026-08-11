using UnityEngine;

namespace AttackSkill.Character
{
    /// <summary>
    /// 飞行气流：Sparks blue Prefab。翅膀左右各一，御剑居中一个。
    /// </summary>
    public sealed class WingFlightAirflowVfx : MonoBehaviour
    {
        public const string RootName = "ExplorationFlight_Airflow";

        const string CenterName = "SparksBlue_Airflow";
        const string LeftName = "SparksBlue_Airflow_L";
        const string RightName = "SparksBlue_Airflow_R";

        const float StrengthEpsilon = 0.01f;
        const float DefaultFallbackLocalY = 1.05f;
        static readonly Vector3 DefaultLocalOffset = new Vector3(0f, 0f, -0.5f);
        // 与原先代码左右粒子挂点一致
        static readonly Vector3 LeftLocalPos = new Vector3(-0.55f, 0.05f, -0.15f);
        static readonly Vector3 RightLocalPos = new Vector3(0.55f, 0.05f, -0.15f);

        GameObject _center;
        GameObject _left;
        GameObject _right;
        ParticleSystem[] _systems;
        float[] _baseRates;
        float[] _baseSpeeds;
        float _lastStrength = -1f;
        bool _built;

        public static WingFlightAirflowVfx Ensure(Transform body)
        {
            if (body == null)
            {
                return null;
            }

            DestroyLegacyRoots(body);

            WingFlightAirflowVfx existing = FindDirect(body);
            if (existing != null)
            {
                existing.EnsureBuilt();
                return existing;
            }

            var go = new GameObject(RootName);
            go.transform.SetParent(body, false);
            go.transform.localRotation = Quaternion.identity;
            var vfx = go.AddComponent<WingFlightAirflowVfx>();
            vfx.EnsureBuilt();
            go.SetActive(false);
            return vfx;
        }

        public void ShowForWingFlight(Transform body, Transform wingsSocket)
        {
            ApplyAnchor(body, wingsSocket);
            EnsureBuilt();
            ApplyLayout(dualWing: true);
            ShowActive();
        }

        public void ShowForSwordFlight(Transform body, Transform swordSocket)
        {
            ApplyAnchor(body, swordSocket);
            EnsureBuilt();
            ApplyLayout(dualWing: false);
            ShowActive();
        }

        public void Hide()
        {
            StopSystems(clear: true);
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }

            _lastStrength = -1f;
        }

        public void SetStrength(float t01)
        {
            t01 = Mathf.Clamp01(t01);
            if (_lastStrength >= 0f && Mathf.Abs(t01 - _lastStrength) < StrengthEpsilon)
            {
                return;
            }

            _lastStrength = t01;
            if (_systems == null || _baseRates == null)
            {
                return;
            }

            float rateMul = Mathf.Lerp(0.45f, 1.35f, t01);
            float speedMul = Mathf.Lerp(0.55f, 1.35f, t01);
            for (int i = 0; i < _systems.Length; i++)
            {
                ParticleSystem ps = _systems[i];
                if (ps == null)
                {
                    continue;
                }

                var emission = ps.emission;
                emission.rateOverTime = _baseRates[i] * rateMul;

                var main = ps.main;
                main.startSpeed = _baseSpeeds[i] * speedMul;
            }
        }

        void ShowActive()
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            SetStrength(0.35f);
            PlaySystems();
        }

        void ApplyLayout(bool dualWing)
        {
            if (dualWing)
            {
                EnsureSideInstances();
                SetActiveSafe(_center, false);
                SetActiveSafe(_left, true);
                SetActiveSafe(_right, true);
                if (_left != null)
                {
                    _left.transform.localPosition = LeftLocalPos;
                    _left.transform.localRotation = Quaternion.identity;
                    _left.transform.localScale = Vector3.one;
                }

                if (_right != null)
                {
                    _right.transform.localPosition = RightLocalPos;
                    _right.transform.localRotation = Quaternion.identity;
                    _right.transform.localScale = Vector3.one;
                }

                CacheSystemsFrom(_left, _right);
            }
            else
            {
                EnsureCenterInstance();
                SetActiveSafe(_left, false);
                SetActiveSafe(_right, false);
                SetActiveSafe(_center, true);
                if (_center != null)
                {
                    _center.transform.localPosition = Vector3.zero;
                    _center.transform.localRotation = Quaternion.identity;
                    _center.transform.localScale = Vector3.one;
                }

                CacheSystemsFrom(_center);
            }
        }

        void ApplyAnchor(Transform body, Transform heightSocket)
        {
            ResolveOffset(body, out Vector3 offset, out float fallbackY);

            float localY = fallbackY;
            if (body != null && heightSocket != null)
            {
                localY = body.InverseTransformPoint(heightSocket.position).y;
            }

            transform.localPosition = new Vector3(offset.x, localY + offset.y, offset.z);
            transform.localRotation = Quaternion.identity;
        }

        static void ResolveOffset(Transform body, out Vector3 offset, out float fallbackY)
        {
            offset = DefaultLocalOffset;
            fallbackY = DefaultFallbackLocalY;

            var avatar = body != null ? body.GetComponent<CharacterAvatar>() : null;
            if (avatar == null && body != null)
            {
                avatar = body.GetComponentInChildren<CharacterAvatar>(true);
            }

            var settings = CharacterRuntimeSettings.Get();
            if (settings != null)
            {
                offset = settings.flightAirflowLocalOffset;
                fallbackY = settings.flightAirflowFallbackLocalY;
            }

            if (avatar != null && avatar.OverrideAirflowOffset)
            {
                offset = avatar.AirflowLocalOffset;
                fallbackY = avatar.AirflowFallbackLocalY;
            }
        }

        static WingFlightAirflowVfx FindDirect(Transform body)
        {
            for (int i = 0; i < body.childCount; i++)
            {
                Transform child = body.GetChild(i);
                if (child == null || child.name != RootName)
                {
                    continue;
                }

                var vfx = child.GetComponent<WingFlightAirflowVfx>();
                if (vfx != null)
                {
                    return vfx;
                }
            }

            return null;
        }

        static void DestroyLegacyRoots(Transform body)
        {
            for (int i = body.childCount - 1; i >= 0; i--)
            {
                Transform child = body.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                string n = child.name;
                if (n == "WingFlight_Airflow" || n == "SwordFlight_Airflow")
                {
                    Destroy(child.gameObject);
                }
            }
        }

        void EnsureBuilt()
        {
            if (_built)
            {
                return;
            }

            _built = true;
            DestroyProceduralEmitters();
            EnsureCenterInstance();
        }

        void EnsureCenterInstance()
        {
            if (_center != null)
            {
                return;
            }

            _center = FindOrCreateInstance(CenterName, Vector3.zero);
        }

        void EnsureSideInstances()
        {
            if (_left == null)
            {
                _left = FindOrCreateInstance(LeftName, LeftLocalPos);
            }

            if (_right == null)
            {
                _right = FindOrCreateInstance(RightName, RightLocalPos);
            }
        }

        GameObject FindOrCreateInstance(string name, Vector3 localPos)
        {
            Transform existing = transform.Find(name);
            if (existing != null)
            {
                existing.localPosition = localPos;
                existing.localRotation = Quaternion.identity;
                existing.localScale = Vector3.one;
                return existing.gameObject;
            }

            GameObject prefab = ResolvePrefab();
            if (prefab == null)
            {
                Debug.LogWarning(
                    "[WingFlightAirflowVfx] 未配置 flightAirflowVfxPrefab（Prefabs/VFX/Sparks blue）。",
                    this);
                return null;
            }

            var go = Instantiate(prefab, transform, false);
            go.name = name;
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go;
        }

        static GameObject ResolvePrefab()
        {
            var settings = CharacterRuntimeSettings.Get();
            return settings != null ? settings.GetFlightAirflowVfx() : null;
        }

        void DestroyProceduralEmitters()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                if (child.name == "Airflow_L" || child.name == "Airflow_R")
                {
                    Destroy(child.gameObject);
                }
            }
        }

        void CacheSystemsFrom(params GameObject[] roots)
        {
            int count = 0;
            for (int r = 0; r < roots.Length; r++)
            {
                if (roots[r] == null || !roots[r].activeSelf)
                {
                    continue;
                }

                count += roots[r].GetComponentsInChildren<ParticleSystem>(true).Length;
            }

            if (count == 0)
            {
                _systems = null;
                _baseRates = null;
                _baseSpeeds = null;
                return;
            }

            _systems = new ParticleSystem[count];
            _baseRates = new float[count];
            _baseSpeeds = new float[count];
            int write = 0;
            for (int r = 0; r < roots.Length; r++)
            {
                GameObject root = roots[r];
                if (root == null || !root.activeSelf)
                {
                    continue;
                }

                var list = root.GetComponentsInChildren<ParticleSystem>(true);
                for (int i = 0; i < list.Length; i++)
                {
                    ParticleSystem ps = list[i];
                    var main = ps.main;
                    main.playOnAwake = false;

                    float rate = ps.emission.rateOverTime.constant;
                    float speed = main.startSpeed.constant;
                    _systems[write] = ps;
                    _baseRates[write] = rate > 0.01f ? rate : 20f;
                    _baseSpeeds[write] = speed > 0.01f ? speed : 6.5f;
                    write++;
                }
            }
        }

        static void SetActiveSafe(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active)
            {
                go.SetActive(active);
            }
        }

        void PlaySystems()
        {
            if (_systems == null)
            {
                return;
            }

            for (int i = 0; i < _systems.Length; i++)
            {
                ParticleSystem ps = _systems[i];
                if (ps != null && !ps.isPlaying)
                {
                    ps.Play(true);
                }
            }
        }

        void StopSystems(bool clear)
        {
            if (_systems == null)
            {
                return;
            }

            var behavior = clear
                ? ParticleSystemStopBehavior.StopEmittingAndClear
                : ParticleSystemStopBehavior.StopEmitting;
            for (int i = 0; i < _systems.Length; i++)
            {
                ParticleSystem ps = _systems[i];
                if (ps != null)
                {
                    ps.Stop(true, behavior);
                }
            }
        }
    }
}
