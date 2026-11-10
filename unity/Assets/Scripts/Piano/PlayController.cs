using System;
using System.Collections.Generic;
using UnityEngine;

namespace Piamo
{
    /// <summary>
    /// 「ピアノで あそぶ」のまとめ役。鍵盤（KeyboardView）・音（PianoAudio）・カメラを動かす。
    ///  - 指やマウス（PianoInput）と、まねっこのおてほん（Demo）の両方から、鍵盤を おす／はなす
    ///  - 同じ鍵盤を何本の指で おさえているかを数え、さいごの指がはなれたら音を止める
    ///  - UI の「鍵盤のエリア」にあわせてカメラを動かし、見えない鍵盤はスクロールで見せる
    /// </summary>
    public class PlayController : MonoBehaviour
    {
        public KeyboardView view;
        public PianoAudio audioOut;
        public Camera cam;
        public MeshRenderer backdrop;

        /// <summary>子どもが鍵盤をおした（おてほんの音はふくまない）。</summary>
        public event Action<int> NotePlayed;
        /// <summary>子どもが鍵盤を はなした（さいごの指が はなれた。ろくおん用）。</summary>
        public event Action<int> NoteReleased;
        /// <summary>レイアウトやスクロールが変わった（ラベルの位置をなおす）。</summary>
        public event Action FramingChanged;

        public KeyboardLayout Layout { get; private set; }
        public KeyboardFraming.Result Framing { get; private set; }
        public bool InputActive { get; set; }
        /// <summary>いまの鍵盤の、まねっこの曲をずらす半音の数。</summary>
        public int Transpose => 12 * SaveSystem.Settings.octaveShift;

        readonly Dictionary<int, HashSet<int>> _holders = new Dictionary<int, HashSet<int>>();
        readonly HashSet<int> _demoDown = new HashSet<int>();
        Rect _area;
        Vector2 _screen;
        float _offset;
        float _minKeyPx = 80f;
        bool _built;
        string _buildKey;
        Mesh _backdropMesh;

        public const int DemoPointer = -1000;

        public float Offset => Framing.offset;
        public Rect Area => _area;

        void Awake()
        {
            if (cam == null) cam = Camera.main;
        }

        /// <summary>せっていを反映する（鍵盤の数・オクターブ・黒鍵が変わったら作りなおす）。</summary>
        public void ApplySettings(PiamoSettings s)
        {
            string key = $"{s.octaves}/{s.octaveShift}/{s.blackKeys}";
            if (!_built || key != _buildKey)
            {
                ReleaseAll();
                Layout = KeyboardLayout.Create(s.octaves, s.octaveShift, s.blackKeys);
                view.Build(Layout, s);
                _built = true;
                _buildKey = key;
                // はじめは まんなかのドが見えるように
                _offset = DefaultOffset();
            }
            else view.RefreshColors(s);
            if (audioOut != null)
            {
                audioOut.SetInstrument(s.instrument);
                audioOut.Volume = s.volume;
                audioOut.Sustain = s.sustain;
                audioOut.UiSounds = s.uiSounds;
            }
            SetBackdrop(Themes.Top[s.theme], Themes.Bottom[s.theme]);
            Reframe();
        }

        float DefaultOffset()
        {
            if (Layout == null) return 0f;
            int c = NoteUtil.MiddleC + Transpose;
            if (!Layout.Contains(c)) c = Layout.Low;
            int w = NoteUtil.WhiteNumber(c) - NoteUtil.WhiteNumber(Layout.Low);
            return w;
        }

        /// <summary>UI の鍵盤のエリア（画面のピクセル。左下が原点）と、白鍵のいちばん細いはば。</summary>
        public void SetArea(Rect area, Vector2 screen, float minKeyPx)
        {
            if (area == _area && screen == _screen && Mathf.Approximately(minKeyPx, _minKeyPx)) return;
            _area = area;
            _screen = screen;
            _minKeyPx = minKeyPx;
            Reframe();
        }

        public void SetOffset(float whiteKeys)
        {
            _offset = whiteKeys;
            Reframe();
        }

        /// <summary>白鍵 count 本ぶん スクロールする。</summary>
        public void Scroll(float count) => SetOffset(Framing.offset + count);

