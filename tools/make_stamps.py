"""
どうぶつの スタンプ 365 こ と、UI で使う どうぶつの絵を作る（PIL）。
  python tools/make_stamps.py            # スタンプ 365 こ ＋ どうぶつの絵 ＋ 一覧の絵
  python tools/make_stamps.py --preview  # 一覧の絵（tools/previews/）だけ

出力:
 - unity/Assets/StreamingAssets/Stamps/stamp_001.png … stamp_365.png
     5 の倍数（5・10・…・365）は でっかいスタンプ（金のわく・ばんごうのリボン・大きい絵）
 - unity/Assets/Resources/stamps.json   （番号・なまえ・どうぶつ・でっかいか）
 - unity/Assets/UI/Characters/<どうぶつ>.png （先生やメニューの どうぶつ）
 - tools/previews/stamps_sheet.png        （365 こ の一覧。確認用）

1 こ ずつ、どうぶつ（47 しゅ）・かお（11 しゅ）・かざり（13 しゅ）・わくの形（8 しゅ）・いろ の組み合わせがちがう。
"""
import json
import math
import os
import random
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from art import (Canvas, Group, bezier, ellipse_pts, heart_pts, hexc, mix, quad, round_poly,  # noqa: E402
                 rounded_rect_pts, rounded_star_pts, scallop_pts, shade, star_pts, transform)

ROOT = os.path.abspath(os.path.join(HERE, ".."))
OUT_STAMPS = os.path.join(ROOT, "unity", "Assets", "StreamingAssets", "Stamps")
OUT_JSON = os.path.join(ROOT, "unity", "Assets", "Resources", "stamps.json")
OUT_CHARS = os.path.join(ROOT, "unity", "Assets", "UI", "Characters")
OUT_PREVIEW = os.path.join(HERE, "previews")
FONT_CANDIDATES = [
    os.path.join(HERE, ".cache", "MPLUSRounded1c-Black.ttf"),
    os.path.join(ROOT, "unity", "Assets", "UI", "Fonts", "PiamoRounded-Black.ttf"),
]

COUNT = 365
BIG_EVERY = 5
SIZE = 256
BIG_SIZE = 320

OUT = hexc("#583a32")         # どうぶつの ふちどり
EYE = hexc("#3c2a28")
WHITE = hexc("#ffffff")
PINK = hexc("#ffb3c6")
NOSE_PINK = hexc("#f07d96")
CHEEK = (255, 140, 165, 120)
GOLD = hexc("#ffd45e")
GOLD_DARK = hexc("#e39a2a")
LINE = 1.7                     # ふちの太さ（デザインの単位）


# ---------------------------------------------------------------------------
# 絵のまとまり（あとから大きさと位置を決めて描く）
# ---------------------------------------------------------------------------
class Drawing:
    def __init__(self):
        self.layers = []

    def group(self, outline=OUT, width=LINE):
        g = Group()
        self.layers.append(("group", g, outline, width))
        return g

    def stroke(self, pts, color, width):
        self.layers.append(("stroke", pts, color, width))

    def bounds(self):
        xs, ys = [], []
        for layer in self.layers:
            if layer[0] == "group":
                w = layer[3] if layer[2] is not None else 0
                for s in layer[1].shapes:
                    for x, y in s.pts:
                        xs += [x - w, x + w]
                        ys += [y - w, y + w]
            else:
                for x, y in layer[1]:
                    xs.append(x)
                    ys.append(y)
        return min(xs), min(ys), max(xs), max(ys)

    def render(self, cv, s=1.0, ox=0.0, oy=0.0):
        for layer in self.layers:
            if layer[0] == "group":
                g, outline, width = layer[1], layer[2], layer[3]
                tg = Group()
                for sh in g.shapes:
                    line = (sh.line[0], sh.line[1] * s) if sh.line else None
                    tg.add(transform(sh.pts, s, ox, oy), sh.fill, line)
                tg.draw(cv, outline, width * s)
            else:
                _, pts, color, width = layer
                cv.stroke(transform(pts, s, ox, oy), color, width * s)


def thick(g, pts, width, color):
    """太い線を、四角と丸のならびとして形のまとまりに入れる（ふちどりが外がわにつく）。"""
    r = width / 2
    for (x0, y0), (x1, y1) in zip(pts[:-1], pts[1:]):
        dx, dy = x1 - x0, y1 - y0
        L = math.hypot(dx, dy) or 1
        nx, ny = -dy / L * r, dx / L * r
        g.add([(x0 + nx, y0 + ny), (x1 + nx, y1 + ny), (x1 - nx, y1 - ny), (x0 - nx, y0 - ny)], color)
    for x, y in pts:
        g.ellipse(x, y, r, r, color)


def arc(cx, cy, rx, ry, a0, a1, n=24):
    return [(cx + math.cos(a0 + (a1 - a0) * i / n) * rx, cy + math.sin(a0 + (a1 - a0) * i / n) * ry) for i in range(n + 1)]


def tri_ear(cx, base_y, tip_y, half, lean, round_k=0.32):
    """とがった耳（三角を まるめる）。lean は 先の よこへの かたむき。"""
    pts = [(cx - half, base_y), (cx + lean, tip_y), (cx + half, base_y + 3)]
    return round_poly(pts, round_k, 8)


# ---------------------------------------------------------------------------
# どうぶつ（かお）
# ---------------------------------------------------------------------------
class Face:
    """1 ぴきの かお。species の関数が、形と、目・口の位置を入れる。"""

    def __init__(self):
        self.d = Drawing()
        self.back = self.d.group()          # 頭のうしろ（たてがみ・羊の毛・しっぽ など）
        self.sil = self.d.group()           # 頭と耳（まわりだけ ふちどり）
        self.mark = self.d.group(None)      # もよう（ふちどりなし）
        self.inner = self.d.group(OUT, 0.8)  # 口のまわり など（細い ふち）
        self.eye_y = 58.0
        self.eye_dx = 10.5
        self.eye_r = 3.4
        self.eye_on_dark = False
        self.eye_color = EYE
        self.nose = None                    # (種類, y, 色)
        self.mouth = "w"                    # w（ω）・smile・beak・none・wide
        self.mouth_y = 66.0
        self.cheek = (65.0, 17.0, 4.6)
        self.whiskers = False
        self.top = 33.0                     # 頭のてっぺん（ぼうしの位置）
        self.ear = (34.0, 33.0)             # 左の耳のつけね（リボン・はな の位置）
        self.chin = 79.0                    # あご（ちょうネクタイの位置）
        self.side = 25.0                    # 頭のよこはば（半分）
        self.no_mouth_expr = False          # くちばし など、口の かたちを かえない


def head(f, cx=50, cy=56, rx=25, ry=22, color=None, rot=0.0):
    f.sil.ellipse(cx, cy, rx, ry, color, rot)
    f.top = cy - ry
    f.side = rx
    f.chin = cy + ry


def sp_cat(f, v):
    cols = [hexc("#f9b866"), hexc("#bfc2d4"), hexc("#fff6ec"), hexc("#55505e"), hexc("#fff6ec")]
    c = cols[v % len(cols)]
    for side in (-1, 1):
        f.sil.add(tri_ear(50 + side * 15, 42, 18, 10, side * 4), c)
        f.sil.add(tri_ear(50 + side * 15, 40, 24, 5.5, side * 3), PINK if v % 5 != 3 else hexc("#8a7f96"))
    head(f, color=c)
    if v % 5 == 0:   # とらねこの しま
        for k in (-6, 0, 6):
            f.mark.add(round_poly([(50 + k - 1.6, 34), (50 + k + 1.6, 34), (50 + k, 42)], 0.3, 4), shade(c, 0.8))
    if v % 5 == 4:   # みけねこ
        f.mark.ellipse(37, 44, 10, 8, hexc("#f5a25a"), 0.4)
        f.mark.ellipse(64, 42, 8, 7, hexc("#5a4e54"), -0.3)
    if v % 5 == 3:
        f.eye_color = hexc("#2c2a30")
        f.eye_on_dark = True
    f.nose = ("tri", 62.5, NOSE_PINK)
    f.whiskers = True
    f.ear = (34, 32)


def sp_dog(f, v):
    cols = [(hexc("#eab47e"), hexc("#b97b4c")), (hexc("#fff8ee"), hexc("#c88a5c")), (hexc("#a8765a"), hexc("#6e4a38")), (hexc("#fffaf2"), hexc("#4a4148"))]
    c, ear = cols[v % len(cols)]
    for side in (-1, 1):
        f.back.ellipse(50 + side * 24, 52, 8, 16, ear, side * -0.35)
    head(f, color=c)
    f.inner.ellipse(50, 66, 11, 8, hexc("#fff3e3"))
    if v % 4 == 3:   # ぶちの犬
        f.mark.ellipse(38, 48, 7, 6, hexc("#4a4148"), 0.5)
        f.mark.ellipse(64, 42, 4, 3.5, hexc("#4a4148"))
    f.nose = ("oval", 62, hexc("#3c2f30"))
    f.mouth = "smile"
    f.mouth_y = 67
    f.ear = (30, 38)


def sp_shiba(f, v):
    c = hexc("#f2a44e") if v % 2 == 0 else hexc("#f6e7d2")
    for side in (-1, 1):
        f.sil.add(tri_ear(50 + side * 16, 42, 19, 10, side * 3), c)
        f.sil.add(tri_ear(50 + side * 16, 40, 25, 5, side * 2), hexc("#fff1df"))
    head(f, color=c, rx=26)
    f.mark.add(ellipse_pts(50, 68, 21, 12), hexc("#fffaf2"))
    f.mark.ellipse(39, 51.5, 2.6, 1.8, hexc("#fff6ea"))
    f.mark.ellipse(61, 51.5, 2.6, 1.8, hexc("#fff6ea"))
    f.nose = ("oval", 62.5, hexc("#3c2f30"))
    f.mouth = "w"
    f.ear = (33, 33)


