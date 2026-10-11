# Blender: iskeletli modelin sadece geometrisini azalt (armature kaldirilir), animasyonsuz/iskeletsiz glb yaz
import bpy, sys, json, mathutils
argv = sys.argv[sys.argv.index('--') + 1:]
src, dst, budget = argv[0], argv[1], int(argv[2])
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
# animasyonu rest pozuna
for o in bpy.context.scene.objects:
    if o.animation_data: o.animation_data.action = None
    if o.type == 'ARMATURE':
        o.data.pose_position = 'REST'
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
def tris(o): return sum(len(p.vertices) - 2 for p in o.data.polygons)
tc = {o.name: tris(o) for o in meshes}; total = sum(tc.values())
small = sum(t for t in tc.values() if t < 3000); big = total - small
r = max(0.02, min(1.0, (budget - small) / max(1, big)))
names = {}
for o in meshes:
    names[o.name] = o.get('glTF_name', None) if hasattr(o, 'get') else None
    for m in list(o.modifiers):
        if m.type == 'ARMATURE': o.modifiers.remove(m)
    o.shape_key_clear() if o.data.shape_keys else None
    if tc[o.name] >= 3000 and r < 1:
        if o.data.users > 1: o.data = o.data.copy()
        m = o.modifiers.new('dec', 'DECIMATE'); m.decimate_type = 'COLLAPSE'; m.ratio = r; m.use_collapse_triangulate = True
        with bpy.context.temp_override(object=o, active_object=o):
            bpy.ops.object.modifier_apply(modifier='dec')
# her mesh objesinin kaynak dugum adini kaydet
for o in meshes: o.data.name = 'MESHNODE__' + o.name
bpy.ops.export_scene.gltf(filepath=dst, export_format='GLB', export_animations=False, export_skins=False, export_morph=False,
                          export_yup=True, export_apply=False, export_image_format='NONE')
print('BPREP', json.dumps({'before': total, 'after': sum(tris(o) for o in meshes), 'ratio': r}))
