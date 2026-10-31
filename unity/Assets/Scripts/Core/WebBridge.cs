using System.Runtime.InteropServices;
using UnityEngine;

namespace Piamo
{
    /// <summary>ブラウザ（Plugins/WebGL/PiamoPage.jslib）とのやりとり。WebGL 以外では何もしない。</summary>
    public static class WebBridge
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int PiamoLocalDate();
        [DllImport("__Internal")] static extern float PiamoSafeInset(int side);
        [DllImport("__Internal")] static extern void PiamoRegisterPageEvents();
        [DllImport("__Internal")] static extern void PiamoVibrate(int ms);
#else
        static int PiamoLocalDate() => 0;
        static float PiamoSafeInset(int side) => 0f;
        static void PiamoRegisterPageEvents() { }
        static void PiamoVibrate(int ms) { }
#endif

        /// <summary>ブラウザの その土地の日付（yyyymmdd）。WebGL 以外は 0。</summary>
        public static int LocalDate()
        {
            try { return PiamoLocalDate(); }
            catch { return 0; }
        }

        /// <summary>
        /// スマホのノッチなどで かくれる画面のはしの はば（画面に対する割合）。x 左・y 上・z 右・w 下。
        /// WebGL 以外は Screen.safeArea から。
        /// </summary>
        public static Vector4 SafeInsets()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try { return new Vector4(PiamoSafeInset(0), PiamoSafeInset(1), PiamoSafeInset(2), PiamoSafeInset(3)); }
            catch { return Vector4.zero; }
#else
            var sa = Screen.safeArea;
            float w = Mathf.Max(1, Screen.width), h = Mathf.Max(1, Screen.height);
            return new Vector4(sa.xMin / w, (h - sa.yMax) / h, (w - sa.xMax) / w, sa.yMin / h);
#endif
        }

        /// <summary>タブをとじる・切りかえるときに、GameObject「App」の OnPageHidden を呼んでもらう。</summary>
        public static void RegisterPageEvents()
        {
            try { PiamoRegisterPageEvents(); }
            catch { }
        }

        public static void Vibrate(int ms)
        {
            try { PiamoVibrate(ms); }
            catch { }
        }
    }
}