def sp_rabbit(f, v):
    cols = [hexc("#fffaf6"), hexc("#ffe3ea"), hexc("#e9d6c4"), hexc("#d9d6e6")]
    c = cols[v % len(cols)]
    lop = v % 4 == 2
    for side in (-1, 1):
        if lop:
            f.sil.ellipse(50 + side * 24, 54, 7, 15, c, side * -0.25)
        else:
            f.sil.ellipse(50 + side * 9, 26, 7, 18, c, side * 0.12)
            f.sil.ellipse(50 + side * 9, 27, 3.6, 13, PINK, side * 0.12)
    head(f, cy=58, color=c)
    f.nose = ("tri", 63.5, NOSE_PINK)
    f.eye_y = 59
    f.mouth_y = 67
    f.ear = (40, 36)


def sp_bear(f, v):
    cols = [(hexc("#b98159"), hexc("#f0cfaa")), (hexc("#e8b05a"), hexc("#fff0cf")), (hexc("#f7f5ef"), hexc("#ffffff")), (hexc("#6b5a52"), hexc("#d8c0a8"))]
    c, m = cols[v % len(cols)]
    for side in (-1, 1):
        f.sil.ellipse(50 + side * 18, 36, 8.5, 8.5, c)
        f.sil.ellipse(50 + side * 18, 37, 4.6, 4.6, shade(m, 0.95))
    head(f, color=c)
    f.inner.ellipse(50, 66, 10.5, 8, m)
    f.nose = ("oval", 62.5, hexc("#4a3530"))
    f.mouth = "w"
    f.mouth_y = 67
    if v % 4 == 3:
        f.eye_on_dark = True
    f.ear = (32, 36)


def sp_panda(f, v):
    blk = hexc("#3d3a45")
    for side in (-1, 1):
        f.sil.ellipse(50 + side * 18, 36, 8.5, 8.5, blk)
    head(f, color=hexc("#fffdfa"))
    for side in (-1, 1):
        f.mark.ellipse(50 + side * 10.5, 58, 6, 7.6, blk, side * 0.5)
    f.eye_on_dark = True
    f.nose = ("oval", 64, blk)
    f.mouth = "w"
    f.mouth_y = 68
    f.cheek = (67, 18, 4.2)
    f.ear = (32, 36)


def sp_fox(f, v):
    c = hexc("#f59c4e") if v % 3 != 2 else hexc("#fbe0b8")
    for side in (-1, 1):
        f.sil.add(tri_ear(50 + side * 15, 42, 14, 11, side * 5), c)
        f.sil.add(tri_ear(50 + side * 15, 40, 21, 6, side * 4), hexc("#5a3b33"))
    head(f, color=c, rx=26, ry=21)
    for side in (-1, 1):
        f.mark.ellipse(50 + side * 11, 68, 12, 9, hexc("#fffaf2"), side * 0.3)
    f.nose = ("oval", 64, hexc("#3c2f30"))
    f.mouth = "w"
    f.mouth_y = 68
    f.ear = (34, 30)


def sp_tanuki(f, v):
    c = hexc("#b48c6c")
    for side in (-1, 1):
        f.sil.ellipse(50 + side * 18, 37, 7.5, 7.5, hexc("#6e5444"))
    head(f, color=c)
    f.mark.ellipse(50, 57, 21, 7.5, hexc("#6e5444"))
    f.inner.ellipse(50, 67, 10, 7.5, hexc("#f6e8d6"))
    f.eye_on_dark = True
    f.nose = ("oval", 63.5, hexc("#3c2f30"))
    f.mouth_y = 68
    f.ear = (32, 36)


def sp_pig(f, v):
    c = hexc("#ffb9c8") if v % 2 == 0 else hexc("#ffd3c2")
    for side in (-1, 1):
        f.sil.add(round_poly([(50 + side * 10, 38), (50 + side * 26, 30), (50 + side * 21, 46)], 0.3, 6), shade(c, 0.95))
    head(f, color=c)
    f.inner.ellipse(50, 65, 8.5, 6.2, hexc("#ff9db3"))
    f.mark.ellipse(47, 65, 1.6, 2.4, hexc("#d9627e"))
    f.mark.ellipse(53, 65, 1.6, 2.4, hexc("#d9627e"))
    f.eye_y = 56
    f.mouth = "smile"
    f.mouth_y = 73
    f.cheek = (66, 19, 4.4)
    f.ear = (34, 36)


def sp_cow(f, v):
    for side in (-1, 1):
        f.sil.add(round_poly([(50 + side * 9, 37), (50 + side * 17, 24), (50 + side * 15, 38)], 0.35, 6), hexc("#fff2cf"))
        f.sil.ellipse(50 + side * 27, 47, 9, 4.6, hexc("#ffffff"), side * 0.35)
    head(f, color=hexc("#ffffff"))
    f.mark.ellipse(38, 45, 8, 6.5, hexc("#4a4148"), 0.4)
    f.mark.ellipse(66, 53, 5, 6, hexc("#4a4148"), -0.2)
    f.inner.ellipse(50, 68, 14, 9, hexc("#ffc3cd"))
    f.mark.ellipse(45, 68, 1.6, 2.2, hexc("#d9627e"))
    f.mark.ellipse(55, 68, 1.6, 2.2, hexc("#d9627e"))
    f.eye_y = 55
    f.mouth = "none"
    f.ear = (32, 38)


def sp_sheep(f, v):
    wool = hexc("#ffffff") if v % 3 else hexc("#fff0d9")
    face = hexc("#f9e9d6") if v % 2 == 0 else hexc("#5a4e54")
    f.back.add(scallop_pts(50, 50, 26, 12, 5, rot=0.2), wool)
    for side in (-1, 1):
        f.sil.ellipse(50 + side * 21, 52, 7.5, 4, face, side * 0.4)
    head(f, cy=58, rx=17, ry=19, color=face)
    f.sil.add(scallop_pts(50, 42, 11, 8, 3), wool)
    if face[0] < 128:
        f.eye_on_dark = True
    f.nose = ("tri", 64, hexc("#c98a96") if face[0] > 128 else hexc("#f0c0c8"))
    f.eye_y = 57
    f.eye_dx = 8
    f.mouth_y = 69
    f.cheek = (66, 12, 3.6)
    f.top = 31
    f.ear = (32, 34)


def sp_lion(f, v):
    mane = [hexc("#e2913e"), hexc("#c8743a"), hexc("#f0b04a")][v % 3]
    f.back.add(scallop_pts(50, 55, 30, 14, 5), mane)
    for side in (-1, 1):
        f.sil.ellipse(50 + side * 16, 38, 6, 6, hexc("#ffd47e"))
        f.sil.ellipse(50 + side * 16, 39, 3, 3, hexc("#f2a55a"))
    head(f, rx=22, ry=20, cy=57, color=hexc("#ffd47e"))
    f.inner.ellipse(45.5, 67, 6.5, 5, hexc("#fff4da"))
    f.inner.ellipse(54.5, 67, 6.5, 5, hexc("#fff4da"))
    f.nose = ("tri", 63, hexc("#8a5a44"))
    f.mouth = "none"
    f.eye_y = 57
    f.eye_dx = 9.5
    f.cheek = (64, 15, 3.6)
    f.top = 26
    f.ear = (32, 34)


def sp_tiger(f, v):
    c = hexc("#ffb04e") if v % 3 != 2 else hexc("#f7f2ea")
    stripe = hexc("#4a3a34")
    for side in (-1, 1):
        f.sil.ellipse(50 + side * 18, 37, 7.5, 7.5, c)
        f.sil.ellipse(50 + side * 18, 38, 3.8, 3.8, hexc("#fff4e4"))
    head(f, color=c)
    f.mark.ellipse(44, 67, 8, 6.5, hexc("#fff6ea"))
    f.mark.ellipse(56, 67, 8, 6.5, hexc("#fff6ea"))
    for k in (-5, 0, 5):
        f.mark.add(round_poly([(50 + k - 1.5, 34), (50 + k + 1.5, 34), (50 + k, 42 - abs(k) * 0.3)], 0.3, 4), stripe)
    for side in (-1, 1):
        for dy in (0, 5):
            x0 = 50 + side * 25
            f.mark.add(round_poly([(x0, 57 + dy), (x0, 60 + dy), (x0 - side * 7, 58.5 + dy)], 0.3, 4), stripe)
    f.nose = ("tri", 63, NOSE_PINK)
    f.mouth_y = 67.5
    f.ear = (32, 35)


def sp_elephant(f, v):
    c = [hexc("#aab9d8"), hexc("#c7b3dc"), hexc("#b6c9c9")][v % 3]
    for side in (-1, 1):
        f.back.ellipse(50 + side * 25, 53, 13, 16, c, side * 0.2)
        f.back.ellipse(50 + side * 26, 54, 8, 11, hexc("#f4c4cc"), side * 0.2)
    head(f, rx=22, ry=21, cy=54, color=c)
    trunk = bezier((50, 58), (50, 70), (49, 78), (57, 80), 10)
    thick(f.sil, trunk, 8, c)
    f.eye_y = 53
    f.eye_dx = 10
    f.mouth = "none"
    f.cheek = (61, 15, 4)
    f.chin = 84
    f.ear = (32, 36)


