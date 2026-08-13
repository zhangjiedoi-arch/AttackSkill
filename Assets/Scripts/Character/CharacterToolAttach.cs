using UnityEngine;
using Object = UnityEngine.Object;

namespace AttackSkill.Character
{
    /// <summary>
    /// 在角色序列化挂点下显示/隐藏工具 Prefab。
    /// Prefab 只从 <see cref="CharacterRuntimeSettings"/> 读取，不走 Resources 副本。
    /// </summary>
    public static class CharacterToolAttach
    {
        const string InstanceMotorcycle = "Tool_Motorcycle";
        const string InstanceSword = "Tool_Sword";
        const string InstanceWings = "Tool_Wings";

        public static void ShowMotorcycle(Transform characterRoot) =>
            Show(ResolveSockets(characterRoot)?.Motorcycle, ResolveMotorcyclePrefab(), InstanceMotorcycle, "Motorcycle");

        public static void HideMotorcycle(Transform characterRoot) =>
            Hide(ResolveSockets(characterRoot)?.Motorcycle, InstanceMotorcycle);

        /// <summary>摩托挂点（Motorcycle_pos）。</summary>
        public static Transform GetMotorcycleSocket(Transform characterRoot) =>
            ResolveSockets(characterRoot)?.Motorcycle;

        public static void ShowSword(Transform characterRoot) =>
            Show(ResolveSwordSocket(characterRoot), ResolveSwordPrefab(), InstanceSword, "Sword");

        public static void HideSword(Transform characterRoot) =>
            Hide(ResolveSwordSocket(characterRoot), InstanceSword);

        /// <summary>御剑挂点（Sword_pos）；未配置时回退 weapon。</summary>
        public static Transform GetSwordSocket(Transform characterRoot) =>
            ResolveSwordSocket(characterRoot);

        /// <summary>翅膀挂点（wings_pos）。</summary>
        public static Transform GetWingsSocket(Transform characterRoot) =>
            ResolveSockets(characterRoot)?.Wings;

        public static void ShowWings(Transform characterRoot) =>
            Show(ResolveSockets(characterRoot)?.Wings, ResolveWingsPrefab(), InstanceWings, "Wings");

        public static void HideWings(Transform characterRoot) =>
            Hide(ResolveSockets(characterRoot)?.Wings, InstanceWings);

        public static void ShowSwordFlightTools(Transform characterRoot)
        {
            ShowSword(characterRoot);
            ShowWings(characterRoot);
        }

        public static void HideSwordFlightTools(Transform characterRoot)
        {
            HideSword(characterRoot);
            HideWings(characterRoot);
        }

        static void Show(Transform socket, GameObject prefab, string instanceName, string label)
        {
            if (socket == null)
            {
                Debug.LogWarning($"[CharacterToolAttach] 未配置挂点「{label}」，请在 CharacterAvatar 上序列化 Tool Sockets。");
                return;
            }

            if (!socket.gameObject.activeSelf)
            {
                socket.gameObject.SetActive(true);
            }

            Transform existing = FindDirectChild(socket, instanceName);
            if (existing != null)
            {
                existing.gameObject.SetActive(true);
                return;
            }

            if (prefab == null)
            {
                Debug.LogWarning(
                    $"[CharacterToolAttach] Prefab「{label}」为空。请在 CharacterRuntimeSettings 指定 Tools Prefab（Prefabs/Tools）。");
                return;
            }

            Vector3 prefabScale = prefab.transform.localScale;
            var instance = Object.Instantiate(prefab, socket, false);
            instance.name = instanceName;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = prefabScale;
            instance.SetActive(true);
        }

        static void Hide(Transform socket, string instanceName)
        {
            if (socket == null)
            {
                return;
            }

            Transform existing = FindDirectChild(socket, instanceName);
            if (existing != null)
            {
                existing.gameObject.SetActive(false);
            }
        }

        static CharacterAvatar.ToolSockets ResolveSockets(Transform characterRoot)
        {
            var avatar = FindAvatar(characterRoot);
            return avatar != null ? avatar.Tools : null;
        }

        static Transform ResolveSwordSocket(Transform characterRoot)
        {
            var avatar = FindAvatar(characterRoot);
            if (avatar == null)
            {
                return null;
            }

            if (avatar.Tools != null && avatar.Tools.Sword != null)
            {
                return avatar.Tools.Sword;
            }

            // 兼容：旧 Prefab 把 Sword_pos 绑在 weapon 上
            return avatar.Weapon;
        }

        static CharacterAvatar FindAvatar(Transform characterRoot)
        {
            if (characterRoot == null)
            {
                return null;
            }

            var avatar = characterRoot.GetComponent<CharacterAvatar>();
            if (avatar != null)
            {
                return avatar;
            }

            return characterRoot.GetComponentInChildren<CharacterAvatar>(true);
        }

        static Transform FindDirectChild(Transform parent, string name)
        {
            if (parent == null)
            {
                return null;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child != null && child.name == name)
                {
                    return child;
                }
            }

            return null;
        }

        static GameObject ResolveMotorcyclePrefab()
        {
            var settings = CharacterRuntimeSettings.Get();
            return settings != null ? settings.motorcyclePrefab : null;
        }

        static GameObject ResolveSwordPrefab()
        {
            var settings = CharacterRuntimeSettings.Get();
            return settings != null ? settings.swordPrefab : null;
        }

        static GameObject ResolveWingsPrefab()
        {
            var settings = CharacterRuntimeSettings.Get();
            return settings != null ? settings.wingsPrefab : null;
        }
    }
}
