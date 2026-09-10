using UnityEngine;
using UnityEngine.UI;

namespace AttackSkill.UI
{
    /// <summary>小地图单位图标：根节点定位，<c>imgIcon</c> 换头像。</summary>
    public sealed class SmallMapIconView : MonoBehaviour
    {
        [SerializeField] Image imgIcon;
        [SerializeField] Image imgFinish;

        RectTransform _rt;
        Enemy.EnemyAgent _enemy;

        public Enemy.EnemyAgent BoundEnemy => _enemy;
        public RectTransform Rect => _rt != null ? _rt : (_rt = transform as RectTransform);

        public void EnsureBound()
        {
            if (_rt == null)
            {
                _rt = transform as RectTransform;
            }

            if (imgIcon == null)
            {
                Transform t = FindNamed(transform, "imgIcon");
                if (t != null)
                {
                    imgIcon = t.GetComponent<Image>();
                }
            }

            if (imgFinish == null)
            {
                Transform t = FindNamed(transform, "imgFinish");
                if (t != null)
                {
                    imgFinish = t.GetComponent<Image>();
                }
            }
        }

        public bool HasIcon => imgIcon != null && imgIcon.sprite != null;

        public void SetIconRaycast(bool enabled)
        {
            EnsureBound();
            if (imgIcon != null)
            {
                imgIcon.raycastTarget = enabled;
            }

            if (imgFinish != null)
            {
                imgFinish.raycastTarget = false;
            }
        }

        public void BindEnemy(Enemy.EnemyAgent agent, Sprite icon)
        {
            EnsureBound();
            _enemy = agent;
            ApplyIcon(icon);
        }

        public void BindPlayer(Sprite icon)
        {
            EnsureBound();
            _enemy = null;
            ApplyIcon(icon);
        }

        public void ApplyIcon(Sprite icon)
        {
            EnsureBound();
            if (imgIcon == null)
            {
                return;
            }

            if (icon != null)
            {
                imgIcon.sprite = icon;
                imgIcon.enabled = true;
                imgIcon.SetAllDirty();
            }

            imgIcon.preserveAspect = true;
        }

        public void BindMarker(Sprite icon, bool finished)
        {
            EnsureBound();
            _enemy = null;
            ApplyIcon(icon);
            SetFinished(finished);
        }

        public void SetFinished(bool finished)
        {
            EnsureBound();
            if (imgFinish == null)
            {
                return;
            }

            if (finished)
            {
                Sprite badge = SmallMapIconCatalog.ForMarker("map_finished");
                if (badge != null)
                {
                    imgFinish.sprite = badge;
                }

                imgFinish.preserveAspect = true;
                imgFinish.enabled = imgFinish.sprite != null;
                if (!imgFinish.gameObject.activeSelf)
                {
                    imgFinish.gameObject.SetActive(true);
                }
            }
            else if (imgFinish.gameObject.activeSelf)
            {
                imgFinish.gameObject.SetActive(false);
            }
        }

        public void SetPixelSize(float pixels)
        {
            EnsureBound();
            if (_rt == null)
            {
                return;
            }

            float size = Mathf.Max(8f, pixels);
            _rt.sizeDelta = new Vector2(size, size);
            if (imgIcon != null)
            {
                imgIcon.rectTransform.sizeDelta = new Vector2(size, size);
            }
        }

        static Transform FindNamed(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            var all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i] != root && all[i].name == name)
                {
                    return all[i];
                }
            }

            return null;
        }

        public void Place(Vector2 anchored, float zDegrees)
        {
            EnsureBound();
            if (_rt == null)
            {
                return;
            }

            _rt.anchorMin = new Vector2(0.5f, 0.5f);
            _rt.anchorMax = new Vector2(0.5f, 0.5f);
            _rt.pivot = new Vector2(0.5f, 0.5f);
            _rt.anchoredPosition = anchored;
            _rt.localEulerAngles = new Vector3(0f, 0f, zDegrees);
            _rt.localScale = Vector3.one;
        }

        public void Recycle()
        {
            _enemy = null;
            SetFinished(false);
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }
    }
}
