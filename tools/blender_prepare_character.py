"""Blender headless: import a (rigged) FBX, decimate to a triangle budget while keeping the armature and skin weights,
strip the mesh from animation-only files, and export a Unity-friendly FBX with textures copied next to it.

  Blender -b --python tools/blender_prepare_character.py -- <in.fbx> <out.fbx> <target_tris> [--anim-only]
"""
import os
import sys

import bpy

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
if len(argv) < 3:
    raise SystemExit("usage: -- <in.fbx> <out.fbx> <target_tris> [--anim-only]")
src, dst, target = argv[0], argv[1], int(argv[2])
anim_only = "--anim-only" in argv

bpy.ops.wm.read_factory_settings(use_empty=True)


def import_fbx(path):
    if hasattr(bpy.ops.import_scene, "fbx"):
        return bpy.ops.import_scene.fbx(filepath=path, use_anim=True, ignore_leaf_bones=True)
    return bpy.ops.wm.fbx_import(filepath=path)


def export_fbx(path):
    kwargs = dict(filepath=path, use_selection=False, apply_scale_options="FBX_SCALE_ALL", add_leaf_bones=False,
                  bake_anim=True, bake_anim_use_all_actions=True, bake_anim_simplify_factor=0.5,
                  path_mode="COPY", embed_textures=False, mesh_smooth_type="FACE", use_mesh_modifiers=True,
                  object_types={"ARMATURE", "MESH", "EMPTY"})
    if hasattr(bpy.ops.export_scene, "fbx"):
        return bpy.ops.export_scene.fbx(**kwargs)
    return bpy.ops.wm.fbx_export(filepath=path, export_animation=True, path_mode="COPY")


def triangle_count(obj):
    return sum(max(0, len(p.vertices) - 2) for p in obj.data.polygons)


import_fbx(src)
meshes = [o for o in bpy.data.objects if o.type == "MESH"]
armatures = [o for o in bpy.data.objects if o.type == "ARMATURE"]
actions = list(bpy.data.actions)
print(f"[prepare] meshes={len(meshes)} armatures={len(armatures)} actions={[a.name for a in actions]}")

if anim_only:
    for m in meshes:
        bpy.data.objects.remove(m, do_unlink=True)
    meshes = []
else:
    for m in meshes:
        tris = triangle_count(m)
        print(f"[prepare] {m.name}: {tris} tris, vertex groups={len(m.vertex_groups)}")
        if tris > target:
            bpy.ops.object.select_all(action="DESELECT")
            m.select_set(True)
            bpy.context.view_layer.objects.active = m
            mod = m.modifiers.new("Decimate", "DECIMATE")
            mod.decimate_type = "COLLAPSE"
            mod.ratio = target / tris
            mod.use_collapse_triangulate = True
            bpy.ops.object.modifier_apply(modifier=mod.name)
            print(f"[prepare] {m.name}: decimated to {triangle_count(m)} tris")

os.makedirs(os.path.dirname(dst), exist_ok=True)
export_fbx(dst)
print(f"[prepare] exported {dst} ({os.path.getsize(dst) / 1e6:.1f} MB)")