def sp_giraffe(f, v):
    c = hexc("#ffd86c")
    spot = hexc("#d98a3e")
    for side in (-1, 1):
        thick(f.sil, [(50 + side * 7, 36), (50 + side * 9, 24)], 3.6, hexc("#e0a04e"))
        f.sil.ellipse(50 + side * 9, 23, 3.4, 3.4, hexc("#c87a3a"))
        f.sil.ellipse(50 + side * 22, 41, 8, 3.8, c, side * 0.5)
    head(f, rx=20, ry=24, cy=57, color=c)
    for x, y, r in ((41, 42, 3.2), (58, 40, 2.6), (35, 55, 2.8), (65, 56, 3.2), (53, 47, 2.2)):
        f.mark.ellipse(x, y, r * 1.1, r, spot)
    f.inner.ellipse(50, 71, 14, 9, hexc("#ffe9b3"))
    f.mark.ellipse(45.5, 70, 1.5, 2.2, hexc("#b26a40"))
    f.mark.ellipse(54.5, 70, 1.5, 2.2, hexc("#b26a40"))
    f.eye_y = 56
    f.eye_dx = 9
    f.mouth = "none"
    f.cheek = (63, 14, 3.8)
    f.top = 26
    f.ear = (35, 32)


def sp_monkey(f, v):
    c = hexc("#b0794f") if v % 2 == 0 else hexc("#d29b6a")
    skin = hexc("#f8d6b6")
    for side in (-1, 1):
        f.sil.ellipse(50 + side * 26, 56, 8, 8, c)
        f.sil.ellipse(50 + side * 26, 56, 4.5, 4.5, skin)
    head(f, color=c)
    f.mark.ellipse(43.5, 55, 8.5, 9, skin)
    f.mark.ellipse(56.5, 55, 8.5, 9, skin)
    f.mark.ellipse(50, 66, 13, 9.5, skin)
    f.mark.ellipse(48, 63, 1.1, 1.5, hexc("#8a5a44"))
    f.mark.ellipse(52, 63, 1.1, 1.5, hexc("#8a5a44"))
    f.mouth = "smile"
    f.mouth_y = 68
    f.eye_y = 56
    f.ear = (30, 46)


def sp_koala(f, v):
    c = hexc("#a9abbd") if v % 2 == 0 else hexc("#bfb3c4")
    for side in (-1, 1):
        f.sil.add(scallop_pts(50 + side * 24, 40, 10, 9, 1.8), c)
        f.sil.ellipse(50 + side * 24, 41, 6, 6, hexc("#f4f2f8"))
    head(f, rx=24, ry=21, cy=57, color=c)
    f.inner.ellipse(50, 63, 5, 6.6, hexc("#4a4553"))
    f.eye_y = 56
    f.eye_dx = 12
    f.eye_r = 3
    f.mouth = "none"
    f.cheek = (66, 18, 4)
    f.ear = (28, 40)


def sp_penguin(f, v):
    c = hexc("#3f4b6e") if v % 2 == 0 else hexc("#4f5a62")
    head(f, rx=25, ry=24, cy=56, color=c)
    f.mark.ellipse(43.5, 58, 10.5, 12, hexc("#ffffff"))
    f.mark.ellipse(56.5, 58, 10.5, 12, hexc("#ffffff"))
    f.mark.ellipse(50, 67, 13, 9, hexc("#ffffff"))
    f.inner.add(round_poly([(46, 63), (54, 63), (50, 68)], 0.3, 5), hexc("#ffa83c"))
    f.mouth = "beak"
    f.eye_y = 58
    f.eye_dx = 8.5
    f.cheek = (66, 15, 3.8)
    f.ear = (34, 38)


def sp_chick(f, v):
    c = hexc("#ffe46a") if v % 2 == 0 else hexc("#fff2a8")
    for i, (x, rot) in enumerate(((46, -0.5), (50, 0.0), (54, 0.5))):
        f.sil.ellipse(x, 33, 2.6, 6, c, rot)
    for side in (-1, 1):
        f.sil.ellipse(50 + side * 25, 64, 6, 9, shade(c, 0.95), side * -0.5)
    head(f, rx=25, ry=24, cy=58, color=c)
    f.inner.add(round_poly([(46, 63), (54, 63), (50, 68.5)], 0.3, 5), hexc("#ff9f43"))
    f.mouth = "beak"
    f.eye_y = 58
    f.ear = (37, 37)
    f.top = 30


def sp_owl(f, v):
    c = [hexc("#b88a62"), hexc("#9a8aa8"), hexc("#d9b07a")][v % 3]
    for side in (-1, 1):
        f.sil.add(tri_ear(50 + side * 17, 40, 25, 7, side * 4), c)
    head(f, rx=26, ry=25, cy=58, color=c)
    f.mark.ellipse(40.5, 55, 10, 10, hexc("#f7e6cc"))
    f.mark.ellipse(59.5, 55, 10, 10, hexc("#f7e6cc"))
    f.inner.add(round_poly([(47, 62), (53, 62), (50, 68)], 0.3, 5), hexc("#f2a33a"))
    for x in (44, 50, 56):
        f.d.stroke(arc(x, 74, 2.4, 1.6, 0.2, math.pi - 0.2, 8), shade(c, 0.75), 0.9)
    f.mouth = "beak"
    f.eye_y = 55
    f.eye_dx = 9.5
    f.eye_r = 3.8
    f.cheek = (64, 19, 3.4)
    f.ear = (33, 36)


def sp_frog(f, v):
    c = [hexc("#8fd36c"), hexc("#a6dc7c"), hexc("#7fcfa0")][v % 3]
    for side in (-1, 1):
        f.sil.ellipse(50 + side * 13, 45, 9.5, 9, c)
    head(f, rx=28, ry=19, cy=62, color=c)
    f.mark.ellipse(50, 70, 18, 7, shade(c, 1.35))
    f.eye_y = 45
    f.eye_dx = 13
    f.eye_r = 3.8
    f.mouth = "wide"
    f.mouth_y = 64
    f.cheek = (66, 20, 4.4)
    f.top = 36
    f.ear = (36, 40)


def sp_mouse(f, v):
    c = hexc("#c9c9d8") if v % 2 == 0 else hexc("#e2d4c4")
    for side in (-1, 1):
        f.sil.ellipse(50 + side * 20, 35, 11.5, 11.5, c)
        f.sil.ellipse(50 + side * 20, 36, 7, 7, PINK)
    head(f, rx=23, ry=21, cy=58, color=c)
    f.nose = ("dot", 64, NOSE_PINK)
    f.whiskers = True
    f.eye_y = 58
    f.eye_dx = 9
    f.mouth_y = 67.5
    f.ear = (28, 32)


def sp_hamster(f, v):
    c = hexc("#f6b977") if v % 2 == 0 else hexc("#d9c4ae")
    for side in (-1, 1):
        f.sil.ellipse(50 + side * 15, 37, 6, 6, c)
        f.sil.ellipse(50 + side * 15, 38, 3.2, 3.2, PINK)
    head(f, rx=27, ry=23, cy=57, color=c)
    f.mark.add(ellipse_pts(50, 68, 25, 13), hexc("#fffaf2"))
    f.nose = ("dot", 63, NOSE_PINK)
    f.cheek = (66, 18, 6)
    f.ear = (34, 36)


def sp_squirrel(f, v):
    c = hexc("#d98f58")
    f.back.add(bezier((66, 78), (96, 70), (92, 30), (74, 28), 16) + bezier((74, 28), (88, 38), (84, 66), (62, 70), 16), hexc("#c27a46"))
    for side in (-1, 1):
        f.sil.add(tri_ear(50 + side * 15, 42, 23, 7, side * 3), c)
        f.sil.ellipse(50 + side * 15 + side * 3, 24, 2.5, 3.5, hexc("#8a5236"))
    head(f, rx=23, ry=21, cy=57, color=c)
    f.mark.ellipse(41, 66, 8, 6, hexc("#fff0dc"))
    f.mark.ellipse(59, 66, 8, 6, hexc("#fff0dc"))
    f.nose = ("dot", 63, hexc("#5a3b33"))
    f.ear = (34, 35)


def sp_hedgehog(f, v):
    spike = hexc("#8e6a52") if v % 2 == 0 else hexc("#a9886a")
    f.back.add(rounded_star_pts(50, 52, 31, 24, 14, -math.pi / 2), spike)
    for side in (-1, 1):
        f.sil.ellipse(50 + side * 14, 44, 5, 5, hexc("#ffe7cf"))
    head(f, rx=19, ry=17, cy=59, color=hexc("#ffe7cf"))
    f.nose = ("dot", 64, hexc("#3c2f30"))
    f.eye_y = 58
    f.eye_dx = 8
    f.eye_r = 3
    f.mouth_y = 68
    f.cheek = (65, 13, 3.4)
    f.top = 22
    f.ear = (36, 40)


