using System.Collections.Generic;
using MelonLoader;

namespace LegionCore.Persistence
{
    // Backed by MelonPreferences, not vanilla's save file - keeps LegionCore as the sole
    // boundary touching the game's own APIs; nothing here shares schema with SaveManager.
    internal sealed class SaveApi : ISaveApi
    {
        private static readonly MelonPreferences_Category Category = MelonPreferences.CreateCategory("LegionCore");
        private static readonly Dictionary<string, object> Entries = new();

        public bool GetBool(string key, bool defaultValue = false) => GetEntry(key, defaultValue).Value;
        public void SetBool(string key, bool value) => Set(key, value);
        public int GetInt(string key, int defaultValue = 0) => GetEntry(key, defaultValue).Value;
        public void SetInt(string key, int value) => Set(key, value);
        public float GetFloat(string key, float defaultValue = 0f) => GetEntry(key, defaultValue).Value;
        public void SetFloat(string key, float value) => Set(key, value);
        public string GetString(string key, string defaultValue = "") => GetEntry(key, defaultValue).Value;
        public void SetString(string key, string value) => Set(key, value);

        private static MelonPreferences_Entry<T> GetEntry<T>(string key, T defaultValue)
        {
            if (Entries.TryGetValue(key, out var existing)) return (MelonPreferences_Entry<T>)existing;
            var entry = Category.CreateEntry(key, defaultValue);
            Entries[key] = entry;
            return entry;
        }

        private static void Set<T>(string key, T value)
        {
            GetEntry(key, value).Value = value;
            MelonPreferences.Save();
        }
    }
}
