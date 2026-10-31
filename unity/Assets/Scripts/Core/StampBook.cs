using System.Collections.Generic;

namespace Piamo
{
    /// <summary>
    /// スタンプちょうの きまり。
    ///  - その日に けんばんを PlaysForStamp 回 おすと、その日のスタンプが 1 こ もらえる（1 日に 1 こ）
    ///  - もらったスタンプは、スタンプちょうで じぶんで おす（おすまでは「まだ おしていない」としてのこる）
    ///  - 5 こめ・10 こめ… のように 5 の倍数のときは、でっかいスタンプ
    ///  - スタンプの絵は 365 しゅるい。366 こめからは 2 さつめ（1 ばんの絵から、もう一度）
    /// </summary>
    public class StampBook
    {
        public const int PlaysForStamp = 10;
        public const int BigEvery = 5;
        public const int Designs = 365;

        readonly PiamoSave _save;

        public StampBook(PiamoSave save)
        {
            _save = save;
        }

        public int Total => _save.stampDays.Count;
        public int PendingCount => _save.pendingDays.Count;
        public IReadOnlyList<int> Days => _save.stampDays;
        public IReadOnlyList<int> PendingDays => _save.pendingDays;

        /// <summary>count こめ（1 から）のスタンプの絵の番号（1〜365）。</summary>
        public static int DesignFor(int count) => (count - 1) % Designs + 1;
        public static bool IsBig(int count) => count > 0 && count % BigEvery == 0;
        /// <summary>count こめは、なんさつめの スタンプちょうか（1 から）。</summary>
        public static int BookFor(int count) => (count - 1) / Designs + 1;

        /// <summary>いちばん新しい（おした・まだおしていない）スタンプの日付。</summary>
        public int LastDay
        {
            get
            {
                int last = 0;
                foreach (int d in _save.stampDays) if (d > last) last = d;
                foreach (int d in _save.pendingDays) if (d > last) last = d;
                return last;
            }
        }

        public bool HasStampFor(int day) => _save.stampDays.Contains(day) || _save.pendingDays.Contains(day);

        /// <summary>きょう あと何回おせば、スタンプがもらえるか（もうもらっていれば 0）。</summary>
        public int PlaysLeft(int today)
        {
            if (HasStampFor(today) || today <= LastDay) return 0;
            int count = _save.playDay == today ? _save.playCount : 0;
            return System.Math.Max(0, PlaysForStamp - count);
        }

        /// <summary>
        /// けんばんを 1 回おしたことを記録する。この 1 回で きょうのスタンプがもらえたら true。
        /// 日付が もどったとき（端末の時計をもどした など）は、もらえない。
        /// </summary>
        public bool RecordPlay(int today)
        {
            if (_save.playDay != today)
            {
                _save.playDay = today;
                _save.playCount = 0;
            }
            if (_save.playCount < PlaysForStamp) _save.playCount++;
            if (_save.playCount >= PlaysForStamp && !HasStampFor(today) && today > LastDay)
            {
                _save.pendingDays.Add(today);
                return true;
            }
            return false;
        }

        /// <summary>まだ おしていないスタンプを 1 こ おす。おしたら、それが何こめか（1 から）。なければ 0。</summary>
        public int Press()
        {
            if (_save.pendingDays.Count == 0) return 0;
            int day = _save.pendingDays[0];
            _save.pendingDays.RemoveAt(0);
            _save.stampDays.Add(day);
            return _save.stampDays.Count;
        }

        /// <summary>count こめのスタンプを おした日（なければ 0）。</summary>
        public int DayOf(int count) => count >= 1 && count <= _save.stampDays.Count ? _save.stampDays[count - 1] : 0;
    }
}
