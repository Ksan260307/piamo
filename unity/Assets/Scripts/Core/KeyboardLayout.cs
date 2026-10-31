using System.Collections.Generic;
using UnityEngine;

namespace Piamo
{
    /// <summary>白鍵の、黒鍵のための切りかき（Blender の Key_White_* のどれを使うか）。</summary>
    public enum Notch
    {
        Full,   // 切りかきなし
        Left,   // 左だけ
        Right,  // 右だけ
        Both,   // 両がわ
    }

    public struct KeyInfo
    {
        public int midi;
        public bool black;
        /// <summary>鍵盤の中心の X（白鍵の 0 本目の中心が 0）。黒鍵は、となりの白鍵のさかい目。</summary>
        public float x;
        /// <summary>白鍵は何本目か。黒鍵は、すぐ左の白鍵の番号。</summary>
        public int whiteIndex;
        public Notch notch;
    }

    /// <summary>
    /// 鍵盤のならび。せっていの「けんばんの かず（オクターブの数）」と「オクターブ」から、
    /// いちばん低い音・高い音と、1 本ずつの位置を決める。ドではじまり、ドでおわる。
    /// </summary>
    public class KeyboardLayout
    {
        public const int MinOctaves = 1;
        public const int MaxOctaves = 5;
        /// <summary>鳴らせる音のはんい（C1〜C8。音のサンプルがあるはんい）。</summary>
        public const int LowestNote = 24;
        public const int HighestNote = 108;

        public int Low { get; private set; }
        public int High { get; private set; }
        public bool BlackKeys { get; private set; }
        public int WhiteCount { get; private set; }
        public readonly List<KeyInfo> Keys = new List<KeyInfo>();
        readonly Dictionary<int, int> _index = new Dictionary<int, int>();

        /// <summary>オクターブの数ごとの、ずらす前のいちばん低いド。1〜2 オクターブは まんなかのド から、3 オクターブより多いと左にもふえる。</summary>
        public static int BaseLow(int octaves)
        {
            switch (Mathf.Clamp(octaves, MinOctaves, MaxOctaves))
            {
                case 1: return 60;
                case 2: return 60;
                case 3: return 48;
                case 4: return 48;
                default: return 36;
            }
        }

        /// <summary>オクターブの数に対して、ずらせるはんい（低いほう・高いほう）。</summary>
        public static Vector2Int ShiftLimits(int octaves)
        {
            octaves = Mathf.Clamp(octaves, MinOctaves, MaxOctaves);
            int b = BaseLow(octaves);
            int min = Mathf.CeilToInt((LowestNote - b) / 12f);
            int max = Mathf.FloorToInt((HighestNote - 12 * octaves - b) / 12f);
            return new Vector2Int(min, max);
        }

        public static int ClampShift(int octaves, int shift)
        {
            var lim = ShiftLimits(octaves);
            return Mathf.Clamp(shift, lim.x, lim.y);
        }

        public static KeyboardLayout Create(int octaves, int shift, bool blackKeys)
        {
            octaves = Mathf.Clamp(octaves, MinOctaves, MaxOctaves);
            shift = ClampShift(octaves, shift);
            var k = new KeyboardLayout();
            k.Low = BaseLow(octaves) + 12 * shift;
            k.High = k.Low + 12 * octaves;
            k.BlackKeys = blackKeys;
            int white = 0;
            for (int m = k.Low; m <= k.High; m++)
            {
                if (!NoteUtil.IsBlack(m))
                {
                    k.Keys.Add(new KeyInfo { midi = m, black = false, x = white * PianoDimensions.Pitch, whiteIndex = white });
                    white++;
                }
                else if (blackKeys)
                {
                    k.Keys.Add(new KeyInfo { midi = m, black = true, x = (white - 0.5f) * PianoDimensions.Pitch, whiteIndex = white - 1 });
                }
            }
            k.WhiteCount = white;
            for (int i = 0; i < k.Keys.Count; i++)
            {
                var key = k.Keys[i];
                if (!key.black)
                {
                    bool left = blackKeys && key.midi - 1 >= k.Low && NoteUtil.IsBlack(key.midi - 1);
                    bool right = blackKeys && key.midi + 1 <= k.High && NoteUtil.IsBlack(key.midi + 1);
                    key.notch = left && right ? Notch.Both : left ? Notch.Left : right ? Notch.Right : Notch.Full;
                    k.Keys[i] = key;
                }
                k._index[key.midi] = i;
            }
            return k;
        }

        public bool Contains(int midi) => _index.ContainsKey(midi);

        public bool TryGet(int midi, out KeyInfo info)
        {
            if (_index.TryGetValue(midi, out int i))
            {
                info = Keys[i];
                return true;
            }
            info = default;
            return false;
        }

        /// <summary>白鍵の何本目かから MIDI 番号へ。</summary>
        public int WhiteMidi(int whiteIndex)
        {
            whiteIndex = Mathf.Clamp(whiteIndex, 0, WhiteCount - 1);
            return NoteUtil.FromWhiteNumber(NoteUtil.WhiteNumber(Low) + whiteIndex);
        }

        /// <summary>
        /// 黒鍵の上の面の高さで当てた点（x = ならびの位置、z = 奥行き。どちらも鍵盤の座標）が、どの黒鍵か。なければ -1。
        /// 白鍵より先にしらべる（黒鍵は白鍵の上にあるので）。
        /// </summary>
        public int HitBlack(float x, float z)
        {
            if (!BlackKeys) return -1;
            float zFront = PianoDimensions.WhiteLength - PianoDimensions.BlackLength - 0.05f;
            if (z < zFront || z > PianoDimensions.WhiteLength + 0.05f) return -1;
            float half = PianoDimensions.BlackWidth / 2 + 0.03f;
            foreach (var k in Keys)
            {
                if (k.black && Mathf.Abs(x - k.x) <= half) return k.midi;
            }
            return -1;
        }

        /// <summary>白鍵の上の面（と手前）の点が、どの白鍵か。なければ -1。</summary>
        public int HitWhite(float x, float z)
        {
            if (z < PianoDimensions.CaseFrontZ - 0.1f || z > PianoDimensions.WhiteLength + 0.1f) return -1;
            int i = Mathf.RoundToInt(x / PianoDimensions.Pitch);
            if (x < -0.5f * PianoDimensions.Pitch || i >= WhiteCount || i < 0) return -1;
            if (x > (WhiteCount - 0.5f) * PianoDimensions.Pitch) return -1;
            return WhiteMidi(i);
        }
    }
}
