using System;
using System.Collections.Generic;
using UnityEngine;

namespace Piamo
{
    /// <summary>えんそうで もらった ほし（曲ごとに いちばん よかったもの）。</summary>
    [Serializable]
    public class SongStars
    {
        public string id;
        public int stars;
    }

    /// <summary>保存するもの（せってい・スタンプ・まねっこで ひけた曲・えんそうの ほし）。</summary>
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
        /// <summary>えんそうの ほし。</summary>
        public List<SongStars> performStars = new List<SongStars>();

        public void Repair()
        {
            if (settings == null) settings = new PiamoSettings();
            settings.Clamp();
            stampDays ??= new List<int>();
            pendingDays ??= new List<int>();
            clearedSongs ??= new List<string>();
            performStars ??= new List<SongStars>();
            // こわれた ほしを なおす（id のないもの・はんいの外・だぶりは いちばん よいもの）
            var best = new Dictionary<string, int>();
            foreach (var p in performStars)
            {
                if (p == null || string.IsNullOrEmpty(p.id)) continue;
                int v = Mathf.Clamp(p.stars, 0, 3);
                if (!best.TryGetValue(p.id, out int b) || v > b) best[p.id] = v;
            }
            performStars.Clear();
            foreach (var kv in best) if (kv.Value > 0) performStars.Add(new SongStars { id = kv.Key, stars = kv.Value });
            stampDays.RemoveAll(d => d <= 0);
            pendingDays.RemoveAll(d => d <= 0 || stampDays.Contains(d));
            // おなじ日の もらいすぎを なおす
            var seen = new HashSet<int>();
            pendingDays.RemoveAll(d => !seen.Add(d));
            pendingDays.Sort();
            version = CurrentVersion;
        }

        public int StarsFor(string id)
        {
            foreach (var p in performStars) if (p.id == id) return p.stars;
            return 0;
        }

        /// <summary>ほしを きろくする（いままでより よいときだけ）。よくなったら true。</summary>
        public bool RecordStars(string id, int stars)
        {
            stars = Mathf.Clamp(stars, 0, 3);
            foreach (var p in performStars)
            {
                if (p.id != id) continue;
                if (stars <= p.stars) return false;
                p.stars = stars;
                return true;
            }
            if (stars <= 0) return false;
            performStars.Add(new SongStars { id = id, stars = stars });
            return true;
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
            Data.performStars.Clear();
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
