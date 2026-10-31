"""
UI のアイコン・ロゴ・アプリのアイコンを作る（PIL）。make_stamps.py の どうぶつも使う。
  python tools/make_icons.py

出力:
 - unity/Assets/UI/Icons/*.png        （ボタンのアイコン・楽器・おんぷ・ヒントのやじるし）
 - unity/Assets/UI/Images/logo.png    （タイトルの「ぴあも」）
 - unity/Assets/WebGLTemplates/Piamo/icon.png・favicon.png・logo.png（読みこみ画面・ホーム画面に追加）
"""
import math
import os
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import make_stamps as ms  # noqa: E402
from art import (Canvas, Group, bezier, ellipse_pts, hexc, quad, round_poly, rounded_rect_pts,  # noqa: E402
                 rounded_star_pts, shade, star_pts)

ROOT = os.path.abspath(os.path.join(HERE, ".."))
OUT_ICONS = os.path.join(ROOT, "unity", "Assets", "UI", "Icons")
OUT_IMAGES = os.path.join(ROOT, "unity", "Assets", "UI", "Images")
OUT_WEB = os.path.join(ROOT, "unity", "Assets", "WebGLTemplates", "Piamo")

OUT = ms.OUT
W = 2.4   # アイコンの ふちの太さ
PINK = hexc("#ff8fb1")
BLUE = hexc("#6fb8f0")
YELLOW = hexc("#ffd45e")
GREEN = hexc("#7fd39a")
PURPLE = hexc("#b597ff")
CREAM = hexc("#fffaf0")
WHITE = hexc("#ffffff")


def icon(name, draw, size=128):
    cv = Canvas(size)
    draw(cv)
    path = os.path.join(OUT_ICONS, name + ".png")
    cv.result().save(path, optimize=True)
    return path


def g_draw(cv, shapes, outline=OUT, width=W):
    g = Group()
    for pts, fill in shapes:
        g.add(pts, fill)
    g.draw(cv, outline, width)


# ---------------------------------------------------------------------------
def d_back(cv):
    pts = round_poly([(14, 50), (46, 20), (46, 38), (84, 38), (84, 62), (46, 62), (46, 80)], 0.18, 6)
    g_draw(cv, [(pts, PINK)])


def d_left(cv):
    g_draw(cv, [(round_poly([(28, 50), (68, 20), (68, 80)], 0.22, 8), PINK)])


def d_right(cv):
    g_draw(cv, [(round_poly([(72, 50), (32, 20), (32, 80)], 0.22, 8), PINK)])


def d_minus(cv):
    g_draw(cv, [(rounded_rect_pts(18, 40, 82, 60, 9), PINK)])


def d_plus(cv):
    g = Group()
    g.add(rounded_rect_pts(18, 40, 82, 60, 9), PINK)
    g.add(rounded_rect_pts(40, 18, 60, 82, 9), PINK)
    g.draw(cv, OUT, W)


def d_gear(cv):
    pts = []
    teeth = 8
    for i in range(teeth * 4):
        a = i / (teeth * 4) * math.tau
        r = 42 if (i % 4) in (0, 1) else 32
        pts.append((50 + math.cos(a) * r, 50 + math.sin(a) * r))
    g = Group()
    g.add(round_poly(pts, 0.2, 4), WHITE)
    g.draw(cv, OUT, W)
    h = Group()
    h.ellipse(50, 50, 13, 13, BLUE)
    h.draw(cv, OUT, W * 0.7)


def piano_shape(cv, x0, y0, x1, y1, body):
    g = Group()
    g.add(rounded_rect_pts(x0, y0, x1, y1, 9), body)
    g.draw(cv, OUT, W)
    kx0, kx1 = x0 + 6, x1 - 6
    ky0, ky1 = y0 + (y1 - y0) * 0.36, y1 - 6
    n = 5
    w = (kx1 - kx0) / n
    keys = Group()
    for i in range(n):
        keys.add(rounded_rect_pts(kx0 + i * w + 0.8, ky0, kx0 + (i + 1) * w - 0.8, ky1, 2.5), WHITE)
    keys.draw(cv, OUT, 1.2)
    blacks = Group()
    for i in (1, 2, 4):
        cx = kx0 + i * w
        blacks.add(rounded_rect_pts(cx - w * 0.28, ky0, cx + w * 0.28, ky0 + (ky1 - ky0) * 0.58, 1.6), hexc("#3d3a45"))
    blacks.draw(cv, OUT, 0.8)


