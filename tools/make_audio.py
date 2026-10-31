"""
Piamo の音をすべてプログラムで合成する（numpy のみ）。
  python tools/make_audio.py            # 楽器と効果音
  python tools/make_audio.py sfx        # 効果音だけ
  python tools/make_audio.py piano harp # えらんだ楽器だけ

出力:
 - unity/Assets/Audio/Instruments/<楽器>/<楽器>_<MIDI 番号>.wav
     楽器ごとに、C1 (24) から C8 (108) まで 4 半音おきの音（22 本）。あいだの音は、アプリが近い音の高さを変えて鳴らす（±2 半音まで）
     ピアノ・トイピアノ・オルゴール・てっきん・もっきん・ハープ・ねこ・いぬ は、たたいて減っていく音（1 回鳴らす）
     オルガン・ふえ は、つなぎ目のないループ（おしている間なりつづける）
 - unity/Assets/Audio/Sfx/*.wav : ボタン・もどる・せってい・スタンプ・でっかいスタンプ・まねっこの正解・ヒントなど
"""
import os
import sys
import wave

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, ".."))
OUT = os.path.join(ROOT, "unity", "Assets", "Audio")
rng = np.random.default_rng(20261031)

ROOT_NOTES = list(range(24, 109, 4))   # C1 … C8（4 半音おき）
MAX_SHIFT = 2                          # アプリでずらす半音の数（ROOT_NOTES のあいだの音）


# ---------------------------------------------------------------------------
# 基本
# ---------------------------------------------------------------------------
def midi(n):
    return 440.0 * 2 ** ((n - 69) / 12)


def t_axis(sec, sr):
    return np.arange(int(round(sec * sr))) / sr


def write_wav(path, data, sr, peak=0.89, normalize=True):
    data = np.asarray(data, dtype=np.float64)
    if normalize:
        data = data / (np.max(np.abs(data)) + 1e-12) * peak
    pcm = (np.clip(data, -1, 1) * 32767).astype(np.int16)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with wave.open(path, "wb") as w:
        w.setnchannels(1 if pcm.ndim == 1 else pcm.shape[1])
        w.setsampwidth(2)
        w.setframerate(sr)
        w.writeframes(pcm.tobytes())


def fade(x, sr, a=0.002, r=0.04):
    x = np.array(x, dtype=np.float64)
    ai = max(1, int(a * sr))
    ri = max(1, int(r * sr))
    x[:ai] *= np.linspace(0, 1, ai)
    x[-ri:] *= np.linspace(1, 0, ri) ** 2
    return x


def lowpass(x, sr, cutoff):
    """1 次のローパス（なだらか）。"""
    a = np.exp(-2 * np.pi * cutoff / sr)
    y = np.empty_like(x)
    s = 0.0
    for i, v in enumerate(x):
        s = (1 - a) * v + a * s
        y[i] = s
    return y


def fft_lowpass(x, sr, cutoff, order=2):
    spec = np.fft.rfft(x)
    f = np.fft.rfftfreq(len(x), 1 / sr)
    spec *= 1 / np.sqrt(1 + (f / cutoff) ** (2 * order))
    return np.fft.irfft(spec, len(x))


def room(x, sr, seconds=0.9, mix=0.14, seed=7):
    """小さな部屋のひびき（指数減衰ノイズとの畳みこみ）。ひびきの分だけ長くなる。"""
    r = np.random.default_rng(seed)
    n = int(seconds * sr)
    ir = r.normal(size=n) * np.exp(-np.linspace(0, 7.0, n))
    ir = fft_lowpass(ir, sr, 4500)
    ir[: int(0.006 * sr)] = 0
    ir /= np.sqrt(np.sum(ir ** 2)) + 1e-12
    m = len(x) + n
    wet = np.fft.irfft(np.fft.rfft(x, m) * np.fft.rfft(ir, m), m)
    dry = np.concatenate([x, np.zeros(n)])
    return dry * (1 - mix) + wet * mix * 1.2


def loudness_match(x, sr, target=0.25, window=0.25):
    """鳴りはじめの大きさ（RMS）をそろえる。高い音と低い音が同じくらいに聞こえるように。"""
    seg = x[: max(1, int(window * sr))]
    rms = np.sqrt(np.mean(seg ** 2)) + 1e-9
    return x * (target / rms)


