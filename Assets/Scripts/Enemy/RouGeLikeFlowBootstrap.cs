using UnityEngine;
using UnityEngine.SceneManagement;

namespace AttackSkill.Enemy
{
    /// <summary>进 GameScene 时自动给 RouGeLikePlane 挂上流程控制器。</summary>
    public static class RouGeLikeFlowBootstrap
    {
        const string SceneName = "GameScene";
        const string PlaneName = "RouGeLikePlane";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void OnAfterSceneLoad()
        {
            TryAttach();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == SceneName)
            {
                TryAttach();
            }
        }

        static void TryAttach()
        {
            if (SceneManager.GetActiveScene().name != SceneName)
            {
                return;
            }

            var plane = GameObject.Find(PlaneName);
            if (plane == null)
            {
                Debug.LogError(
                    $"[RouGeLike] GameScene 中找不到 \"{PlaneName}\"，肉鸽流程无法挂载。请确认场景物体名或预挂 RouGeLikeFlowController。");
                return;
            }

            if (plane.GetComponent<RouGeLikeFlowController>() == null)
            {
                plane.AddComponent<RouGeLikeFlowController>();
            }
        }
    }
}