        /// <summary>note が見えるようにスクロールする（まねっこ）。</summary>
        public void EnsureVisible(int lowNote, int highNote)
        {
            if (Layout == null || !Framing.scrolling) return;
            float off = OffsetToShow(lowNote, highNote);
            if (!Mathf.Approximately(off, Framing.offset)) SetOffset(off);
        }

        /// <summary>lowNote〜highNote が見える スクロールの位置（いまの位置から いちばん すくなく うごかす）。</summary>
        public float OffsetToShow(int lowNote, int highNote)
        {
            if (Layout == null) return 0f;
            int baseW = NoteUtil.WhiteNumber(Layout.Low);
            float lo = NoteUtil.WhiteNumber(lowNote) - baseW;
            float hi = NoteUtil.WhiteNumber(highNote) - baseW;
            float off = Framing.offset;
            if (lo < off) off = lo;
            if (hi > off + Framing.visible - 1) off = hi - Framing.visible + 1;
            return off;
        }

        void Reframe()
        {
            if (Layout == null || cam == null || _area.width < 1f || _area.height < 1f) return;
            var f = KeyboardFraming.Compute(_area, _screen, Layout.WhiteCount, _minKeyPx, _offset);
            Framing = f;
            _offset = f.offset;
            view.SetStretch(f.stretch);
            cam.orthographic = true;
            cam.orthographicSize = f.orthoSize;
            cam.transform.SetPositionAndRotation(f.cameraPosition, f.cameraRotation);
            FitBackdrop();
            FramingChanged?.Invoke();
        }

        // ------------------------------------------------------------------
        // おす・はなす

        public void KeyDown(int midi, int pointer)
        {
            if (Layout == null || !Layout.Contains(midi)) return;
            if (!_holders.TryGetValue(midi, out var set)) _holders[midi] = set = new HashSet<int>();
            if (!set.Add(pointer) || set.Count > 1) return;   // 2 本目の指では鳴らしなおさない
            audioOut?.NoteOn(midi);
            view.SetPressed(midi, true);
            NotePlayed?.Invoke(midi);
        }

        public void KeyUp(int midi, int pointer)
        {
            if (!_holders.TryGetValue(midi, out var set)) return;
            if (!set.Remove(pointer)) return;
            if (set.Count > 0) return;
            if (!_demoDown.Contains(midi))
            {
                audioOut?.NoteOff(midi);
                view.SetPressed(midi, false);
            }
            NoteReleased?.Invoke(midi);
        }

        public bool IsHeld(int midi) => _holders.TryGetValue(midi, out var s) && s.Count > 0;

        /// <summary>いま 子どもが おさえている 鍵盤（ひくい順）。</summary>
        public void HeldNotes(List<int> into)
        {
            into.Clear();
            foreach (var kv in _holders) if (kv.Value.Count > 0) into.Add(kv.Key);
            into.Sort();
        }

        /// <summary>おてほんの音（light: 鍵盤もしずめて ひからせる。velocity: 音の大きさ 0〜1）。</summary>
        public void DemoDown(int midi, bool light, float velocity = 0.95f)
        {
            _demoDown.Add(midi);
            audioOut?.NoteOn(midi, velocity);
            if (light) view.SetPressed(midi, true);
        }

        public void DemoUp(int midi)
        {
            if (!_demoDown.Remove(midi)) return;
            if (IsHeld(midi)) return;
            audioOut?.NoteOff(midi);
            view.SetPressed(midi, false);
        }

        public void ReleaseAll()
        {
            foreach (var kv in _holders)
            {
                if (kv.Value.Count == 0) continue;
                kv.Value.Clear();
                audioOut?.NoteOff(kv.Key);
                if (view != null) view.SetPressed(kv.Key, false);
            }
            foreach (int m in _demoDown)
            {
                audioOut?.NoteOff(m);
                if (view != null) view.SetPressed(m, false);
            }
            _demoDown.Clear();
        }

        // ------------------------------------------------------------------
        // 画面の点 → 鍵盤

