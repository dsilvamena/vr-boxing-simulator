"""Construye el cinturón de campeón (RF-19) desde cero y lo exporta a un FBX ligero para el Quest.

No hay un modelo con licencia en el repositorio, así que este es el respaldo que prevé el plan de la entrega 5: un
cinturón sencillo y propio, hecho solo con geometría y tres materiales lisos (sin texturas).

- La correa es un lazo elíptico de cuero (0,40 × 0,30 m, 10 cm de alto), como un cinturón cerrado sobre un maniquí,
  con dos filetes dorados en los bordes.
- Delante lleva la placa central dorada (27 × 21 cm), un óvalo de esmalte rojo, una estrella dorada en relieve y una
  corona encima. A cada lado, dos placas pequeñas doradas con el centro de esmalte.
- Tres materiales: Cinturon_Cuero, Cinturon_Oro y Cinturon_Esmalte. Unity los remapea por nombre a los materiales
  Standard de Materials/. Unas 1 700 caras triangulares, sin UV.
- Cada pieza tiene sus propios vértices por cara (bordes duros) y todo se exporta triangulado.
- El frente (la placa) mira hacia -Y en Blender, que en Unity queda hacia +Z (el frente de un objeto).

Uso (Blender 4 o 5, sin interfaz):
    /Applications/Blender.app/Contents/MacOS/Blender -b --python Tools/crear_cinturon.py -- \
        Assets/_Project/Art/CinturonCampeon

Escribe CinturonCampeon.fbx en esa carpeta y el .blend en su Source~ (que Unity no importa). Sobrescribir el FBX
conserva su .meta.
"""
import math
import os
import sys

import bpy
from mathutils import Vector

argumentos = sys.argv[sys.argv.index("--") + 1:]
carpeta = os.path.abspath(argumentos[0])
carpeta_fuente = os.path.join(carpeta, "Source~")
os.makedirs(carpeta_fuente, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)

# Medidas en metros
SEMIEJE_X = 0.20      # mitad del ancho del lazo
SEMIEJE_Y = 0.15      # mitad del fondo del lazo
ALTO = 0.10           # alto de la correa
GROSOR = 0.012        # grosor del cuero
SEGMENTOS = 48        # divisiones del lazo

CUERO, ORO, ESMALTE = 0, 1, 2

vertices = []
caras = []
materiales = []


def cara(puntos, material, normal_esperada):
    """Añade un polígono con vértices propios, girado para que su normal apunte hacia normal_esperada."""
    # Normal de Newell: vale también para polígonos cóncavos (la estrella, la corona)
    normal = Vector((0.0, 0.0, 0.0))
    for i, p in enumerate(puntos):
        q = puntos[(i + 1) % len(puntos)]
        normal += Vector(((p.y - q.y) * (p.z + q.z), (p.z - q.z) * (p.x + q.x), (p.x - q.x) * (p.y + q.y)))
    if normal.dot(normal_esperada) < 0:
        puntos = list(reversed(puntos))
    inicio = len(vertices)
    vertices.extend(puntos)
    caras.append(tuple(range(inicio, inicio + len(puntos))))
    materiales.append(material)


def punto_lazo(angulo):
    """Punto de la elipse y su normal hacia fuera (en el plano XY)."""
    p = Vector((SEMIEJE_X * math.cos(angulo), SEMIEJE_Y * math.sin(angulo), 0.0))
    n = Vector((SEMIEJE_Y * math.cos(angulo), SEMIEJE_X * math.sin(angulo), 0.0)).normalized()
    return p, n


