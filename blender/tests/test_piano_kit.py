"""
build_piano_kit.py の単体テスト（Blender の中で動かす）。
  blender -b --factory-startup --python-exit-code 1 --python blender/tests/test_piano_kit.py
"""
import math
import os
import sys
import tempfile
import unittest

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "scripts"))
import build_piano_kit as kit  # noqa: E402


def bounds(ob):
    xs = [v.co.x for v in ob.data.vertices]
    ys = [v.co.y for v in ob.data.vertices]
    zs = [v.co.z for v in ob.data.vertices]
    return (min(xs), min(ys), min(zs)), (max(xs), max(ys), max(zs))


class PianoKitTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.objs = {o.name: o for o in kit.build_all()}

    def test_all_parts(self):
        for n in ("Key_White_Full", "Key_White_L", "Key_White_R", "Key_White_LR", "Key_Black",
                  "Case_End_L", "Case_End_R", "Case_Back", "Case_Front", "Case_Felt", "Case_Bed"):
            self.assertIn(n, self.objs)

    def test_white_key_size(self):
        lo, hi = bounds(self.objs["Key_White_Full"])
        self.assertAlmostEqual(hi[0] - lo[0], kit.WHITE_W, places=3)
        self.assertAlmostEqual(lo[1], 0.0, places=3)
        self.assertAlmostEqual(hi[1], kit.WHITE_L, places=3)
        self.assertAlmostEqual(hi[2], 0.0, places=3)
        self.assertAlmostEqual(lo[2], -kit.WHITE_H, places=3)

    def test_notches_are_on_the_right_side(self):
        # 奥のほう（切りかきのところ）だけを見て、はばが へっている側をしらべる
        def back_extent(name):
            ob = self.objs[name]
            back = [v.co for v in ob.data.vertices if v.co.y > kit.NOTCH_Y + 0.3]
            return min(c.x for c in back), max(c.x for c in back)
        hw = kit.WHITE_W / 2
        inner = kit.PITCH / 2 - kit.NOTCH_HALF
        l = back_extent("Key_White_L")
        r = back_extent("Key_White_R")
        b = back_extent("Key_White_LR")
        self.assertAlmostEqual(l[0], -inner, places=2)
        self.assertAlmostEqual(l[1], hw, places=2)
        self.assertAlmostEqual(r[0], -hw, places=2)
        self.assertAlmostEqual(r[1], inner, places=2)
        self.assertAlmostEqual(b[0], -inner, places=2)
        self.assertAlmostEqual(b[1], inner, places=2)

    def test_black_key_fits_in_the_notch(self):
        lo, hi = bounds(self.objs["Key_Black"])
        self.assertLessEqual(hi[0] - lo[0], 2 * kit.NOTCH_HALF)
        self.assertGreaterEqual(lo[1], kit.NOTCH_Y - 0.01)
        self.assertAlmostEqual(hi[2], kit.BLACK_H, places=3)

    def test_case_segments_are_unit_length(self):
        for n in ("Case_Back", "Case_Front", "Case_Felt", "Case_Bed"):
            lo, hi = bounds(self.objs[n])
            self.assertAlmostEqual(lo[0], 0.0, places=4, msg=n)
            self.assertAlmostEqual(hi[0], 1.0, places=4, msg=n)

    def test_case_ends_touch_the_keys(self):
        lo, hi = bounds(self.objs["Case_End_L"])
        self.assertAlmostEqual(hi[0], 0.0, places=3)
        lo, hi = bounds(self.objs["Case_End_R"])
        self.assertAlmostEqual(lo[0], 0.0, places=3)

    def test_normals_point_outward(self):
        # とじた形の面が 外を向いていれば、面から計算した体積がプラスになる（へこんだ形でも使える）
        for n in ("Key_White_Full", "Key_White_L", "Key_White_R", "Key_White_LR", "Key_Black", "Case_End_L", "Case_End_R"):
            me = self.objs[n].data
            vol = 0.0
            for p in me.polygons:
                vs = [me.vertices[i].co for i in p.vertices]
                for k in range(1, len(vs) - 1):
                    vol += vs[0].dot(vs[k].cross(vs[k + 1])) / 6.0
            lo, hi = bounds(self.objs[n])
            box = (hi[0] - lo[0]) * (hi[1] - lo[1]) * (hi[2] - lo[2])
            self.assertGreater(vol, box * 0.5, n)

    def test_export_puts_keys_forward_for_unity(self):
        # 書き出しのとき Z まわりに 180 度まわし、書き出したあと もとにもどす
        ob = self.objs["Key_White_R"]
        before = [v.co.copy() for v in ob.data.vertices]
        d = tempfile.mkdtemp()
        kit.export_fbx(ob, d)
        self.assertTrue(os.path.exists(os.path.join(d, "Key_White_R.fbx")))
        after = [v.co for v in ob.data.vertices]
        self.assertTrue(all((a - b).length < 1e-5 for a, b in zip(before, after)))


if __name__ == "__main__":
    result = unittest.main(argv=["test"], exit=False).result
    sys.exit(0 if result.wasSuccessful() else 1)