        /// <summary>画面の点（ピクセル。左下が原点）にある鍵盤。なければ -1。黒鍵を先にしらべる。</summary>
        public int KeyAtScreen(Vector2 screen)
        {
            if (Layout == null || cam == null) return -1;
            if (!_area.Contains(screen)) return -1;
            var ray = cam.ScreenPointToRay(new Vector3(screen.x, screen.y, 0));
            float s = view.Stretch;
            if (Layout.BlackKeys && Hit(ray, PianoDimensions.BlackHeight, out var pb))
            {
                int b = Layout.HitBlack(pb.x / s, pb.z);
                if (b >= 0) return b;
            }
            // 黒鍵の横の面のあたりも黒鍵にする（黒鍵の上の面と、白鍵の上の面のあいだ）
            if (Layout.BlackKeys && Hit(ray, PianoDimensions.BlackHeight * 0.5f, out var pm))
            {
                int b = Layout.HitBlack(pm.x / s, pm.z);
                if (b >= 0) return b;
            }
            if (Hit(ray, 0f, out var pw))
            {
                int w = Layout.HitWhite(pw.x / s, pw.z);
                if (w >= 0) return w;
            }
            // 白鍵の手前の面（前から見える縦の面）
            if (Hit(ray, -PianoDimensions.WhiteHeight * 0.5f, out var pf) && pf.z < 0.2f)
            {
                return Layout.HitWhite(pf.x / s, Mathf.Max(pf.z, PianoDimensions.CaseFrontZ));
            }
            return -1;
        }

        static bool Hit(Ray ray, float y, out Vector3 p)
        {
            p = default;
            if (Mathf.Abs(ray.direction.y) < 1e-5f) return false;
            float t = (y - ray.origin.y) / ray.direction.y;
            if (t < 0) return false;
            p = ray.origin + ray.direction * t;
            return true;
        }

        /// <summary>鍵盤の点（ワールド）の、画面の位置（ピクセル。左下が原点）。</summary>
        public Vector2 WorldToScreen(Vector3 world)
        {
            if (cam == null) return Vector2.zero;
            var p = cam.WorldToScreenPoint(world);
            return new Vector2(p.x, p.y);
        }

        /// <summary>鍵盤の中心の画面の位置（テストで、その鍵盤をタップするため）。</summary>
        public Vector2 KeyCenterScreen(int midi)
        {
            if (Layout == null || !Layout.TryGet(midi, out var k)) return Vector2.zero;
            float s = view.Stretch;
            Vector3 w = k.black
                ? new Vector3(k.x * s, PianoDimensions.BlackHeight, PianoDimensions.WhiteLength - PianoDimensions.BlackLength * 0.5f)
                : new Vector3(k.x * s, 0f, 1.2f);
            return WorldToScreen(w);
        }

        /// <summary>画面に見えている白鍵のはんい（Layout の何本目から何本目まで）。</summary>
        public Vector2Int VisibleWhiteRange()
        {
            if (Layout == null) return Vector2Int.zero;
            if (!Framing.scrolling) return new Vector2Int(0, Layout.WhiteCount - 1);
            int a = Mathf.FloorToInt(Framing.offset + 0.01f);
            return new Vector2Int(a, Mathf.Min(Layout.WhiteCount - 1, a + Framing.visible - 1));
        }

        // ------------------------------------------------------------------
        // はいけい（カメラのうしろの グラデーションの板）

        void SetBackdrop(Color top, Color bottom)
        {
            if (backdrop == null) return;
            if (_backdropMesh == null)
            {
                _backdropMesh = new Mesh { name = "Backdrop" };
                _backdropMesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(-0.5f, 0.5f, 0), new Vector3(0.5f, 0.5f, 0) };
                _backdropMesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
                _backdropMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
                backdrop.GetComponent<MeshFilter>().sharedMesh = _backdropMesh;
            }
            // 頂点カラーは リニアの色空間では そのまま使われるので、自分で変換する
            Color b = QualitySettings.activeColorSpace == ColorSpace.Linear ? bottom.linear : bottom;
            Color t = QualitySettings.activeColorSpace == ColorSpace.Linear ? top.linear : top;
            _backdropMesh.colors = new[] { b, b, t, t };
            if (cam != null) cam.backgroundColor = bottom;
        }

        void FitBackdrop()
        {
            if (backdrop == null || cam == null) return;
            float h = cam.orthographicSize * 2f;
            float w = h * Mathf.Max(0.1f, cam.aspect);
            var t = backdrop.transform;
            t.localPosition = new Vector3(0, 0, KeyboardFraming.CameraDistance * 2f);
            t.localRotation = Quaternion.identity;
            t.localScale = new Vector3(w * 1.05f, h * 1.05f, 1f);
        }

        void OnDisable() => ReleaseAll();
    }
}