def d_piano(cv):
    piano_shape(cv, 10, 22, 90, 80, PINK)


def d_stamp(cv):
    g = Group()
    g.ellipse(50, 22, 13, 12, hexc("#ff7a8f"))
    g.add(rounded_rect_pts(42, 28, 58, 50, 4), hexc("#d79a64"))
    g.add(rounded_rect_pts(22, 46, 78, 62, 6), hexc("#b77a48"))
    g.draw(cv, OUT, W)
    s = Group()
    s.add(ellipse_pts(50, 80, 32, 11), hexc("#ff8fb1"))
    s.draw(cv, OUT, W * 0.8)
    st = Group()
    st.add(rounded_star_pts(50, 80, 8, 3.6, 5), WHITE)
    st.draw(cv)


def d_star(cv):
    g_draw(cv, [(rounded_star_pts(50, 53, 44, 20, 5), YELLOW)], hexc("#e08a1e"), W)


def note_shape(g, x, y, s, fill):
    g.ellipse(x, y + 26 * s, 13 * s, 10 * s, fill, -0.45)
    ms.thick(g, [(x + 10.5 * s, y + 24 * s), (x + 10.5 * s, y - 24 * s)], 6 * s, fill)
    g.add(bezier((x + 10.5 * s, y - 24 * s), (x + 30 * s, y - 14 * s), (x + 32 * s, y + 2 * s), (x + 22 * s, y + 10 * s), 12)
          + [(x + 21 * s, y + 4 * s), (x + 10.5 * s, y - 10 * s)], fill)


def d_note1(cv):
    g = Group()
    note_shape(g, 40, 45, 1.25, WHITE)
    g.draw(cv, hexc("#c8c0c0"), 1.8)


def d_note2(cv):
    g = Group()
    g.ellipse(28, 76, 13, 10, WHITE, -0.45)
    g.ellipse(72, 68, 13, 10, WHITE, -0.45)
    ms.thick(g, [(38.5, 74), (38.5, 24)], 7, WHITE)
    ms.thick(g, [(82.5, 66), (82.5, 16)], 7, WHITE)
    g.add([(35, 20), (86, 10), (86, 26), (35, 36)], WHITE)
    g.draw(cv, hexc("#c8c0c0"), 1.8)


def d_hint(cv):
    pts = round_poly([(50, 92), (14, 52), (34, 52), (34, 10), (66, 10), (66, 52), (86, 52)], 0.14, 6)
    g_draw(cv, [(pts, YELLOW)], hexc("#e08a1e"), W * 1.1)
    sp = Group()
    sp.add(round_poly(star_pts(50, 30, 9, 3, 4, 0), 0.2, 4), WHITE)
    sp.draw(cv)


def speaker(cv, waves, fill=WHITE, x=0):
    g = Group()
    g.add(round_poly([(14 + x, 38), (30 + x, 38), (52 + x, 18), (52 + x, 82), (30 + x, 62), (14 + x, 62)], 0.12, 5), fill)
    g.draw(cv, OUT, W)
    for k in range(waves):
        r = 14 + k * 12
        pts = [(52 + x + math.cos(a) * r, 50 + math.sin(a) * r) for a in [(-0.75 + i * 1.5 / 16) for i in range(17)]]
        cv.stroke(pts, OUT, 9)
        cv.stroke(pts, fill if fill != WHITE else YELLOW, 4.5)


def d_ear(cv):
    speaker(cv, 3)


def d_volume_low(cv):
    speaker(cv, 1, PINK)


def d_volume_high(cv):
    speaker(cv, 3, PINK)


def d_songs(cv):
    g = Group()
    g.add(round_poly([(50, 26), (14, 18), (14, 80), (50, 88)], 0.12, 4), WHITE)
    g.add(round_poly([(50, 26), (86, 18), (86, 80), (50, 88)], 0.12, 4), hexc("#fff4dc"))
    g.draw(cv, OUT, W)
    n = Group()
    note_shape(n, 26, 50, 0.55, PINK)
    note_shape(n, 62, 48, 0.55, BLUE)
    n.draw(cv, OUT, 1.2)