def barrido(desde, hasta, z_abajo, z_arriba, material, caras_activas=("fuera", "dentro", "arriba", "abajo")):
    """Barre un rectángulo a lo largo del lazo. desde/hasta: distancia a la línea media, hacia fuera."""
    for i in range(SEGMENTOS):
        a0 = 2 * math.pi * i / SEGMENTOS
        a1 = 2 * math.pi * (i + 1) / SEGMENTOS
        p0, n0 = punto_lazo(a0)
        p1, n1 = punto_lazo(a1)
        arriba = Vector((0, 0, 1))
        def v(p, n, d, z):
            return p + n * d + arriba * z
        medio = (n0 + n1).normalized()
        if "fuera" in caras_activas:
            cara([v(p0, n0, hasta, z_abajo), v(p1, n1, hasta, z_abajo), v(p1, n1, hasta, z_arriba), v(p0, n0, hasta, z_arriba)], material, medio)
        if "dentro" in caras_activas:
            cara([v(p0, n0, desde, z_abajo), v(p1, n1, desde, z_abajo), v(p1, n1, desde, z_arriba), v(p0, n0, desde, z_arriba)], material, -medio)
        if "arriba" in caras_activas:
            cara([v(p0, n0, desde, z_arriba), v(p1, n1, desde, z_arriba), v(p1, n1, hasta, z_arriba), v(p0, n0, hasta, z_arriba)], material, arriba)
        if "abajo" in caras_activas:
            cara([v(p0, n0, desde, z_abajo), v(p1, n1, desde, z_abajo), v(p1, n1, hasta, z_abajo), v(p0, n0, hasta, z_abajo)], material, -arriba)


def prisma(contorno, origen, eje_u, eje_v, normal, grosor, material):
    """Extruye un contorno 2D (u, v) en sentido antihorario sobre el plano (origen, eje_u, eje_v), hacia normal."""
    base = [origen + eje_u * u + eje_v * v for u, v in contorno]
    tapa = [p + normal * grosor for p in base]
    cara(tapa, material, normal)
    cara(list(base), material, -normal)
    for i in range(len(contorno)):
        j = (i + 1) % len(contorno)
        du = contorno[j][0] - contorno[i][0]
        dv = contorno[j][1] - contorno[i][1]
        fuera = (eje_u * dv - eje_v * du).normalized()
        cara([base[i], base[j], tapa[j], tapa[i]], material, fuera)


def superelipse(a, b, puntos, exponente=4.0):
    resultado = []
    for i in range(puntos):
        t = 2 * math.pi * i / puntos
        c, s = math.cos(t), math.sin(t)
        resultado.append((a * math.copysign(abs(c) ** (2 / exponente), c), b * math.copysign(abs(s) ** (2 / exponente), s)))
    return resultado


def poligono(radio, lados, giro=0.0):
    return [(radio * math.cos(giro + 2 * math.pi * i / lados), radio * math.sin(giro + 2 * math.pi * i / lados)) for i in range(lados)]


def estrella(exterior, interior, puntas=5):
    resultado = []
    for i in range(puntas * 2):
        r = exterior if i % 2 == 0 else interior
        t = math.pi / 2 + math.pi * i / puntas
        resultado.append((r * math.cos(t), r * math.sin(t)))
    return resultado


# 1. Correa de cuero y filetes dorados en los bordes
barrido(-GROSOR / 2, GROSOR / 2, -ALTO / 2, ALTO / 2, CUERO)
for signo in (1, -1):
    z_borde = signo * ALTO / 2
    z0, z1 = sorted((z_borde - signo * 0.009, z_borde + signo * 0.002))
    barrido(GROSOR / 2 - 0.002, GROSOR / 2 + 0.003, z0, z1, ORO, ("fuera", "arriba", "abajo"))

