"""
楽器の音の高さを ぜんぶ しらべる（音のファイルが、ねらった音の高さに なっているか）。
  python tools/check_pitch.py            # すべての楽器・すべての音
  python tools/check_pitch.py piano harp # えらんだ楽器だけ

しらべかた：
 - たたく音は、鳴りはじめ（0.03 秒から 0.5 秒ぶん）、ループの音は ループぜんぶ、
   どうぶつの声は のばしている ところ（15%〜65%）を使う
 - ハン窓をかけて 16 倍に 0 をつめた FFT で、ねらった高さの ±1 半音の中の いちばん強い山をさがし、
   山の まわり 3 点の 放物線で、1 セントより こまかく もとめる
 - ずれ（セント）が TOLERANCE_CENTS をこえたら しっぱい
"""
import math
import os
import sys
import wave

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, ".."))
FOLDER = os.path.join(ROOT, "unity", "Assets", "Audio", "Instruments")
TOLERANCE_CENTS = 3.0
VOICES = ("cat", "dog")
LOOPS = ("organ", "flute")


def midi_hz(n):
    return 440.0 * 2 ** ((n - 69) / 12)


def read_wav(path):
    with wave.open(path) as w:
        sr = w.getframerate()
        ch = w.getnchannels()
        x = np.frombuffer(w.readframes(w.getnframes()), np.int16).astype(np.float64) / 32768.0
    if ch > 1:
        x = x.reshape(-1, ch).mean(axis=1)
    return x, sr


def segment(x, sr, kind):
    n = len(x)
    if kind == "loop":
        return x
    if kind == "voice":
        return x[int(n * 0.15): int(n * 0.65)]
    a = int(0.03 * sr)
    return x[a: a + int(0.5 * sr)]


def peak_hz(seg, sr, expect, pad=16):
    """expect の ±1 半音の中で いちばん強い周波数（放物線で なめらかに）。"""
    n = len(seg)
    win = np.hanning(n)
    spec = np.abs(np.fft.rfft(seg * win, n * pad))
    df = sr / (n * pad)
    lo = int(expect * 2 ** (-1 / 12) / df)
    hi = int(expect * 2 ** (1 / 12) / df) + 1
    k = lo + int(np.argmax(spec[lo:hi]))
    a, b, c = np.log(spec[k - 1:k + 2] + 1e-12)
    d = 0.5 * (a - c) / (a - 2 * b + c) if (a - 2 * b + c) != 0 else 0.0
    return (k + d) * df, spec


def mean_hz(loop, sr, expect):
    """ループの音の、基音の 平均の周波数（ビブラートがあっても くるわない）。
    基音のまわり（±1 半音）だけを のこした 解析信号の 位相の すすみ から もとめる。"""
    n = len(loop)
    spec = np.fft.fft(loop)
    fr = np.fft.fftfreq(n, 1 / sr)
    band = (fr > expect * 2 ** (-1 / 12)) & (fr < expect * 2 ** (1 / 12))
    analytic = np.fft.ifft(np.where(band, 2 * spec, 0))
    ph = np.unwrap(np.angle(analytic))
    return (ph[-1] - ph[0]) / (2 * np.pi) / ((n - 1) / sr)


def measure(path, note, kind="hit"):
    """(はかった周波数, ずれのセント, 基音が いちばん強いか)"""
    x, sr = read_wav(path)
    seg = segment(x, sr, kind)
    expect = midi_hz(note)
    f, spec = peak_hz(seg, sr, expect)
    if kind == "loop":
        f = mean_hz(seg, sr, expect)
    cents = 1200 * math.log2(f / expect)
    # 基音の山が、まわりの音（基音より上の 12 半音より下）の いちばん強い山と くらべて しっかり あるか
    df = sr / (len(seg) * 16)
    k0 = int(round(f / df))
    lo = max(1, int(expect * 0.5 / df))
    strongest = int(np.argmax(spec[lo:])) + lo
    dominant = spec[k0] >= 0.25 * spec[strongest]
    return f, cents, dominant


def kind_of(name):
    return "voice" if name in VOICES else "loop" if name in LOOPS else "hit"


def check(names=None, notes=None, verbose=True):
    """すべてを はかって、(楽器, 音, ずれ, 基音が強いか) の ならびを かえす。"""
    sys.path.insert(0, HERE)
    import make_audio
    rows = []
    for name, _, _, _ in make_audio.INSTRUMENTS:
        if names and name not in names:
            continue
        for note in notes or make_audio.ROOT_NOTES:
            path = os.path.join(FOLDER, name, f"{name}_{note:03d}.wav")
            f, cents, dominant = measure(path, note, kind_of(name))
            rows.append((name, note, cents, dominant))
        if verbose:
            mine = [r for r in rows if r[0] == name]
            worst = max(mine, key=lambda r: abs(r[2]))
            weak = [r[1] for r in mine if not r[3]]
            print(f"{name:9s} {len(mine)} notes  max |ずれ| {abs(worst[2]):5.2f} セント (note {worst[1]})"
                  + (f"  基音が よわい: {weak}" if weak else ""))
    return rows


def main(argv):
    rows = check(argv or None)
    bad = [r for r in rows if abs(r[2]) > TOLERANCE_CENTS or not r[3]]
    if bad:
        for name, note, cents, dominant in bad[:30]:
            print(f"NG {name} {note}: {cents:+.2f} セント" + ("" if dominant else "（基音が よわい）"))
        sys.exit(1)
    print(f"OK: {len(rows)} の音が ±{TOLERANCE_CENTS} セント以内")


if __name__ == "__main__":
    main(sys.argv[1:])
