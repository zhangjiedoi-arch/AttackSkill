using UnityEngine;
using UnityEngine.UI;

namespace AttackSkill.UI
{
    /// <summary>小地图单位图标：根节点定位，<c>imgIcon</c> 换头像。</summary>
    public sealed class SmallMapIconView : MonoBehaviour
    {
        [SerializeField] Image imgIcon;

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
        }

        public bool HasIcon => imgIcon != null && imgIcon.sprite != null;

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
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }
    }
}
