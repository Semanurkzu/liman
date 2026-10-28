using System;
using System.Collections.Generic;
using UnityEngine;

namespace Liman.Game
{
    /// <summary>Player progress, stored as JSON in PlayerPrefs. Fields are public for JsonUtility.</summary>
    [Serializable]
    public sealed class SaveData
    {
        private const string Key = "liman.save.v1";

        public int CurrentLevel = 1;
        public int HighestCompletedLevel;
        public bool RemoveAdsOwned;
        public List<int> StarsByLevel = new List<int>();

        public static SaveData Load()
        {
            string json = PlayerPrefs.GetString(Key, string.Empty);
            if (string.IsNullOrEmpty(json)) return new SaveData();
            try
            {
                return JsonUtility.FromJson<SaveData>(json) ?? new SaveData();
            }
            catch (ArgumentException e)
            {
                Debug.LogWarning($"Corrupt save data, starting fresh: {e.Message}");
                return new SaveData();
            }
        }

        public void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }

        public void RecordCompletion(int level, int stars)
        {
            while (StarsByLevel.Count < level) StarsByLevel.Add(0);
            StarsByLevel[level - 1] = Math.Max(StarsByLevel[level - 1], stars);
            HighestCompletedLevel = Math.Max(HighestCompletedLevel, level);
            CurrentLevel = level + 1;
        }
    }
}
