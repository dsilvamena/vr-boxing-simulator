"""Genera las frases del réferi con Voice Studio (API local, motor OmniVoice) como WAV mono 16 bits.

Uso: python3 Tools/voces_arbitro.py Assets/_Project/Audio/Arbitro [prefijo ...] [--url http://127.0.0.1:3900]

Todas las frases salen de una misma voz clonada (perfil "Réferi Boxeo" de Voice Studio), así suenan a la misma
persona. Si el perfil no existe se crea a partir de Source~/Referencia_Voz.wav (una voz diseñada con OmniVoice:
"male, middle-aged, low pitch", semilla 1, diciendo REFERENCIA_TEXTO).
Cada frase se recorta (silencio al principio y al final), se normaliza y se comprueba transcribiéndola con el Whisper
de Voice Studio: si no se entiende o no cabe en su tiempo máximo se prueba con otra semilla.
Con prefijos solo se escriben las frases cuyo nombre empieza por alguno (p. ej. "Voz_Cuenta Voz_Box"); el resto de los
WAV no se toca, así que una frase regrabada a mano se conserva si su prefijo no se pide.
Solo biblioteca estándar.
"""
import array
import difflib
import json
import math
import os
import sys
import unicodedata
import urllib.request
import uuid
import wave
import zlib

URL = "http://127.0.0.1:3900"
PERFIL = "Réferi Boxeo"
REFERENCIA_TEXTO = ("¡Escuchen bien los dos! Quiero una pelea limpia. Protéjanse en todo momento y obedezcan mis "
                    "órdenes. ¡Choquen los guantes!")
INSTRUCT = "male, middle-aged, low pitch"
INTENTOS = 6

# (nombre, texto, segundos máximos tras recortar, cuándo lo dice). Los tiempos salen de Arbitro:
# - la cuenta va a un número por segundo, y cada número corta al anterior;
# - las instrucciones caben en la preparación (5 s antes de la primera campana; el "¡Box!" llega 0,5 s después).
LINEAS = [
    ("Voz_Cuenta_01", "¡Uno!", 0.85, "Cuenta"),
    ("Voz_Cuenta_02", "¡Dos!", 0.85, "Cuenta"),
    ("Voz_Cuenta_03", "¡Tres!", 0.85, "Cuenta"),
    ("Voz_Cuenta_04", "¡Cuatro!", 0.85, "Cuenta"),
    ("Voz_Cuenta_05", "¡Cinco!", 0.85, "Cuenta"),
    ("Voz_Cuenta_06", "¡Seis!", 0.85, "Cuenta"),
    ("Voz_Cuenta_07", "¡Siete!", 0.85, "Cuenta"),
    ("Voz_Cuenta_08", "¡Ocho!", 0.85, "Cuenta"),
    ("Voz_Cuenta_09", "¡Nueve!", 0.85, "Cuenta"),
    ("Voz_Cuenta_10", "¡Diez!", 0.95, "Cuenta"),
    ("Voz_Instrucciones_1", "¡Pelea limpia, caballeros! ¡Protéjanse en todo momento!", 3.6, "Antes del primer round"),
    ("Voz_Instrucciones_2", "¡Quiero una pelea limpia! ¡Obedezcan mis órdenes!", 3.6, "Antes del primer round"),
    ("Voz_Box_1", "¡Box!", 0.9, "Empieza un round"),
    ("Voz_Box_2", "¡A pelear!", 1.2, "Empieza un round"),
    ("Voz_Box_3", "¡Adelante! ¡Box!", 1.5, "Empieza un round"),
    ("Voz_Seguir_1", "¿Puede seguir? ¡Box!", 1.9, "Tras una cuenta superada"),
    ("Voz_Seguir_2", "¡Levante los guantes! ¡Box!", 2.0, "Tras una cuenta superada"),
    ("Voz_Seguir_3", "¡Muy bien, sigan!", 1.5, "Tras una cuenta superada"),
    ("Voz_Tiempo_1", "¡Tiempo! ¡A sus esquinas!", 2.0, "Termina un round (sin acabar el combate)"),
    ("Voz_Tiempo_2", "¡Alto! ¡Tiempo!", 1.5, "Termina un round (sin acabar el combate)"),
    ("Voz_Tiempo_3", "¡Se acabó el round!", 1.6, "Termina un round (sin acabar el combate)"),
    ("Voz_Nocaut_1", "¡Se acabó! ¡Nocaut!", 1.9, "Nocaut (cualquiera de los dos)"),
    ("Voz_Nocaut_2", "¡Fuera! ¡Es nocaut!", 1.9, "Nocaut (cualquiera de los dos)"),
    ("Voz_Nocaut_3", "¡Nocaut!", 1.1, "Nocaut (cualquiera de los dos)"),
    ("Voz_Decision_1", "¡Se acabó! ¡Vamos a las tarjetas!", 2.4, "Fin del último round: deciden los jueces"),
    ("Voz_Decision_2", "¡Tiempo! ¡Deciden los jueces!", 2.2, "Fin del último round: deciden los jueces"),
]

