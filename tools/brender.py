# Blender: OBJ'yi seffaf arka planla render et (buyuk on izleme + 256 ikon)
import bpy, sys, math, mathutils
from bpy_extras.object_utils import world_to_camera_view
argv = sys.argv[sys.argv.index('--') + 1:]
obj_path, out_big, out_icon = argv[0], argv[1], argv[2]
yaw = float(argv[3]) if len(argv) > 3 else 25.0
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.wm.obj_import(filepath=obj_path, forward_axis='NEGATIVE_Z', up_axis='Y')
objs = [o for o in bpy.context.scene.objects if o.type == 'MESH']
for o in objs:
    for m in o.data.materials:
        if not m or not m.use_nodes: continue
        bs = m.node_tree.nodes.get('Principled BSDF')
        if bs:
            bs.inputs['Roughness'].default_value = 0.55
            bs.inputs['Metallic'].default_value = 0.0
            bs.inputs['Specular IOR Level'].default_value = 0.3
            tex = [n for n in m.node_tree.nodes if n.type == 'TEX_IMAGE']
            if tex and tex[0].image and tex[0].image.filepath.lower().endswith('.png') and tex[0].image.channels == 4:
                m.node_tree.links.new(tex[0].outputs['Alpha'], bs.inputs['Alpha'])
                try: m.surface_render_method = 'DITHERED'
                except Exception: pass
        m.use_backface_culling = False
pts = [o.matrix_world @ mathutils.Vector(v) for o in objs for v in o.bound_box]
lo = mathutils.Vector([min(p[i] for p in pts) for i in range(3)]); hi = mathutils.Vector([max(p[i] for p in pts) for i in range(3)])
ctr = (lo + hi) / 2; size = hi - lo
k = max(size.x, size.y, size.z)
sc = bpy.context.scene
sc.render.engine = 'BLENDER_EEVEE_NEXT'
sc.render.film_transparent = True
sc.view_settings.view_transform = 'Standard'
world = bpy.data.worlds.new('w'); sc.world = world; world.use_nodes = True
world.node_tree.nodes['Background'].inputs[0].default_value = (0.6, 0.58, 0.65, 1)
world.node_tree.nodes['Background'].inputs[1].default_value = 0.8
cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera = cam
cam.data.lens = 70
yr = math.radians(yaw)
direction = mathutils.Vector((math.sin(yr), -math.cos(yr), 0.2)).normalized()
cam.location = ctr + direction * k * 4.0
cam.rotation_euler = (ctr - cam.location).to_track_quat('-Z', 'Y').to_euler()
cam.data.clip_end = k * 20; cam.data.clip_start = k * 0.01
def light(name, off, energy):
    l = bpy.data.objects.new(name, bpy.data.lights.new(name, 'AREA')); l.data.energy = energy; l.data.size = k
    l.location = ctr + mathutils.Vector(off) * k
    l.rotation_euler = (ctr - l.location).to_track_quat('-Z', 'Y').to_euler(); sc.collection.objects.link(l)
light('key', (-1.2, -1.6, 1.2), 280 * k * k)
light('fill', (1.5, -1.0, 0.4), 120 * k * k)
light('rim', (0.3, 1.8, 1.0), 180 * k * k)
sc.render.resolution_x = 1000; sc.render.resolution_y = 1000
# kadraj: modelin goruntu kutusu cercevenin %90'i olsun
for _ in range(40):
    bpy.context.view_layer.update()
    cs = [world_to_camera_view(sc, cam, p) for p in pts]
    xs = [c.x for c in cs]; ys = [c.y for c in cs]
    span = max(max(xs) - min(xs), max(ys) - min(ys))
    cam.data.shift_x += ((max(xs) + min(xs)) / 2 - 0.5)
    cam.data.shift_y += ((max(ys) + min(ys)) / 2 - 0.5)
    if abs(span - 0.92) < 0.005: break
    cam.data.lens *= 0.92 / span
sc.eevee.taa_render_samples = 32
sc.render.filepath = out_big
bpy.ops.render.render(write_still=True)
sc.render.resolution_x = 256; sc.render.resolution_y = 256
sc.render.filepath = out_icon
bpy.ops.render.render(write_still=True)
print('BRENDER ok')
