using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace AttackSkill.UI
{
    /// <summary>
    /// 本地「账号资料」：账号哈希、性别偏好（PlayerPrefs）。
    /// 与进度存档 <see cref="AttackSkill.Game.GameSaveData"/> 分离：此处不含场景/坐标/HP。
    /// </summary>
    public static class LocalAccountStore
    {
        const string KeyAccount = "AttackSkill.Local.Account";
        const string KeyPassword = "AttackSkill.Local.Password";
        const string KeyPasswordHash = "AttackSkill.Local.PasswordHash";
        const string KeyPasswordSalt = "AttackSkill.Local.PasswordSalt";
        const string KeyGender = "AttackSkill.Local.Gender";
        const string KeyHasGender = "AttackSkill.Local.HasGender";
        const string KeyGenderLocked = "AttackSkill.Local.GenderLocked";

        public static bool HasAccount => !string.IsNullOrEmpty(Account);

        public static bool HasGender => PlayerPrefs.GetInt(KeyHasGender, 0) == 1;

        /// <summary>进入 GameScene 后为 true；OpenScene 会解锁以便改性别。</summary>
        public static bool IsGenderLocked => PlayerPrefs.GetInt(KeyGenderLocked, 0) == 1;

        public static bool HasPasswordHash =>
            !string.IsNullOrEmpty(PlayerPrefs.GetString(KeyPasswordHash, string.Empty));

        public static string Account
        {
            get => PlayerPrefs.GetString(KeyAccount, string.Empty);
            private set => PlayerPrefs.SetString(KeyAccount, value ?? string.Empty);
        }

        public static OpenSceneGender Gender
        {
            get
            {
                int v = PlayerPrefs.GetInt(KeyGender, (int)OpenSceneGender.Female);
                return v == (int)OpenSceneGender.Male ? OpenSceneGender.Male : OpenSceneGender.Female;
            }
            private set => PlayerPrefs.SetInt(KeyGender, (int)value);
        }

        public static void SaveAccount(string account, string password)
        {
            Account = account != null ? account.Trim() : string.Empty;
            PurgeLegacyPlaintextPassword();

            if (!string.IsNullOrEmpty(password))
            {
                string salt = NewSalt();
                PlayerPrefs.SetString(KeyPasswordSalt, salt);
                PlayerPrefs.SetString(KeyPasswordHash, HashPassword(Account, password, salt));
            }

            PlayerPrefs.Save();
        }

        public static bool ValidateCredentials(string account, string password)
        {
            string trimmed = account != null ? account.Trim() : string.Empty;
            if (string.IsNullOrEmpty(trimmed))
            {
                return false;
            }

            if (!HasAccount)
            {
                return !string.IsNullOrEmpty(password);
            }

            if (!string.Equals(Account, trimmed, StringComparison.Ordinal))
            {
                return false;
            }

            if (!HasPasswordHash)
            {
                return !string.IsNullOrEmpty(password);
            }

            string salt = PlayerPrefs.GetString(KeyPasswordSalt, string.Empty);
            string expected = PlayerPrefs.GetString(KeyPasswordHash, string.Empty);
            string actual = HashPassword(trimmed, password ?? string.Empty, salt);
            return string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>写性别；已锁定时忽略并返回 false（可用 <see cref="UnlockGender"/> 解除）。</summary>
        public static bool SaveGender(OpenSceneGender gender)
        {
            if (IsGenderLocked)
            {
                Debug.LogWarning($"[LocalAccountStore] 性别已锁定为 {Gender}，忽略改为 {gender}。");
                return false;
            }

            Gender = gender;
            PlayerPrefs.SetInt(KeyHasGender, 1);
            PlayerPrefs.Save();
            return true;
        }

        /// <summary>进入游戏时调用；局内设置只读。</summary>
        public static void LockGender()
        {
            if (!HasGender)
            {
                Gender = OpenSceneGender.Female;
                PlayerPrefs.SetInt(KeyHasGender, 1);
            }

            PlayerPrefs.SetInt(KeyGenderLocked, 1);
            PlayerPrefs.Save();
        }

        /// <summary>回到 OpenScene 时调用，允许再次修改性别。</summary>
        public static void UnlockGender()
        {
            if (!IsGenderLocked)
            {
                return;
            }

            PlayerPrefs.SetInt(KeyGenderLocked, 0);
            PlayerPrefs.Save();
        }

        public static void ClearAll()
        {
            PlayerPrefs.DeleteKey(KeyAccount);
            PurgeLegacyPlaintextPassword();
            PlayerPrefs.DeleteKey(KeyPasswordHash);
            PlayerPrefs.DeleteKey(KeyPasswordSalt);
            PlayerPrefs.DeleteKey(KeyGender);
            PlayerPrefs.DeleteKey(KeyHasGender);
            PlayerPrefs.DeleteKey(KeyGenderLocked);
            PlayerPrefs.Save();
        }

        public static void MigrateLegacySecrets()
        {
            if (!PlayerPrefs.HasKey(KeyPassword))
            {
                return;
            }

            PurgeLegacyPlaintextPassword();
            PlayerPrefs.Save();
        }

        static void PurgeLegacyPlaintextPassword()
        {
            if (PlayerPrefs.HasKey(KeyPassword))
            {
                PlayerPrefs.DeleteKey(KeyPassword);
            }
        }

        static string NewSalt()
        {
            var bytes = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }

            return Convert.ToBase64String(bytes);
        }

        static string HashPassword(string account, string password, string salt)
        {
            string payload = (salt ?? string.Empty) + "\n" + (account ?? string.Empty) + "\n" +
                             (password ?? string.Empty);
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(payload));
                return BitConverter.ToString(hash).Replace("-", string.Empty);
            }
        }
    }
}
