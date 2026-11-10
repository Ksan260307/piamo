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
        /// <summary>まねっこの ほし（まちがいの 少なさ）。</summary>
        public List<SongStars> mimicStars = new List<SongStars>();
        /// <summary>さいごに あそんだ モード（0 じゆう・1 まねっこ・2 えんそう）と 曲。</summary>
        public int lastPlayMode;
        public string lastMimicSong = "";
        public string lastPerformSong = "";
        /// <summary>じゆうに ひく の ろくおん（ミリ秒・MIDI 番号・おした 1／はなした 0 の 3 つずつ）。</summary>
        public List<int> recording = new List<int>();

        public void Repair()
        {
            if (settings == null) settings = new PiamoSettings();
            settings.Clamp();
            stampDays ??= new List<int>();
            pendingDays ??= new List<int>();
            clearedSongs ??= new List<string>();
            performStars = RepairStars(performStars);
            mimicStars = RepairStars(mimicStars);
            lastPlayMode = Mathf.Clamp(lastPlayMode, 0, 2);
            lastMimicSong ??= "";
            lastPerformSong ??= "";
            recording ??= new List<int>();
            if (recording.Count % 3 != 0 || recording.Count > Recorder.MaxEvents * 3) recording.Clear();
            stampDays.RemoveAll(d => d <= 0);
            pendingDays.RemoveAll(d => d <= 0 || stampDays.Contains(d));
            // おなじ日の もらいすぎを なおす
            var seen = new HashSet<int>();
            pendingDays.RemoveAll(d => !seen.Add(d));
            pendingDays.Sort();
            version = CurrentVersion;
        }

        /// <summary>こわれた ほしを なおす（id のないもの・はんいの外・だぶりは いちばん よいもの）。</summary>
        static List<SongStars> RepairStars(List<SongStars> list)
        {
            var best = new Dictionary<string, int>();
            if (list != null)
            {
                foreach (var p in list)
                {
                    if (p == null || string.IsNullOrEmpty(p.id)) continue;
                    int v = Mathf.Clamp(p.stars, 0, 3);
                    if (!best.TryGetValue(p.id, out int b) || v > b) best[p.id] = v;
                }
            }
            var result = new List<SongStars>();
            foreach (var kv in best) if (kv.Value > 0) result.Add(new SongStars { id = kv.Key, stars = kv.Value });
            return result;
        }

        static int Stars(List<SongStars> list, string id)
        {
            foreach (var p in list) if (p.id == id) return p.stars;
            return 0;
        }

        static bool Record(List<SongStars> list, string id, int stars)
        {
            stars = Mathf.Clamp(stars, 0, 3);
            foreach (var p in list)
            {
                if (p.id != id) continue;
                if (stars <= p.stars) return false;
                p.stars = stars;
                return true;
            }
            if (stars <= 0) return false;
            list.Add(new SongStars { id = id, stars = stars });
            return true;
        }

        public int StarsFor(string id) => Stars(performStars, id);

        /// <summary>えんそうの ほしを きろくする（いままでより よいときだけ）。よくなったら true。</summary>
        public bool RecordStars(string id, int stars) => Record(performStars, id, stars);

        /// <summary>まねっこの ほし（むかしの「ひけた」しるしだけ あれば 1）。</summary>
        public int MimicStarsFor(string id)
        {
            int s = Stars(mimicStars, id);
            return s > 0 ? s : clearedSongs.Contains(id) ? 1 : 0;
        }

        public bool RecordMimicStars(string id, int stars) => Record(mimicStars, id, stars);

        /// <summary>えんそうで あつめた ほしの 合計。</summary>
        public int TotalPerformStars
        {
            get
            {
                int t = 0;
                foreach (var p in performStars) t += p.stars;
                return t;
            }
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

        /// <summary>保存に しっぱいした回数（テスト・ようすを見る用）。</summary>
        public static int SaveFailures { get; private set; }

        public static void Save()
        {
            if (_data == null) return;
            // ブラウザの 容量が いっぱい などで 保存できなくても、アプリは とめない
            try
            {
                PlayerPrefs.SetString(ActiveKey, JsonUtility.ToJson(_data));
                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                SaveFailures++;
                Debug.LogWarning("[Save] 保存できませんでした: " + e.Message);
            }
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
            Data.mimicStars.Clear();
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
