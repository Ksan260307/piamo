"""
tools/ のスクリプトの単体テスト（音の合成・スタンプ・アイコン・フォント・ビルド検査）。
  python -m unittest discover -s tools/tests -v
"""
import importlib.util
import io
import json
import os
import re
import shutil
import sys
import tempfile
import unittest
import wave
from contextlib import redirect_stdout

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS = os.path.abspath(os.path.join(HERE, ".."))
ROOT = os.path.abspath(os.path.join(TOOLS, ".."))
sys.path.insert(0, TOOLS)


def load(name):
    spec = importlib.util.spec_from_file_location(name, os.path.join(TOOLS, name + ".py"))
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


audio = load("make_audio")
stamps = load("make_stamps")
check = load("check_build")
fonts = load("make_fonts")
songs = load("check_songs")


def peak_freq(x, sr):
    """いちばん強い周波数（倍音ではなく基音を見つけるため、低い方から探す）。"""
    n = len(x)
    win = np.hanning(n)
    spec = np.abs(np.fft.rfft(x * win, n * 4))
    freqs = np.fft.rfftfreq(n * 4, 1 / sr)
    return freqs[int(np.argmax(spec))]


class AudioTests(unittest.TestCase):
    def test_midi_a4_is_440(self):
        self.assertAlmostEqual(audio.midi(69), 440.0)
        self.assertAlmostEqual(audio.midi(60), 261.6256, places=3)

    def test_every_semitone_has_its_own_sample(self):
        # C2〜C7 の すべての半音に、その高さの音がある（アプリは 音の高さを変えて鳴らさない）
        self.assertEqual(audio.ROOT_NOTES, list(range(36, 97)))
        self.assertEqual(audio.MAX_SHIFT, 0)

    def test_piano_pitch_is_right(self):
        for note in (48, 60, 72):
            x = audio.piano(note, 32000)
            seg = x[int(0.05 * 32000): int(0.45 * 32000)]
            f = peak_freq(seg, 32000)
            self.assertAlmostEqual(f / audio.midi(note), 1.0, delta=0.02, msg=f"note {note}: {f}Hz")

    def test_all_610_notes_in_tune(self):
        # 10 の楽器 × 61 の音 ぜんぶの 音の高さを はかる（tools/check_pitch.py と同じ しらべかた）
        pitch = load("check_pitch")
        with redirect_stdout(io.StringIO()):
            rows = pitch.check(verbose=False)
        self.assertEqual(len(rows), 610)
        bad = [f"{name} {note}: {cents:+.2f}" for name, note, cents, dominant in rows
               if abs(cents) > pitch.TOLERANCE_CENTS or not dominant]
        self.assertEqual(bad, [], "音の高さが ずれている")

    def test_pitch_probe_catches_a_wrong_note(self):
        # しらべかたが ほんとうに ずれを見つけられるか（半音 ずれた ファイルを わざと はかる）
        pitch = load("check_pitch")
        folder = os.path.join(ROOT, "unity", "Assets", "Audio", "Instruments", "piano")
        f, cents, _ = pitch.measure(os.path.join(folder, "piano_061.wav"), 60)
        self.assertGreater(cents, 90)
        f, cents, _ = pitch.measure(os.path.join(folder, "piano_060.wav"), 60)
        self.assertLess(abs(cents), 3)

    def test_percussive_instruments_decay(self):
        for name, fn, sr, loop in audio.INSTRUMENTS:
            if loop or name in ("cat", "dog"):   # ループの音と、どうぶつの声はのぞく
                continue
            x = fn(60, sr)
            head = np.sqrt(np.mean(x[: int(0.2 * sr)] ** 2))
            tail = np.sqrt(np.mean(x[-int(0.1 * sr):] ** 2))
            self.assertGreater(head, tail * 4, name)
            self.assertLess(abs(x[-1]), 0.02, f"{name} の おわりが ぷつっと きれる")

    def test_loops_are_seamless(self):
        for name, fn, sr, loop in audio.INSTRUMENTS:
            if not loop:
                continue
            for note in (36, 60, 84):
                x = fn(note, sr)
                x = x / np.max(np.abs(x))
                # ループのつなぎ目の段差が、ふだんの となりどうしの差と同じくらい
                step = np.max(np.abs(np.diff(x)))
                self.assertLess(abs(x[0] - x[-1]), step * 1.6, f"{name} {note}")

    def test_loop_length_has_whole_cycles(self):
        n, f = audio.loop_length(261.63, 24000, 0.5)
        cycles = f * n / 24000
        self.assertAlmostEqual(cycles, round(cycles), places=6)
        self.assertAlmostEqual(f / 261.63, 1.0, delta=0.002)

    def test_generated_instrument_files(self):
        folder = os.path.join(ROOT, "unity", "Assets", "Audio", "Instruments")
        for name, fn, sr, loop in audio.INSTRUMENTS:
            for note in audio.ROOT_NOTES:
                path = os.path.join(folder, name, f"{name}_{note:03d}.wav")
                self.assertTrue(os.path.exists(path), path)
            with wave.open(os.path.join(folder, name, f"{name}_060.wav")) as w:
                self.assertEqual(w.getframerate(), sr)
                self.assertEqual(w.getnchannels(), 1)

    def test_sfx_files(self):
        for n in ("tap", "back", "open", "stamp", "stamp_big", "success", "clear", "oops", "hint", "earn", "page", "listen", "count", "count_go"):
            self.assertTrue(os.path.exists(os.path.join(ROOT, "unity", "Assets", "Audio", "Sfx", n + ".wav")), n)


class StampTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.plans = stamps.plan()

    def test_there_are_365(self):
        self.assertEqual(len(self.plans), 365)
        self.assertEqual([p["no"] for p in self.plans], list(range(1, 366)))

    def test_every_fifth_is_big(self):
        for p in self.plans:
            self.assertEqual(p["big"], p["no"] % 5 == 0, p["no"])
        self.assertEqual(sum(p["big"] for p in self.plans), 73)

    def test_designs_are_all_different(self):
        keys = {(p["species"], p["expr"], p["acc"]) for p in self.plans}
        self.assertEqual(len(keys), 365)
        names = [p["name"] for p in self.plans]
        self.assertEqual(len(set(names)), 365, "スタンプの なまえが かぶっている")

    def test_many_animals(self):
        self.assertGreaterEqual(len({p["species"] for p in self.plans}), 40)
        # となりあう スタンプは ちがう どうぶつ
        for a, b in zip(self.plans, self.plans[1:]):
            self.assertNotEqual(a["species"], b["species"])

    def test_big_stamps_show_day_number(self):
        for p in self.plans:
            if p["big"]:
                self.assertEqual(p["label"], f"{p['no']}にちめ")

    def test_render_is_square_and_not_empty(self):
        for p in (self.plans[0], self.plans[4]):
            img = stamps.render_stamp(p)
            self.assertEqual(img.width, img.height)
            self.assertEqual(img.width, stamps.BIG_SIZE if p["big"] else stamps.SIZE)
            a = np.asarray(img)[..., 3]
            self.assertGreater((a > 200).mean(), 0.45)
            # 四すみは すきとおっている（スタンプの形）
            self.assertEqual(a[0, 0], 0)

    def test_generated_files(self):
        folder = os.path.join(ROOT, "unity", "Assets", "StreamingAssets", "Stamps")
        for n in range(1, 366):
            self.assertTrue(os.path.exists(os.path.join(folder, f"stamp_{n:03d}.png")), n)
        with open(os.path.join(ROOT, "unity", "Assets", "Resources", "stamps.json"), encoding="utf-8") as f:
            data = json.load(f)["stamps"]
        self.assertEqual(len(data), 365)
        self.assertTrue(all(d["name"] for d in data))

    def test_characters_for_songs_and_menu(self):
        folder = os.path.join(ROOT, "unity", "Assets", "UI", "Characters")
        for name in ("owl", "frog", "sheep", "rabbit", "dog", "lion", "duck", "penguin", "cat", "bear", "panda", "chick", "monkey", "elephant", "bird"):
            self.assertTrue(os.path.exists(os.path.join(folder, name + ".png")), name)


