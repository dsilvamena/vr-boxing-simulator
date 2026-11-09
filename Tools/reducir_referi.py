"""Reduce la malla del réferi para el Quest y la exporta con su esqueleto y sus dos poses.

El modelo original (Referee_Unity, ~379 000 triángulos y ~190 000 vértices) es unas 50 veces más pesado que los rivales
(7 000 a 9 000 triángulos). Con un SkinnedMeshRenderer el coste es por vértice en cada frame, así que el Mesh LOD de
Unity no sirve: comparte el buffer de vértices. Este script aplica un Decimate (colapso) antes del modificador Armature,
lo que conserva los pesos de los huesos, el UV y el material, y exporta el FBX que importa Unity.

Uso (Blender 4 o 5, sin interfaz):
    /Applications/Blender.app/Contents/MacOS/Blender -b --python Tools/reducir_referi.py -- \
        Assets/_Project/Art/Characters/Referee/Source~/Referee.fbx \
        Assets/_Project/Art/Characters/Referee/Referee.fbx 20000

El último argumento es el número de triángulos buscado. La carpeta Source~ no la importa Unity (termina en ~).
Sobrescribir Referee.fbx conserva su .meta: Humanoid, el material remapeado y los clips.
"""
import sys

import bpy

origen, destino, objetivo = sys.argv[-3], sys.argv[-2], int(sys.argv[-1])

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=origen)

# Blender importa las acciones como "Armature|Armature|Pose": se dejan con el nombre corto para que el FBX exportado
# las llame "Referee_Armature|Pose_Referee" y "Referee_Armature|TPose", como el original
for accion in bpy.data.actions:
    accion.name = accion.name.split("|")[-1]

malla = next(o for o in bpy.data.objects if o.type == 'MESH')
triangulos = sum(len(p.vertices) - 2 for p in malla.data.polygons)
reducir = malla.modifiers.new("Reducir", 'DECIMATE')
reducir.decimate_type = 'COLLAPSE'
reducir.ratio = min(1.0, objetivo / triangulos)
reducir.use_collapse_triangulate = True

# El Decimate tiene que ir antes que el Armature para reducir la malla en reposo y no la posada
bpy.context.view_layer.objects.active = malla
while malla.modifiers[0].name != reducir.name:
    bpy.ops.object.modifier_move_up(modifier=reducir.name)
bpy.ops.object.modifier_apply(modifier=reducir.name)

datos = malla.data
print(f"Réferi: {triangulos} -> {sum(len(p.vertices) - 2 for p in datos.polygons)} triángulos, "
      f"{len(datos.vertices)} vértices")

bpy.ops.export_scene.fbx(
    filepath=destino,
    object_types={'ARMATURE', 'MESH'},
    add_leaf_bones=False,
    bake_anim=True,
    bake_anim_use_all_actions=True,
    bake_anim_use_nla_strips=False,
    use_mesh_modifiers=False,
    mesh_smooth_type='FACE',
    path_mode='STRIP',
)
