using UnityEngine;

namespace Piamo
{
    /// <summary>
    /// 3D の鍵盤を、画面の「鍵盤のエリア」にぴったり入れるカメラの計算（正投影・ななめ上から見下ろす）。
    ///  - 鍵盤が少ないとき（よこ長の画面）は、白鍵をよこにのばして（最大 MaxStretch 倍）、はばいっぱいにする
    ///  - 鍵盤が多くて、白鍵が MinKeyPx より細くなるときは、見える本数をへらしてスクロールする
    /// 画面の座標はピクセルで、左下が原点。
    /// </summary>
    public static class KeyboardFraming
    {
        public const float TiltDegrees = 35f;
        public const float MaxStretch = 1.9f;
        public const float CameraDistance = 40f;

        public struct Result
        {
            /// <summary>見えている白鍵の数（全部見えるときは WhiteCount）。</summary>
            public int visible;
            /// <summary>スクロールできるか（visible &lt; whiteCount）。</summary>
            public bool scrolling;
            /// <summary>白鍵のよこの のばし方。</summary>
            public float stretch;
            /// <summary>1 ワールド単位が何ピクセルか。</summary>
            public float ppu;
            public float orthoSize;
            public Vector3 cameraPosition;
            public Quaternion cameraRotation;
            /// <summary>鍵盤（ケースもふくむ）の、画面での高さ（ピクセル）。</summary>
            public float contentHeight;
            /// <summary>いちばん左に見える白鍵（スクロールの位置）。</summary>
            public float offset;
            public float maxOffset;
            /// <summary>白鍵 1 本の、画面でのはば（ピクセル）。</summary>
            public float keyPx;
        }

        static Vector3 Up => Quaternion.Euler(TiltDegrees, 0, 0) * Vector3.up;
        static Vector3 Forward => Quaternion.Euler(TiltDegrees, 0, 0) * Vector3.forward;

        /// <summary>ケース全体の、カメラの上向きの軸での下のはしと上のはし（ワールド単位）。</summary>
        public static Vector2 VerticalExtent()
        {
            Vector3 up = Up;
            float lo = float.MaxValue, hi = float.MinValue;
            foreach (float y in new[] { PianoDimensions.CaseBottom, PianoDimensions.CaseBackTop })
            foreach (float z in new[] { PianoDimensions.CaseFrontZ, PianoDimensions.CaseBackZ })
            {
                float v = y * up.y + z * up.z;
                lo = Mathf.Min(lo, v);
                hi = Mathf.Max(hi, v);
            }
            return new Vector2(lo, hi);
        }

        public static float ContentUnitsHigh => VerticalExtent().y - VerticalExtent().x;

        /// <summary>
        /// 鍵盤のエリアの高さの目安。よこはばに合わせたときの鍵盤の高さと、上限（画面の maxFraction）の小さいほう。
        /// </summary>
        public static float PreferredHeight(float areaWidth, float screenHeight, int whiteCount, float minKeyPx, float maxFraction)
        {
            float cap = screenHeight * maxFraction;
            int n = Mathf.Max(1, whiteCount);
            float ppuW = areaWidth / (n + 2 * PianoDimensions.CaseEndWidth);
            if (ppuW < minKeyPx)
            {
                // スクロールになる：白鍵を MinKeyPx 以上にしたときの高さ
                int v = Mathf.Clamp(Mathf.FloorToInt(areaWidth / Mathf.Max(1f, minKeyPx)), 1, n);
                ppuW = areaWidth / v;
            }
            return Mathf.Min(cap, ContentUnitsHigh * ppuW);
        }

        public static Result Compute(Rect area, Vector2 screen, int whiteCount, float minKeyPx, float offset)
        {
            var r = new Result();
            int n = Mathf.Max(1, whiteCount);
            float hk = ContentUnitsHigh;
            float cheeks = 2 * PianoDimensions.CaseEndWidth;
            float w = Mathf.Max(1f, area.width), h = Mathf.Max(1f, area.height);

            // まず全部を見せられるか
            float ppu = Mathf.Min(h / hk, w / (n + cheeks));
            float s = Mathf.Clamp((w / ppu - cheeks) / n, 1f, MaxStretch);
            if (s * ppu >= minKeyPx - 0.5f)
            {
                r.visible = n;
                r.scrolling = false;
            }
            else
            {
                int v = Mathf.Clamp(Mathf.FloorToInt(w / Mathf.Max(1f, minKeyPx)), 1, n);
                if (v >= n)
                {
                    r.visible = n;
                    r.scrolling = false;
                }
                else
                {
                    float keyPx = w / v;
                    ppu = Mathf.Min(h / hk, keyPx);
                    s = Mathf.Clamp(keyPx / ppu, 1f, MaxStretch);
                    r.visible = v;
                    r.scrolling = true;
                }
            }
            r.ppu = ppu;
            r.stretch = s;
            r.keyPx = s * ppu;
            r.contentHeight = hk * ppu;
            r.maxOffset = r.scrolling ? n - r.visible : 0f;
            r.offset = Mathf.Clamp(offset, 0f, r.maxOffset);

            // 見せる鍵盤の中心（ワールドの X）
            float centerX = r.scrolling
                ? s * (r.offset - 0.5f + r.visible / 2f)
                : s * (n - 1) / 2f;
            float camX = centerX - (area.center.x - screen.x / 2f) / ppu;
            // 鍵盤のケースの下のはしを、エリアの下のはしに合わせる
            float vMin = VerticalExtent().x;
            float vCam = vMin - (area.yMin - screen.y / 2f) / ppu;

            r.orthoSize = screen.y / (2f * ppu);
            r.cameraRotation = Quaternion.Euler(TiltDegrees, 0, 0);
            r.cameraPosition = new Vector3(camX, 0, 0) + Up * vCam - Forward * CameraDistance;
            return r;
        }
    }
}
