# Blender: import gltf/glb, decimate to tri budget (keeps UV/weights), export glb
import bpy, sys, json
argv = sys.argv[sys.argv.index('--') + 1:]
src, dst, budget, keep_anim = argv[0], argv[1], int(argv[2]), argv[3] == '1'
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
def tris(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)
tc = {o.name: tris(o) for o in meshes}
total = sum(tc.values())
info = {'before': total}
if total > budget:
    small = sum(t for t in tc.values() if t < 3000)
    big = total - small
    r = max(0.02, min(1.0, (budget - small) / max(1, big)))
    for o in meshes:
        if tc[o.name] < 3000: continue
        if o.data.users > 1: o.data = o.data.copy()
        bpy.context.view_layer.objects.active = o
        m = o.modifiers.new('dec', 'DECIMATE')
        m.decimate_type = 'COLLAPSE'; m.ratio = r; m.use_collapse_triangulate = True
        # decimate before armature
        while o.modifiers.find('dec') > 0:
            bpy.ops.object.modifier_move_up({'object': o} if False else None, modifier='dec') if False else None
            with bpy.context.temp_override(object=o, active_object=o):
                bpy.ops.object.modifier_move_up(modifier='dec')
        with bpy.context.temp_override(object=o, active_object=o):
            bpy.ops.object.modifier_apply(modifier='dec')
    info['ratio'] = r
info['after'] = sum(tris(o) for o in meshes)
bpy.ops.export_scene.gltf(filepath=dst, export_format='GLB', export_animations=keep_anim, export_skins=True,
                          export_yup=True, export_apply=False, export_image_format='AUTO', export_force_sampling=True)
print('BPREP', json.dumps(info))
