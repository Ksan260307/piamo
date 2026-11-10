"""
まねっこ・えんそうの 曲の データを しらべる（Unity が なくても CI で うごく）。
  python tools/check_songs.py
しらべること:
 - えんそう（PerformSongs.cs）: 30 きょく・id と なまえが かぶらない・どの小節も 拍子どおりの長さ（弱起と さいごは みじかくてよい）・
   メロディは C4〜C6・和音の 小節の数が メロディと おなじ・和音の かわりめ／小節の はじめの メロディは その和音の音
 - まねっこ（Songs.cs）: どの曲も 1 オクターブ（C4〜C5）の 白鍵だけ・id と なまえが かぶらない
"""
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
PERFORM = os.path.join(ROOT, "unity", "Assets", "Scripts", "Perform", "PerformSongs.cs")
MIMIC = os.path.join(ROOT, "unity", "Assets", "Scripts", "Mimic", "Songs.cs")

PCS = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}
QUAL = {"": [0, 4, 7], "m": [0, 3, 7], "7": [0, 4, 7, 10], "m7": [0, 3, 7, 10], "6": [0, 4, 7, 9], "5": [0, 7], "dim": [0, 3, 6]}
WHITE = {0, 2, 4, 5, 7, 9, 11}


def pitch(name):
    m = re.fullmatch(r"([A-G])(#|b|♯|♭)?(-?\d)", name)
    if not m:
        raise ValueError(f"音の名前が おかしい: {name}")
    pc = PCS[m.group(1)] + {"#": 1, "♯": 1, "b": -1, "♭": -1, None: 0}[m.group(2)]
    return (int(m.group(3)) + 1) * 12 + pc


def note(tok):
    """「C4」「E4:0.5」「R:2」「F4~」→ (midi または None, 拍, つぎに つなぐか)。"""
    tie = tok.endswith("~")
    tok = tok.rstrip("~")
    name, _, d = tok.partition(":")
    beats = float(d) if d else 1.0
    return (None if name == "R" else pitch(name)), beats, tie


def chord(name):
    m = re.fullmatch(r"([A-G])(#|b)?(m7|m|7|6|5|dim)?", name)
    if not m:
        raise ValueError(f"和音の名前が おかしい: {name}")
    root = PCS[m.group(1)] + {"#": 1, "b": -1, None: 0}[m.group(2)]
    return [(root + i) % 12 for i in QUAL[m.group(3) or ""]]


def cs_strings(expr):
    """C# の "a" + "b" を つなげた 文字列。"""
    return "".join(re.findall(r'"((?:[^"\\]|\\.)*)"', expr))


def perform_songs(path=PERFORM):
    with open(path, encoding="utf-8") as f:
        text = f.read()
    songs = []
    for m in re.finditer(r'Make\("(\w+)", "([^"]+)", PerformCategory\.(\w+), "(\w+)", "([^"]*)", (\d), (\d+), "(\d+/\d+)", PerformStyle\.(\w+),(.*?)\),\n', text, re.S):
        args = m.group(10)
        # メロディ（+ で つないだ 文字列）と 和音（さいごの 文字列）に わける
        strings = re.findall(r'((?:"(?:[^"\\]|\\.)*"\s*\+\s*)*"(?:[^"\\]|\\.)*")', args)
        if len(strings) < 2:
            raise ValueError(f"{m.group(1)}: メロディと 和音が 見つからない")
        flats = "flats: true" in args
        songs.append({
            "id": m.group(1), "title": m.group(2), "category": m.group(3), "animal": m.group(4), "source": m.group(5),
            "stars": int(m.group(6)), "bpm": int(m.group(7)), "meter": m.group(8), "style": m.group(9),
            "melody": cs_strings(strings[0]), "chords": cs_strings(strings[1]), "flats": flats,
        })
    return songs