def lerp_by_note(note, lo, hi):
    """C1 (24) で lo、C8 (108) で hi になる値。"""
    k = (note - 24) / (108 - 24)
    return lo + (hi - lo) * min(1.0, max(0.0, k))


# ---------------------------------------------------------------------------
# 楽器（どれも 1 本の音を返す。sr はサンプリング周波数）
# ---------------------------------------------------------------------------
def piano(note, sr):
    f0 = midi(note)
    dur = lerp_by_note(note, 4.0, 1.3)
    t = t_axis(dur, sr)
    B = 0.00012 * 2 ** ((note - 48) / 18)          # 弦の硬さ（高い音ほど倍音が高めにずれる）
    tau_base = lerp_by_note(note, 2.6, 0.35)       # 基音の減衰の速さ
    bright = lerp_by_note(note, 0.55, 1.0)
    out = np.zeros_like(t)
    nyq = sr / 2 * 0.92
    strings = [0.0] if note < 36 else ([-0.45, 0.45] if note < 48 else [-0.6, 0.0, 0.55])
    for k in range(1, 30):
        fk = k * f0 * np.sqrt(1 + B * k * k)
        if fk > nyq:
            break
        # ハンマーでたたいた弦：低い倍音が強く、高い倍音はなだらかに弱い
        amp = (1.0 / k ** 1.15) * np.exp(-((k - 1) * 0.11) / bright)
        if k % 7 == 0:
            amp *= 0.35   # たたく位置（弦の 1/7）で消える倍音
        tau_fast = tau_base / (1 + 0.32 * (k - 1))
        tau_slow = tau_fast * 4.5
        env = 0.72 * np.exp(-t / tau_fast) + 0.28 * np.exp(-t / tau_slow)
        ph = rng.uniform(0, 2 * np.pi)
        for c in strings:
            det = 2 ** (c / 1200)
            out += amp * env * np.cos(2 * np.pi * fk * det * t + ph) / len(strings)
    # ハンマーの音（ごく短いノイズ）
    n = len(t)
    hn = rng.normal(size=n) * np.exp(-t / 0.006)
    hn = fft_lowpass(hn, sr, min(6000, f0 * 6))
    out += hn * 0.08
    out = fade(out, sr, 0.0015, 0.08)
    out = room(out, sr, 0.9, 0.12)
    return out


def tine(note, sr, ratios, amps, taus, dur, click=0.0, click_tau=0.004):
    """金属や木の棒をたたいた音（倍音の比 ratios と、それぞれの大きさ・減衰）。"""
    f0 = midi(note)
    t = t_axis(dur, sr)
    out = np.zeros_like(t)
    nyq = sr / 2 * 0.92
    for r, a, tau in zip(ratios, amps, taus):
        f = f0 * r
        if f > nyq:
            continue
        out += a * np.exp(-t / tau) * np.sin(2 * np.pi * f * t + rng.uniform(0, 6.28))
    if click > 0:
        out += click * rng.normal(size=len(t)) * np.exp(-t / click_tau)
    return out


def toy_piano(note, sr):
    dur = lerp_by_note(note, 1.6, 0.8)
    s = lerp_by_note(note, 1.0, 0.45)
    out = tine(note, sr, [1.0, 2.01, 3.9, 5.43, 8.7], [1.0, 0.35, 0.28, 0.18, 0.08],
               [0.55 * s, 0.25 * s, 0.12 * s, 0.07 * s, 0.04 * s], dur, click=0.08)
    return room(fade(out, sr, 0.001, 0.05), sr, 0.6, 0.12)


def music_box(note, sr):
    dur = lerp_by_note(note, 2.4, 1.3)
    s = lerp_by_note(note, 1.1, 0.6)
    a = tine(note, sr, [1.0, 2.0, 4.1, 5.95, 9.2], [1.0, 0.22, 0.32, 0.12, 0.05],
             [1.2 * s, 0.6 * s, 0.25 * s, 0.12 * s, 0.06 * s], dur, click=0.05, click_tau=0.002)
    # くしの歯の、ほんの少しずれたうなり
    b = tine(note + 0.07, sr, [1.0], [0.25], [1.0 * s], dur)
    return room(fade(a + b, sr, 0.001, 0.05), sr, 1.1, 0.18)


