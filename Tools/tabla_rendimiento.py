"""Tabla de rendimiento por nivel (entrega 5, bloque J: RNF-01, RNF-02 y RNF-08).

Lee lo que escribe RegistroSesion en el visor (sacado con `adb pull` de
/sdcard/Android/data/com.utec.vrhola/files/Sesiones) y produce, en Markdown:

1. Las sesiones: dispositivo, duración, combates y si se cerraron bien (RNF-08: sesión continua de 30 minutos).
2. Por nivel: FPS medio y porcentaje de fotogramas lentos (RNF-01: menos del 5 % bajo 72 FPS), ponderados por los
   fotogramas de cada combate; latencia háptica medida en el código y retraso de detección estimado (RNF-02).

Usa los JSON de sesión (traen el número de fotogramas). Si no hay JSON, usa combates.csv ponderando por segundos.
Los combates sin rounds jugados (0 fotogramas) no cuentan para los FPS; los combates sin golpes que vibraran no
cuentan para la latencia. El p95 de un nivel es el más alto de sus combates (el JSON no guarda cada muestra), y el
máximo es el máximo de todos.

Uso: python3 Tools/tabla_rendimiento.py <carpeta Sesiones> [--sesion ID ...] [--dispositivo TEXTO]
     (sin dependencias; --dispositivo filtra por SystemInfo.deviceModel, p. ej. "Quest" para dejar fuera el Editor)
"""
import argparse
import csv
import glob
import json
import os
import sys

FPS_OBJETIVO = 72
LENTOS_MAX = 0.05     # RNF-01
LATENCIA_MAX_MS = 15  # RNF-02
SESION_MIN_S = 30 * 60  # RNF-08
NOMBRES = {1: "Glass Joe", 2: "Bald Bull", 3: "Soda Popinski", 4: "Mr. Sandman", 5: "Iron Mike Tyson"}


def num(v, defecto=0.0):
    try:
        return float(v)
    except (TypeError, ValueError):
        return defecto


def leer_json(carpeta):
    sesiones = []
    for ruta in sorted(glob.glob(os.path.join(carpeta, "sesion_*.json"))):
        try:
            with open(ruta, encoding="utf-8") as f:
                sesiones.append(json.load(f))
        except (OSError, json.JSONDecodeError) as e:
            print(f"Aviso: no se pudo leer {ruta}: {e}", file=sys.stderr)
    return sesiones


def leer_csv(carpeta):
    """Agrupa las filas de combates.csv en sesiones con la misma forma que el JSON (sin fotogramas)."""
    ruta = os.path.join(carpeta, "combates.csv")
    if not os.path.exists(ruta):
        return []
    sesiones = {}
    with open(ruta, encoding="utf-8", newline="") as f:
        for fila in csv.DictReader(f):
            s = sesiones.setdefault(fila["sesion"], {"id": fila["sesion"], "dispositivo": "?", "segundosSesion": None,
                                                     "combatesJugados": []})
            s["combatesJugados"].append({
                "nivel": int(num(fila.get("nivel"))), "rival": fila.get("rival", ""), "resultado": fila.get("resultado", ""),
                "segundosCombate": num(fila.get("segundos")), "fotogramas": None,
                "fpsMedio": num(fila.get("fpsMedio")), "porcentajeFotogramasLentos": num(fila.get("fotogramasLentos")),
                "pasoFisicaMs": num(fila.get("pasoFisicaMs")),
                "muestrasLatencia": int(num(fila.get("muestrasLatencia"))),
                "latenciaHapticaMediaMs": num(fila.get("latenciaMediaMs")),
                "latenciaHapticaP95Ms": num(fila.get("latenciaP95Ms")),
                "latenciaHapticaMaxMs": num(fila.get("latenciaMaxMs")),
                "muestrasRetraso": int(num(fila.get("muestrasRetraso"))),
                "retrasoDeteccionMedioMs": num(fila.get("retrasoDeteccionMedioMs")),
                "retrasoDeteccionP95Ms": num(fila.get("retrasoDeteccionP95Ms")),
                "retrasoDeteccionMaxMs": num(fila.get("retrasoDeteccionMaxMs")),
            })
    return list(sesiones.values())


def fmt(v, dec=1, sufijo=""):
    return "–" if v is None else f"{v:.{dec}f}{sufijo}".replace(".", ",")


def ok(cumple):
    return "–" if cumple is None else ("sí" if cumple else "**no**")


def tabla_sesiones(sesiones):
    print("## Sesiones\n")
    print("| Sesión | Dispositivo | Inicio | Fin | Duración | Combates | Victorias | Reintentos | ≥ 30 min (RNF-08) |")
    print("|---|---|---|---|---|---|---|---|---|")
    for s in sesiones:
        seg = s.get("segundosSesion")
        dur = "–" if seg is None else f"{int(seg) // 60} min {int(seg) % 60:02d} s"
        print(f"| {s.get('id')} | {s.get('dispositivo', '?')} | {s.get('inicio', '–')} | {s.get('fin', '–')} | {dur} | "
              f"{len(s.get('combatesJugados', []))} | {s.get('victorias', '–')} | {s.get('reintentos', '–')} | "
              f"{ok(None if seg is None else seg >= SESION_MIN_S)} |")
    print()


