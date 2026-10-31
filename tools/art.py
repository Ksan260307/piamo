"""
2D の絵を描く道具（PIL）。スタンプ・どうぶつ・アイコンで共通に使う。
 - 4 倍の大きさで描いてから縮めて、ふちをなめらかにする
 - 座標は「デザインの単位」（ふつうは 0〜100）で書き、Canvas が画素へ変換する
 - シール風のふちどり：形のまとまり（Group）を、まず ふとらせて ふちの色でぬり、その上に中身をぬる
"""
import math

from PIL import Image, ImageDraw, ImageFont

SUPER = 4


def hexc(h, a=255):
    h = h.lstrip("#")
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a)


def mix(c1, c2, t):
    return tuple(int(round(c1[i] + (c2[i] - c1[i]) * t)) for i in range(4))


def shade(c, k):
    """k < 1 で こく、k > 1 で うすく（白へ）。"""
    if k <= 1:
        return (int(c[0] * k), int(c[1] * k), int(c[2] * k), c[3])
    t = min(1.0, k - 1)
    return mix(c, (255, 255, 255, c[3]), t)


def ellipse_pts(cx, cy, rx, ry, rot=0.0, n=72, a0=0.0, a1=math.tau):
    pts = []
    cr, sr = math.cos(rot), math.sin(rot)
    full = abs(a1 - a0 - math.tau) < 1e-6
    count = n if full else max(2, int(n * abs(a1 - a0) / math.tau) + 1)
    for i in range(count):
        t = a0 + (a1 - a0) * i / (count if full else count - 1)
        x, y = math.cos(t) * rx, math.sin(t) * ry
        pts.append((cx + x * cr - y * sr, cy + x * sr + y * cr))
    return pts


def bezier(p0, p1, p2, p3, n=20):
    pts = []
    for i in range(n + 1):
        t = i / n
        u = 1 - t
        x = u * u * u * p0[0] + 3 * u * u * t * p1[0] + 3 * u * t * t * p2[0] + t * t * t * p3[0]
        y = u * u * u * p0[1] + 3 * u * u * t * p1[1] + 3 * u * t * t * p2[1] + t * t * t * p3[1]
        pts.append((x, y))
    return pts


def quad(p0, p1, p2, n=16):
    pts = []
    for i in range(n + 1):
        t = i / n
        u = 1 - t
        pts.append((u * u * p0[0] + 2 * u * t * p1[0] + t * t * p2[0], u * u * p0[1] + 2 * u * t * p1[1] + t * t * p2[1]))
    return pts


def star_pts(cx, cy, r_out, r_in, points=5, rot=-math.pi / 2):
    pts = []
    for i in range(points * 2):
        r = r_out if i % 2 == 0 else r_in
        t = rot + i * math.pi / points
        pts.append((cx + math.cos(t) * r, cy + math.sin(t) * r))
    return pts


def rounded_star_pts(cx, cy, r_out, r_in, points=5, rot=-math.pi / 2, n=8):
    """角の丸い星（とがりを少しまるめる）。"""
    raw = star_pts(cx, cy, r_out, r_in, points, rot)
    return round_poly(raw, 0.22, n)


def round_poly(pts, k=0.25, n=6):
    """多角形の角を、となりの辺の k の割合で 2 次ベジェにまるめる。"""
    out = []
    m = len(pts)
    for i in range(m):
        p = pts[i]
        a = pts[i - 1]
        b = pts[(i + 1) % m]
        p0 = (p[0] + (a[0] - p[0]) * k, p[1] + (a[1] - p[1]) * k)
        p2 = (p[0] + (b[0] - p[0]) * k, p[1] + (b[1] - p[1]) * k)
        out.extend(quad(p0, p, p2, n))
    return out


def heart_pts(cx, cy, size, n=80):
    pts = []
    for i in range(n):
        t = i / n * math.tau
        x = 16 * math.sin(t) ** 3
        y = 13 * math.cos(t) - 5 * math.cos(2 * t) - 2 * math.cos(3 * t) - math.cos(4 * t)
        pts.append((cx + x * size / 17, cy - y * size / 17))
    return pts


def scallop_pts(cx, cy, r, bumps, depth, n=360, rot=0.0):
    pts = []
    for i in range(n):
        t = i / n * math.tau
        rr = r + depth * abs(math.cos(bumps * (t + rot) / 2))
        pts.append((cx + math.cos(t) * rr, cy + math.sin(t) * rr))
    return pts