def sp_deer(f, v):
    c = hexc("#d9a06a")
    ant = hexc("#b8875a")
    for side in (-1, 1):
        s = side
        thick(f.back, [(50 + s * 8, 38), (50 + s * 12, 24), (50 + s * 18, 14)], 3.2, ant)
        thick(f.back, [(50 + s * 12, 25), (50 + s * 20, 24)], 3.0, ant)
        thick(f.back, [(50 + s * 15, 18), (50 + s * 13, 11)], 2.8, ant)
        f.sil.ellipse(50 + s * 23, 44, 8.5, 4.2, c, s * 0.45)
        f.sil.ellipse(50 + s * 23, 44, 5, 2.2, PINK, s * 0.45)
    head(f, rx=21, ry=23, cy=57, color=c)
    for x, y in ((44, 40), (50, 37.5), (56, 40)):
        f.mark.ellipse(x, y, 1.8, 1.8, hexc("#fff4e2"))
    f.inner.ellipse(50, 70, 11, 8, hexc("#f6dcc0"))
    f.nose = ("oval", 66.5, hexc("#3c2f30"))
    f.mouth = "none"
    f.eye_y = 56
    f.eye_dx = 9.5
    f.top = 14
    f.ear = (30, 42)


def horse_base(f, c, mane, muzzle, horn=None):
    for side in (-1, 1):
        f.sil.add(tri_ear(50 + side * 11, 38, 24, 5, side * 2), c)
    if horn:
        thick(f.back, [(50, 34), (50, 15)], 4.2, horn)
    head(f, rx=20, ry=25, cy=57, color=c)
    if isinstance(mane, list):
        for i, mc in enumerate(mane):
            f.sil.ellipse(42 + i * 4, 33 + abs(i - 2) * 1.2, 4, 6, mc, (i - 2) * 0.3)
    else:
        f.sil.add(scallop_pts(50, 35, 6, 6, 2), mane)
    f.inner.ellipse(50, 72, 16, 10, muzzle)
    f.mark.ellipse(44.5, 71, 1.6, 2.4, shade(muzzle, 0.6))
    f.mark.ellipse(55.5, 71, 1.6, 2.4, shade(muzzle, 0.6))
    f.eye_y = 54
    f.eye_dx = 9
    f.mouth = "none"
    f.cheek = (62, 14, 3.6)
    f.top = 30
    f.ear = (36, 32)


def sp_horse(f, v):
    c = [hexc("#c48c5c"), hexc("#f1e6d8"), hexc("#8a6248")][v % 3]
    horse_base(f, c, hexc("#5a3b33"), hexc("#ecc8a2"))


def sp_zebra(f, v):
    horse_base(f, hexc("#ffffff"), hexc("#3c3a44"), hexc("#b9b4c2"))
    for y in (40, 46):
        for side in (-1, 1):
            f.mark.add(round_poly([(50 + side * 20, y), (50 + side * 20, y + 3), (50 + side * 9, y + 1.8)], 0.3, 4), hexc("#3c3a44"))
    f.mark.add(round_poly([(46, 44), (54, 44), (50, 49)], 0.3, 4), hexc("#3c3a44"))


def sp_unicorn(f, v):
    mane = [hexc("#ffb3c6"), hexc("#ffd36e"), hexc("#a8e6a0"), hexc("#9fd4ff"), hexc("#c9b3ff")]
    horse_base(f, hexc("#fffaff"), mane, hexc("#ffd9e4"), horn=hexc("#ffd45e"))
    f.top = 14


def sp_hippo(f, v):
    c = hexc("#b9a9d9") if v % 2 == 0 else hexc("#a7b8d6")
    for side in (-1, 1):
        f.sil.ellipse(50 + side * 16, 38, 5, 5, c)
        f.sil.ellipse(50 + side * 16, 38.5, 2.6, 2.6, PINK)
        f.sil.ellipse(50 + side * 9, 45, 6.5, 6, c)
    head(f, rx=24, ry=17, cy=52, color=c)
    f.sil.ellipse(50, 68, 24, 13, shade(c, 1.25))
    f.mark.ellipse(42, 63, 2.2, 1.6, shade(c, 0.7))
    f.mark.ellipse(58, 63, 2.2, 1.6, shade(c, 0.7))
    f.eye_y = 45
    f.eye_dx = 9
    f.eye_r = 3
    f.mouth = "wide"
    f.mouth_y = 71
    f.cheek = (58, 19, 3.6)
    f.chin = 81
    f.ear = (34, 38)


def sp_seal(f, v):
    c = hexc("#eef2f7") if v % 2 == 0 else hexc("#d6dde8")
    head(f, rx=26, ry=23, cy=56, color=c)
    f.inner.ellipse(46, 65, 5.5, 4.4, hexc("#ffffff"))
    f.inner.ellipse(54, 65, 5.5, 4.4, hexc("#ffffff"))
    for x in (44, 46.5, 53.5, 56):
        f.mark.ellipse(x, 65.5 + (x % 2), 0.6, 0.6, hexc("#9a9aa8"))
    f.nose = ("oval", 61.5, hexc("#3c3a44"))
    f.eye_r = 4.2
    f.eye_dx = 12
    f.mouth = "none"
    f.whiskers = True
    f.ear = (32, 40)


def sp_whale(f, v):
    c = hexc("#72b8ea") if v % 2 == 0 else hexc("#8aa6e8")
    for i, (x, y, r) in enumerate(((50, 22, 3.6), (44, 25, 3), (56, 25, 3), (40, 21, 2.4), (60, 21, 2.4))):
        f.back.ellipse(x, y, r, r * 1.2, hexc("#bfe9ff"))
    thick(f.back, [(50, 34), (50, 26)], 3, hexc("#bfe9ff"))
    head(f, rx=30, ry=22, cy=58, color=c)
    f.mark.add(ellipse_pts(50, 72, 22, 8), hexc("#e3f6ff"))
    f.eye_y = 56
    f.eye_dx = 15
    f.mouth = "smile"
    f.mouth_y = 64
    f.cheek = (63, 21, 4.4)
    f.top = 18
    f.ear = (30, 42)


def sp_octopus(f, v):
    c = hexc("#ff8f8f") if v % 2 == 0 else hexc("#ffa3c8")
    for i in range(6):
        x = 31 + i * 7.6
        f.sil.ellipse(x, 72 + (i % 2) * 2, 4.4, 8, c, (i - 2.5) * 0.18)
    head(f, rx=24, ry=23, cy=50, color=c)
    for x, y in ((38, 38), (60, 36), (64, 46)):
        f.mark.ellipse(x, y, 2.4, 2.2, shade(c, 1.3))
    f.inner.ellipse(50, 63, 3.4, 3, shade(c, 0.85))
    f.eye_y = 52
    f.eye_dx = 10
    f.mouth = "none"
    f.cheek = (58, 16, 4)
    f.chin = 84
    f.ear = (32, 34)


def sp_turtle(f, v):
    shell = hexc("#5fb36a") if v % 2 == 0 else hexc("#7fae5a")
    f.back.add(ellipse_pts(50, 66, 31, 20), shell)
    for x, y in ((38, 66), (50, 70), (62, 66), (44, 77), (56, 77)):
        f.back.add(round_poly([(x + math.cos(a) * 5, y + math.sin(a) * 4) for a in np.linspace(0, math.tau, 7)[:-1]], 0.3, 3), shade(shell, 1.25))
    head(f, rx=17, ry=16, cy=47, color=hexc("#b8e08c"))
    f.eye_y = 47
    f.eye_dx = 7.5
    f.eye_r = 3
    f.mouth_y = 55
    f.mouth = "smile"
    f.cheek = (53, 11, 3)
    f.chin = 86
    f.ear = (38, 34)


def sp_crab(f, v):
    c = hexc("#ff7d6b") if v % 2 == 0 else hexc("#ff9a5a")
    for side in (-1, 1):
        thick(f.back, [(50 + side * 22, 62), (50 + side * 30, 52)], 3.4, c)
        f.back.add(round_poly([(50 + side * 26, 50), (50 + side * 38, 40), (50 + side * 34, 30), (50 + side * 30, 38), (50 + side * 26, 30), (50 + side * 22, 40)], 0.35, 5), c)
        for k in range(3):
            thick(f.back, [(50 + side * 20, 70 + k * 3), (50 + side * 30, 74 + k * 4)], 2.4, c)
        thick(f.sil, [(50 + side * 8, 52), (50 + side * 9, 42)], 2.6, c)
        f.sil.ellipse(50 + side * 9, 40, 6, 6, hexc("#ffffff"))
    head(f, rx=24, ry=16, cy=63, color=c)
    f.eye_y = 40
    f.eye_dx = 9
    f.eye_r = 3
    f.mouth_y = 67
    f.mouth = "smile"
    f.cheek = (67, 15, 3.6)
    f.top = 33
    f.ear = (40, 36)


def sp_duck(f, v):
    c = hexc("#ffffff") if v % 2 == 0 else hexc("#fff4c2")
    f.sil.add(bezier((50, 36), (48, 26), (54, 24), (56, 30), 8) + bezier((56, 30), (54, 28), (52, 32), (52, 37), 8), c)
    head(f, rx=24, ry=23, cy=57, color=c)
    f.inner.ellipse(50, 66, 11, 4.5, hexc("#ffa94a"))
    f.inner.ellipse(50, 69, 8, 3, hexc("#f28f30"))
    f.mouth = "beak"
    f.eye_y = 56
    f.eye_dx = 10
    f.cheek = (64, 17, 4)
    f.ear = (35, 36)


def sp_chicken(f, v):
    for x, y, r in ((44, 32, 5), (50, 29, 6), (56, 32, 5)):
        f.sil.ellipse(x, y, r, r, hexc("#ff5d5d"))
    head(f, rx=23, ry=22, cy=56, color=hexc("#ffffff"))
    f.inner.add(round_poly([(46, 60), (54, 60), (50, 66)], 0.3, 5), hexc("#ffc23c"))
    f.inner.ellipse(50, 70, 3, 4, hexc("#ff5d5d"))
    f.mouth = "beak"
    f.eye_y = 55
    f.eye_dx = 9.5
    f.top = 26
    f.ear = (36, 36)


