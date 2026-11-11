"""Tablas de las pruebas HEART con usuarios (entrega 5, bloque K: RNF-03, RNF-05 y RF-21).

Cruza por participante (P01, P02...) tres fuentes, todas sin nombres:

1. Lo que escribe RegistroSesion en el visor (`adb pull` de /sdcard/Android/data/com.utec.vrhola/files/Sesiones):
   los sesion_*.json o, si no hay JSON, combates.csv.
2. La hoja de observación pasada a CSV (plantilla: docs/heart/observacion_plantilla.csv). Su columna `sesion` es el id
   de la sesión de cada participante (el nombre sesion_<id>.json) y es lo que une a la persona con sus datos.
3. Las respuestas del cuestionario post-prueba (plantilla: docs/heart/cuestionario_plantilla.csv), Likert 1 a 5.

Escribe en Markdown: una fila por participante, la tabla por dimensión HEART, si se cumplen RNF-03 y RNF-05, y el
resumen de las observaciones que pide el plan (cuenta del réferi, roce háptico del esquive, combos).

Definiciones (docs/Protocolo_HEART.md):
- RNF-03: un participante novato cumple si adopta la guardia Y conecta un golpe en 120 s o menos de juego desde la
  primera campana de su sesión. Se usan los campos de sesión `segundosHastaGuardia` y `segundosHastaGolpeConectado`
  (tiempo de combate acumulado, sin pausas); si la sesión no los tiene (APK anterior al bloque K) o no se encuentra,
  se usa el cronómetro de la hoja de observación. Se cumple el requisito si lo logra al menos el 80 % de los novatos.
- RNF-05: media de C1, C2 y C3 de cada participante (ítems redactados en positivo: 5 = ningún malestar), y media de
  esas medias entre todos los participantes. Se cumple si es 4 o más.
- Retention (adaptada): R1 ("me gustaría volver a jugar") 4 o 5, y si siguió jugando cuando se le ofreció parar.

Uso: python3 Tools/tabla_heart.py <carpeta Sesiones> --observacion obs.csv --cuestionario resp.csv [--dispositivo TEXTO]
     (sin dependencias; los CSV pueden usar coma o punto y coma como separador y coma decimal)
"""
import argparse
import csv
import glob
import json
import math
import os
import statistics
import sys

ADOPCION_MAX_S = 120   # RNF-03: 2 minutos de juego libre
ADOPCION_MIN = 0.80    # RNF-03: al menos el 80 % de los novatos
CINETOSIS_MIN = 4.0    # RNF-05
DISCREPANCIA_S = 10    # avisar si registro y cronómetro difieren más que esto
HAPPINESS = ["H1", "H2", "H3", "H4"]
CINETOSIS = ["C1", "C2", "C3"]
RETENCION = ["R1", "R2"]


# ---------------------------------------------------------------- lectura

def num(v, defecto=None):
    if v is None:
        return defecto
    try:
        return float(str(v).strip().replace(",", "."))
    except ValueError:
        return defecto


def si_no(v):
    """'sí'/'si'/'s'/'x' → True, 'no'/'n' → False, 'parcial' → 'parcial', vacío o 'na' → None."""
    t = str(v or "").strip().lower()
    if t in ("sí", "si", "s", "x", "yes", "1"):
        return True
    if t in ("no", "n", "0"):
        return False
    if t.startswith("parc"):
        return "parcial"
    return None


