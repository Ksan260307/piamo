using System;
using System.Collections.Generic;
using UnityEngine;

namespace Piamo
{
    /// <summary>保存するもの（せってい・スタンプ・まねっこで ひけた曲）。</summary>
    [Serializable]
    public class PiamoSave
    {
        public const int CurrentVersion = 1;
        public int version = CurrentVersion;
        public PiamoSettings settings = new PiamoSettings();
        /// <summary>おしたスタンプの日付（yyyymmdd）。ならびの順が、スタンプの番号（1 こめ、2 こめ…）。</summary>
        public List<int> stampDays = new List<int>();
        /// <summary>もらえたけれど、まだ おしていないスタンプの日付。</summary>
        public List<int> pendingDays = new List<int>();
        /// <summary>きょう（playDay）に けんばんを おした回数。</summary>
        public int playDay;
        public int playCount;
        /// <summary>まねっこで さいごまで ひけた曲の id。</summary>
        public List<string> clearedSongs = new List<string>();

        public void Repair()
        {
            if (settings == null) settings = new PiamoSettings();
            settings.Clamp();
            stampDays ??= new List<int>();
            pendingDays ??= new List<int>();
            clearedSongs ??= new List<string>();
            stampDays.RemoveAll(d => d <= 0);
            pendingDays.RemoveAll(d => d <= 0 || stampDays.Contains(d));
            // おなじ日の もらいすぎを なおす
            var seen = new HashSet<int>();
            pendingDays.RemoveAll(d => !seen.Add(d));
            pendingDays.Sort();
            version = CurrentVersion;
        }
    }

    /// <summary>
    /// 保存と読みこみ（PlayerPrefs に JSON で）。WebGL ではブラウザの IndexedDB に入る。
    /// </summary>
    public static class SaveSystem
    {
        public const string Key = "piamo.save";
        static PiamoSave _data;

        /// <summary>テストでは保存先をかえて、ふだんの記録をこわさない。</summary>
        public static string KeyOverride;
        static string ActiveKey => string.IsNullOrEmpty(KeyOverride) ? Key : KeyOverride;

        public static event Action SettingsChanged;

        public static PiamoSave Data
        {
            get
            {
                if (_data == null) Load();
                return _data;
            }
        }

        public static PiamoSettings Settings => Data.settings;

        public static void Load()
        {
            _data = null;
            string json = PlayerPrefs.GetString(ActiveKey, "");
            if (!string.IsNullOrEmpty(json))
            {
                try { _data = JsonUtility.FromJson<PiamoSave>(json); }
                catch (Exception e) { Debug.LogWarning("[Save] 読みこめませんでした: " + e.Message); }
            }
            _data ??= new PiamoSave();
            _data.Repair();
        }

        public static void Save()
        {
            if (_data == null) return;
            PlayerPrefs.SetString(ActiveKey, JsonUtility.ToJson(_data));
            PlayerPrefs.Save();
        }

        /// <summary>せっていを かえたあとに呼ぶ（保存して、画面や音に知らせる）。</summary>
        public static void ApplySettings()
        {
            Data.settings.Clamp();
            Save();
            SettingsChanged?.Invoke();
        }

        public static void ResetSettings()
        {
            Data.settings = new PiamoSettings();
            ApplySettings();
        }

        public static void ResetStamps()
        {
            Data.stampDays.Clear();
            Data.pendingDays.Clear();
            Data.playCount = 0;
            Data.clearedSongs.Clear();
            Save();
        }

        /// <summary>すべてけす（テスト用）。</summary>
        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(ActiveKey);
            _data = new PiamoSave();
            _data.Repair();
        }

        public static void ClearListeners() => SettingsChanged = null;
    }
}