def d_sec_sound(cv):
    speaker(cv, 2, PINK)


def d_sec_keys(cv):
    piano_shape(cv, 10, 22, 90, 80, BLUE)


def d_sec_mimic(cv):
    g = Group()
    g.add(round_poly([(12, 18), (88, 18), (88, 70), (46, 70), (28, 88), (32, 70), (12, 70)], 0.15, 5), WHITE)
    g.draw(cv, OUT, W)
    n = Group()
    note_shape(n, 38, 40, 0.6, PINK)
    note_shape(n, 62, 40, 0.6, YELLOW)
    n.draw(cv, OUT, 1.2)


def d_sec_screen(cv):
    g = Group()
    g.add(round_poly([(10, 50), (30, 16), (70, 12), (92, 40), (82, 70), (60, 66), (52, 88), (24, 84)], 0.3, 8), hexc("#ffe6c4"))
    g.draw(cv, OUT, W)
    dots = Group()
    for (x, y), c in zip(((32, 38), (52, 28), (72, 36), (30, 64)), (PINK, YELLOW, BLUE, GREEN)):
        dots.ellipse(x, y, 7, 7, c)
    dots.draw(cv, OUT, 1.2)


def d_sec_parents(cv):
    g = Group()
    g.add(round_poly([(50, 10), (90, 46), (80, 46), (80, 88), (20, 88), (20, 46), (10, 46)], 0.1, 4), hexc("#ffe6c4"))
    g.draw(cv, OUT, W)
    h = Group()
    h.add(ms.heart_pts(50, 62, 15), PINK)
    h.draw(cv, OUT, 1.4)


# えんそう ---------------------------------------------------------------------
def d_play(cv):
    g_draw(cv, [(round_poly([(30, 16), (84, 50), (30, 84)], 0.2, 8), WHITE)])


def d_pause(cv):
    g_draw(cv, [(rounded_rect_pts(22, 18, 42, 82, 8), WHITE), (rounded_rect_pts(58, 18, 78, 82, 8), WHITE)])


def d_again(cv):
    # ぐるっと まわる やじるし
    a0, a1, r_out, r_in = math.radians(-40), math.radians(230), 36, 22
    outer = [(50 + math.cos(a0 + (a1 - a0) * i / 40) * r_out, 52 + math.sin(a0 + (a1 - a0) * i / 40) * r_out) for i in range(41)]
    inner = [(50 + math.cos(a1 - (a1 - a0) * i / 40) * r_in, 52 + math.sin(a1 - (a1 - a0) * i / 40) * r_in) for i in range(41)]
    ex, ey = 50 + math.cos(a0) * 29, 52 + math.sin(a0) * 29
    tx, ty = math.cos(a0 + math.pi / 2), math.sin(a0 + math.pi / 2)   # すすむ向き
    nx, ny = math.cos(a0), math.sin(a0)
    head = [(ex + nx * 19, ey + ny * 19), (ex - tx * 22, ey - ty * 22), (ex - nx * 19, ey - ny * 19)]
    g = Group()
    g.add(outer + inner, PINK)
    g.add(round_poly(head, 0.15, 4), PINK)
    g.draw(cv, OUT, W)


def d_speed(cv):
    # メトロノーム
    g = Group()
    g.add(round_poly([(36, 12), (64, 12), (84, 88), (16, 88)], 0.14, 5), WHITE)
    g.draw(cv, OUT, W)
    arm = Group()
    ms.thick(arm, [(50, 80), (68, 26)], 7, PINK)
    arm.add(rounded_rect_pts(55, 40, 71, 52, 4), PINK)
    arm.draw(cv, OUT, 1.6)
    base = Group()
    base.add(rounded_rect_pts(22, 76, 78, 88, 5), hexc("#ffd45e"))
    base.draw(cv, OUT, W * 0.8)