def leer_tabla(ruta):
    """Lee un CSV con coma o punto y coma (Excel en español usa ';')."""
    with open(ruta, encoding="utf-8-sig", newline="") as f:
        muestra = f.read(4096)
        f.seek(0)
        sep = ";" if muestra.count(";") > muestra.count(",") else ","
        filas = [{(k or "").strip(): (v or "").strip() for k, v in fila.items()} for fila in csv.DictReader(f, delimiter=sep)]
    return [f for f in filas if f.get("participante")]


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
    """Agrupa combates.csv en sesiones con la forma del JSON. Las columnas que falten (CSV viejos) quedan en None."""
    ruta = os.path.join(carpeta, "combates.csv")
    if not os.path.exists(ruta):
        return []
    sesiones = {}
    with open(ruta, encoding="utf-8", newline="") as f:
        for fila in csv.DictReader(f):
            s = sesiones.setdefault(fila["sesion"], {"id": fila["sesion"], "dispositivo": "?", "segundosSesion": None,
                                                     "combatesJugados": []})
            s["combatesJugados"].append({
                "nivel": int(num(fila.get("nivel"), 0)), "rival": fila.get("rival", ""), "inicio": fila.get("inicio", ""),
                "reintento": fila.get("reintento") == "1", "resultado": fila.get("resultado", ""),
                "segundosCombate": num(fila.get("segundos"), 0.0),
                "golpesLanzados": num(fila.get("lanzados"), 0), "golpesConectados": num(fila.get("conectados"), 0),
                "precision": num(fila.get("precision")), "ataquesRecibidos": num(fila.get("ataquesRecibidos"), 0),
                "ataquesBloqueados": num(fila.get("ataquesBloqueados"), 0),
                "caidasJugador": num(fila.get("caidasJugador"), 0), "cuentasSuperadas": num(fila.get("cuentasSuperadas"), 0),
                "segundosHastaGuardia": num(fila.get("segundosHastaGuardia")),
                "segundosHastaGolpeConectado": num(fila.get("segundosHastaGolpeConectado")),
            })
    for s in sesiones.values():
        s["combatesJugados"].sort(key=lambda c: c["inicio"])
    return list(sesiones.values())


def adopcion_de_sesion(s, campo):
    """Segundos de juego desde la primera campana de la sesión hasta el evento; -1 si no ocurrió; None si no se midió.

    El JSON del bloque K ya trae el valor de sesión. Si no (CSV), se reconstruye igual que RegistroSesion: se suman los
    segundos de los combates anteriores al primero en que ocurrió."""
    if s.get(campo) is not None:
        return s[campo]
    combates = s.get("combatesJugados", [])
    if not combates or all(c.get(campo) is None for c in combates):
        return None
    acumulado = 0.0
    for c in combates:
        v = c.get(campo)
        if v is not None and v >= 0:
            return acumulado + v
        acumulado += c.get("segundosCombate") or 0.0
    return -1.0


# ---------------------------------------------------------------- cálculo

def media(valores):
    valores = [v for v in valores if v is not None]
    return statistics.mean(valores) if valores else None


def mediana(valores):
    valores = [v for v in valores if v is not None]
    return statistics.median(valores) if valores else None


def proporcion(cuenta, total):
    return None if not total else cuenta / total


def participante(obs, resp, sesiones_por_id):
    p = {"codigo": obs["participante"], "obs": obs, "resp": resp or {}}
    s = sesiones_por_id.get(obs.get("sesion", ""))
    p["sesion"] = s
    if obs.get("sesion") and s is None:
        print(f"Aviso: {p['codigo']}: no se encontró la sesión {obs['sesion']}", file=sys.stderr)
    combates = s.get("combatesJugados", []) if s else []
    p["combates"] = combates
    p["novato"] = si_no(obs.get("novato")) is not False  # por defecto, novato (es el perfil que se recluta)

    # RNF-03: registro primero, cronómetro de respaldo
    for clave, campo, crono in (("guardia", "segundosHastaGuardia", "cronoGuardiaS"),
                                ("golpe", "segundosHastaGolpeConectado", "cronoGolpeS")):
        reg = adopcion_de_sesion(s, campo) if s else None
        cro = num(obs.get(crono))
        p[clave + "_registro"], p[clave + "_crono"] = reg, cro
        p[clave] = reg if reg is not None else (cro if cro is not None else None)
        p[clave + "_fuente"] = "registro" if reg is not None else ("cronómetro" if cro is not None else "–")
        if reg is not None and reg >= 0 and cro is not None and abs(reg - cro) > DISCREPANCIA_S:
            print(f"Aviso: {p['codigo']}: {clave} {reg:.1f} s en el registro y {cro:.1f} s en el cronómetro", file=sys.stderr)
    g, h = p["guardia"], p["golpe"]
    p["rnf03"] = None if g is None or h is None else (0 <= g <= ADOPCION_MAX_S and 0 <= h <= ADOPCION_MAX_S)

    r = p["resp"]
    p["happiness"] = media([num(r.get(k)) for k in HAPPINESS])
    p["cinetosis"] = media([num(r.get(k)) for k in CINETOSIS])
    p["r1"], p["r2"] = num(r.get("R1")), num(r.get("R2"))
    return p


