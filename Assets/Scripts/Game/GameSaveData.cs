using System;
using System.IO;
using UnityEngine;
using AttackSkill.UI;

namespace AttackSkill.Game
{
    /// <summary>
    /// 进度存档（与 <see cref="LocalAccountStore"/> 账号资料分离）。
    /// v1：场景+位姿+队员下标；v2：+性别快照+当前角色 HP；v3：+轮盘 T 技能下标。
    /// </summary>
    [Serializable]
    public class GameSaveData
    {
        public const int CurrentVersion = 3;

        public int version = CurrentVersion;
        public string sceneName;
        public float posX;
        public float posY;
        public float posZ;
        public float rotY;
        public int activeIndex;
        public long savedAtUnix;

        /// <summary>进度侧性别快照（账号资料仍以 LocalAccountStore 为准；读档用于校验/补齐）。</summary>
        public int gender;

        /// <summary>当前操控角色 HP；&lt;0 表示未记录（满血/默认）。</summary>
        public float activeHp = -1f;

        /// <summary>轮盘装备到 T 的技能下标（0–7）；&lt;0 表示未记录（用 PlayerPrefs/默认）。</summary>
        public int equippedSkillIndex = -1;

        public Vector3 Position
        {
            get => new Vector3(posX, posY, posZ);
            set
            {
                posX = value.x;
                posY = value.y;
                posZ = value.z;
            }
        }

        public Quaternion Rotation
        {
            get => Quaternion.Euler(0f, rotY, 0f);
            set => rotY = value.eulerAngles.y;
        }

        public OpenSceneGender Gender
        {
            get => gender == (int)OpenSceneGender.Male ? OpenSceneGender.Male : OpenSceneGender.Female;
            set => gender = (int)value;
        }

        public static GameSaveData Create(
            string scene,
            Vector3 position,
            Quaternion rotation,
            int activeIndex,
            OpenSceneGender genderSnapshot,
            float activeHpSnapshot = -1f,
            int equippedSkillIndexSnapshot = -1)
        {
            return new GameSaveData
            {
                version = CurrentVersion,
                sceneName = scene,
                Position = position,
                Rotation = rotation,
                activeIndex = Mathf.Max(0, activeIndex),
                Gender = genderSnapshot,
                activeHp = activeHpSnapshot,
                equippedSkillIndex = equippedSkillIndexSnapshot,
                savedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
        }

        /// <summary>旧档升级到 CurrentVersion；并把已废弃场景名映射到当前局内场景。</summary>
        public void MigrateToCurrent()
        {
            // 场景改名：SampleScene → GameScene（任意版本旧档都映射）
            if (string.Equals(sceneName, "SampleScene", StringComparison.Ordinal))
            {
                sceneName = "GameScene";
            }

            if (version >= CurrentVersion)
            {
                version = CurrentVersion;
                return;
            }

            if (version < 2)
            {
                if (!Enum.IsDefined(typeof(OpenSceneGender), gender))
                {
                    Gender = LocalAccountStore.HasGender
                        ? LocalAccountStore.Gender
                        : OpenSceneGender.Female;
                }

                if (activeHp < 0f)
                {
                    activeHp = -1f;
                }
            }

            if (version < 3)
            {
                // 旧档无字段时保持 -1，启动时走 PlayerPrefs
                if (equippedSkillIndex < -1 || equippedSkillIndex >= 8)
                {
                    equippedSkillIndex = -1;
                }
            }

            version = CurrentVersion;
        }
    }

    /// <summary>JSON 进度存档读写 + 启动时 PendingRestore。不含账号密码。</summary>
    public static class GameSaveService
    {
        const string FileName = "game_progress.json";

        static GameSaveData _pendingRestore;

        public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

        public static bool HasPendingRestore => _pendingRestore != null;

        /// <summary>仅清内存 Pending，不删磁盘档。</summary>
        public static void ClearPendingRestore()
        {
            _pendingRestore = null;
        }

        public static void SetPendingRestore(GameSaveData data)
        {
            if (data != null)
            {
                data.MigrateToCurrent();
            }

            _pendingRestore = data;
        }

        public static bool TryConsumePendingRestore(out GameSaveData data)
        {
            data = _pendingRestore;
            _pendingRestore = null;
            if (data != null)
            {
                data.MigrateToCurrent();
            }

            return data != null && !string.IsNullOrEmpty(data.sceneName);
        }

        public static bool TryPeekPendingRestore(out GameSaveData data)
        {
            data = _pendingRestore;
            return data != null && !string.IsNullOrEmpty(data.sceneName);
        }

        public static bool Exists()
        {
            return File.Exists(SavePath);
        }

        public static bool TryLoad(out GameSaveData data)
        {
            data = null;
            try
            {
                if (!File.Exists(SavePath))
                {
                    return false;
                }

                string json = File.ReadAllText(SavePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return false;
                }

                data = JsonUtility.FromJson<GameSaveData>(json);
                if (data == null || string.IsNullOrEmpty(data.sceneName))
                {
                    data = null;
                    return false;
                }

                data.MigrateToCurrent();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GameSave] 读档失败：{e.Message}");
                data = null;
                return false;
            }
        }

        public static bool Save(GameSaveData data)
        {
            if (data == null || string.IsNullOrEmpty(data.sceneName))
            {
                return false;
            }

            try
            {
                data.version = GameSaveData.CurrentVersion;
                data.savedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                string json = JsonUtility.ToJson(data, prettyPrint: true);
                File.WriteAllText(SavePath, json);
                Debug.Log(
                    $"[GameSave] 已保存 → {SavePath}\n{data.sceneName} slot={data.activeIndex} hp={data.activeHp} gender={data.Gender} skillT={data.equippedSkillIndex}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameSave] 存档失败：{e.Message}");
                return false;
            }
        }

        public static bool Delete()
        {
            try
            {
                if (File.Exists(SavePath))
                {
                    File.Delete(SavePath);
                }

                _pendingRestore = null;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GameSave] 删档失败：{e.Message}");
                return false;
            }
        }
    }
}