def sp_bird(f, v):
    c = [hexc("#7cbaf0"), hexc("#f59ab6"), hexc("#8fd6a8"), hexc("#ffd46a")][v % 4]
    f.sil.ellipse(47, 32, 3, 6, shade(c, 0.9), -0.4)
    f.sil.ellipse(52, 31, 3, 6.5, shade(c, 0.9), 0.3)
    for side in (-1, 1):
        f.sil.ellipse(50 + side * 24, 63, 6, 10, shade(c, 0.9), side * -0.5)
    head(f, rx=25, ry=24, cy=57, color=c)
    f.mark.add(ellipse_pts(50, 68, 17, 12), hexc("#ffffff"))
    f.inner.add(round_poly([(46.5, 61), (53.5, 61), (50, 66)], 0.3, 5), hexc("#ffb347"))
    f.mouth = "beak"
    f.eye_y = 56
    f.ear = (36, 38)


def sp_alpaca(f, v):
    c = hexc("#fff3df") if v % 2 == 0 else hexc("#f3dcc8")
    for side in (-1, 1):
        f.sil.ellipse(50 + side * 17, 30, 4.6, 10, c, side * 0.5)
    head(f, rx=19, ry=23, cy=58, color=c)
    f.sil.add(scallop_pts(50, 37, 12, 9, 3.5), c)
    f.inner.ellipse(50, 70, 9, 7, hexc("#ffffff"))
    f.nose = ("y", 68, hexc("#8a6a5a"))
    f.mouth = "none"
    f.eye_y = 56
    f.eye_dx = 8.5
    f.cheek = (63, 13, 3.6)
    f.top = 24
    f.ear = (34, 30)


def sp_redpanda(f, v):
    c = hexc("#e8854c")
    for side in (-1, 1):
        f.sil.add(tri_ear(50 + side * 17, 42, 26, 8, side * 3), c)
        f.sil.add(tri_ear(50 + side * 17, 40, 30, 4.4, side * 2), hexc("#fff4ea"))
    head(f, rx=26, ry=21, cy=57, color=c)
    for side in (-1, 1):
        f.mark.ellipse(50 + side * 11, 51, 4, 2.5, hexc("#fff4ea"))
        f.mark.ellipse(50 + side * 17, 66, 7, 6, hexc("#fff4ea"))
        f.mark.add(round_poly([(50 + side * 9, 60), (50 + side * 13, 60), (50 + side * 12, 70)], 0.3, 4), hexc("#8a3f24"))
    f.inner.ellipse(50, 67, 8, 6, hexc("#fff4ea"))
    f.nose = ("oval", 64.5, hexc("#3c2f30"))
    f.mouth_y = 69
    f.ear = (32, 34)


def sp_capybara(f, v):
    c = hexc("#b98d66")
    for side in (-1, 1):
        f.sil.ellipse(50 + side * 17, 34, 4.2, 3.4, shade(c, 0.85))
    f.sil.add(rounded_rect_pts(27, 33, 73, 78, 16), c)
    f.inner.add(rounded_rect_pts(39, 60, 61, 72, 6), hexc("#6e4c38"))
    f.top = 33
    f.side = 23
    f.chin = 78
    f.eye_y = 50
    f.eye_dx = 12
    f.eye_r = 2.8
    f.mouth = "none"
    f.cheek = (58, 17, 3.6)
    f.ear = (33, 33)


def sp_otter(f, v):
    c = hexc("#a87b5c")
    for side in (-1, 1):
        f.sil.ellipse(50 + side * 21, 40, 4.6, 4.6, c)
    head(f, rx=25, ry=22, cy=57, color=c)
    f.mark.add(ellipse_pts(50, 61, 19, 15), hexc("#efdfcc"))
    f.nose = ("oval", 61.5, hexc("#3c2f30"))
    f.whiskers = True
    f.mouth_y = 66
    f.ear = (31, 40)


def sp_ladybug(f, v):
    red = hexc("#ff5f5f") if v % 2 == 0 else hexc("#ffb03c")
    for side in (-1, 1):
        f.sil.add(quad((50 + side * 5, 34), (50 + side * 8, 18), (50 + side * 15, 16), 10) + [(50 + side * 15.5, 17.5)], hexc("#3c3a44"))
        f.sil.ellipse(50 + side * 15, 16, 2.6, 2.6, hexc("#3c3a44"))
    f.sil.add(ellipse_pts(50, 64, 27, 21), red)
    f.sil.ellipse(50, 42, 15, 12, hexc("#3c3a44"))
    for x, y, r in ((38, 60, 4.2), (62, 60, 4.2), (42, 74, 3.4), (58, 74, 3.4)):
        f.mark.ellipse(x, y, r, r, hexc("#3c3a44"))
    f.d.stroke([(50, 52), (50, 84)], hexc("#3c3a44"), 1.4)
    f.eye_on_dark = True
    f.eye_y = 42
    f.eye_dx = 6.5
    f.eye_r = 2.8
    f.mouth = "smile"
    f.mouth_y = 47.5
    f.cheek = (46, 11, 2.6)
    f.top = 30
    f.side = 27
    f.chin = 85
    f.ear = (40, 32)


def sp_bee(f, v):
    for side in (-1, 1):
        f.back.ellipse(50 + side * 18, 34, 10, 7, hexc("#d8f0ff"), side * -0.5)
        thick(f.back, [(50 + side * 5, 36), (50 + side * 9, 22)], 1.8, hexc("#3c3a44"))
        f.back.ellipse(50 + side * 9.5, 21, 2.6, 2.6, hexc("#3c3a44"))
    head(f, rx=25, ry=24, cy=58, color=hexc("#ffd94a"))
    f.mark.add(ellipse_pts(50, 72, 21, 3.4), hexc("#3c3a44"))
    f.mark.add(ellipse_pts(50, 79, 15, 2.8), hexc("#3c3a44"))
    f.eye_y = 54
    f.mouth_y = 62
    f.mouth = "smile"
    f.cheek = (60, 17, 4)
    f.top = 22
    f.ear = (36, 36)


def sp_polar(f, v):
    sp_bear(f, 2)


def sp_raccoon(f, v):
    c = hexc("#9a9aa6")
    for side in (-1, 1):
        f.sil.add(tri_ear(50 + side * 17, 42, 24, 7, side * 3), c)
        f.sil.add(tri_ear(50 + side * 17, 40, 28, 3.8, side * 2), hexc("#ffffff"))
    head(f, rx=26, ry=21, cy=57, color=c)
    f.mark.ellipse(50, 51, 6, 4, hexc("#ffffff"))
    for side in (-1, 1):
        f.mark.ellipse(50 + side * 11, 57, 8, 5.4, hexc("#4a4553"), side * 0.25)
    f.inner.ellipse(50, 67, 9, 6.5, hexc("#ffffff"))
    f.eye_on_dark = True
    f.nose = ("oval", 64, hexc("#3c3a44"))
    f.mouth_y = 68.5
    f.ear = (32, 34)


def sp_sloth(f, v):
    c = hexc("#c8ad8f")
    head(f, rx=25, ry=23, cy=57, color=c)
    f.mark.add(ellipse_pts(50, 60, 19, 15), hexc("#f6e9d6"))
    for side in (-1, 1):
        f.mark.ellipse(50 + side * 9, 56, 6.4, 4, hexc("#6e5444"), side * -0.35)
    f.eye_on_dark = True
    f.nose = ("oval", 62.5, hexc("#4a3530"))
    f.mouth_y = 67
    f.eye_y = 56
    f.eye_dx = 9.5
    f.ear = (32, 40)


# どうぶつの いちらん：id・なまえ・かく関数
SPECIES = [
    ("cat", "ねこ", sp_cat), ("dog", "いぬ", sp_dog), ("rabbit", "うさぎ", sp_rabbit), ("bear", "くま", sp_bear),
    ("panda", "パンダ", sp_panda), ("fox", "きつね", sp_fox), ("tanuki", "たぬき", sp_tanuki), ("pig", "ぶた", sp_pig),
    ("cow", "うし", sp_cow), ("sheep", "ひつじ", sp_sheep), ("lion", "ライオン", sp_lion), ("tiger", "とら", sp_tiger),
    ("elephant", "ぞう", sp_elephant), ("giraffe", "きりん", sp_giraffe), ("monkey", "おさる", sp_monkey), ("koala", "コアラ", sp_koala),
    ("penguin", "ペンギン", sp_penguin), ("chick", "ひよこ", sp_chick), ("owl", "ふくろう", sp_owl), ("frog", "かえる", sp_frog),
    ("mouse", "ねずみ", sp_mouse), ("hamster", "ハムスター", sp_hamster), ("squirrel", "りす", sp_squirrel), ("hedgehog", "はりねずみ", sp_hedgehog),
    ("deer", "しか", sp_deer), ("horse", "うま", sp_horse), ("zebra", "しまうま", sp_zebra), ("hippo", "かば", sp_hippo),
    ("seal", "あざらし", sp_seal), ("whale", "くじら", sp_whale), ("octopus", "たこ", sp_octopus), ("turtle", "かめ", sp_turtle),
    ("crab", "かに", sp_crab), ("duck", "あひる", sp_duck), ("chicken", "にわとり", sp_chicken), ("bird", "ことり", sp_bird),
    ("alpaca", "アルパカ", sp_alpaca), ("redpanda", "レッサーパンダ", sp_redpanda), ("capybara", "カピバラ", sp_capybara), ("otter", "ラッコ", sp_otter),
    ("shiba", "しばいぬ", sp_shiba), ("ladybug", "てんとうむし", sp_ladybug), ("bee", "はち", sp_bee), ("unicorn", "ユニコーン", sp_unicorn),
    ("polar", "しろくま", sp_polar), ("raccoon", "アライグマ", sp_raccoon), ("sloth", "ナマケモノ", sp_sloth),
]
SPECIES_IDS = [s[0] for s in SPECIES]