def glockenspiel(note, sr):
    dur = lerp_by_note(note, 2.4, 1.3)
    s = lerp_by_note(note, 1.0, 0.6)
    out = tine(note, sr, [1.0, 2.76, 5.40, 8.93], [1.0, 0.42, 0.2, 0.08],
               [1.1 * s, 0.35 * s, 0.14 * s, 0.07 * s], dur, click=0.06, click_tau=0.0015)
    return room(fade(out, sr, 0.001, 0.05), sr, 1.0, 0.16)


def marimba(note, sr):
    dur = lerp_by_note(note, 1.5, 0.55)
    s = lerp_by_note(note, 1.0, 0.4)
    out = tine(note, sr, [1.0, 3.93, 9.2], [1.0, 0.25, 0.06], [0.42 * s, 0.09 * s, 0.03 * s], dur,
               click=0.1, click_tau=0.003)
    out = fft_lowpass(out, sr, 5000)
    return room(fade(out, sr, 0.002, 0.04), sr, 0.7, 0.12)


def harp(note, sr):
    f0 = midi(note)
    dur = lerp_by_note(note, 3.0, 1.2)
    t = t_axis(dur, sr)
    tau = lerp_by_note(note, 1.6, 0.45)
    out = np.zeros_like(t)
    nyq = sr / 2 * 0.9
    pos = 0.23   # はじく位置
    for k in range(1, 16):
        f = f0 * k
        if f > nyq:
            break
        amp = abs(np.sin(np.pi * k * pos)) / k ** 1.6
        out += amp * np.exp(-t * (1 + 0.45 * (k - 1)) / tau) * np.sin(2 * np.pi * f * t)
    out = fade(out, sr, 0.003, 0.06)
    return room(out, sr, 1.1, 0.16)


def loop_length(f0, sr, seconds):
    """f0 の波がちょうど整数回入る長さ（サンプル数）と、そのときの周波数。"""
    cycles = max(1, int(round(f0 * seconds)))
    n = int(round(cycles * sr / f0))
    return n, cycles * sr / n


def organ(note, sr):
    f0 = midi(note)
    n, f = loop_length(f0, sr, 0.5)
    t = np.arange(n) / sr
    out = np.zeros(n)
    # ドローバー（16' 8' 4' 2 2/3' 2'）の音色
    for k, a in ((1, 1.0), (2, 0.55), (3, 0.3), (4, 0.22), (6, 0.08), (8, 0.06)):
        if f * k < sr / 2 * 0.9:
            out += a * np.sin(2 * np.pi * f * k * t)
    return out


