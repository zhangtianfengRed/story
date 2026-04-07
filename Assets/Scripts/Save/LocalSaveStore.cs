using UnityEngine;

/// <summary>
/// 统一的本地存储入口。
/// 目前底层仍使用 PlayerPrefs，但业务层不再直接依赖它。
/// 后续如果你想切到文件、加密或云存档，优先改这里。
/// </summary>
public static class LocalSaveStore
{
    public static class Keys
    {
        public const string GameSettings = "save.game.settings";
        public const string SceneProgress = "save.game.sceneProgress";
    }

    public static bool HasKey(string key)
    {
        return PlayerPrefs.HasKey(key);
    }

    public static void DeleteKey(string key, bool saveImmediately = true)
    {
        PlayerPrefs.DeleteKey(key);

        if (saveImmediately)
        {
            Flush();
        }
    }

    public static void SaveJson<T>(string key, T data, bool saveImmediately = true)
    {
        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(key, json);

        if (saveImmediately)
        {
            Flush();
        }
    }

    public static bool TryLoadJson<T>(string key, out T data) where T : class
    {
        data = null;

        if (!HasKey(key))
        {
            return false;
        }

        string json = PlayerPrefs.GetString(key);
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        data = JsonUtility.FromJson<T>(json);
        return data != null;
    }

    public static void SaveInt(string key, int value, bool saveImmediately = true)
    {
        PlayerPrefs.SetInt(key, value);

        if (saveImmediately)
        {
            Flush();
        }
    }

    public static int LoadInt(string key, int defaultValue = 0)
    {
        return PlayerPrefs.GetInt(key, defaultValue);
    }

    public static void SaveFloat(string key, float value, bool saveImmediately = true)
    {
        PlayerPrefs.SetFloat(key, value);

        if (saveImmediately)
        {
            Flush();
        }
    }

    public static float LoadFloat(string key, float defaultValue = 0f)
    {
        return PlayerPrefs.GetFloat(key, defaultValue);
    }

    public static void SaveString(string key, string value, bool saveImmediately = true)
    {
        PlayerPrefs.SetString(key, value);

        if (saveImmediately)
        {
            Flush();
        }
    }

    public static string LoadString(string key, string defaultValue = "")
    {
        return PlayerPrefs.GetString(key, defaultValue);
    }

    public static void Flush()
    {
        PlayerPrefs.Save();
    }
}