NUMEROS = {"1": "uno", "2": "dos", "3": "tres", "4": "cuatro", "5": "cinco", "6": "seis", "7": "siete", "8": "ocho",
           "9": "nueve", "10": "diez",
           # Cómo escribe Whisper lo que el réferi dice bien
           "knockout": "nocaut", "vox": "box"}


# ---------- API ----------

def pedir(ruta, datos=None, tipo=None, metodo=None):
    req = urllib.request.Request(URL + ruta, data=datos, method=metodo or ("POST" if datos is not None else "GET"))
    if tipo:
        req.add_header("Content-Type", tipo)
    with urllib.request.urlopen(req, timeout=600) as r:
        return r.read()


def multipart(campos, archivos):
    """campos: {nombre: texto}; archivos: {nombre: (nombre_archivo, bytes)}."""
    limite = uuid.uuid4().hex
    partes = []
    for k, v in campos.items():
        partes.append(f'--{limite}\r\nContent-Disposition: form-data; name="{k}"\r\n\r\n{v}\r\n'.encode())
    for k, (archivo, datos) in archivos.items():
        partes.append(f'--{limite}\r\nContent-Disposition: form-data; name="{k}"; filename="{archivo}"\r\n'
                      f'Content-Type: application/octet-stream\r\n\r\n'.encode() + datos + b"\r\n")
    partes.append(f"--{limite}--\r\n".encode())
    return b"".join(partes), f"multipart/form-data; boundary={limite}"


def perfil(salida):
    for p in json.loads(pedir("/profiles")):
        if p["name"] == PERFIL:
            return p["id"]
    ref = os.path.join(salida, "Source~", "Referencia_Voz.wav")
    with open(ref, "rb") as f:
        cuerpo, tipo = multipart({"name": PERFIL, "ref_text": REFERENCIA_TEXTO, "instruct": INSTRUCT, "language": "es",
                                  "seed": "1", "kind": "clone"}, {"ref_audio": ("referencia.wav", f.read())})
    creado = json.loads(pedir("/profiles", cuerpo, tipo))
    print(f"Perfil «{PERFIL}» creado: {creado['id']}")
    return creado["id"]


def sintetizar(voz, texto, semilla, velocidad):
    cuerpo = json.dumps({"model": "omnivoice", "input": texto, "voice": voz, "language": "es", "response_format": "wav",
                         "seed": semilla, "num_step": 32, "speed": velocidad}).encode()
    return pedir("/v1/audio/speech", cuerpo, "application/json")


def transcribir(ruta):
    with open(ruta, "rb") as f:
        cuerpo, tipo = multipart({"model": "whisper-1", "language": "es"}, {"file": ("frase.wav", f.read())})
    return json.loads(pedir("/v1/audio/transcriptions", cuerpo, tipo)).get("text", "")


# ---------- Audio ----------

