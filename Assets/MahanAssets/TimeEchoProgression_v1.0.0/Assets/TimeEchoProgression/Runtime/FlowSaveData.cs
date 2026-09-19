using System;
using UnityEngine;

namespace TimeEcho.Flow
{
    [Serializable]
    internal sealed class FlowSaveData
    {
        public int currentLevelIndex;
        public int highestUnlockedLevelIndex;
        public int deathCount;
        public bool hasStarted;
        public bool hasWon;
    }

    internal static class FlowSaveStore
    {
        public static FlowSaveData Load(TimeEchoFlowConfig config)
        {
            FlowSaveData fresh = new FlowSaveData();
            if (config == null || !config.saveProgress || !PlayerPrefs.HasKey(config.playerPrefsKey))
            {
                return fresh;
            }

            try
            {
                string json = PlayerPrefs.GetString(config.playerPrefsKey, string.Empty);
                FlowSaveData loaded = JsonUtility.FromJson<FlowSaveData>(json);
                return loaded ?? fresh;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Time Echo could not read its progression save. A fresh save will be used. " + exception.Message);
                return fresh;
            }
        }

        public static void Save(TimeEchoFlowConfig config, FlowSaveData data)
        {
            if (config == null || data == null || !config.saveProgress)
            {
                return;
            }

            PlayerPrefs.SetString(config.playerPrefsKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        public static void Delete(TimeEchoFlowConfig config)
        {
            if (config == null || string.IsNullOrWhiteSpace(config.playerPrefsKey))
            {
                return;
            }

            PlayerPrefs.DeleteKey(config.playerPrefsKey);
            PlayerPrefs.Save();
        }
    }
}