def d_sec_perform(cv):
    piano_shape(cv, 10, 52, 90, 92, PURPLE)
    bars = Group()
    for x, y0, y1, c in ((22, 10, 30, PINK), (40, 22, 44, YELLOW), (58, 6, 26, GREEN), (76, 18, 40, BLUE)):
        bars.add(rounded_rect_pts(x - 7, y0, x + 7, y1, 5), c)
    bars.draw(cv, OUT, 1.6)


# 楽器 -------------------------------------------------------------------------
def d_inst_piano(cv):
    g = Group()
    g.add(rounded_rect_pts(16, 12, 84, 70, 8), hexc("#6b4a3e"))
    g.add(rounded_rect_pts(22, 70, 30, 90, 3), hexc("#6b4a3e"))
    g.add(rounded_rect_pts(70, 70, 78, 90, 3), hexc("#6b4a3e"))
    g.draw(cv, OUT, W)
    piano_shape(cv, 10, 44, 90, 76, hexc("#8a6250"))
    deco = Group()
    note_shape(deco, 44, 22, 0.32, YELLOW)
    deco.draw(cv, OUT, 0.8)


def d_inst_toy(cv):
    g = Group()
    g.add(rounded_rect_pts(14, 22, 86, 70, 14), PINK)
    g.add(rounded_rect_pts(24, 68, 32, 90, 4), hexc("#ff6f95"))
    g.add(rounded_rect_pts(68, 68, 76, 90, 4), hexc("#ff6f95"))
    g.draw(cv, OUT, W)
    piano_shape(cv, 20, 40, 80, 66, hexc("#ffc2d6"))
    h = Group()
    h.add(ms.heart_pts(50, 30, 6), WHITE)
    h.draw(cv)


def d_inst_musicbox(cv):
    g = Group()
    g.add(round_poly([(14, 34), (86, 34), (92, 14), (20, 10)], 0.15, 4), hexc("#e8b07a"))
    g.add(rounded_rect_pts(14, 34, 86, 86, 6), hexc("#d4925a"))
    g.add(rounded_rect_pts(86, 52, 96, 58, 2), hexc("#b8b8c8"))
    g.ellipse(96, 50, 4, 6, hexc("#b8b8c8"))
    g.draw(cv, OUT, W)
    deco = Group()
    deco.add(ms.heart_pts(50, 60, 10), PINK)
    deco.draw(cv, OUT, 1.2)


def d_inst_glock(cv):
    cols = [hexc("#ff7a8f"), hexc("#ffb05a"), hexc("#ffe066"), hexc("#7fd39a"), hexc("#6fb8f0"), hexc("#b597ff")]
    g = Group()
    for i, c in enumerate(cols):
        x = 12 + i * 13
        h = 62 - i * 6
        g.add(rounded_rect_pts(x, 50 - h / 2, x + 10, 50 + h / 2, 3), c)
    g.draw(cv, OUT, W * 0.8)
    m = Group()
    ms.thick(m, [(60, 92), (88, 64)], 4, hexc("#d4925a"))
    m.ellipse(88, 62, 7, 7, hexc("#ff7a8f"))
    m.draw(cv, OUT, W * 0.8)


def d_inst_marimba(cv):
    cols = [hexc("#c8844e"), hexc("#d4925a"), hexc("#b97446"), hexc("#d89c64"), hexc("#c07a48")]
    g = Group()
    for i, c in enumerate(cols):
        x = 12 + i * 15
        h = 64 - i * 7
        g.add(rounded_rect_pts(x, 46 - h / 2, x + 12, 46 + h / 2, 3), c)
    g.draw(cv, OUT, W * 0.8)
    m = Group()
    ms.thick(m, [(30, 94), (54, 72)], 4, hexc("#e8c08a"))
    m.ellipse(56, 70, 7, 7, hexc("#6fb8f0"))
    m.draw(cv, OUT, W * 0.8)


def d_inst_organ(cv):
    g = Group()
    for i, h in enumerate((40, 54, 66, 54, 40)):
        x = 16 + i * 14
        g.add(rounded_rect_pts(x, 70 - h, x + 11, 72, 4), hexc("#ffd45e") if i % 2 == 0 else hexc("#e8eaf4"))
    g.add(rounded_rect_pts(10, 66, 90, 90, 6), hexc("#a8765a"))
    g.draw(cv, OUT, W * 0.9)


