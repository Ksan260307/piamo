"""
日本語の まるい フォントを用意する。
M PLUS Rounded 1c（SIL Open Font License 1.1）の ExtraBold と Black を、アプリで使う文字だけにして
unity/Assets/UI/Fonts に置く（PiamoRounded-ExtraBold.ttf・PiamoRounded-Black.ttf）。

  python tools/make_fonts.py [フォントのフォルダ]

フォントは tools/.cache/ にあれば それを使い、なければ Google Fonts のリポジトリから取ってくる。
アプリの文字（.cs / .uxml / .uss / スタンプの なまえ）を ふやしたら、もう一度 実行してください。
"""
import glob
import os
import sys
import urllib.request

from fontTools import subset

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, ".."))
OUT = os.path.join(ROOT, "unity", "Assets", "UI", "Fonts")
CACHE = os.path.join(HERE, ".cache")
SOURCE_URL = "https://github.com/google/fonts/raw/main/ofl/mplusrounded1c/"
WEIGHTS = [("MPLUSRounded1c-ExtraBold.ttf", "PiamoRounded-ExtraBold"), ("MPLUSRounded1c-Black.ttf", "PiamoRounded-Black")]


def collect_text():
    chars = set()
    for i in range(0x20, 0x7F):
        chars.add(chr(i))
    for start, end in ((0x3000, 0x303F), (0x3040, 0x309F), (0x30A0, 0x30FF), (0xFF00, 0xFFEF)):
        for c in range(start, end + 1):
            chars.add(chr(c))
    chars.update("♪♫♯♭★☆○●◎◆→←↑↓・…—〜「」『』（）【】ー！？、。")
    for pat in ("**/*.cs", "**/*.uxml", "**/*.uss", "**/*.json"):
        for path in glob.glob(os.path.join(ROOT, "unity", "Assets", pat), recursive=True):
            with open(path, encoding="utf-8", errors="ignore") as f:
                chars.update(f.read())
    return "".join(sorted(c for c in chars if ord(c) >= 0x20))


def find_source(name, folder=None):
    for d in [folder, CACHE]:
        if d and os.path.exists(os.path.join(d, name)):
            return os.path.join(d, name)
    os.makedirs(CACHE, exist_ok=True)
    path = os.path.join(CACHE, name)
    print("downloading", name)
    urllib.request.urlretrieve(SOURCE_URL + name, path)
    return path


def make(src, name, text):
    opts = subset.Options()
    opts.layout_features = ["*"]
    opts.name_IDs = ["*"]
    opts.name_languages = ["*"]
    opts.notdef_outline = True
    opts.glyph_names = False
    f = subset.load_font(src, opts)
    s = subset.Subsetter(opts)
    s.populate(text=text)
    s.subset(f)
    out = os.path.join(OUT, name + ".ttf")
    subset.save_font(f, out, opts)
    print("wrote", out, os.path.getsize(out) // 1024, "KB")


def main(argv):
    folder = argv[0] if argv else None
    os.makedirs(OUT, exist_ok=True)
    text = collect_text()
    print(len(text), "chars")
    for src_name, out_name in WEIGHTS:
        make(find_source(src_name, folder), out_name, text)
    with open(os.path.join(OUT, "LICENSE-MPLUSRounded1c.txt"), "w", encoding="utf-8") as f:
        f.write("PiamoRounded-ExtraBold.ttf / PiamoRounded-Black.ttf are subsets of M PLUS Rounded 1c.\n"
                "Copyright 2016 The Rounded M+ Project Authors.\n\n"
                "This Font Software is licensed under the SIL Open Font License, Version 1.1.\n"
                "https://openfontlicense.org\n")


if __name__ == "__main__":
    main(sys.argv[1:])