# 2. Placa central, al frente (-Y)
frente, normal_frente = punto_lazo(-math.pi / 2)
origen = frente + normal_frente * (GROSOR / 2)
eje_u = Vector((1, 0, 0))
eje_v = Vector((0, 0, 1))
GROSOR_PLACA = 0.016
prisma(superelipse(0.135, 0.105, 32), origen, eje_u, eje_v, normal_frente, GROSOR_PLACA, ORO)
corona = [(-0.05, 0.085), (0.05, 0.085), (0.055, 0.14), (0.026, 0.115), (0.0, 0.155), (-0.026, 0.115), (-0.055, 0.14)]
prisma(corona, origen + normal_frente * 0.003, eje_u, eje_v, normal_frente, 0.010, ORO)
origen_esmalte = origen + normal_frente * GROSOR_PLACA
prisma(superelipse(0.098, 0.074, 28, 3.0), origen_esmalte, eje_u, eje_v, normal_frente, 0.005, ESMALTE)
prisma(estrella(0.058, 0.024), origen_esmalte + normal_frente * 0.005, eje_u, eje_v, normal_frente, 0.006, ORO)

# 3. Placas laterales, dos a cada lado
for desvio in (38, 68):
    for signo in (1, -1):
        angulo = -math.pi / 2 + signo * math.radians(desvio)
        p, n = punto_lazo(angulo)
        tangente = Vector((-SEMIEJE_X * math.sin(angulo), SEMIEJE_Y * math.cos(angulo), 0.0)).normalized()
        origen_lado = p + n * (GROSOR / 2)
        prisma(poligono(0.044, 8, math.pi / 8), origen_lado, tangente, eje_v, n, 0.010, ORO)
        prisma(poligono(0.022, 12), origen_lado + n * 0.010, tangente, eje_v, n, 0.004, ESMALTE)

# 4. Malla, materiales y triangulado
malla = bpy.data.meshes.new("CinturonCampeon")
malla.from_pydata([tuple(v) for v in vertices], [], caras)
malla.update()
for poligono_malla, material in zip(malla.polygons, materiales):
    poligono_malla.material_index = material
    poligono_malla.use_smooth = False

objeto = bpy.data.objects.new("CinturonCampeon", malla)
bpy.context.scene.collection.objects.link(objeto)


def material(nombre, color, metalico, rugosidad):
    m = bpy.data.materials.new(nombre)
    if bpy.app.version < (5, 0, 0):
        m.use_nodes = True  # en Blender 5 los materiales siempre usan nodos
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Metallic"].default_value = metalico
    bsdf.inputs["Roughness"].default_value = rugosidad
    m.diffuse_color = (*color, 1.0)
    return m


objeto.data.materials.append(material("Cinturon_Cuero", (0.03, 0.03, 0.035), 0.0, 0.6))
objeto.data.materials.append(material("Cinturon_Oro", (1.0, 0.72, 0.25), 1.0, 0.25))
objeto.data.materials.append(material("Cinturon_Esmalte", (0.55, 0.02, 0.03), 0.0, 0.35))

objeto.modifiers.new("Triangular", "TRIANGULATE")

bpy.context.view_layer.objects.active = objeto
objeto.select_set(True)

ruta_blend = os.path.join(carpeta_fuente, "cinturon_campeon.blend")
if os.path.exists(ruta_blend):
    os.remove(ruta_blend)  # si no, Blender deja la copia anterior como .blend1
bpy.ops.wm.save_as_mainfile(filepath=ruta_blend)

ruta_fbx = os.path.join(carpeta, "CinturonCampeon.fbx")
bpy.ops.export_scene.fbx(
    filepath=ruta_fbx,
    use_selection=False,
    object_types={"MESH"},
    apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_ALL",
    axis_forward="-Z",
    axis_up="Y",
    bake_space_transform=True,  # la conversión de ejes va en la malla: en Unity el nodo no lleva rotación
    use_mesh_modifiers=True,
    mesh_smooth_type="FACE",
    path_mode="STRIP",
    embed_textures=False,
    bake_anim=False,
)

depsgraph = bpy.context.evaluated_depsgraph_get()
evaluada = objeto.evaluated_get(depsgraph).to_mesh()
triangulos = sum(len(p.vertices) - 2 for p in evaluada.polygons)
print(f"Cinturón: {len(evaluada.vertices)} vértices, {triangulos} triángulos → {ruta_fbx}")