def d_inst_flute(cv):
    g = Group()
    pts = [(18, 88), (82, 14)]
    ms.thick(g, pts, 13, hexc("#fff0d0"))
    g.add(round_poly([(76, 8), (92, 22), (86, 28), (70, 14)], 0.3, 4), hexc("#ffd8a8"))
    g.draw(cv, OUT, W)
    h = Group()
    for t in (0.3, 0.42, 0.54, 0.66):
        x = 18 + (82 - 18) * t
        y = 88 + (14 - 88) * t
        h.ellipse(x, y, 2.4, 2.4, OUT)
    h.draw(cv)


def d_inst_harp(cv):
    g = Group()
    ms.thick(g, [(22, 88), (22, 16)], 7, hexc("#e8a83c"))
    g.add(bezier((18, 18), (40, 8), (70, 30), (88, 30), 14) + [(90, 38)] + bezier((90, 38), (70, 38), (42, 18), (26, 26), 14), hexc("#ffd45e"))
    ms.thick(g, [(86, 34), (30, 88)], 6, hexc("#e8a83c"))
    g.draw(cv, OUT, W)
    for i in range(5):
        x = 30 + i * 10
        top = 22 + i * 3.6
        bottom = 82 - i * 9
        cv.stroke([(x, top), (x, bottom)], hexc("#8a6a5a"), 1.4)


def d_inst_animal(species, expr):
    def draw(cv):
        img = ms.render_character(species, cv.w * 4, expr)
        cv.img.alpha_composite(img.resize(cv.img.size, Image.LANCZOS))
    return draw


ICONS = {
    "back": d_back, "left": d_left, "right": d_right, "minus": d_minus, "plus": d_plus, "gear": d_gear,
    "piano": d_piano, "stamp": d_stamp, "star": d_star, "note1": d_note1, "note2": d_note2, "hint": d_hint,
    "ear": d_ear, "songs": d_songs, "volume_low": d_volume_low, "volume_high": d_volume_high,
    "sec_sound": d_sec_sound, "sec_keys": d_sec_keys, "sec_mimic": d_sec_mimic, "sec_screen": d_sec_screen, "sec_parents": d_sec_parents,
    "inst_piano": d_inst_piano, "inst_toy": d_inst_toy, "inst_musicbox": d_inst_musicbox, "inst_glock": d_inst_glock,
    "inst_marimba": d_inst_marimba, "inst_organ": d_inst_organ, "inst_flute": d_inst_flute, "inst_harp": d_inst_harp,
    "inst_cat": d_inst_animal("cat", "happy"), "inst_dog": d_inst_animal("dog", "happy"),
    "play": d_play, "pause": d_pause, "again": d_again, "speed": d_speed, "sec_perform": d_sec_perform,
}


# ロゴ ----------------------------------------------------------------------
def make_logo(path, w=1024, h=400):
    font = ms.find_font()
    cv = Canvas(w, h, units=w / 10)   # 1 単位 = 10 画素
    k = 1
    if font is None:
        raise SystemExit("フォントが見つかりません。tools/make_fonts.py を先に実行してください。")
    # 下の けんばん
    keys = Group()
    n = 12
    x0, x1 = 6, 96
    kw = (x1 - x0) / n
    for i in range(n):
        keys.add(rounded_rect_pts(x0 + i * kw + 0.3, 28, x0 + (i + 1) * kw - 0.3, 38.5, 1.4), WHITE)
    keys.draw(cv, OUT, 0.8)
    blacks = Group()
    for i in range(1, n):
        if i % 7 in (3, 0):
            continue
        cx = x0 + i * kw
        blacks.add(rounded_rect_pts(cx - kw * 0.3, 28, cx + kw * 0.3, 34, 0.8), hexc("#3d3a45"))
    blacks.draw(cv, OUT, 0.4)
    # もじ
    chars = ["ぴ", "あ", "も"]
    cols = [hexc("#ff7aa2"), hexc("#ffb43a"), hexc("#4fb0f0")]
    for i, (c, col) in enumerate(zip(chars, cols)):
        x = 22 + i * 28
        y = 15 + (2 if i == 1 else 0)
        cv.text(x, y, c, font, 26, OUT, stroke=2.2, stroke_fill=OUT)
        cv.text(x, y, c, font, 26, WHITE, stroke=1.4, stroke_fill=WHITE)
        cv.text(x, y, c, font, 26, col)
    # おんぷ
    deco = Group()
    note_shape(deco, 92, 8, 0.12, hexc("#ff7aa2"))
    note_shape(deco, 4, 10, 0.1, hexc("#7fd39a"))
    deco.draw(cv, OUT, 0.5)
    img = cv.result()
    img.save(path, optimize=True)
    return img