# ---------------------------------------------------------------- salida

def fmt(v, dec=1, sufijo=""):
    return "–" if v is None else f"{v:.{dec}f}{sufijo}".replace(".", ",")


def pct(v):
    return "–" if v is None else f"{round(v * 100)} %"


def ok(cumple):
    return "–" if cumple is None else ("sí" if cumple else "**no**")


def seg(v):
    if v is None:
        return "–"
    if v < 0:
        return "no ocurrió"
    return fmt(v, 1, " s")


def tabla_participantes(ps):
    print("## Participantes\n")
    print("Tiempos desde la primera campana de la sesión, en juego sin pausas. Fuente: registro del juego "
          "(`RegistroSesion`) o, si falta, cronómetro de la hoja de observación.\n")
    print("| Part. | Novato | Sesión | Combates | Niveles | Minutos de combate | Guardia (registro / crono) | "
          "Primer golpe conectado (registro / crono) | RNF-03 | Happiness | Cinetosis (RNF-05) | Volvería (R1) |")
    print("|---|---|---|---|---|---|---|---|---|---|---|---|")
    for p in ps:
        cs = p["combates"]
        niveles = ", ".join(str(n) for n in sorted({c.get("nivel") for c in cs})) or "–"
        minutos = sum(c.get("segundosCombate") or 0 for c in cs) / 60 if cs else None
        print(f"| {p['codigo']} | {'sí' if p['novato'] else 'no'} | {p['obs'].get('sesion') or '–'} | {len(cs) or '–'} | {niveles} | "
              f"{fmt(minutos)} | {seg(p['guardia_registro'])} / {seg(p['guardia_crono'])} | "
              f"{seg(p['golpe_registro'])} / {seg(p['golpe_crono'])} | {ok(p['rnf03'])} | {fmt(p['happiness'])} | "
              f"{fmt(p['cinetosis'])} | {fmt(p['r1'], 0)} |")
    print()


