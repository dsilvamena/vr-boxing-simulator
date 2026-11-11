"""Modelo de dificultad de los rivales (entrega 4, bloque C; reconstruido en la entrega 5, bloque H).

Lee los PerfilOponente de Assets/_Project/Data/Oponentes/ y calcula, para cada rival:
- El daño por segundo que hace si todo conecta con la guardia del jugador arriba (sin castigoGuardiaBaja):
  daño del combo / (espera media entre ataques + tiempo entre el lanzamiento del primer y del último golpe del combo).
  AtaqueOponente empieza la espera al lanzar el último golpe del combo.
- Los golpes que necesita el jugador para ganarle, simulando N peleas con la misma lógica que Oponente.RecibirGolpe,
  DefensaOponente.Bloquea y EsquiveOponente.Esquiva: crosses a la cabeza con toda la fuerza (5 × 1,5 = 7,5 de daño).
  Los golpes durante una caída no cuentan, así que tampoco se cuentan aquí: el rival no recibe golpes caído.

Es una estimación, no una medida de juego: el rival no ataca y el jugador nunca falla.

Uso: python3 Tools/modelo_dificultad.py [peleas]   (sin dependencias)
"""
import os
import random
import re
import sys

CARPETA = os.path.join(os.path.dirname(__file__), "..", "Assets", "_Project", "Data", "Oponentes")
RIVALES = ["GlassJoe", "BaldBull", "SodaPopinski", "MrSandman", "MikeTyson"]
ABERTURA_TRAS_GUARDIA = 0.6  # DefensaOponente.aberturaTrasGuardia, igual en todas las escenas
DANO_GOLPE = 5 * 1.5         # danoCross × multiplicadorCabeza con toda la fuerza
TIPOS = {0: "Jab", 1: "Uppercut", 2: "Gancho", 3: "Gancho2"}


def leer_perfil(nombre):
    ruta = os.path.join(CARPETA, nombre + ".asset")
    if not os.path.exists(ruta):
        return None
    p = {}
    for linea in open(ruta, encoding="utf-8"):
        m = re.match(r"^  (\w+): (.*)$", linea)
        if not m:
            continue
        clave, valor = m.groups()
        v = re.match(r"\{x: ([-\d.e]+), y: ([-\d.e]+)\}", valor)
        if v:
            p[clave] = (float(v.group(1)), float(v.group(2)))
        elif clave == "combo":
            # int32 little endian en hexadecimal
            p[clave] = [int(valor[i:i + 2], 16) for i in range(0, len(valor.strip()), 8)]
        else:
            try:
                p[clave] = float(valor)
            except ValueError:
                p[clave] = valor
    return p


def retardo(p, tipo):
    return {0: p["retardoImpactoJab"], 1: p["retardoImpactoUppercut"], 2: p["retardoImpactoGancho"],
            3: p["retardoImpactoGancho2"]}[tipo] / p["velocidadAtaque"]


def dano_ataque(p, tipo):
    return {0: p["danoAtaqueJab"], 1: p["danoAtaqueUppercut"]}.get(tipo, p["danoAtaqueGancho"])


def dano_por_segundo(p):
    espera = sum(p["intervaloAtaque"]) / 2
    if p["combo"]:
        lanzamiento, ultimo = 0.0, 0.0
        for i, tipo in enumerate(p["combo"]):
            ultimo = lanzamiento
            lanzamiento += retardo(p, tipo) + p["segundosEntreGolpesCombo"]
        dano = sum(dano_ataque(p, t) for t in p["combo"])
        return dano / (espera + ultimo)
    n = int(p["uppercutCada"])
    dano = ((n - 1) * p["danoAtaqueJab"] + p["danoAtaqueUppercut"]) / n
    return dano / espera


def golpes_para_ganar(p, tiempos, rng):
    """tiempos: generador de instantes de los golpes del jugador. Devuelve los golpes que contaron."""
    salud = p["saludMaxima"]
    caidas, total_caidas = 0, int(p["caidasParaDerrota"])
    guardia_hasta, ultimo, seguidos = -10.0, -10.0, 0
    esquive_desde, listo_en = -10.0, 0.0
    golpes = 0
    for t in tiempos:
        golpes += 1
        # EsquiveOponente.Esquiva
        en_mov = t < esquive_desde + p["segundosEsquive"] + p["segundosRegresoEsquive"]
        if t < esquive_desde + p["segundosEsquive"]:
            continue
        ocupada = t < guardia_hasta + ABERTURA_TRAS_GUARDIA
        if not en_mov and t >= listo_en and not ocupada and rng.random() < p["probabilidadEsquive"]:
            esquive_desde, listo_en = t, t + p["enfriamientoEsquive"]
            continue
        # DefensaOponente.Bloquea
        n = seguidos + 1 if t - ultimo <= p["ventanaGolpesSeguidos"] else 0
        prob = max(p["probabilidadBloqueo"], min(p["bloqueoMaximo"], p["probabilidadBloqueo"] + n * p["bloqueoPorGolpeSeguido"]))
        seguidos, ultimo = n, t
        if t < guardia_hasta:
            bloqueado = True
        elif t < guardia_hasta + ABERTURA_TRAS_GUARDIA:
            bloqueado = False
        elif rng.random() < prob:
            bloqueado, guardia_hasta, seguidos = True, t + p["segundosGuardia"], 0
        else:
            bloqueado = False
        # Oponente.RecibirGolpe
        umbral = p["saludMaxima"] * (1 - (caidas + 1) / total_caidas)
        if bloqueado:
            salud = max(salud - DANO_GOLPE * p["danoTrasBloqueo"], min(salud, umbral + 1))
        else:
            salud = max(0.0, salud - DANO_GOLPE)
            if salud <= umbral:
                salud, caidas = umbral, caidas + 1
                if caidas >= total_caidas:
                    return golpes
    return golpes


def cada(intervalo):
    t = 0.0
    while True:
        yield t
        t += intervalo


def rafagas(n=5, separacion=0.5, pausa=6.0):
    t = 0.0
    while True:
        for _ in range(n):
            yield t
            t += separacion
        t += pausa - separacion


def main():
    peleas = int(sys.argv[1]) if len(sys.argv) > 1 else 20000
    rng = random.Random(1)
    filas = []
    for nombre in RIVALES:
        p = leer_perfil(nombre)
        if p is None:
            continue
        medias = []
        for tiempos in (lambda: cada(0.5), lambda: rafagas(), lambda: cada(1.5)):
            medias.append(sum(golpes_para_ganar(p, tiempos(), rng) for _ in range(peleas)) / peleas)
        combo = ", ".join(TIPOS[t] for t in p["combo"]) or f"uppercut cada {int(p['uppercutCada'])}"
        filas.append((nombre, p["saludMaxima"], dano_por_segundo(p), *medias, combo))
    print(f"{'Rival':14} {'Salud':>5} {'DPS':>5} {'c/0,5 s':>8} {'ráfagas':>8} {'c/1,5 s':>8}  Patrón")
    for f in filas:
        print(f"{f[0]:14} {f[1]:5.0f} {f[2]:5.2f} {f[3]:8.1f} {f[4]:8.1f} {f[5]:8.1f}  {f[6]}")


if __name__ == "__main__":
    main()