def rounded_rect_pts(x0, y0, x1, y1, r, n=10):
    pts = []
    r = min(r, (x1 - x0) / 2, (y1 - y0) / 2)
    corners = [(x1 - r, y0 + r, -math.pi / 2, 0), (x1 - r, y1 - r, 0, math.pi / 2),
               (x0 + r, y1 - r, math.pi / 2, math.pi), (x0 + r, y0 + r, math.pi, math.pi * 1.5)]
    for cx, cy, a0, a1 in corners:
        for i in range(n + 1):
            t = a0 + (a1 - a0) * i / n
            pts.append((cx + math.cos(t) * r, cy + math.sin(t) * r))
    return pts


def transform(pts, s=1.0, ox=0.0, oy=0.0, rot=0.0, cx=0.0, cy=0.0):
    """点を (cx, cy) を中心に rot 回して、s 倍して、(ox, oy) ずらす。"""
    cr, sr = math.cos(rot), math.sin(rot)
    out = []
    for x, y in pts:
        x, y = x - cx, y - cy
        x, y = x * cr - y * sr, x * sr + y * cr
        out.append((ox + (x + cx) * s, oy + (y + cy) * s))
    return out


class Canvas:
    """size × size（または w × h）画素の絵。座標は units 単位（既定 100）で、そのまま画素に のびる。"""

    def __init__(self, w, h=None, units=100.0, bg=(0, 0, 0, 0)):
        self.w = w
        self.h = h or w
        self.units = units
        self.k = self.w * SUPER / units
        self.img = Image.new("RGBA", (self.w * SUPER, self.h * SUPER), bg)
        self.d = ImageDraw.Draw(self.img)

    def P(self, pts):
        return [(x * self.k, y * self.k) for x, y in pts]

    def poly(self, pts, fill):
        if len(pts) >= 3:
            self.d.polygon(self.P(pts), fill=fill)

    def stroke(self, pts, color, width, closed=False):
        if len(pts) < 2:
            return
        p = self.P(pts)
        if closed:
            p = p + [p[0], p[1]]
        w = max(1, int(round(width * self.k)))
        self.d.line(p, fill=color, width=w, joint="curve")
        r = width * self.k / 2
        for x, y in (p[0], p[-1]):
            self.d.ellipse([x - r, y - r, x + r, y + r], fill=color)

    def ellipse(self, cx, cy, rx, ry, fill):
        k = self.k
        self.d.ellipse([(cx - rx) * k, (cy - ry) * k, (cx + rx) * k, (cy + ry) * k], fill=fill)

    def dot(self, x, y, r, fill):
        self.ellipse(x, y, r, r, fill)

    def text(self, x, y, s, font_path, size, fill, stroke=0.0, stroke_fill=None, anchor="mm"):
        font = ImageFont.truetype(font_path, max(1, int(size * self.k)))
        self.d.text((x * self.k, y * self.k), s, font=font, fill=fill, anchor=anchor,
                    stroke_width=int(round(stroke * self.k)), stroke_fill=stroke_fill)

    def result(self):
        return self.img.resize((self.w, self.h), Image.LANCZOS)


class Shape:
    def __init__(self, pts, fill, line=None):
        self.pts = pts
        self.fill = fill
        self.line = line   # (色, はば) 中の線（ふちの外がわではなく、形の上に引く線）


class Group:
    """ひとまとまりの形。ふちどり（outline）は、まとまり全体の まわりだけに つく。"""

    def __init__(self):
        self.shapes = []

    def add(self, pts, fill, line=None):
        self.shapes.append(Shape(pts, fill, line))
        return self

    def ellipse(self, cx, cy, rx, ry, fill, rot=0.0, line=None):
        return self.add(ellipse_pts(cx, cy, rx, ry, rot), fill, line)

    def draw(self, cv, outline=None, width=0.0):
        if outline is not None and width > 0:
            for s in self.shapes:
                cv.poly(s.pts, outline)
                cv.stroke(s.pts, outline, width * 2, closed=True)
        for s in self.shapes:
            cv.poly(s.pts, s.fill)
            if s.line is not None:
                cv.stroke(s.pts, s.line[0], s.line[1], closed=True)