def tabla_heart(ps):
    combates = [c for p in ps for c in p["combates"]]
    sesiones = [p["sesion"] for p in ps if p["sesion"]]
    novatos = [p for p in ps if p["novato"] and p["rnf03"] is not None]
    logran = sum(1 for p in novatos if p["rnf03"])

    hap = [p["happiness"] for p in ps if p["happiness"] is not None]
    items_h = {k: media([num(p["resp"].get(k)) for p in ps]) for k in HAPPINESS}
    cin = [p["cinetosis"] for p in ps if p["cinetosis"] is not None]
    r1 = [p["r1"] for p in ps if p["r1"] is not None]
    r2 = [p["r2"] for p in ps if p["r2"] is not None]
    siguio = [si_no(p["obs"].get("siguioJugando")) for p in ps]
    siguio = [v for v in siguio if v is not None]

    dur = [s.get("segundosSesion") for s in sesiones if s.get("segundosSesion")]
    seg_combate = [sum(c.get("segundosCombate") or 0 for c in p["combates"]) for p in ps if p["combates"]]
    n_comb = [len(p["combates"]) for p in ps if p["combates"]]
    reint = [sum(1 for c in p["combates"] if c.get("reintento")) for p in ps if p["combates"]]

    recibidos = sum(c.get("ataquesRecibidos") or 0 for c in combates)
    bloqueados = sum(c.get("ataquesBloqueados") or 0 for c in combates)
    lanzados = sum(c.get("golpesLanzados") or 0 for c in combates)
    conectados = sum(c.get("golpesConectados") or 0 for c in combates)
    caidas = sum(c.get("caidasJugador") or 0 for c in combates)
    cuentas = sum(c.get("cuentasSuperadas") or 0 for c in combates)
    nivel1 = [p for p in ps if any(c.get("nivel") == 1 for c in p["combates"])]
    gana1 = sum(1 for p in nivel1 if any(c.get("nivel") == 1 and str(c.get("resultado", "")).startswith("Victoria")
                                         for c in p["combates"]))

    print("## Por dimensión HEART\n")
    print("| Dimensión | Indicador | Resultado | Instrumento |")
    print("|---|---|---|---|")
    print(f"| Happiness | Satisfacción inmersiva (media de H1–H4, 1 a 5) | {fmt(media(hap), 2)} (n = {len(hap)}; "
          f"{', '.join(f'{k} {fmt(v, 2)}' for k, v in items_h.items())}) | Cuestionario |")
    print(f"| Happiness | Ausencia de cinetosis (media de C1–C3, 1 a 5) | {fmt(media(cin), 2)} (mín. {fmt(min(cin), 2) if cin else '–'}; "
          f"n = {len(cin)}) | Cuestionario |")
    print(f"| Engagement | Duración de la sesión | media {fmt(media(dur) / 60 if dur else None)} min; combate "
          f"{fmt(media(seg_combate) / 60 if seg_combate else None)} min por persona | `segundosSesion`, `segundosCombate` |")
    print(f"| Engagement | Combates y reintentos por persona | {fmt(media(n_comb))} combates, {fmt(media(reint))} reintentos | "
          "`combatesJugados`, `reintento` |")
    print(f"| Adoption | Guardia y golpe conectado en ≤ {ADOPCION_MAX_S} s (novatos) | {logran} de {len(novatos)} "
          f"({pct(proporcion(logran, len(novatos)))}) | Registro y cronómetro |")
    print(f"| Adoption | Mediana hasta la guardia / hasta el primer golpe conectado | "
          f"{seg(mediana([p['guardia'] for p in novatos if p['guardia'] is not None and p['guardia'] >= 0]))} / "
          f"{seg(mediana([p['golpe'] for p in novatos if p['golpe'] is not None and p['golpe'] >= 0]))} | Registro (o cronómetro) |")
    print(f"| Retention (adaptada) | Volvería a jugar: R1 ≥ 4 | {sum(1 for v in r1 if v >= 4)} de {len(r1)} "
          f"({pct(proporcion(sum(1 for v in r1 if v >= 4), len(r1)))}); media R1 {fmt(media(r1), 2)}, R2 {fmt(media(r2), 2)} | Cuestionario |")
    print(f"| Retention (adaptada) | Siguió jugando cuando se le ofreció parar | {sum(1 for v in siguio if v is True)} de "
          f"{len(siguio)} | Observación |")
    print(f"| Task Success | Ataques bloqueados | {int(bloqueados)} de {int(recibidos)} ({pct(proporcion(bloqueados, recibidos))}) | "
          "`ataquesBloqueados` |")
    print(f"| Task Success | Precisión de golpes | {int(conectados)} de {int(lanzados)} ({pct(proporcion(conectados, lanzados))}) | "
          "`golpesConectados` / `golpesLanzados` |")
    print(f"| Task Success | Cuentas del réferi superadas | {int(cuentas)} de {int(caidas)} caídas | `cuentasSuperadas` |")
    print(f"| Task Success | Ganaron el nivel 1 | {gana1} de {len(nivel1)} | `resultado` |")
    print()

    print("## Requisitos\n")
    if novatos:
        p03 = proporcion(logran, len(novatos))
        necesarios = math.ceil(ADOPCION_MIN * len(novatos))
        print(f"- **RNF-03:** {logran} de {len(novatos)} novatos ({pct(p03)}) adoptaron la guardia y conectaron un golpe en "
              f"{ADOPCION_MAX_S} s o menos; hacen falta {necesarios}. **{'Se cumple' if logran >= necesarios else 'No se cumple'}.**")
    else:
        print("- **RNF-03:** sin datos de novatos.")
    if cin:
        m = media(cin)
        print(f"- **RNF-05:** ausencia de cinetosis {fmt(m, 2)} de 5 (mínimo {fmt(CINETOSIS_MIN, 0)}). "
              f"**{'Se cumple' if m >= CINETOSIS_MIN else 'No se cumple'}.**")
    else:
        print("- **RNF-05:** sin respuestas del cuestionario.")
    print()