class FontTests(unittest.TestCase):
    def test_font_covers_app_text(self):
        from fontTools.ttLib import TTFont
        text = fonts.collect_text()
        for name in ("PiamoRounded-ExtraBold.ttf", "PiamoRounded-Black.ttf"):
            path = os.path.join(ROOT, "unity", "Assets", "UI", "Fonts", name)
            self.assertTrue(os.path.exists(path), path)
            cmap = TTFont(path).getBestCmap()
            missing = [c for c in text if ord(c) not in cmap and c.strip() and ord(c) < 0xFFF0 and not (0xFF00 <= ord(c) <= 0xFFEF)
                       and not (0x3000 <= ord(c) <= 0x30FF)]
            # 漢字やかなで フォントにない字は、画面で とうふ（□）になる
            missing = [c for c in missing if ord(c) >= 0x3000 or c in "♪♯"]
            self.assertEqual(missing, [], f"{name} に ない字: {''.join(missing)}")


class IconTests(unittest.TestCase):
    def test_icons_exist(self):
        folder = os.path.join(ROOT, "unity", "Assets", "UI", "Icons")
        icons = load("make_icons")
        for name in icons.ICONS:
            p = os.path.join(folder, name + ".png")
            self.assertTrue(os.path.exists(p), name)
            with Image.open(p) as im:
                self.assertEqual(im.size, (128, 128))
        for n in ("icon.png", "favicon.png", "logo.png"):
            self.assertTrue(os.path.exists(os.path.join(ROOT, "unity", "Assets", "WebGLTemplates", "Piamo", n)), n)


class SongDataTests(unittest.TestCase):
    """まねっこ・えんそうの 曲の データ（check_songs.py）。"""

    @classmethod
    def setUpClass(cls):
        cls.perform = songs.perform_songs()
        cls.mimic = songs.mimic_songs()

    def test_thirty_perform_songs_all_clean(self):
        self.assertEqual(len(self.perform), 30)
        bad = {s["title"]: songs.check_perform(s) for s in self.perform if songs.check_perform(s)}
        self.assertEqual(bad, {})

    def test_mimic_songs_are_one_octave_white_keys(self):
        self.assertGreaterEqual(len(self.mimic), 20)
        bad = {s["title"]: songs.check_mimic(s) for s in self.mimic if songs.check_mimic(s)}
        self.assertEqual(bad, {})

    def test_ids_and_titles_are_unique(self):
        for items in (self.perform, self.mimic):
            for key in ("id", "title"):
                values = [s[key] for s in items]
                self.assertEqual(len(values), len(set(values)), key)

    def test_birthday_is_in_a_flat_key(self):
        flats = {s["id"] for s in self.perform if s["flats"]}
        self.assertIn("birthday", flats)

    def test_catches_a_short_bar(self):
        song = {"title": "x", "meter": "4/4", "style": "Pop", "melody": "C4 D4 E4 F4 | G4 A4 | C5:4", "chords": "C | C | C"}
        problems = songs.check_perform(song)
        self.assertTrue(any("2 小節め" in p for p in problems), problems)

    def test_catches_a_note_against_the_chord(self):
        song = {"title": "x", "meter": "4/4", "style": "Pop", "melody": "D4:4 | C4:4", "chords": "C | C"}
        problems = songs.check_perform(song)
        self.assertTrue(any("あわない" in p for p in problems), problems)

    def test_catches_a_black_key_in_mimic(self):
        self.assertTrue(songs.check_mimic({"notes": "C4 F#4 G4"}))
        self.assertFalse(songs.check_mimic({"notes": "C4 E4 G4 | C5"}))

    def test_main_passes(self):
        with redirect_stdout(io.StringIO()) as out:
            try:
                songs.main()
            except SystemExit as e:
                self.assertIn(e.code, (0, None), out.getvalue())


