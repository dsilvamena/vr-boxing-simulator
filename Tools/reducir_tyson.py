"""Reduce la malla de Iron Mike Tyson para el Quest y la exporta a un FBX listo para Unity (Humanoid).

El FBX riggeado del equipo (Source~/export/mike_rigged_1.78m_v1.fbx: esqueleto con nombres de Mixamo, 1,78 m, una malla
y un material con la textura embebida) tiene ~31 000 triángulos, el doble que Mr. Sandman (14 131). Con un
SkinnedMeshRenderer el coste es por vértice en cada frame, así que se reduce aquí y no con el Mesh LOD de Unity.

- Corrige los pesos de los vértices del hombro que el rig ataba al antebrazo (ver el paso 2).
- Aplica un Decimate (colapso) antes del modificador Armature: reduce la malla en reposo y conserva pesos, UV y material.
- Renombra el material a MikeTyson y guarda su textura (embebida en el FBX) en Textures/MikeTyson_BaseColor.jpg.
- Exporta MikeTyson.fbx en metros, sin huesos hoja, solo con los huesos que deforman y sin la textura dentro.

Uso (Blender 4 o 5, sin interfaz):
    /Applications/Blender.app/Contents/MacOS/Blender -b --python Tools/reducir_tyson.py -- \
        "Assets/_Project/Art/Characters/MikeTyson/Source~/export/mike_rigged_1.78m_v1.fbx" \
        Assets/_Project/Art/Characters/MikeTyson 15000

El último argumento es el número de triángulos buscado. La carpeta Source~ no la importa Unity (termina en ~).
Sobrescribir MikeTyson.fbx conserva su .meta: Humanoid, el mapeo de huesos y el material remapeado.
"""
import os
import sys

import bpy

argumentos = sys.argv[sys.argv.index("--") + 1:]
origen = os.path.abspath(argumentos[0])
carpeta = os.path.abspath(argumentos[1])
objetivo = int(argumentos[2])
carpeta_texturas = os.path.join(carpeta, "Textures")
os.makedirs(carpeta_texturas, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=origen)

armadura = next(o for o in bpy.data.objects if o.type == "ARMATURE")
malla = next(o for o in bpy.data.objects if o.type == "MESH")
malla.name = "MikeTyson_Cuerpo"
malla.data.name = "MikeTyson_Cuerpo"

# 1. Material y textura: la textura viene empaquetada; se guarda tal cual (JPEG de 2048 px) para no inflarla
for mat in malla.data.materials:
    mat.name = "MikeTyson"
    for nodo in mat.node_tree.nodes:
        if nodo.type == "TEX_IMAGE" and nodo.image:
            img = nodo.image
            ruta = os.path.join(carpeta_texturas, "MikeTyson_BaseColor.jpg")
            if img.packed_file:
                with open(ruta, "wb") as archivo:
                    archivo.write(img.packed_file.data)
                img.unpack(method="REMOVE")
            img.filepath = ruta
            img.name = "MikeTyson_BaseColor"

# 2. Corregir pesos: en el rig original, ~130 vértices de lo alto de cada hombro (junto al cuello) llevan hasta un 94 %
# de peso en el antebrazo, y al doblar el codo para la guardia salían estirados como una cuchilla. Un antebrazo no debe
# mover nada a más de DISTANCIA_MAXIMA de su hueso: ese peso pasa al hueso más cercano (aquí, el hombro).
# Las manos no se tocan: sin dedos, su hueso mide 17 cm y el guante llega a 18 cm de él.
DISTANCIA_MAXIMA = 0.15
HUESOS_A_LIMPIAR = ("LeftForeArm", "RightForeArm")


def segmento(hueso):
    """Del hueso a su primer hijo (o a su cola), en coordenadas del mundo y en reposo."""
    inicio = armadura.matrix_world @ hueso.head_local
    fin = armadura.matrix_world @ (hueso.children[0].head_local if hueso.children else hueso.tail_local)
    return inicio, fin


def distancia(punto, inicio, fin):
    eje = fin - inicio
    f = 0.0 if eje.length_squared == 0 else max(0.0, min(1.0, (punto - inicio).dot(eje) / eje.length_squared))
    return (punto - (inicio + eje * f)).length


segmentos = {b.name: segmento(b) for b in armadura.data.bones if b.use_deform}
grupos = {g.name: g for g in malla.vertex_groups}
corregidos = 0
for v in malla.data.vertices:
    punto = malla.matrix_world @ v.co
    for nombre in [n for n in grupos if n.split(":")[-1] in HUESOS_A_LIMPIAR]:
        grupo = grupos[nombre]
        try:
            peso = grupo.weight(v.index)
        except RuntimeError:
            continue  # el vértice no está en el grupo
        if peso <= 0 or distancia(punto, *segmentos[nombre]) <= DISTANCIA_MAXIMA:
            continue
        cercano = min((n for n in segmentos if n != nombre and n in grupos),
                      key=lambda n: distancia(punto, *segmentos[n]))
        grupo.remove([v.index])
        grupos[cercano].add([v.index], peso, "ADD")
        corregidos += 1
print(f"Pesos corregidos en {corregidos} vértices")

# 3. Reducir antes del Armature, para trabajar sobre la malla en reposo
triangulos = sum(len(p.vertices) - 2 for p in malla.data.polygons)
reducir = malla.modifiers.new("Reducir", "DECIMATE")
reducir.decimate_type = "COLLAPSE"
reducir.ratio = min(1.0, objetivo / triangulos)
reducir.use_collapse_triangulate = True
bpy.context.view_layer.objects.active = malla
while malla.modifiers[0].name != reducir.name:
    bpy.ops.object.modifier_move_up(modifier=reducir.name)
bpy.ops.object.modifier_apply(modifier=reducir.name)

# 4. Exportar con los mismos ejes que Mr. Sandman (de frente hacia +Z en Unity)
ruta_fbx = os.path.join(carpeta, "MikeTyson.fbx")
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
    path_mode="STRIP",
)
datos = malla.data
print(f"EXPORTADO {ruta_fbx}: {triangulos} -> {sum(len(p.vertices) - 2 for p in datos.polygons)} triángulos, "
      f"{len(datos.vertices)} vértices, {len(datos.materials)} materiales, "
      f"{len([b for b in armadura.data.bones if b.use_deform])} huesos que deforman")