def tabla_observaciones(ps):
    print("## Observaciones del plan\n")
    print("| Pregunta | Sí | Parcial | No | No aplica |")
    print("|---|---|---|---|---|")
    for campo, texto in (("entendioPulsar", "Entendió que había que pulsar A/B/X/Y en la cuenta (de los que cayeron)"),
                         ("notoRoceEsquive", "Notó el roce háptico cuando el rival esquivaba"),
                         ("combosDefendibles", "Pudo defenderse de los combos (de los que llegaron al nivel 3 o más)"),
                         ("pidioAyuda", "Pidió ayuda o instrucciones durante el juego")):
        v = [si_no(p["obs"].get(campo)) for p in ps]
        print(f"| {texto} | {v.count(True)} | {v.count('parcial')} | {v.count(False)} | {v.count(None)} |")
    print()
    notas = [(p["codigo"], p["obs"].get("incidencias", ""), p["obs"].get("comentarios", "")) for p in ps]
    notas = [n for n in notas if n[1] or n[2]]
    if notas:
        print("| Part. | Incidencias | Comentarios |")
        print("|---|---|---|")
        for codigo, inc, com in notas:
            print(f"| {codigo} | {inc or '–'} | {com or '–'} |")
        print()


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("carpeta", help="carpeta Sesiones sacada del visor")
    ap.add_argument("--observacion", required=True, help="hoja de observación en CSV (columna sesion = id de la sesión)")
    ap.add_argument("--cuestionario", help="respuestas del cuestionario en CSV")
    ap.add_argument("--dispositivo", help="solo sesiones cuyo dispositivo contenga este texto (p. ej. Quest)")
    args = ap.parse_args()

    sesiones = leer_json(args.carpeta) or leer_csv(args.carpeta)
    if args.dispositivo:
        sesiones = [s for s in sesiones if args.dispositivo.lower() in str(s.get("dispositivo", "")).lower()]
    por_id = {s.get("id"): s for s in sesiones}
    observaciones = leer_tabla(args.observacion)
    if not observaciones:
        sys.exit(f"No hay participantes en {args.observacion}")
    respuestas = {r["participante"]: r for r in leer_tabla(args.cuestionario)} if args.cuestionario else {}
    for codigo in sorted(set(respuestas) - {o["participante"] for o in observaciones}):
        print(f"Aviso: {codigo} tiene cuestionario pero no hoja de observación; no se cuenta", file=sys.stderr)

    ps = [participante(o, respuestas.get(o["participante"]), por_id) for o in observaciones]
    print(f"# Pruebas HEART: {len(ps)} participantes ({sum(1 for p in ps if p['novato'])} novatos), "
          f"{sum(len(p['combates']) for p in ps)} combates\n")
    tabla_participantes(ps)
    tabla_heart(ps)
    tabla_observaciones(ps)


if __name__ == "__main__":
    main()