class UiReferenceTests(unittest.TestCase):
    """C# の 画面の コードが つかう 名前（UXML の 要素・アイコン・こうかおん）が ほんとうに あるか。"""

    UI = os.path.join(ROOT, "unity", "Assets", "Scripts", "UI")

    @classmethod
    def setUpClass(cls):
        cls.code = ""
        for name in sorted(os.listdir(cls.UI)):
            if name.endswith(".cs"):
                with open(os.path.join(cls.UI, name), encoding="utf-8") as f:
                    cls.code += f.read()
        with open(os.path.join(ROOT, "unity", "Assets", "UI", "Main.uxml"), encoding="utf-8") as f:
            cls.uxml_names = set(re.findall(r'name="([^"]+)"', f.read()))
        cls.icons = {os.path.splitext(n)[0] for n in os.listdir(os.path.join(ROOT, "unity", "Assets", "UI", "Icons")) if n.endswith(".png")}
        cls.sfx = {os.path.splitext(n)[0] for n in os.listdir(os.path.join(ROOT, "unity", "Assets", "Audio", "Sfx")) if n.endswith(".wav")}

    def test_queried_elements_exist_in_uxml(self):
        names = set(re.findall(r'_root\.Q(?:<\w+>)?\("([^"]+)"\)', self.code))
        names |= set(re.findall(r'\bSetIcon\("([^"]+)",', self.code))
        self.assertGreater(len(names), 60)
        missing = sorted(n for n in names if n not in self.uxml_names)
        self.assertEqual(missing, [], "UXML に ない 要素")

    def test_icons_used_in_code_exist(self):
        def literals(arg):
            # "inst_" + id のような つなぎの 文字は のぞく
            return {m.group(1) for m in re.finditer(r'(?<!\+ )"([^"]+)"(?!\s*\+)', arg)}

        used = set()
        for arg in re.findall(r'\bIcon\(([^;{}]*?)\)', self.code):
            used |= literals(arg)
        for arg in re.findall(r'\bSetIcon\("[^"]+", ([^;]*?)\);', self.code):
            used |= literals(arg)
        used |= set(re.findall(r'\bSection\(c, "[^"]+", "([^"]+)"', self.code))
        self.assertIn("rec", used)
        missing = sorted(n for n in used if n not in self.icons)
        self.assertEqual(missing, [], "アイコンの 絵が ない")

    def test_sounds_used_in_code_exist(self):
        used = set()
        for arg in re.findall(r'PlaySfx\(([^,)]*)', self.code):
            used |= set(re.findall(r'"([^"]+)"', arg))
        self.assertIn("count_go", used)
        missing = sorted(n for n in used if n not in self.sfx)
        self.assertEqual(missing, [], "こうかおんの ファイルが ない")


class CheckBuildTests(unittest.TestCase):
    def make_site(self, d, jslib=True, stamps_count=365):
        os.makedirs(os.path.join(d, "Build"))
        os.makedirs(os.path.join(d, "StreamingAssets", "Stamps"))
        for n in ("WebGL.loader.js", "WebGL.data.gz", "WebGL.wasm.gz"):
            open(os.path.join(d, "Build", n), "wb").write(b"x")
        with open(os.path.join(d, "Build", "WebGL.framework.js.gz"), "wb") as f:
            import gzip
            f.write(gzip.compress(b"function PiamoLocalDate(){}" if jslib else b"nothing"))
        with open(os.path.join(d, "index.html"), "w", encoding="utf-8") as f:
            f.write('<script>const loaderUrl = buildUrl + "/WebGL.loader.js";</script>')
        for n in ("manifest.webmanifest", "icon.png", "favicon.png", "logo.png", "pitchcheck.js"):
            open(os.path.join(d, n), "wb").write(b"x")
        for i in range(1, stamps_count + 1):
            open(os.path.join(d, "StreamingAssets", "Stamps", f"stamp_{i:03d}.png"), "wb").write(b"x")

    def run_check(self, d):
        argv = sys.argv
        sys.argv = ["check_build.py", d]
        try:
            with redirect_stdout(io.StringIO()):
                check.main()
            return True
        except SystemExit:
            return False
        finally:
            sys.argv = argv

    def test_good_site_passes(self):
        d = tempfile.mkdtemp()
        try:
            self.make_site(d)
            self.assertTrue(self.run_check(d))
        finally:
            shutil.rmtree(d)

    def test_missing_jslib_fails(self):
        d = tempfile.mkdtemp()
        try:
            self.make_site(d, jslib=False)
            self.assertFalse(self.run_check(d))
        finally:
            shutil.rmtree(d)

    def test_missing_stamps_fails(self):
        d = tempfile.mkdtemp()
        try:
            self.make_site(d, stamps_count=300)
            self.assertFalse(self.run_check(d))
        finally:
            shutil.rmtree(d)


if __name__ == "__main__":
    unittest.main()
