"""Exporta a Mr. Sandman desde el .blend riggeado a un FBX listo para Unity (Humanoid).

Uso (Blender sin interfaz):
    /Applications/Blender.app/Contents/MacOS/Blender -b \
        "Assets/_Project/Art/Characters/MrSandman/Source~/mr_sandman_tpose_rig.blend" \
        --python Tools/exportar_sandman.py -- Assets/_Project/Art/Characters/MrSandman

- Borra lo que no es el personaje (cubo, cámara, luz).
- Une todas las piezas en una sola malla: en el Quest cada SkinnedMeshRenderer se skinnea aparte.
- Renombra los materiales a Sandman_<pieza> y guarda sus texturas (empaquetadas en el .blend) en Textures/.
- Exporta MrSandman.fbx en metros, sin huesos hoja y solo con los huesos que deforman.
No guarda el .blend: el original de Source~ queda intacto.
"""
import os
import sys

import bpy

carpeta = os.path.abspath(sys.argv[sys.argv.index("--") + 1])
carpeta_texturas = os.path.join(carpeta, "Textures")
os.makedirs(carpeta_texturas, exist_ok=True)

# 1. Fuera lo que no es el personaje
for obj in list(bpy.data.objects):
    if obj.type not in {"ARMATURE", "MESH"} or (obj.type == "MESH" and obj.parent is None):
        bpy.data.objects.remove(obj, do_unlink=True)

armadura = next(o for o in bpy.data.objects if o.type == "ARMATURE")
armadura.name = "Armature"
mallas = [o for o in bpy.data.objects if o.type == "MESH"]


# 2. Materiales y texturas: "SM_Belt_fg_too_mat_002.002" -> "Sandman_Belt"
def nombre_limpio(nombre):
    base = nombre.split("_fg_too_mat")[0]
    for prefijo in ("SM_", "SP_"):
        if base.startswith(prefijo):
            base = base[len(prefijo):]
    return "Sandman_" + base


for mat in {m for o in mallas for m in o.data.materials if m}:
    mat.name = nombre_limpio(mat.name)
    for nodo in mat.node_tree.nodes:
        if nodo.type == "TEX_IMAGE" and nodo.image:
            img = nodo.image
            ruta = os.path.join(carpeta_texturas, mat.name + ".png")
            img.filepath_raw = ruta
            img.file_format = "PNG"
            img.save()
            if img.packed_file:
                img.unpack(method="REMOVE")
            img.filepath = ruta
            img.name = mat.name

# 3. Una sola malla (los grupos de vértices se combinan por nombre de hueso)
cuerpo = max(mallas, key=lambda o: len(o.data.vertices))
with bpy.context.temp_override(active_object=cuerpo, object=cuerpo,
                               selected_objects=mallas, selected_editable_objects=mallas):
    bpy.ops.object.join()
cuerpo.name = "MrSandman_Cuerpo"
cuerpo.data.name = "MrSandman_Cuerpo"
mods = [m for m in cuerpo.modifiers if m.type == "ARMATURE"]
for m in mods[1:]:
    cuerpo.modifiers.remove(m)
mods[0].object = armadura

# 4. Exportar
ruta_fbx = os.path.join(carpeta, "MrSandman.fbx")
bpy.ops.export_scene.fbx(
    filepath=ruta_fbx,
    use_selection=False,
    object_types={"ARMATURE", "MESH"},
    apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_ALL",
    axis_forward="-Z",
    axis_up="Y",
    use_mesh_modifiers=False,
    mesh_smooth_type="FACE",
    add_leaf_bones=False,
    use_armature_deform_only=True,
    primary_bone_axis="Y",
    secondary_bone_axis="X",
    bake_anim=False,
    path_mode="RELATIVE",
)
tris = sum(len(p.vertices) - 2 for p in cuerpo.data.polygons)
print(f"EXPORTADO {ruta_fbx}: {len(cuerpo.data.vertices)} vértices, {tris} triángulos, "
      f"{len(cuerpo.data.materials)} materiales, {len(armadura.data.bones)} huesos")