def flute(note, sr):
    f0 = midi(note)
    n, f = loop_length(f0, sr, 0.6)
    t = np.arange(n) / sr
    vib = 1 + 0.004 * np.sin(2 * np.pi * (5.0 * n / sr) / (n / sr) * t)  # 0.6 秒に 3 回のビブラート
    phase = 2 * np.pi * f * np.cumsum(vib) / sr
    phase -= phase[0]
    # ビブラートでずれた位相を、ループの終わりでぴったり合わせる
    phase *= (2 * np.pi * round(f * n / sr)) / (phase[-1] + 2 * np.pi * f / sr)
    out = np.sin(phase) + 0.12 * np.sin(2 * phase) + 0.05 * np.sin(3 * phase)
    # 息の音（周期的なノイズなのでそのままループする）
    spec = rng.normal(size=n // 2 + 1) + 1j * rng.normal(size=n // 2 + 1)
    fr = np.fft.rfftfreq(n, 1 / sr)
    spec *= np.exp(-((fr - f * 2) / (f * 3 + 400)) ** 2)
    breath = np.fft.irfft(spec, n)
    breath /= np.max(np.abs(breath)) + 1e-9
    return out + 0.05 * breath


def formant_voice(sr, f0_curve, formants, dur, breath=0.03, nasal=0.0):
    """声の合成（のこぎり波 + 時間で動くフォルマント）。formants は [(時刻 0〜1, [(周波数, 帯域), …]), …]"""
    n = int(dur * sr)
    t = np.arange(n) / sr
    u = t / dur
    f0 = np.interp(u, *zip(*f0_curve))
    ph = np.cumsum(2 * np.pi * f0 / sr)
    src = np.zeros(n)
    for k in range(1, 40):
        m = f0 * k < sr / 2 * 0.9
        src += m * np.sin(ph * k) / k
    src += breath * rng.normal(size=n)
    keys = [p for p, _ in formants]
    out = np.zeros(n)
    nf = len(formants[0][1])
    for j in range(nf):
        fc = np.interp(u, keys, [fs[j][0] for _, fs in formants])
        bw = np.interp(u, keys, [fs[j][1] for _, fs in formants])
        r = np.exp(-np.pi * bw / sr)
        th = 2 * np.pi * fc / sr
        a1 = -2 * r * np.cos(th)
        a2 = r * r
        g = 1 - r
        y = np.zeros(n)
        y1 = y2 = 0.0
        for i in range(n):
            v = g[i] * src[i] - a1[i] * y1 - a2[i] * y2
            y[i] = v
            y2, y1 = y1, v
        out += y * (1.0 / (j + 1))
    if nasal > 0:
        out += nasal * np.sin(ph) * np.exp(-((u - 0.85) / 0.12) ** 2)
    return out


def cat(note, sr):
    """にゃー（音の高さで鳴く）。"""
    f = midi(note)
    dur = lerp_by_note(note, 0.75, 0.5)
    curve = [(0.0, f * 0.95), (0.1, f * 1.0), (0.68, f * 1.0), (0.86, f * 0.98), (1.0, f * 0.93)]
    fm = [
        (0.0, [(420, 120), (2300, 200), (3100, 300)]),   # い
        (0.25, [(820, 150), (1500, 200), (2800, 300)]),  # あ
        (0.7, [(780, 150), (1250, 200), (2700, 300)]),
        (1.0, [(380, 120), (900, 150), (2500, 300)]),    # う
    ]
    out = formant_voice(sr, curve, fm, dur, breath=0.02)
    env = np.interp(np.linspace(0, 1, len(out)), [0, 0.08, 0.7, 1.0], [0, 1, 0.85, 0])
    return fade(out * env, sr, 0.004, 0.05)


def dog(note, sr):
    """わん（音の高さで鳴く）。"""
    f = midi(note)
    dur = lerp_by_note(note, 0.42, 0.3)
    curve = [(0.0, f * 0.93), (0.1, f * 1.0), (0.72, f * 1.0), (1.0, f * 0.9)]
    fm = [
        (0.0, [(350, 120), (700, 150), (2400, 300)]),    # う
        (0.3, [(850, 150), (1300, 200), (2600, 300)]),   # あ
        (0.75, [(750, 150), (1200, 200), (2500, 300)]),
        (1.0, [(300, 100), (1100, 300), (2400, 300)]),   # ん
    ]
    out = formant_voice(sr, curve, fm, dur, breath=0.05, nasal=0.3)
    env = np.interp(np.linspace(0, 1, len(out)), [0, 0.06, 0.55, 1.0], [0, 1, 0.8, 0])
    return fade(out * env, sr, 0.003, 0.04)


# 楽器の一覧：id、合成関数、サンプリング周波数、ループするか
INSTRUMENTS = [
    ("piano", piano, 32000, False),
    ("toy", toy_piano, 24000, False),
    ("musicbox", music_box, 32000, False),
    ("glock", glockenspiel, 32000, False),
    ("marimba", marimba, 24000, False),
    ("organ", organ, 24000, True),
    ("flute", flute, 24000, True),
    ("harp", harp, 32000, False),
    ("cat", cat, 22050, False),
    ("dog", dog, 22050, False),
]


def make_instrument(name, fn, sr, loop):
    folder = os.path.join(OUT, "Instruments", name)
    os.makedirs(folder, exist_ok=True)
    for f in os.listdir(folder):
        if f.endswith(".wav"):
            os.remove(os.path.join(folder, f))
    for note in ROOT_NOTES:
        x = fn(note, sr)
        if loop:
            x = x / (np.max(np.abs(x)) + 1e-12) * 0.5
        else:
            x = loudness_match(x, sr)
        x = np.clip(x, -1, 1)
        # 大きさは楽器ごとにそろえて、0.95 をこえないようにだけ気をつける
        peak = np.max(np.abs(x))
        if peak > 0.95:
            x *= 0.95 / peak
        write_wav(os.path.join(folder, f"{name}_{note:03d}.wav"), x, sr, normalize=False)
    print("wrote instrument", name, len(ROOT_NOTES), "notes")


# ---------------------------------------------------------------------------
# 効果音
# ---------------------------------------------------------------------------
SFX_SR = 32000


def bell(f, dur, sr=SFX_SR, bright=1.0):
    t = t_axis(dur, sr)
    out = np.zeros_like(t)
    for r, a, tau in ((1, 1.0, 0.5), (2.0, 0.35 * bright, 0.25), (3.0, 0.2 * bright, 0.15), (4.2, 0.12 * bright, 0.08)):
        out += a * np.exp(-t / (tau * dur)) * np.sin(2 * np.pi * f * r * t)
    return fade(out, sr, 0.002, 0.03)


def place(buf, start_sec, sig, gain=1.0, sr=SFX_SR):
    i = int(start_sec * sr)
    j = min(len(buf), i + len(sig))
    buf[i:j] += sig[: j - i] * gain


def brass(f, dur, sr=SFX_SR):
    t = t_axis(dur, sr)
    vib = 1 + 0.006 * np.sin(2 * np.pi * 5.5 * t) * np.clip(t / 0.25, 0, 1)
    ph = 2 * np.pi * f * np.cumsum(vib) / sr
    out = np.zeros_like(t)
    for k in range(1, 14):
        if f * k > sr / 2 * 0.9:
            break
        out += np.sin(ph * k) / k * (0.9 ** k)
    env = np.interp(t, [0, 0.04, 0.12, dur - 0.08, dur], [0, 1, 0.8, 0.7, 0])
    return fft_lowpass(out * env, sr, 3200)


def thump(dur=0.18, f0=170, f1=60, sr=SFX_SR):
    t = t_axis(dur, sr)
    f = f1 + (f0 - f1) * np.exp(-t / 0.03)
    ph = 2 * np.pi * np.cumsum(f) / sr
    out = np.sin(ph) * np.exp(-t / 0.05)
    out += 0.3 * rng.normal(size=len(t)) * np.exp(-t / 0.008)
    return fade(out, sr, 0.001, 0.02)


def make_sfx():
    sr = SFX_SR
    folder = os.path.join(OUT, "Sfx")
    os.makedirs(folder, exist_ok=True)

    # ボタン：ぽん
    t = t_axis(0.12, sr)
    f = 900 + 500 * np.exp(-t / 0.02)
    x = np.sin(2 * np.pi * np.cumsum(f) / sr) * np.exp(-t / 0.035)
    write_wav(os.path.join(folder, "tap.wav"), fade(x, sr, 0.001, 0.02), sr, 0.6)

    # もどる：ぽよん（さがる 2 音）
    buf = np.zeros(int(0.3 * sr))
    place(buf, 0.0, bell(midi(79), 0.18), 0.8)
    place(buf, 0.08, bell(midi(72), 0.2), 0.8)
    write_wav(os.path.join(folder, "back.wav"), buf, sr, 0.55)

    # ひらく：ぴろりん（あがる 3 音）
    buf = np.zeros(int(0.45 * sr))
    for i, n in enumerate((72, 76, 79)):
        place(buf, i * 0.06, bell(midi(n + 12), 0.3), 0.7)
    write_wav(os.path.join(folder, "open.wav"), buf, sr, 0.55)

    # スタンプ：ぽんっ（ゴムのはんこ）＋ きらり
    buf = np.zeros(int(0.9 * sr))
    place(buf, 0.0, thump(), 1.0)
    for i, n in enumerate((84, 88, 91, 96)):
        place(buf, 0.12 + i * 0.05, bell(midi(n), 0.5), 0.35)
    write_wav(os.path.join(folder, "stamp.wav"), room(buf, sr, 0.8, 0.15), sr, 0.8)

    # でっかいスタンプ：どーん ＋ ファンファーレ
    buf = np.zeros(int(2.6 * sr))
    place(buf, 0.0, thump(0.3, 140, 45), 1.0)
    fan = [(0.18, 67, 0.14), (0.33, 67, 0.14), (0.48, 67, 0.14), (0.63, 72, 0.9)]
    for st, n, d in fan:
        place(buf, st, brass(midi(n), d), 0.5)
        place(buf, st, brass(midi(n - 5), d), 0.25)
    for i, n in enumerate((84, 88, 91, 96, 100, 103)):
        place(buf, 0.7 + i * 0.07, bell(midi(n), 0.9), 0.3)
    write_wav(os.path.join(folder, "stamp_big.wav"), room(buf, sr, 1.2, 0.18), sr, 0.85)

    # まねっこの正解：ピロリン♪
    buf = np.zeros(int(1.0 * sr))
    for i, n in enumerate((72, 76, 79, 84)):
        place(buf, i * 0.08, bell(midi(n), 0.6), 0.6)
    write_wav(os.path.join(folder, "success.wav"), room(buf, sr, 0.8, 0.15), sr, 0.7)

    # 1 曲ぜんぶできた：パンパカパーン
    buf = np.zeros(int(2.4 * sr))
    seq = [(0.0, 72, 0.12), (0.14, 72, 0.12), (0.28, 72, 0.12), (0.42, 77, 0.3), (0.76, 81, 0.25), (1.05, 84, 1.0)]
    for st, n, d in seq:
        place(buf, st, brass(midi(n), d), 0.55)
    for i, n in enumerate((96, 100, 103, 108)):
        place(buf, 1.05 + i * 0.06, bell(midi(n), 0.9), 0.25)
    write_wav(os.path.join(folder, "clear.wav"), room(buf, sr, 1.2, 0.18), sr, 0.85)

    # まちがい：やさしい「ぷぷ」
    buf = np.zeros(int(0.4 * sr))
    t = t_axis(0.12, sr)
    for i, f in enumerate((330, 262)):
        x = np.sin(2 * np.pi * f * t) + 0.3 * np.sin(4 * np.pi * f * t)
        x *= np.exp(-t / 0.06)
        place(buf, i * 0.13, fade(x, sr, 0.004, 0.02), 0.6)
    write_wav(os.path.join(folder, "oops.wav"), buf, sr, 0.45)

    # ヒント：きらきら
    buf = np.zeros(int(1.0 * sr))
    for i in range(7):
        n = 96 + (i * 5) % 12
        place(buf, i * 0.07, bell(midi(n), 0.35), 0.4 * (1 - i * 0.08))
    write_wav(os.path.join(folder, "hint.wav"), room(buf, sr, 0.8, 0.2), sr, 0.5)

    # スタンプがもらえる：ちゃららん
    buf = np.zeros(int(1.2 * sr))
    for i, n in enumerate((79, 84, 88, 91)):
        place(buf, i * 0.1, bell(midi(n), 0.8), 0.55)
    write_wav(os.path.join(folder, "earn.wav"), room(buf, sr, 1.0, 0.18), sr, 0.65)

    # ページをめくる：さっ
    n = int(0.22 * sr)
    t = np.arange(n) / sr
    x = rng.normal(size=n) * np.interp(t, [0, 0.05, 0.22], [0, 1, 0])
    x = fft_lowpass(x, sr, 3500) - fft_lowpass(x, sr, 600)
    write_wav(os.path.join(folder, "page.wav"), x, sr, 0.35)

    # おてほんの合図：ぽろん
    buf = np.zeros(int(0.6 * sr))
    for i, n in enumerate((84, 79)):
        place(buf, i * 0.12, bell(midi(n), 0.4), 0.6)
    write_wav(os.path.join(folder, "listen.wav"), buf, sr, 0.5)
    print("wrote sfx")


def main(argv):
    names = [a for a in argv if a != "sfx"]
    want_sfx = not argv or "sfx" in argv
    for name, fn, sr, loop in INSTRUMENTS:
        if argv and name not in names:
            continue
        make_instrument(name, fn, sr, loop)
    if want_sfx:
        make_sfx()


if __name__ == "__main__":
    main(sys.argv[1:])
