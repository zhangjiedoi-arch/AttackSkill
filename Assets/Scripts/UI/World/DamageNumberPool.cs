using System.Collections.Generic;
using UnityEngine;
using AttackSkill.Combat;

namespace AttackSkill.UI.World
{
    /// <summary>伤害跳字对象池。</summary>
    public sealed class DamageNumberPool
    {
        readonly GameObject _prefab;
        readonly Transform _parent;
        readonly WorldUiService _ui;
        readonly Stack<DamageNumberView> _free = new Stack<DamageNumberView>(16);

        public DamageNumberPool(GameObject prefab, Transform parent, WorldUiService ui)
        {
            _prefab = prefab;
            _parent = parent;
            _ui = ui;
        }

        public void Prewarm(int count)
        {
            if (_prefab == null)
            {
                return;
            }

            for (int i = 0; i < count; i++)
            {
                var view = Create();
                view.StopAndHide();
                _free.Push(view);
            }
        }

        public void Spawn(float amount, Vector3 worldPosition, float lifetime, Transform ignoreRoot,
            bool isCritical = false, CombatElement element = CombatElement.Light)
        {
            if (_prefab == null || _ui == null)
            {
                return;
            }

            DamageNumberView view = null;
            while (_free.Count > 0)
            {
                view = _free.Pop();
                if (view != null)
                {
                    break;
                }
            }

            if (view == null)
            {
                view = Create();
            }

            view.Play(amount, worldPosition, lifetime, _ui, ignoreRoot, Return, isCritical, element);
        }

        public void Dispose() => _free.Clear();

        void Return(DamageNumberView view)
        {
            if (view != null)
            {
                _free.Push(view);
            }
        }

        DamageNumberView Create()
        {
            var go = Object.Instantiate(_prefab, _parent, false);
            go.name = "DamageNumber";
            WorldUiScreen.PrepareOverlayItem(go);
            var view = go.GetComponent<DamageNumberView>() ?? go.AddComponent<DamageNumberView>();
            return view;
        }
    }
}