def mimic_songs(path=MIMIC):
    with open(path, encoding="utf-8") as f:
        text = f.read()
    songs = []
    for m in re.finditer(r'Make\("(\w+)", "([^"]+)", "(\w+)", (\d+),\s*((?:"(?:[^"\\]|\\.)*"\s*\+?\s*)+)(?:, exercise: true)?\)', text):
        songs.append({"id": m.group(1), "title": m.group(2), "animal": m.group(3), "bpm": int(m.group(4)), "notes": cs_strings(m.group(5))})
    return songs


def check_perform(song):
    problems = []
    n, d = song["meter"].split("/")
    bar = int(n) * 4 / int(d)
    bars = [[note(t) for t in b.split()] for b in song["melody"].split("|")]
    chords = [c.split() for c in song["chords"].split("|")]
    if len(bars) != len(chords):
        return [f"和音の小節 {len(chords)} と メロディの小節 {len(bars)} が ちがう"]
    midis = []
    for i, (mb, cb) in enumerate(zip(bars, chords)):
        length = sum(b for _, b, _ in mb)
        edge = i == 0 or i == len(bars) - 1
        if (not edge and abs(length - bar) > 1e-6) or (edge and length > bar + 1e-6):
            problems.append(f"{i + 1} 小節め：{length} 拍（{song['meter']}）")
        fixed = sum(float(t.split(":")[1]) for t in cb if ":" in t)
        free = [t for t in cb if ":" not in t]
        each = (length - fixed) / len(free) if free else 0
        spans, at = [], 0.0
        for t in cb:
            name, _, dur = t.partition(":")
            dur = float(dur) if dur else each
            spans.append((at, at + dur, None if name == "R" else chord(name), name))
            at += dur
        if abs(at - length) > 1e-6:
            problems.append(f"{i + 1} 小節め：和音が {at} 拍（小節は {length} 拍）")
        pos, tied = 0.0, False
        for midi, beats, tie in mb:
            if midi is not None:
                midis.append(midi)
                if song["style"] not in ("Drone", "None") and not tied:
                    for a, e, tones, name in spans:
                        if abs(pos - a) < 1e-6 and tones is not None and midi % 12 not in tones:
                            problems.append(f"{i + 1} 小節め：{midi} が 和音 {name} に あわない")
            tied = tie
            pos += beats
    if midis and (min(midis) < 60 or max(midis) > 84):
        problems.append(f"音の はんい {min(midis)}〜{max(midis)} が C4〜C6 の そと")
    return problems


def check_mimic(song):
    problems = []
    for phrase in song["notes"].split("|"):
        for tok in phrase.split():
            midi, beats, _ = note(tok)
            if beats <= 0:
                problems.append(f"{tok}: 長さが 0")
            if midi is not None and (midi < 60 or midi > 72 or midi % 12 not in WHITE):
                problems.append(f"{tok}: 1 オクターブ（C4〜C5）の 白鍵では ない")
    return problems


def main():
    bad = 0
    perform = perform_songs()
    mimic = mimic_songs()
    for name, items, key in (("えんそう", perform, "id"), ("えんそう", perform, "title"), ("まねっこ", mimic, "id"), ("まねっこ", mimic, "title")):
        values = [s[key] for s in items]
        if len(values) != len(set(values)):
            print(f"::error::{name}の {key} が かぶっている")
            bad += 1
    if len(perform) != 30:
        print(f"::error::えんそうの曲が {len(perform)} きょく（30 きょくの はず）")
        bad += 1
    for s in perform:
        for p in check_perform(s):
            print(f"::error::えんそう {s['title']}: {p}")
            bad += 1
    for s in mimic:
        for p in check_mimic(s):
            print(f"::error::まねっこ {s['title']}: {p}")
            bad += 1
    if bad:
        sys.exit(1)
    print(f"OK: えんそう {len(perform)} きょく・まねっこ {len(mimic)} きょく")


if __name__ == "__main__":
    main()