def leer(datos):
    ruta = os.path.join(os.environ.get("TMPDIR", "/tmp"), "voces_arbitro_tmp.wav")
    with open(ruta, "wb") as f:
        f.write(datos)
    with wave.open(ruta) as w:
        sr = w.getframerate()
        muestras = array.array("h", w.readframes(w.getnframes()))
    return [m / 32768 for m in muestras], sr


def recortar(x, sr):
    """Quita el silencio de los extremos (umbral relativo al pico), deja un margen y suaviza los bordes."""
    pico = max(1e-9, max(abs(v) for v in x))
    ventana = int(0.01 * sr)
    umbral = pico * 10 ** (-34 / 20)
    activas = [i for i in range(0, len(x), ventana)
               if math.sqrt(sum(v * v for v in x[i:i + ventana]) / max(1, len(x[i:i + ventana]))) > umbral]
    if not activas:
        return x
    ini = max(0, activas[0] - int(0.03 * sr))
    fin = min(len(x), activas[-1] + ventana + int(0.08 * sr))
    y = x[ini:fin]
    entrada, salida = int(0.005 * sr), int(0.03 * sr)
    for i in range(min(entrada, len(y))):
        y[i] *= i / entrada
    for i in range(min(salida, len(y))):
        y[-1 - i] *= i / salida
    escala = 0.89 / max(1e-9, max(abs(v) for v in y))
    return [v * escala for v in y]


def escribir(ruta, y, sr):
    with wave.open(ruta, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(sr)
        w.writeframes(array.array("h", (int(max(-1, min(1, v)) * 32767) for v in y)).tobytes())


# ---------- Comprobación ----------

def normalizar(texto):
    texto = unicodedata.normalize("NFD", texto.lower())
    texto = "".join(c for c in texto if unicodedata.category(c) != "Mn")
    palabras = "".join(c if c.isalnum() or c == " " else " " for c in texto).split()
    return " ".join(NUMEROS.get(p, p) for p in palabras)


def parecido(esperado, oido):
    return difflib.SequenceMatcher(None, normalizar(esperado), normalizar(oido)).ratio()


def generar(voz, salida, nombre, texto, maximo):
    ruta = os.path.join(salida, f"{nombre}.wav")
    mejor = None
    for intento in range(INTENTOS):
        semilla = zlib.crc32(nombre.encode()) % 10000 + intento * 101
        velocidad = 1.0 if intento < 3 else 1.12  # si no cabe, un poco más deprisa
        x, sr = leer(sintetizar(voz, texto, semilla, velocidad))
        y = recortar(x, sr)
        escribir(ruta, y, sr)
        oido = transcribir(ruta)
        nota = parecido(texto, oido)
        dur = len(y) / sr
        candidato = (nota >= 0.8 and dur <= maximo, nota, -dur, y, sr, oido, semilla, velocidad)
        if mejor is None or candidato[:3] > mejor[:3]:
            mejor = candidato
        if candidato[0]:
            break
    ok, nota, menos_dur, y, sr, oido, semilla, velocidad = mejor
    escribir(ruta, y, sr)
    estado = "ok " if ok else "REVISAR"
    print(f"{estado} {nombre:22s} {-menos_dur:4.2f}/{maximo:.2f} s  semilla {semilla:5d} x{velocidad:.2f}  "
          f"{nota:.2f}  «{texto}» → «{oido}»")
    return ok


if __name__ == "__main__":
    args = sys.argv[1:]
    if "--url" in args:
        i = args.index("--url")
        URL = args[i + 1].rstrip("/")
        del args[i:i + 2]
    if not args:
        sys.exit(__doc__)
    salida, solo = args[0], args[1:]
    voz = perfil(salida)
    fallos = [n for n, t, m, _ in LINEAS
              if (not solo or any(n.startswith(p) for p in solo)) and not generar(voz, salida, n, t, m)]
    if fallos:
        print(f"\nA revisar a mano: {', '.join(fallos)}")
