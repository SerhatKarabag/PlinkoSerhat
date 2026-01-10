using UnityEngine;

namespace Plinko.Services
{
    public sealed class UnityPreferences : IPreferences
    {
        public bool HasKey(string key) => PlayerPrefs.HasKey(key);
        public string GetString(string key, string defaultValue = "") => PlayerPrefs.GetString(key, defaultValue);
        public void SetString(string key, string value) => PlayerPrefs.SetString(key, value);
        public void DeleteKey(string key) => PlayerPrefs.DeleteKey(key);
        public void Save() => PlayerPrefs.Save();
    }
}