def tabla_niveles(combates):
    print("## Por nivel\n")
    print(f"FPS durante los rounds; fotogramas lentos = más de 1/{FPS_OBJETIVO} s × 1,05. Latencia = de Puno.OnTriggerEnter "
          "a la vuelta de OVRInput (solo el código). Retraso = estimación del contacto a su detección por la física.\n")
    print("| Nivel | Rival | Combates | Fotogramas | FPS medio | Fotogramas lentos | < 5 % (RNF-01) | Golpes con vibración "
          "| Latencia media | Latencia p95 | Latencia máx. | Retraso medio | Retraso p95 | Retraso máx. | Paso de física |")
    print("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|")
    for nivel in sorted({c["nivel"] for c in combates}):
        grupo = [c for c in combates if c["nivel"] == nivel]
        fila_nivel(str(nivel), grupo[0].get("rival") or NOMBRES.get(nivel, "?"), grupo)
    if len({c["nivel"] for c in combates}) > 1:
        fila_nivel("**Todos**", "", combates)
    print()


def fila_nivel(etiqueta, rival, grupo):
    # FPS: total de fotogramas entre tiempo total (JSON); sin fotogramas (CSV), media ponderada por segundos
    con_rounds = [c for c in grupo if c.get("fpsMedio", 0) > 0]
    fotogramas = sum(c.get("fotogramas") or 0 for c in con_rounds)
    if con_rounds and all(c.get("fotogramas") for c in con_rounds):
        tiempo = sum(c["fotogramas"] / c["fpsMedio"] for c in con_rounds)
        fps = fotogramas / tiempo
        lentos = sum(c["porcentajeFotogramasLentos"] * c["fotogramas"] for c in con_rounds) / fotogramas
    elif con_rounds and sum(c.get("segundosCombate", 0) for c in con_rounds) > 0:
        peso = sum(c["segundosCombate"] for c in con_rounds)
        fps = sum(c["fpsMedio"] * c["segundosCombate"] for c in con_rounds) / peso
        lentos = sum(c["porcentajeFotogramasLentos"] * c["segundosCombate"] for c in con_rounds) / peso
    else:
        fps = lentos = None

    lat = [c for c in grupo if c.get("muestrasLatencia", 0) > 0]
    n_lat = sum(c["muestrasLatencia"] for c in lat)
    lat_media = sum(c["latenciaHapticaMediaMs"] * c["muestrasLatencia"] for c in lat) / n_lat if n_lat else None
    lat_p95 = max((c["latenciaHapticaP95Ms"] for c in lat), default=None)
    lat_max = max((c["latenciaHapticaMaxMs"] for c in lat), default=None)

    ret = [c for c in grupo if c.get("muestrasRetraso", 0) > 0]
    n_ret = sum(c["muestrasRetraso"] for c in ret)
    ret_media = sum(c["retrasoDeteccionMedioMs"] * c["muestrasRetraso"] for c in ret) / n_ret if n_ret else None
    ret_p95 = max((c["retrasoDeteccionP95Ms"] for c in ret), default=None)
    ret_max = max((c["retrasoDeteccionMaxMs"] for c in ret), default=None)
    pasos = sorted({round(c.get("pasoFisicaMs", 0), 2) for c in grupo if c.get("pasoFisicaMs")})

    print(f"| {etiqueta} | {rival} | {len(grupo)} | {fotogramas or '–'} | {fmt(fps)} | {fmt(None if lentos is None else lentos * 100, 1, ' %')} | "
          f"{ok(None if lentos is None else lentos < LENTOS_MAX)} | {n_lat} | {fmt(lat_media, 3, ' ms')} | {fmt(lat_p95, 3, ' ms')} | "
          f"{fmt(lat_max, 3, ' ms')} | {fmt(ret_media, 1, ' ms')} | {fmt(ret_p95, 1, ' ms')} | {fmt(ret_max, 1, ' ms')} | "
          f"{', '.join(fmt(p, 2, ' ms') for p in pasos) or '–'} |")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("carpeta", help="carpeta Sesiones sacada del visor")
    ap.add_argument("--sesion", nargs="*", help="solo estas sesiones (id, p. ej. 20261120_153000)")
    ap.add_argument("--dispositivo", help="solo sesiones cuyo dispositivo contenga este texto")
    args = ap.parse_args()

    sesiones = leer_json(args.carpeta) or leer_csv(args.carpeta)
    if not sesiones:
        sys.exit(f"No hay sesion_*.json ni combates.csv en {args.carpeta}")
    if args.sesion:
        sesiones = [s for s in sesiones if s.get("id") in args.sesion]
    if args.dispositivo:
        sesiones = [s for s in sesiones if args.dispositivo.lower() in str(s.get("dispositivo", "")).lower()]
    if not sesiones:
        sys.exit("Ninguna sesión pasa los filtros")

    combates = [c for s in sesiones for c in s.get("combatesJugados", [])]
    print(f"# Rendimiento: {len(sesiones)} sesiones, {len(combates)} combates\n")
    tabla_sesiones(sesiones)
    if combates:
        tabla_niveles(combates)
    lat_p95 = max((c.get("latenciaHapticaP95Ms", 0) for c in combates if c.get("muestrasLatencia", 0) > 0), default=None)
    ret_p95 = max((c.get("retrasoDeteccionP95Ms", 0) for c in combates if c.get("muestrasRetraso", 0) > 0), default=None)
    if lat_p95 is None or ret_p95 is None:
        print("Sin golpes con vibración: no hay datos de latencia háptica.")
        return
    print(f"Retraso de detección + latencia del código, p95 del peor combate: {fmt(ret_p95, 1, ' ms')} + "
          f"{fmt(lat_p95, 3, ' ms')} = {fmt(ret_p95 + lat_p95, 1, ' ms')} (RNF-02: < {LATENCIA_MAX_MS} ms). Falta lo que "
          "añaden el runtime de OpenXR y el motor del control, que el código no ve.")


if __name__ == "__main__":
    main()
