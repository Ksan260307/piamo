"""
piano_kit.blend の 1 オクターブの鍵盤を、アプリと同じくらいの向きから描いた確認用の絵を作る。
  blender -b blender/piano_kit.blend --python blender/scripts/render_preview.py -- [out.png]
"""
import math
import os
import sys

import bpy
from mathutils import Vector

a = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
here = os.path.dirname(os.path.abspath(__file__))
out = a[0] if a else os.path.join(here, "..", "previews", "piano_kit.png")
os.makedirs(os.path.dirname(os.path.abspath(out)), exist_ok=True)

scene = bpy.context.scene
colors = {
    "Key_White": (0.97, 0.96, 0.93, 1),
    "Key_Black": (0.13, 0.13, 0.2, 1),
    "Case_Felt": (0.8, 0.15, 0.2, 1),
    "Case_Bed": (0.2, 0.15, 0.2, 1),
    "Case": (0.98, 0.62, 0.72, 1),
}
for ob in scene.objects:
    if ob.type != "MESH":
        continue
    key = next((k for k in colors if ob.name.startswith(k)), "Case")
    ob.color = colors[key]

scene.render.engine = "BLENDER_WORKBENCH"
shading = scene.display.shading
shading.light = "STUDIO"
shading.color_type = "OBJECT"
shading.show_specular_highlight = True
shading.show_cavity = True
scene.render.resolution_x = 1280
scene.render.resolution_y = 720
scene.render.film_transparent = False
scene.world = scene.world or bpy.data.worlds.new("World")

cam_data = bpy.data.cameras.new("Cam")
cam_data.type = "ORTHO"
cam_data.ortho_scale = 10.5
cam = bpy.data.objects.new("Cam", cam_data)
scene.collection.objects.link(cam)
target = Vector((3.5, 2.6, 0.0))
tilt = math.radians(40)
cam.location = target + Vector((0, -math.cos(tilt), math.sin(tilt))) * 20
cam.rotation_euler = (math.pi / 2 - tilt, 0, 0)
scene.camera = cam
scene.render.filepath = os.path.abspath(out)
bpy.ops.render.render(write_still=True)
print("wrote", os.path.abspath(out))