# ---------------------------------------------------------------------------
# かお（目と口）
# ---------------------------------------------------------------------------
# id・なまえ（スタンプの なまえに つかう）・目・口
EXPRESSIONS = [
    ("smile", "にこにこ", "dot", None),
    ("happy", "うきうき", "happy", "open"),
    ("wink", "ウインクの", "wink", "tongue"),
    ("sparkle", "きらきら", "sparkle", None),
    ("love", "ラブラブ", "heart", "open"),
    ("star", "わくわく", "star", "open"),
    ("sleepy", "ねむねむ", "sleepy", "o"),
    ("wow", "びっくり", "wow", "o"),
    ("laugh", "わはは", "squeeze", "open"),
    ("calm", "ほっこり", "calm", None),
    ("kiss", "ちゅっ", "dot", "kiss"),
]


def draw_eye(d, kind, x, y, r, f):
    g = d.group(None)
    col = f.eye_color
    if kind == "dot":
        if f.eye_on_dark:
            g.ellipse(x, y, r * 1.05, r * 1.2, WHITE)
            g.ellipse(x, y + 0.3, r * 0.7, r * 0.85, col)
        else:
            g.ellipse(x, y, r * 0.88, r * 1.08, col)
        g.ellipse(x - r * 0.28, y - r * 0.38, r * 0.34, r * 0.34, WHITE)
    elif kind == "sparkle":
        if f.eye_on_dark:
            g.ellipse(x, y, r * 1.35, r * 1.5, WHITE)
        g.ellipse(x, y, r * 1.1, r * 1.3, col)
        g.ellipse(x - r * 0.35, y - r * 0.45, r * 0.45, r * 0.45, WHITE)
        g.ellipse(x + r * 0.35, y + r * 0.45, r * 0.22, r * 0.22, WHITE)
    elif kind == "heart":
        g.add(heart_pts(x, y + 0.2, r * 1.55), hexc("#ff5c86"), (shade(hexc("#ff5c86"), 0.7), 0.5))
        g.ellipse(x - r * 0.45, y - r * 0.35, r * 0.28, r * 0.28, WHITE)
    elif kind == "star":
        g.add(rounded_star_pts(x, y, r * 1.7, r * 0.8, 5), hexc("#ffd23c"), (hexc("#e08a1e"), 0.55))
    elif kind == "wow":
        g.ellipse(x, y, r * 1.35, r * 1.45, WHITE, line=(col, 0.7))
        g.ellipse(x, y + 0.2, r * 0.55, r * 0.6, col)
    if kind in ("happy", "wink_closed"):
        d.stroke(arc(x, y + r * 0.55, r * 1.05, r * 1.05, math.pi * 1.15, math.pi * 1.85, 12), col if not f.eye_on_dark else WHITE, 1.25)
    elif kind == "calm":
        d.stroke(arc(x, y - r * 0.35, r * 1.0, r * 0.9, math.pi * 0.15, math.pi * 0.85, 12), col if not f.eye_on_dark else WHITE, 1.25)
    elif kind == "sleepy":
        d.stroke([(x - r * 1.0, y + 0.3), (x + r * 1.0, y + 0.3)], col if not f.eye_on_dark else WHITE, 1.25)
    elif kind == "squeeze":
        s = -1 if x < 50 else 1
        pts = [(x - s * r * 0.9, y - r * 0.85), (x + s * r * 0.75, y), (x - s * r * 0.9, y + r * 0.85)]
        d.stroke(pts, col if not f.eye_on_dark else WHITE, 1.2)


def draw_features(f, expr_id):
    d = f.d
    expr = next(e for e in EXPRESSIONS if e[0] == expr_id)
    eye_kind, mouth_kind = expr[2], expr[3]
    # ほっぺ
    if f.cheek:
        cy, dx, r = f.cheek
        g = d.group(None)
        g.ellipse(50 - dx, cy, r * 1.25, r * 0.8, CHEEK)
        g.ellipse(50 + dx, cy, r * 1.25, r * 0.8, CHEEK)
    # 目
    for side in (-1, 1):
        x = 50 + side * f.eye_dx
        kind = eye_kind
        if eye_kind == "wink":
            kind = "dot" if side < 0 else "wink_closed"
        draw_eye(d, kind, x, f.eye_y, f.eye_r, f)
    # 鼻
    if f.nose:
        kind, y, col = f.nose
        g = d.group(None)
        if kind == "tri":
            g.add(round_poly([(47.6, y - 1.3), (52.4, y - 1.3), (50, y + 1.5)], 0.35, 5), col)
        elif kind == "oval":
            g.ellipse(50, y, 3.2, 2.3, col)
            g.ellipse(49, y - 0.9, 1.0, 0.6, (255, 255, 255, 170))
        elif kind == "dot":
            g.ellipse(50, y, 1.9, 1.6, col)
        elif kind == "y":
            d.stroke([(50, y - 2), (50, y + 0.5)], col, 0.9)
            d.stroke([(47, y + 2.2), (50, y + 0.5), (53, y + 2.2)], col, 0.9)
    # 口
    y = f.mouth_y
    col = EYE if not f.eye_on_dark or f.mouth != "beak" else EYE
    if f.mouth in ("beak", "none") and mouth_kind not in ("kiss",):
        pass
    elif mouth_kind == "open" and f.mouth != "beak":
        w = 4.2 if f.mouth != "wide" else 7
        pts = [(50 - w, y - 0.8)] + arc(50, y - 0.8, w, w * 0.95, 0.0, math.pi, 16)[::-1] + [(50 + w, y - 0.8)]
        g = d.group(OUT, 0.7)
        g.add(arc(50, y - 0.8, w, w * 0.95, 0.0, math.pi, 16), hexc("#c94a5e"))
        g2 = d.group(None)
        g2.ellipse(50, y + w * 0.55, w * 0.6, w * 0.35, hexc("#ff8fa3"))
    elif mouth_kind == "o":
        g = d.group(OUT, 0.6)
        g.ellipse(50, y + 1, 1.9, 2.3, hexc("#c94a5e"))
    elif mouth_kind == "tongue":
        g = d.group(OUT, 0.6)
        g.ellipse(51.5, y + 2.2, 2.2, 2.6, hexc("#ff8fa3"))
        mouth_line(d, f, y)
    elif mouth_kind == "kiss":
        d.stroke(arc(50, y - 0.2, 1.4, 1.1, -math.pi / 2, math.pi / 2, 8), EYE, 1.0)
        d.stroke(arc(50, y + 2.0, 1.4, 1.1, -math.pi / 2, math.pi / 2, 8), EYE, 1.0)
        g = d.group(None)
        g.add(heart_pts(58, y - 4, 2.6), hexc("#ff6f95"))
    else:
        mouth_line(d, f, y)
    # ひげ
    if f.whiskers:
        for side in (-1, 1):
            for k, dy in enumerate((-1.6, 1.6)):
                x0 = 50 + side * 14
                d.stroke([(x0, f.mouth_y - 2 + dy), (x0 + side * 9, f.mouth_y - 3 + dy * 2)], shade(OUT, 1.2), 0.75)
    if expr_id == "sleepy":
        g = d.group(None)
        d.stroke([(68, 34), (72, 34), (68, 38), (72, 38)], hexc("#7a90c8"), 0.9)
        d.stroke([(74, 28), (77, 28), (74, 31), (77, 31)], hexc("#7a90c8"), 0.8)


def mouth_line(d, f, y):
    if f.mouth == "w":
        d.stroke(arc(48, y - 0.5, 2.0, 1.8, 0.1, math.pi - 0.1, 10), EYE, 1.0)
        d.stroke(arc(52, y - 0.5, 2.0, 1.8, 0.1, math.pi - 0.1, 10), EYE, 1.0)
        d.stroke([(50, y - 2.8), (50, y - 0.6)], EYE, 1.0)
    elif f.mouth == "wide":
        d.stroke(arc(50, y - 3, 10, 4.5, 0.25, math.pi - 0.25, 18), EYE, 1.1)
    elif f.mouth == "smile":
        d.stroke(arc(50, y - 1.6, 3.4, 2.4, 0.2, math.pi - 0.2, 12), EYE, 1.05)


# ---------------------------------------------------------------------------
# かざり
# ---------------------------------------------------------------------------
ACCESSORIES = [
    ("none", ""), ("ribbon", "リボンの"), ("flower", "おはなの"), ("crown", "おうさまの"), ("party", "パーティーの"),
    ("beret", "ベレーぼうの"), ("glasses", "めがねの"), ("headphones", "ヘッドホンの"), ("bowtie", "ちょうネクタイの"),
    ("leaf", "はっぱの"), ("starclip", "ほしの"), ("note", "おんぷの"), ("sakura", "さくらの"),
]
ACC_COLORS = [hexc("#ff7aa2"), hexc("#6fb8ff"), hexc("#ffcf4a"), hexc("#8fd88f"), hexc("#b597ff"), hexc("#ff9a5c")]