def make_app_icon(size):
    cv = Canvas(size)
    g = Group()
    g.add(rounded_rect_pts(2, 2, 98, 98, 22), hexc("#ffd6e2"))
    g.draw(cv)
    piano_shape(cv, 10, 58, 90, 92, PINK)
    cat = ms.render_character("cat", size * 4, "happy")
    sub = cat.resize((int(size * 4 * 0.62), int(size * 4 * 0.62)), Image.LANCZOS)
    cv.img.alpha_composite(sub, (int(size * 4 * 0.19), int(size * 4 * 0.02)))
    return cv.result()


# はいけい（PiamoSettings の Themes と同じ いろ：そら・ゆうやけ・よる・もり）
THEMES = [
    ((0.62, 0.85, 0.98), (1.0, 0.97, 0.88)),
    ((1.0, 0.72, 0.55), (1.0, 0.9, 0.78)),
    ((0.16, 0.18, 0.42), (0.36, 0.3, 0.58)),
    ((0.62, 0.86, 0.62), (0.95, 0.97, 0.82)),
]


def make_sky(top, bottom, night, w=384, h=768):
    """上から下への グラデーションに、くも（よるは ほし）を すこし。"""
    cv = Canvas(w, h, units=100)
    hh = 100 * h / w
    steps = 64
    for i in range(steps):
        t = i / (steps - 1)
        c = tuple(int(255 * (top[k] + (bottom[k] - top[k]) * t)) for k in range(3)) + (255,)
        y0 = hh * i / steps
        cv.poly([(0, y0), (100, y0), (100, y0 + hh / steps + 0.5), (0, y0 + hh / steps + 0.5)], c)
    deco = Group()
    if night:
        import random
        rnd = random.Random(4)
        for _ in range(26):
            x, y = rnd.uniform(3, 97), rnd.uniform(3, hh * 0.6)
            deco.add(round_poly(star_pts(x, y, rnd.uniform(0.8, 1.8), 0.4, 4, 0), 0.2, 3), (255, 248, 210, 200))
        deco.ellipse(80, 20, 9, 9, (255, 246, 200, 230))
    else:
        for x, y, s in ((18, 22, 1.0), (78, 40, 0.8), (40, 70, 0.6), (88, 110, 0.7), (10, 130, 0.5)):
            for dx, dy, r in ((-7, 2, 6), (0, -2, 8), (8, 2, 6), (0, 4, 6)):
                deco.ellipse(x + dx * s, y + dy * s, r * s * 1.3, r * s, (255, 255, 255, 120))
    deco.draw(cv)
    return cv.result()


def main():
    os.makedirs(OUT_ICONS, exist_ok=True)
    os.makedirs(OUT_IMAGES, exist_ok=True)
    os.makedirs(OUT_WEB, exist_ok=True)
    for name, fn in ICONS.items():
        icon(name, fn)
    print("wrote", len(ICONS), "icons")
    for i, (top, bottom) in enumerate(THEMES):
        make_sky(top, bottom, i == 2).save(os.path.join(OUT_ICONS, f"sky_{i}.png"), optimize=True)
    logo = make_logo(os.path.join(OUT_IMAGES, "logo.png"))
    logo.resize((640, 250), Image.LANCZOS).save(os.path.join(OUT_WEB, "logo.png"), optimize=True)
    make_app_icon(256).save(os.path.join(OUT_WEB, "icon.png"), optimize=True)
    make_app_icon(64).save(os.path.join(OUT_WEB, "favicon.png"), optimize=True)
    print("wrote logo and app icons")


if __name__ == "__main__":
    main()
