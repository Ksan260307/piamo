using System;
using System.Collections.Generic;
using UnityEngine;

namespace Piamo
{
    /// <summary>
    /// 365 このスタンプの いちらん（tools/make_stamps.py が作る Resources/stamps.json）。
    /// 絵は StreamingAssets/Stamps/stamp_001.png … にあり、見るときにだけ読みこむ（はじめの読みこみを軽くするため）。
    /// </summary>
    public static class StampCatalog
    {
        [Serializable]
        public class Entry
        {
            public int no;
            public string name;
            public string animal;
            public bool big;
        }

        [Serializable]
        class Wrapper
        {
            public List<Entry> stamps = new List<Entry>();
        }

        static List<Entry> _entries;

        public static IReadOnlyList<Entry> Entries
        {
            get
            {
                if (_entries == null) Load();
                return _entries;
            }
        }

        public static void Load()
        {
            var ta = Resources.Load<TextAsset>("stamps");
            _entries = ta != null ? JsonUtility.FromJson<Wrapper>(ta.text).stamps : new List<Entry>();
        }

        /// <summary>no（1〜365）のスタンプ。</summary>
        public static Entry Get(int no)
        {
            var e = Entries;
            if (no >= 1 && no <= e.Count && e[no - 1].no == no) return e[no - 1];
            foreach (var x in e) if (x.no == no) return x;
            return null;
        }

        public static string FileName(int no) => $"stamp_{no:000}.png";

        public static string Url(int no)
        {
            string p = Application.streamingAssetsPath + "/Stamps/" + FileName(no);
            // WebGL では http(s) の URL。エディタ・PC では ファイルの場所なので、file:/// の URL にする
            return p.Contains("://") ? p : new Uri(p).AbsoluteUri;
        }
    }
}