def draw_accessory(f, acc, color):
    d = f.d
    if acc == "none":
        return
    ex, ey = f.ear
    if acc == "ribbon":
        g = d.group()
        for side in (-1, 1):
            g.add(round_poly([(ex, ey), (ex + side * 9, ey - 6), (ex + side * 9, ey + 6)], 0.3, 6), color)
        g.ellipse(ex, ey, 2.8, 2.8, shade(color, 0.85))
    elif acc in ("flower", "sakura"):
        g = d.group()
        petal = hexc("#ffc2d6") if acc == "sakura" else color
        for i in range(5):
            a = i / 5 * math.tau - math.pi / 2
            px, py = ex + math.cos(a) * 4.4, ey + math.sin(a) * 4.4
            if acc == "sakura":
                g.add(heart_pts(px, py, 3.4) if False else ellipse_pts(px, py, 3.6, 2.8, a), petal)
            else:
                g.ellipse(px, py, 3.4, 3.4, petal)
        g.ellipse(ex, ey, 2.6, 2.6, hexc("#ffe066") if acc == "flower" else hexc("#ff7fa8"))
    elif acc == "starclip":
        g = d.group()
        g.add(rounded_star_pts(ex, ey, 6.5, 3, 5), hexc("#ffd23c"))
    elif acc == "crown":
        y = f.top + 1
        g = d.group()
        g.add(round_poly([(38, y), (38, y - 11), (44, y - 5), (50, y - 14), (56, y - 5), (62, y - 11), (62, y)], 0.15, 4), GOLD)
        g2 = d.group(None)
        g2.ellipse(50, y - 4.5, 2, 2, hexc("#ff6f95"))
        g2.ellipse(42, y - 3.5, 1.4, 1.4, hexc("#6fb8ff"))
        g2.ellipse(58, y - 3.5, 1.4, 1.4, hexc("#8fd88f"))
    elif acc == "party":
        y = f.top + 2
        g = d.group()
        pts = [(52, y), (68, y - 3), (63, y - 20)]
        g.add(round_poly(pts, 0.12, 4), color)
        g.ellipse(63, y - 20, 3.2, 3.2, hexc("#ffffff"))
        g2 = d.group(None)
        for t in (0.35, 0.65):
            a = (52 + (63 - 52) * t, y + (y - 20 - y) * t)
            b = (68 + (63 - 68) * t, y - 3 + (y - 20 - y + 3) * t)
            g2.add([(a[0], a[1]), (b[0], b[1]), (b[0], b[1] - 2.2), (a[0], a[1] - 2.2)], hexc("#ffffff"))
    elif acc == "beret":
        y = f.top + 3
        g = d.group()
        g.ellipse(50, y - 2, 17, 6.5, color, -0.12)
        g.ellipse(50, y - 9, 1.8, 2.6, shade(color, 0.85))
    elif acc == "glasses":
        y = f.eye_y
        r = max(f.eye_r * 1.9, 5.2)
        for side in (-1, 1):
            d.stroke(ellipse_pts(50 + side * f.eye_dx, y, r, r * 0.92, 0, 40) + [ellipse_pts(50 + side * f.eye_dx, y, r, r * 0.92, 0, 40)[0]], hexc("#5a4e6a"), 1.3)
        d.stroke(arc(50, y - 0.5, f.eye_dx - r, 2, math.pi * 1.1, math.pi * 1.9, 8), hexc("#5a4e6a"), 1.2)
    elif acc == "headphones":
        y = f.eye_y
        d.stroke(arc(50, y, f.side + 4, (y - f.top) + 7, math.pi * 1.05, math.pi * 1.95, 30), shade(color, 0.85), 2.6)
        g = d.group()
        for side in (-1, 1):
            g.add(rounded_rect_pts(50 + side * (f.side + 4) - 4, y - 7, 50 + side * (f.side + 4) + 4, y + 7, 3.5), color)
    elif acc == "bowtie":
        y = f.chin + 2
        g = d.group()
        for side in (-1, 1):
            g.add(round_poly([(50, y), (50 + side * 9, y - 5.5), (50 + side * 9, y + 5.5)], 0.25, 5), color)
        g.ellipse(50, y, 2.6, 2.6, shade(color, 0.85))
    elif acc == "leaf":
        y = f.top + 1
        g = d.group()
        g.add(bezier((50, y + 1), (40, y - 6), (46, y - 16), (54, y - 16), 12) + bezier((54, y - 16), (58, y - 10), (58, y - 3), (50, y + 1), 12), hexc("#7fcf6a"))
        d.stroke(quad((51, y), (51.5, y - 8), (53, y - 14), 8), hexc("#4f9a46"), 0.8)
    elif acc == "note":
        x, y = 80, 40
        g = d.group()
        g.ellipse(x, y + 10, 4.2, 3.2, color, -0.4)
        thick(g, [(x + 3.4, y + 9.5), (x + 3.4, y - 6)], 1.8, color)
        g.add(bezier((x + 3.4, y - 6), (x + 9, y - 3), (x + 10, y + 1), (x + 7, y + 4), 8) + [(x + 6.5, y + 2), (x + 3.4, y - 2.5)], color)


# ---------------------------------------------------------------------------
# わく（スタンプの形）
# ---------------------------------------------------------------------------
FRAMES = ["circle", "scallop", "flower", "star", "rsquare", "heart", "cloud", "hexagon"]
FRAME_COLORS = [hexc("#ffb3c8"), hexc("#a8d8ff"), hexc("#ffe08a"), hexc("#b5e8b0"), hexc("#d4c2ff"), hexc("#ffc9a0"),
                hexc("#9fe3dc"), hexc("#ffc2e8"), hexc("#c8e68a"), hexc("#b8c8ff")]
LABELS = ["よくできました", "すごいね！", "がんばったね", "ピアノ だいすき", "じょうず！", "はなまる", "にこにこ", "ありがとう", "またね", "やったね！"]


def frame_pts(kind, cx, cy, r):
    if kind == "circle":
        return ellipse_pts(cx, cy, r, r, 0, 120)
    if kind == "scallop":
        return scallop_pts(cx, cy, r * 0.93, 16, r * 0.07)
    if kind == "flower":
        return scallop_pts(cx, cy, r * 0.84, 8, r * 0.16)
    if kind == "star":
        return rounded_star_pts(cx, cy, r * 1.05, r * 0.8, 12, -math.pi / 2)
    if kind == "rsquare":
        return rounded_rect_pts(cx - r * 0.9, cy - r * 0.9, cx + r * 0.9, cy + r * 0.9, r * 0.3)
    if kind == "heart":
        return heart_pts(cx, cy + r * 0.05, r * 1.02)
    if kind == "cloud":
        pts = []
        for i in range(240):
            t = i / 240 * math.tau
            rr = r * (0.86 + 0.09 * abs(math.sin(3.5 * t)))
            pts.append((cx + math.cos(t) * rr, cy + math.sin(t) * rr * 0.92))
        return pts
    if kind == "hexagon":
        return round_poly([(cx + math.cos(a) * r, cy + math.sin(a) * r) for a in np.linspace(-math.pi / 2, 1.5 * math.pi, 7)[:-1]], 0.2, 6)
    raise ValueError(kind)


def find_font():
    for p in FONT_CANDIDATES:
        if os.path.exists(p):
            return p
    return None


