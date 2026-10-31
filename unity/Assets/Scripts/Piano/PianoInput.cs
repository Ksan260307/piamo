using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace Piamo
{
    /// <summary>
    /// 指・マウス・パソコンのキーで鍵盤をおす。
    ///  - 何本の指でも同時におせる（和音）。指をすべらせると、となりの鍵盤へうつる（グリッサンド）
    ///  - ボタンなどの UI の上ではじまった指は、鍵盤には使わない
    ///  - パソコンのキー：A S D F G H J K L ; ' が白鍵、W E T Y U O P が黒鍵（見えているいちばん左のドから）
    /// </summary>
    public class PianoInput : MonoBehaviour
    {
        public PlayController play;
        /// <summary>画面の点が UI（ボタンなど）の上かどうか（AppUI が入れる）。</summary>
        public Func<Vector2, bool> IsOverUI;

        /// <summary>テスト用の指（id → 画面の点。null で はなす）。</summary>
        public static readonly Dictionary<int, Vector2?> VirtualPointers = new Dictionary<int, Vector2?>();
        /// <summary>テスト用：1 フレームのうちに おして はなす タップ（画面の点）。</summary>
        public static readonly List<Vector2> VirtualQuickTaps = new List<Vector2>();

        const int MouseId = -1;
        const int KeyboardIdBase = -100;
        const int VirtualIdBase = -10000;

        readonly Dictionary<int, int> _pointerKey = new Dictionary<int, int>();
        readonly HashSet<int> _uiPointers = new HashSet<int>();
        readonly HashSet<int> _seen = new HashSet<int>();
        readonly List<int> _drop = new List<int>();
        /// <summary>1 フレームのうちに おして はなした（とても はやいタップ・重い端末）鍵盤。つぎのフレームで はなす。</summary>
        readonly List<(int id, int midi)> _quick = new List<(int, int)>();
        float _lastTouchTime = -10f;

        static readonly Key[] WhiteKeys = { Key.A, Key.S, Key.D, Key.F, Key.G, Key.H, Key.J, Key.K, Key.L, Key.Semicolon, Key.Quote };
        // 白鍵 i と i+1 のあいだの黒鍵（なければ None）
        static readonly Key[] BlackKeys = { Key.W, Key.E, Key.None, Key.T, Key.Y, Key.U, Key.None, Key.O, Key.P, Key.None, Key.None };

        void OnEnable()
        {
            if (!EnhancedTouchSupport.enabled) EnhancedTouchSupport.Enable();
        }

        void Update()
        {
            if (play == null) return;
            if (!play.InputActive)
            {
                ReleaseAll();
                return;
            }
            _seen.Clear();
            foreach (var (qid, qmidi) in _quick) play.KeyUp(qmidi, qid);
            _quick.Clear();

            // タッチ
            if (Touchscreen.current != null)
            {
                foreach (var t in Touch.activeTouches)
                {
                    int id = t.touchId;
                    bool down = t.phase != UnityEngine.InputSystem.TouchPhase.Ended && t.phase != UnityEngine.InputSystem.TouchPhase.Canceled;
                    _lastTouchTime = Time.unscaledTime;
                    if (down)
                    {
                        _seen.Add(id);
                        Handle(id, t.screenPosition, t.phase == UnityEngine.InputSystem.TouchPhase.Began);
                    }
                    else if (t.isTap && !_pointerKey.ContainsKey(id) && !_uiPointers.Contains(id))
                    {
                        // おした フレームで、もう はなれていた（前のフレームで おしているのを見ていない タップ）
                        QuickTap(id, t.startScreenPosition);
                    }
                }
            }

            // マウス（タッチの直後は、ブラウザがまねしたマウスの動きなので使わない）
            var mouse = Mouse.current;
            if (mouse != null && Time.unscaledTime - _lastTouchTime > 0.6f && Touch.activeTouches.Count == 0)
            {
                if (mouse.leftButton.isPressed)
                {
                    _seen.Add(MouseId);
                    Handle(MouseId, mouse.position.ReadValue(), mouse.leftButton.wasPressedThisFrame);
                }
                else if (mouse.leftButton.wasPressedThisFrame && !_pointerKey.ContainsKey(MouseId))
                {
                    QuickTap(MouseId, mouse.position.ReadValue());
                }
            }

            // テストの指
            foreach (var kv in VirtualPointers)
            {
                if (kv.Value == null) continue;
                int id = VirtualIdBase - kv.Key;
                bool began = !_pointerKey.ContainsKey(id) && !_uiPointers.Contains(id);
                _seen.Add(id);
                Handle(id, kv.Value.Value, began);
            }

            for (int i = 0; i < VirtualQuickTaps.Count; i++) QuickTap(VirtualIdBase - 1000 - i, VirtualQuickTaps[i]);
            VirtualQuickTaps.Clear();

            // パソコンのキー
            var kb = Keyboard.current;
            if (kb != null) HandleKeyboard(kb);

            // はなれた指
            _drop.Clear();
            foreach (var kv in _pointerKey) if (!_seen.Contains(kv.Key)) _drop.Add(kv.Key);
            foreach (int id in _drop)
            {
                if (_pointerKey.TryGetValue(id, out int key) && key >= 0) play.KeyUp(key, id);
                _pointerKey.Remove(id);
            }
            _uiPointers.RemoveWhere(id => !_seen.Contains(id));
        }

        /// <summary>おして すぐ はなした：その鍵盤を鳴らして、つぎのフレームで はなす。</summary>
        void QuickTap(int id, Vector2 screen)
        {
            if (IsOverUI != null && IsOverUI(screen)) return;
            int key = play.KeyAtScreen(screen);
            if (key < 0) return;
            play.KeyDown(key, id);
            _quick.Add((id, key));
        }

        void Handle(int id, Vector2 screen, bool began)
        {
            if (began && IsOverUI != null && IsOverUI(screen))
            {
                _uiPointers.Add(id);
                return;
            }
            if (_uiPointers.Contains(id)) return;
            int key = play.KeyAtScreen(screen);
            // 鍵盤の外ではじまった指が、すべって鍵盤に入ってきたときも、その鍵盤をおす
            int prev = _pointerKey.TryGetValue(id, out int p) ? p : -1;
            if (key == prev)
            {
                _pointerKey[id] = key;
                return;
            }
            if (prev >= 0) play.KeyUp(prev, id);
            if (key >= 0) play.KeyDown(key, id);
            _pointerKey[id] = key;
        }

        void HandleKeyboard(Keyboard kb)
        {
            var layout = play.Layout;
            if (layout == null) return;
            var range = play.VisibleWhiteRange();
            // 見えているいちばん左のド（なければ いちばん左の白鍵）
            int startWhite = range.x;
            for (int i = range.x; i <= range.y; i++)
            {
                if (NoteUtil.PitchClass(layout.WhiteMidi(i)) == 0) { startWhite = i; break; }
            }
            for (int i = 0; i < WhiteKeys.Length; i++)
            {
                int w = startWhite + i;
                int midi = w < layout.WhiteCount ? layout.WhiteMidi(w) : -1;
                KeyState(kb, WhiteKeys[i], KeyboardIdBase - i * 2, midi);
                if (BlackKeys[i] != Key.None)
                {
                    int bm = midi >= 0 && NoteUtil.IsBlack(midi + 1) && layout.Contains(midi + 1) ? midi + 1 : -1;
                    KeyState(kb, BlackKeys[i], KeyboardIdBase - i * 2 - 1, bm);
                }
            }
        }

        void KeyState(Keyboard kb, Key key, int id, int midi)
        {
            var ctrl = kb[key];
            if (ctrl == null) return;
            if (ctrl.isPressed && midi >= 0)
            {
                _seen.Add(id);
                if (!_pointerKey.TryGetValue(id, out int prev) || prev != midi)
                {
                    if (_pointerKey.TryGetValue(id, out prev) && prev >= 0) play.KeyUp(prev, id);
                    play.KeyDown(midi, id);
                    _pointerKey[id] = midi;
                }
            }
            else if (_pointerKey.TryGetValue(id, out int prev2))
            {
                if (prev2 >= 0) play.KeyUp(prev2, id);
                _pointerKey.Remove(id);
            }
        }

        public void ReleaseAll()
        {
            foreach (var (qid, qmidi) in _quick) play?.KeyUp(qmidi, qid);
            _quick.Clear();
            foreach (var kv in _pointerKey) if (kv.Value >= 0) play?.KeyUp(kv.Value, kv.Key);
            _pointerKey.Clear();
            _uiPointers.Clear();
        }

        void OnDisable() => ReleaseAll();
    }
}
