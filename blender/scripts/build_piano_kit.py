"""
Piamo — ピアノの鍵盤キット生成スクリプト

白鍵（くろけんの切りかきの形 4 種）・黒鍵・ピアノのケース（左右のそで・おくの板・前のふち・
赤いフェルト・鍵盤の下の台）を、プロシージャルに作り、Unity 用の FBX と編集用の .blend を書き出します。
色は Unity のマテリアルでつけます（頂点カラー Col には、すき間の暗さだけを入れる）。

使い方:
  blender -b --factory-startup --python blender/scripts/build_piano_kit.py -- [fbx_out_dir] [blend_out]

座標（Blender）: X = 鍵盤のならび（右へ +）、Y = おく（奥へ +）、Z = 上
単位: 1 = 白鍵 1 本ぶんのはば（白鍵の中心の間かく）
  - 白鍵は X の中心が 0、前のはしが Y = 0、上の面が Z = 0
  - 黒鍵は、となりあう白鍵のさかい目（X = 0）にのる
  - ケースの「おくの板・前のふち・フェルト・台」は X が 0〜1 の長さ 1 の部品で、Unity で鍵盤の数だけのばす
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

# ---------------------------------------------------------------------------
# 寸法（Unity の PianoDimensions と同じ値。変えたら両方なおす）
# ---------------------------------------------------------------------------
PITCH = 1.0            # 白鍵の中心の間かく
WHITE_W = 0.92         # 白鍵のはば（すき間 0.08）
WHITE_L = 4.6          # 白鍵の長さ（前から奥へ）
WHITE_H = 0.55         # 白鍵の厚み（上の面 Z=0 から下へ）
BLACK_W = 0.56         # 黒鍵の下のはば
BLACK_TOP_W = 0.42     # 黒鍵の上の面のはば（上がすぼまる）
BLACK_L = 2.8          # 黒鍵の長さ
BLACK_H = 0.42         # 黒鍵の、白鍵の上の面からの高さ
NOTCH_CLEAR = 0.05     # 黒鍵と白鍵の切りかきのすき間
NOTCH_Y = WHITE_L - BLACK_L - NOTCH_CLEAR          # 切りかきの始まる奥行き
NOTCH_HALF = BLACK_W / 2 + NOTCH_CLEAR             # さかい目から切りかきのはしまで

CASE_END_W = 0.8       # 左右のそでのはば
CASE_FRONT_Y = -0.55   # ケースの前のはし
CASE_BACK_Y0 = WHITE_L + 0.12   # おくの板の前の面
CASE_BACK_Y1 = 6.2     # ケースの奥のはし
CASE_BOTTOM = -1.05    # ケースの底
CASE_BACK_TOP = 1.25   # おくの板の上のはし
CASE_END_TOP = 0.75    # そでの上のはし


def parse_args():
    a = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    here = os.path.dirname(os.path.abspath(__file__))
    repo = os.path.abspath(os.path.join(here, "..", ".."))
    out = a[0] if len(a) > 0 else os.path.join(repo, "unity", "Assets", "Art", "Models")
    blend = a[1] if len(a) > 1 else os.path.join(repo, "blender", "piano_kit.blend")
    return out, blend


# ---------------------------------------------------------------------------
# 形を作る道具
# ---------------------------------------------------------------------------
def prism(bm, outline, z0, z1):
    """XY の輪郭（反時計まわり）を Z0 から Z1 まで押し出した柱を bm に加える。"""
    bottom = [bm.verts.new((x, y, z0)) for x, y in outline]
    top = [bm.verts.new((x, y, z1)) for x, y in outline]
    n = len(outline)
    bm.faces.new(list(reversed(bottom)))
    bm.faces.new(top)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new([bottom[i], bottom[j], top[j], top[i]])
    return bottom + top


def frustum(bm, lo, hi):
    """下の輪郭 lo（Z つき）と上の輪郭 hi をつないだ台形の柱（黒鍵）。どちらも反時計まわりの同じ点の数。"""
    bottom = [bm.verts.new(p) for p in lo]
    top = [bm.verts.new(p) for p in hi]
    n = len(lo)
    bm.faces.new(list(reversed(bottom)))
    bm.faces.new(top)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new([bottom[i], bottom[j], top[j], top[i]])


def bevel_all(bm, offset, segments=3):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bmesh.ops.bevel(bm, geom=list(bm.edges) + list(bm.verts), offset=offset, segments=segments,
                    profile=0.5, affect="EDGES", clamp_overlap=True)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)


def rounded_outline(points, radii, steps=6):
    """多角形の角を丸めた輪郭（2D）。radii は角ごとの半径。"""
    n = len(points)
    out = []
    for i in range(n):
        p = Vector(points[i])
        a = Vector(points[i - 1])
        b = Vector(points[(i + 1) % n])
        r = radii[i]
        if r <= 0:
            out.append((p.x, p.y))
            continue
        da = (a - p).normalized()
        db = (b - p).normalized()
        ang = math.acos(max(-1.0, min(1.0, da.dot(db))))
        dist = r / math.tan(ang / 2)
        dist = min(dist, (a - p).length * 0.5, (b - p).length * 0.5)
        r = dist * math.tan(ang / 2)
        p0 = p + da * dist
        p1 = p + db * dist
        bis = (da + db).normalized()
        c = p + bis * (r / math.sin(ang / 2))
        a0 = math.atan2(p0.y - c.y, p0.x - c.x)
        a1 = math.atan2(p1.y - c.y, p1.x - c.x)
        d = a1 - a0
        while d > math.pi:
            d -= math.tau
        while d < -math.pi:
            d += math.tau
        for k in range(steps + 1):
            t = a0 + d * k / steps
            out.append((c.x + math.cos(t) * r, c.y + math.sin(t) * r))
    return out


def sweep_x(bm, profile_yz, x0=0.0, x1=1.0, caps=True):
    """YZ の輪郭（反時計まわり：Y 右・Z 上で見て）を X 方向に x0 から x1 までのばした部品。"""
    left = [bm.verts.new((x0, y, z)) for y, z in profile_yz]
    right = [bm.verts.new((x1, y, z)) for y, z in profile_yz]
    n = len(profile_yz)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new([left[i], left[j], right[j], right[i]])
    if caps:
        bm.faces.new(list(reversed(left)))
        bm.faces.new(right)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)


def finish(bm, name, ao=(0.55, 1.0), sharp_deg=40.0):
    """bmesh をオブジェクトにする。頂点カラーに、下ほど暗い「すき間の陰」を入れる。"""
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.validate(clean_customdata=False)
    me.update(calc_edges=True)
    zs = [v.co.z for v in me.vertices]
    zmin, zmax = min(zs), max(zs)
    attr = me.color_attributes.new("Col", "BYTE_COLOR", "CORNER")
    cols = []
    for loop in me.loops:
        z = me.vertices[loop.vertex_index].co.z
        t = (z - zmin) / max(zmax - zmin, 1e-6)
        k = ao[0] + (ao[1] - ao[0]) * math.sqrt(max(0.0, t))
        cols.extend((k, k, k, 1.0))
    attr.data.foreach_set("color", cols)
    try:
        me.color_attributes.active_color = attr
        me.color_attributes.render_color_index = me.color_attributes.active_color_index
    except Exception:
        pass
    me.polygons.foreach_set("use_smooth", [True] * len(me.polygons))
    me.set_sharp_from_angle(angle=math.radians(sharp_deg))
    me.update()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


# ---------------------------------------------------------------------------
# 鍵盤
# ---------------------------------------------------------------------------
def white_outline(notch_left, notch_right):
    """白鍵の輪郭（上から見て反時計まわり）。くろけんのある側を、奥のほうだけ切りかく。"""
    hw = WHITE_W / 2
    inner = PITCH / 2 - NOTCH_HALF     # 切りかいたあとの、中心からのはば
    pts = [(-hw, 0.0), (hw, 0.0)]
    if notch_right:
        pts += [(hw, NOTCH_Y), (inner, NOTCH_Y), (inner, WHITE_L)]
    else:
        pts += [(hw, WHITE_L)]
    if notch_left:
        pts += [(-inner, WHITE_L), (-inner, NOTCH_Y), (-hw, NOTCH_Y)]
    else:
        pts += [(-hw, WHITE_L)]
    return pts


def make_white(name, notch_left, notch_right):
    bm = bmesh.new()
    prism(bm, white_outline(notch_left, notch_right), -WHITE_H, 0.0)
    bevel_all(bm, 0.055, 3)
    return finish(bm, name, ao=(0.62, 1.0))


def make_black():
    bm = bmesh.new()
    hw, tw = BLACK_W / 2, BLACK_TOP_W / 2
    y0 = WHITE_L - BLACK_L
    z0 = -0.12
    z1 = BLACK_H
    lo = [(-hw, y0, z0), (hw, y0, z0), (hw, WHITE_L, z0), (-hw, WHITE_L, z0)]
    # 上の面は、前のはしが少し奥へさがる（手前の面がななめ）
    hi = [(-tw, y0 + 0.16, z1), (tw, y0 + 0.16, z1), (tw, WHITE_L - 0.02, z1), (-tw, WHITE_L - 0.02, z1)]
    frustum(bm, lo, hi)
    bevel_all(bm, 0.045, 3)
    return finish(bm, "Key_Black", ao=(0.7, 1.0))


# ---------------------------------------------------------------------------
# ケース
# ---------------------------------------------------------------------------
def make_case_end(name, side):
    """左右のそで（side = -1 左 / +1 右）。鍵盤のはしにくっつく、丸いかどの箱。"""
    bm = bmesh.new()
    if side < 0:
        x0, x1 = -CASE_END_W, 0.0
    else:
        x0, x1 = 0.0, CASE_END_W
    outline = [(x0, CASE_FRONT_Y), (x1, CASE_FRONT_Y), (x1, CASE_BACK_Y1), (x0, CASE_BACK_Y1)]
    prism(bm, outline, CASE_BOTTOM, CASE_END_TOP)
    bevel_all(bm, 0.22, 5)
    return finish(bm, name, ao=(0.72, 1.0))


def make_case_back():
    """鍵盤の奥の板（名前の板）。前の面は少しうしろにたおれ、上のかどは丸い。X は 0〜1。"""
    y0, y1 = CASE_BACK_Y0, CASE_BACK_Y1
    prof = [
        (y0, -0.45),
        (y1, -0.45),
        (y1, CASE_BACK_TOP),
        (y0 + 0.35, CASE_BACK_TOP),
        (y0, 0.25),
    ]
    rad = [0.0, 0.0, 0.3, 0.35, 0.12]
    bm = bmesh.new()
    sweep_x(bm, rounded_outline(prof, rad, steps=6))
    return finish(bm, "Case_Back", ao=(0.8, 1.0), sharp_deg=50.0)


def make_case_front():
    """鍵盤の手前の下のふち。白鍵の前の面の下に見える、丸い帯。X は 0〜1。"""
    prof = [
        (CASE_FRONT_Y, CASE_BOTTOM),
        (0.6, CASE_BOTTOM),
        (0.6, -WHITE_H - 0.06),
        (CASE_FRONT_Y, -WHITE_H - 0.06),
    ]
    rad = [0.12, 0.0, 0.0, 0.22]
    bm = bmesh.new()
    sweep_x(bm, rounded_outline(prof, rad, steps=6))
    return finish(bm, "Case_Front", ao=(0.75, 1.0), sharp_deg=50.0)


def make_case_felt():
    """鍵盤の奥のはしと、おくの板のあいだの赤いフェルト。X は 0〜1。"""
    prof = [(WHITE_L - 0.05, -0.4), (CASE_BACK_Y0 + 0.02, -0.4), (CASE_BACK_Y0 + 0.02, 0.1), (WHITE_L - 0.05, 0.1)]
    bm = bmesh.new()
    sweep_x(bm, prof)
    return finish(bm, "Case_Felt", ao=(0.8, 1.0), sharp_deg=30.0)


def make_case_bed():
    """鍵盤の下の台（すき間から見える、暗い面）。X は 0〜1。"""
    prof = [(0.0, -WHITE_H - 0.3), (CASE_BACK_Y0, -WHITE_H - 0.3), (CASE_BACK_Y0, -WHITE_H - 0.08), (0.0, -WHITE_H - 0.08)]
    bm = bmesh.new()
    sweep_x(bm, prof)
    return finish(bm, "Case_Bed", ao=(1.0, 1.0), sharp_deg=30.0)


# ---------------------------------------------------------------------------
# 書き出し
# ---------------------------------------------------------------------------
def export_fbx(ob, out_dir):
    """FBX に書き出す。この書き出しの設定では Unity に (-X, Z, -Y) で入るので、
    Z まわりに 180 度まわしてから書き出し、Unity で (X, Z, Y)＝右・上・奥 になるようにする。"""
    for o in bpy.context.scene.objects:
        o.select_set(False)
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    path = os.path.join(out_dir, ob.name + ".fbx")
    flip = Matrix.Rotation(math.pi, 4, "Z")
    ob.data.transform(flip)
    try:
        _export(path)
    finally:
        ob.data.transform(flip)
    return path


def _export(path):
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={"MESH"},
        use_mesh_modifiers=True,
        mesh_smooth_type="OFF",
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z",
        axis_up="Y",
        bake_space_transform=True,
        colors_type="SRGB",
        prioritize_active_color=True,
        add_leaf_bones=False,
        use_tspace=False,
        embed_textures=False,
        path_mode="AUTO",
    )


MAKERS = [
    lambda: make_white("Key_White_Full", False, False),
    lambda: make_white("Key_White_L", True, False),
    lambda: make_white("Key_White_R", False, True),
    lambda: make_white("Key_White_LR", True, True),
    make_black,
    lambda: make_case_end("Case_End_L", -1),
    lambda: make_case_end("Case_End_R", +1),
    make_case_back,
    make_case_front,
    make_case_felt,
    make_case_bed,
]


def build_all():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1.0
    return [mk() for mk in MAKERS]


def arrange_preview(objs):
    """.blend では、1 オクターブ（ド〜ド）の鍵盤として組み立てて見られるようにする。"""
    names = {o.name: o for o in objs}
    pattern = ["Key_White_R", "Key_White_LR", "Key_White_L", "Key_White_R", "Key_White_LR", "Key_White_LR", "Key_White_L", "Key_White_Full"]
    for i, n in enumerate(pattern):
        src = names[n]
        ob = src if i == pattern.index(n) else bpy.data.objects.new(f"{n}.{i}", src.data)
        if ob is not src:
            bpy.context.scene.collection.objects.link(ob)
        ob.location = (i * PITCH, 0, 0)
    blacks = [0.5, 1.5, 3.5, 4.5, 5.5]
    for i, x in enumerate(blacks):
        src = names["Key_Black"]
        ob = src if i == 0 else bpy.data.objects.new(f"Key_Black.{i}", src.data)
        if ob is not src:
            bpy.context.scene.collection.objects.link(ob)
        ob.location = (x * PITCH, 0, 0)
    width = len(pattern) * PITCH
    for n in ("Case_Back", "Case_Front", "Case_Felt", "Case_Bed"):
        names[n].location = (-PITCH / 2, 0, 0)
        names[n].scale = (width, 1, 1)
    names["Case_End_L"].location = (-PITCH / 2, 0, 0)
    names["Case_End_R"].location = (width - PITCH / 2, 0, 0)


def main():
    out_dir, blend_out = parse_args()
    os.makedirs(out_dir, exist_ok=True)
    os.makedirs(os.path.dirname(blend_out), exist_ok=True)
    objs = build_all()
    for ob in objs:
        p = export_fbx(ob, out_dir)
        print("exported", ob.name, len(ob.data.vertices), "verts ->", os.path.basename(p))
    arrange_preview(objs)
    bpy.ops.wm.save_as_mainfile(filepath=blend_out, compress=True)
    print("saved", blend_out)


if __name__ == "__main__":
    main()