# ---------------------------------------------------------------------------
# スタンプの組み合わせ（365 こ、ぜんぶちがう）
# ---------------------------------------------------------------------------
def plan(count=COUNT, seed=20261031):
    rng = random.Random(seed)
    used = set()
    out = []
    species = SPECIES_IDS
    for i in range(count):
        no = i + 1
        big = no % BIG_EVERY == 0
        sp = species[i % len(species)]
        # 同じ どうぶつでも、かお・かざり・いろ を かえる
        for _ in range(500):
            expr = EXPRESSIONS[rng.randrange(len(EXPRESSIONS))][0]
            if big:
                acc = rng.choice(["crown", "party", "note", "ribbon", "flower", "headphones", "bowtie", "starclip"])
            else:
                acc = ACCESSORIES[rng.randrange(len(ACCESSORIES))][0]
            key = (sp, expr, acc)
            if key not in used:
                used.add(key)
                break
        variant = (i // len(species)) + rng.randrange(3)
        frame = FRAMES[(i * 3 + i // len(FRAMES)) % len(FRAMES)]
        color = FRAME_COLORS[(i * 7) % len(FRAME_COLORS)]
        acc_color = ACC_COLORS[(i * 5 + 1) % len(ACC_COLORS)]
        label = None
        if big:
            label = f"{no}にちめ"
        elif rng.random() < 0.3:
            label = LABELS[rng.randrange(len(LABELS))]
        sp_name = next(s[1] for s in SPECIES if s[0] == sp)
        expr_name = next(e[1] for e in EXPRESSIONS if e[0] == expr)
        acc_name = next(a[1] for a in ACCESSORIES if a[0] == acc)
        name = f"{expr_name} {acc_name}{sp_name}" if acc != "none" else f"{expr_name} {sp_name}"
        if big:
            name = f"{no}にちめの {acc_name}{sp_name}"
        out.append(dict(no=no, big=big, species=sp, animal=sp_name, expr=expr, acc=acc, variant=variant,
                        frame=frame, color=color, acc_color=acc_color, label=label, name=name))
    return out


def build_face(species_id, variant, expr, acc, acc_color):
    f = Face()
    fn = next(s[2] for s in SPECIES if s[0] == species_id)
    fn(f, variant)
    draw_features(f, expr)
    draw_accessory(f, acc, acc_color)
    return f


def fit_face(f, box):
    """どうぶつの絵を、box（x0, y0, x1, y1）に入る大きさと位置にする。"""
    x0, y0, x1, y1 = f.d.bounds()
    bw, bh = x1 - x0, y1 - y0
    s = min((box[2] - box[0]) / bw, (box[3] - box[1]) / bh)
    ox = (box[0] + box[2]) / 2 - (x0 + x1) / 2 * s
    oy = (box[1] + box[3]) / 2 - (y0 + y1) / 2 * s
    return s, ox, oy


def ribbon(cv, text, font, y, color, width=62, height=13):
    """スタンプの下の リボン（文字入り）。"""
    x0, x1 = 50 - width / 2, 50 + width / 2
    g = Group()
    dark = shade(color, 0.62)
    for side in (-1, 1):
        ex = x0 if side < 0 else x1
        g.add([(ex - side * 2, y - height * 0.32), (ex + side * 7, y - height * 0.32), (ex + side * 4, y + height * 0.12),
               (ex + side * 7, y + height * 0.55), (ex - side * 2, y + height * 0.55)], shade(color, 0.85))
    g.add(round_poly([(x0, y - height / 2), (x1, y - height / 2), (x1, y + height / 2), (x0, y + height / 2)], 0.08, 4), color)
    g.draw(cv, dark, 1.2)
    if font:
        size = height * 0.62 if len(text) <= 6 else height * 0.62 * 6 / len(text) * 1.15
        cv.text(50, y + 0.3, text, font, size, WHITE, stroke=0.9, stroke_fill=dark)


def sparkle(g, x, y, r, color):
    g.add(round_poly(star_pts(x, y, r, r * 0.28, 4, 0), 0.2, 4), color)


def render_stamp(p, size=None):
    big = p["big"]
    size = size or (BIG_SIZE if big else SIZE)
    cv = Canvas(size)
    font = find_font()
    color = p["color"]
    dark = shade(color, 0.6)
    milestone = p["no"] in (100, 200, 300, 365)

    if big:
        # 金の ぎざぎざの わくと、ひかり
        rays = Group()
        for i in range(16):
            a = i / 16 * math.tau
            rays.add([(50, 50), (50 + math.cos(a - 0.09) * 50, 50 + math.sin(a - 0.09) * 50), (50 + math.cos(a + 0.09) * 50, 50 + math.sin(a + 0.09) * 50)],
                     (255, 236, 160, 150))
        rays.draw(cv)
        ring = Group()
        if milestone:
            for k, c in enumerate([hexc("#ff8fb1"), hexc("#ffd45e"), hexc("#8fe0a0"), hexc("#8fc8ff"), hexc("#c3a8ff")]):
                ring.add(scallop_pts(50, 50, 47 - k * 1.6, 20, 2.4), c)
        else:
            ring.add(scallop_pts(50, 50, 46, 20, 2.6), GOLD)
        ring.draw(cv, GOLD_DARK, 1.4)
        base_r = 39
    else:
        base_r = 46

    base = Group()
    base.add(frame_pts(p["frame"] if not big else "circle", 50, 50, base_r), color)
    base.draw(cv, dark, 1.6)
    inner_r = base_r * 0.8
    kind = p["frame"] if not big else "circle"
    cream = Group()
    if kind in ("heart", "rsquare"):
        # 中の白い ところも、わくと同じ形にする（丸だと はみだす）
        cream.add(frame_pts(kind, 50, 50 + (2.2 if kind == "heart" else 0), base_r * 0.8), hexc("#fffdf6"))
        inner_r = base_r * (0.72 if kind == "heart" else 0.8)
    else:
        cream.add(ellipse_pts(50, 50, inner_r, inner_r, 0, 100), hexc("#fffdf6"))
    cream.draw(cv)
    # まわりの 白い てんてん（わくの ふちに そって）
    dots = Group()
    ring = frame_pts(kind, 50, 50 + (1.1 if kind == "heart" else 0), base_r * 0.9)
    n = 22 if not big else 18
    step = len(ring) / n
    for i in range(n):
        x, y = ring[int(i * step) % len(ring)]
        dots.ellipse(x, y, 1.15, 1.15, (255, 255, 255, 230))
    dots.draw(cv)

    f = build_face(p["species"], p["variant"], p["expr"], p["acc"], p["acc_color"])
    has_label = p["label"] is not None
    r = inner_r * 0.93
    box = (50 - r * 0.86, 50 - r * 0.92, 50 + r * 0.86, 50 + (r * 0.62 if has_label else r * 0.86))
    s, ox, oy = fit_face(f, box)
    f.d.render(cv, s, ox, oy)

    if big:
        sp = Group()
        for x, y, rr in ((14, 20, 4.5), (86, 18, 4), (88, 78, 3.5), (12, 80, 3.2), (50, 6, 3)):
            sparkle(sp, x, y, rr, WHITE)
        sp.draw(cv, GOLD_DARK, 0.7)
    if has_label:
        ribbon(cv, p["label"], font, 50 + inner_r * 0.82, shade(color, 0.85) if not big else hexc("#ff7aa2"),
               width=62 if not big else 66, height=12.5 if not big else 14)
    img = cv.result()
    return ink(img, p["no"])


def ink(img, seed):
    """ゴムのスタンプらしく、インクの ムラを ほんの少し入れる。"""
    a = np.asarray(img).astype(np.float32)
    rng = np.random.default_rng(seed)
    h, w = a.shape[:2]
    noise = rng.random((h // 3 + 1, w // 3 + 1))
    noise = np.kron(noise, np.ones((3, 3)))[:h, :w]
    mask = (noise > 0.965).astype(np.float32) * 0.22
    a[..., :3] = a[..., :3] * (1 - mask[..., None]) + 255 * mask[..., None]
    return Image.fromarray(np.clip(a, 0, 255).astype(np.uint8), "RGBA")


def save_png(img, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    q = img.quantize(colors=255, method=Image.Quantize.FASTOCTREE, dither=Image.Dither.NONE)
    q.save(path, optimize=True)


def render_character(species_id, size=320, expr="smile", variant=0, acc="none", acc_color=None):
    """UI で使う どうぶつの絵（わくなし・すきとおった はいけい）。"""
    f = build_face(species_id, variant, expr, acc, acc_color or ACC_COLORS[0])
    cv = Canvas(size)
    s, ox, oy = fit_face(f, (6, 6, 94, 94))
    f.d.render(cv, s, ox, oy)
    return cv.result()


CHARACTERS = {
    # UI の名前: (どうぶつ, かお, いろちがい, かざり)
    "cat": ("cat", "smile", 0, "none"), "rabbit": ("rabbit", "happy", 0, "ribbon"), "bear": ("bear", "smile", 0, "none"),
    "panda": ("panda", "happy", 0, "none"), "frog": ("frog", "happy", 0, "none"), "chick": ("chick", "smile", 0, "none"),
    "owl": ("owl", "sparkle", 0, "none"), "sheep": ("sheep", "calm", 0, "none"), "dog": ("dog", "happy", 0, "none"),
    "lion": ("lion", "smile", 0, "crown"), "duck": ("duck", "smile", 0, "none"), "penguin": ("penguin", "happy", 0, "none"),
    "monkey": ("monkey", "smile", 0, "none"), "elephant": ("elephant", "smile", 0, "none"), "bird": ("bird", "smile", 0, "none"),
    "fox": ("fox", "smile", 0, "none"), "pig": ("pig", "happy", 0, "none"), "koala": ("koala", "calm", 0, "none"),
}


def contact_sheet(images, cols=20, cell=96, pad=6, bg=(255, 250, 240, 255)):
    rows = (len(images) + cols - 1) // cols
    sheet = Image.new("RGBA", (cols * (cell + pad) + pad, rows * (cell + pad) + pad), bg)
    for i, im in enumerate(images):
        t = im.convert("RGBA").resize((cell, cell), Image.LANCZOS)
        x = pad + (i % cols) * (cell + pad)
        y = pad + (i // cols) * (cell + pad)
        sheet.alpha_composite(t, (x, y))
    return sheet


def main(argv):
    preview_only = "--preview" in argv
    plans = plan()
    images = []
    if not preview_only:
        os.makedirs(OUT_STAMPS, exist_ok=True)
        for f in os.listdir(OUT_STAMPS):
            if f.startswith("stamp_") and f.endswith(".png"):
                os.remove(os.path.join(OUT_STAMPS, f))
    for p in plans:
        img = render_stamp(p)
        images.append(img)
        if not preview_only:
            save_png(img, os.path.join(OUT_STAMPS, f"stamp_{p['no']:03d}.png"))
    if not preview_only:
        os.makedirs(os.path.dirname(OUT_JSON), exist_ok=True)
        with open(OUT_JSON, "w", encoding="utf-8") as fp:
            json.dump({"stamps": [{"no": p["no"], "name": p["name"], "animal": p["animal"], "big": p["big"]} for p in plans]},
                      fp, ensure_ascii=False, indent=1)
        os.makedirs(OUT_CHARS, exist_ok=True)
        for name, (sp, expr, variant, acc) in CHARACTERS.items():
            render_character(sp, 320, expr, variant, acc).save(os.path.join(OUT_CHARS, f"{name}.png"), optimize=True)
        print("wrote", len(plans), "stamps,", len(CHARACTERS), "characters")
    os.makedirs(OUT_PREVIEW, exist_ok=True)
    contact_sheet(images).save(os.path.join(OUT_PREVIEW, "stamps_sheet.png"))
    print("wrote preview", os.path.join(OUT_PREVIEW, "stamps_sheet.png"))


if __name__ == "__main__":
    main(sys.argv[1:])
