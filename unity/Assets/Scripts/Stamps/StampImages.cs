using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace Piamo
{
    /// <summary>スタンプの絵を読みこんで、しばらく とっておく。</summary>
    public class StampImages : MonoBehaviour
    {
        public const int CacheSize = 90;
        /// <summary>読みこみに しっぱいしたとき、もう一度 ためす回数。</summary>
        public const int Retries = 2;
        readonly Dictionary<int, Texture2D> _cache = new Dictionary<int, Texture2D>();
        readonly LinkedList<int> _order = new LinkedList<int>();
        readonly Dictionary<int, List<Action<Texture2D>>> _waiting = new Dictionary<int, List<Action<Texture2D>>>();

        public int Loaded => _cache.Count;

        public bool TryGet(int no, out Texture2D tex) => _cache.TryGetValue(no, out tex) && tex != null;

        /// <summary>絵をもらう（読みこみおわったら done を呼ぶ。もうあれば、すぐ呼ぶ）。</summary>
        public void Request(int no, Action<Texture2D> done)
        {
            if (_cache.TryGetValue(no, out var tex) && tex != null)
            {
                Touch(no);
                done?.Invoke(tex);
                return;
            }
            if (_waiting.TryGetValue(no, out var list))
            {
                if (done != null) list.Add(done);
                return;
            }
            _waiting[no] = new List<Action<Texture2D>>();
            if (done != null) _waiting[no].Add(done);
            StartCoroutine(LoadRoutine(no));
        }

        IEnumerator LoadRoutine(int no)
        {
            Texture2D tex = null;
            for (int attempt = 0; attempt <= Retries && tex == null; attempt++)
            {
                if (attempt > 0) yield return new WaitForSecondsRealtime(0.6f * attempt);
                yield return Fetch(no, t => tex = t);
            }
            if (tex != null)
            {
                _cache[no] = tex;
                Touch(no);
                Trim();
            }
            if (_waiting.TryGetValue(no, out var list))
            {
                _waiting.Remove(no);
                foreach (var cb in list) cb?.Invoke(tex);
            }
        }

        IEnumerator Fetch(int no, Action<Texture2D> done)
        {
            Texture2D tex = null;
            using (var req = UnityWebRequest.Get(StampCatalog.Url(no)))
            {
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success)
                {
                    tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (tex.LoadImage(req.downloadHandler.data, true))
                    {
                        tex.name = StampCatalog.FileName(no);
                        tex.wrapMode = TextureWrapMode.Clamp;
                        tex.filterMode = FilterMode.Bilinear;
                    }
                    else
                    {
                        Debug.LogWarning($"[Stamps] {StampCatalog.FileName(no)} の絵を ひらけませんでした（{req.downloadHandler.data?.Length ?? 0} バイト）");
                        Destroy(tex);
                        tex = null;
                    }
                }
                else Debug.LogWarning($"[Stamps] {StampCatalog.FileName(no)} を読みこめませんでした: {req.error}");
            }
            done(tex);
        }

        void Touch(int no)
        {
            _order.Remove(no);
            _order.AddLast(no);
        }

        void Trim()
        {
            while (_cache.Count > CacheSize && _order.Count > 0)
            {
                int old = _order.First.Value;
                _order.RemoveFirst();
                if (_cache.TryGetValue(old, out var t))
                {
                    _cache.Remove(old);
                    if (t != null) Destroy(t);
                }
            }
        }
    }
}
