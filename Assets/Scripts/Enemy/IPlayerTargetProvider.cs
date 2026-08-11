using UnityEngine;

namespace AttackSkill.Enemy
{
    /// <summary>供敌人仇恨系统查询当前可攻击的玩家。</summary>
    public interface IPlayerTargetProvider
    {
        Transform GetActivePlayerTransform();
        bool IsActivePlayer(Component component);
    }

    /// <summary>全局查找：优先已注册 Provider，同帧缓存结果。</summary>
    public static class PlayerTargetLocator
    {
        static IPlayerTargetProvider _provider;
        static Transform _frameCache;
        static int _frameCacheId = -1;
        static bool _frameResolved;

        public static void Register(IPlayerTargetProvider provider)
        {
            _provider = provider;
            InvalidateCache();
        }

        public static void Unregister(IPlayerTargetProvider provider)
        {
            if (_provider == provider)
            {
                _provider = null;
            }

            InvalidateCache();
        }

        public static void InvalidateCache()
        {
            _frameCacheId = -1;
            _frameCache = null;
            _frameResolved = false;
        }

        public static Transform GetActivePlayerTransform()
        {
            int frame = Time.frameCount;
            if (_frameResolved && _frameCacheId == frame)
            {
                return _frameCache;
            }

            _frameCache = ResolveActivePlayerTransform();
            _frameCacheId = frame;
            _frameResolved = true;
            return _frameCache;
        }

        static Transform ResolveActivePlayerTransform()
        {
            // 已注册 Provider 时以其为准（含死亡返回 null），不再 Find 扫描
            if (_provider != null)
            {
                return _provider.GetActivePlayerTransform();
            }

            var party = Character.PartyController.Instance;
            if (party != null && party.Active != null && !party.Active.IsDead)
            {
                return party.Active.transform;
            }

            var chars = Object.FindObjectsOfType<Character.HSM.GenshinLikeCharacter>();
            for (int i = 0; i < chars.Length; i++)
            {
                var c = chars[i];
                if (c != null && c.IsActive && !c.IsDead)
                {
                    return c.transform;
                }
            }

            return null;
        }

        public static bool IsActivePlayer(Component component)
        {
            if (component == null)
            {
                return false;
            }

            if (_provider != null)
            {
                return _provider.IsActivePlayer(component);
            }

            var character = component.GetComponentInParent<Character.HSM.GenshinLikeCharacter>();
            return character != null && character.IsActive && !character.IsDead;
        }
    }
}
