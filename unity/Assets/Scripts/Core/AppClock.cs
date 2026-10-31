using System;

namespace Piamo
{
    /// <summary>
    /// きょうの日付（yyyymmdd の数）。スタンプは「1 日に 1 こ」なので、端末の その土地の日付を使う。
    /// WebGL では、ブラウザの Date から読む（C# の DateTime.Now が UTC になる環境があるため）。
    /// </summary>
    public static class AppClock
    {
        /// <summary>テスト用：日付を決める。</summary>
        public static Func<int> TodayOverride;

        public static int Today()
        {
            if (TodayOverride != null) return TodayOverride();
            int web = WebBridge.LocalDate();
            if (web > 0) return web;
            var n = DateTime.Now;
            return n.Year * 10000 + n.Month * 100 + n.Day;
        }

        public static int Year(int day) => day / 10000;
        public static int Month(int day) => day / 100 % 100;
        public static int DayOfMonth(int day) => day % 100;

        /// <summary>「10/31」</summary>
        public static string Short(int day) => day <= 0 ? "" : $"{Month(day)}/{DayOfMonth(day)}";

        /// <summary>「2026ねん 10がつ 31にち」</summary>
        public static string Long(int day) => day <= 0 ? "" : $"{Year(day)}ねん {Month(day)}がつ {DayOfMonth(day)}にち";

        /// <summary>日付の数をもとにした、次の日（テスト用）。</summary>
        public static int AddDays(int day, int add)
        {
            var d = new DateTime(Year(day), Month(day), DayOfMonth(day)).AddDays(add);
            return d.Year * 10000 + d.Month * 100 + d.Day;
        }
    }
}
