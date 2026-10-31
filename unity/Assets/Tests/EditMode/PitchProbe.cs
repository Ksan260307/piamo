using UnityEngine;

namespace Piamo.Tests
{
    /// <summary>
    /// 音の高さを はかる（テスト用）。ねらった高さの ±1 半音を 0.5 セントきざみで しらべ（ゲルツェル法）、
    /// いちばん強いところを その音の高さとする。
    /// </summary>
    public static class PitchProbe
    {
        public static float Cents(float[] interleaved, int channels, int sampleRate, int midi, bool loop, bool voice)
        {
            int frames = interleaved.Length / Mathf.Max(1, channels);
            // たたく音は 鳴りはじめ、どうぶつの声は のばしている ところ、ループは まんなか
            int start, length;
            if (voice) { start = (int)(frames * 0.15f); length = (int)(frames * 0.5f); }
            else if (loop) { start = 0; length = frames; }
            else { start = (int)(0.03f * sampleRate); length = Mathf.Min(frames - start, (int)(0.4f * sampleRate)); }
            var x = new float[length];
            for (int i = 0; i < length; i++)
            {
                float v = 0f;
                for (int c = 0; c < channels; c++) v += interleaved[(start + i) * channels + c];
                float w = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * i / (length - 1));
                x[i] = v * w;
            }
            float expect = 440f * Mathf.Pow(2f, (midi - 69) / 12f);
            float best = -1f, bestCents = 0f;
            for (float c = -100f; c <= 100f; c += 0.5f)
            {
                double f = expect * System.Math.Pow(2.0, c / 1200.0);
                double k = 2.0 * System.Math.Cos(2.0 * System.Math.PI * f / sampleRate);
                double s1 = 0, s2 = 0;
                for (int i = 0; i < length; i++)
                {
                    double s0 = x[i] + k * s1 - s2;
                    s2 = s1;
                    s1 = s0;
                }
                double power = s1 * s1 + s2 * s2 - k * s1 * s2;
                if (power > best) { best = (float)power; bestCents = c; }
            }
            return bestCents;
        }
    }
}
