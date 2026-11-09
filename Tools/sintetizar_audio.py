"""Sintetiza los efectos de sonido del juego (golpes, bloqueo, latido, multitud, campana, voz del réferi) como WAV mono 16 bits.

Uso: python3 Tools/sintetizar_audio.py Assets/_Project/Audio/Efectos
"""
import math
import random
import struct
import sys
import wave

SR = 22050
OUT = sys.argv[1]


def guardar(nombre, muestras, sr=SR):
    pico = max(1e-9, max(abs(s) for s in muestras))
    escala = 0.89 / pico
    with wave.open(f"{OUT}/{nombre}.wav", "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(sr)
        w.writeframes(b"".join(struct.pack("<h", int(max(-1, min(1, s * escala)) * 32767)) for s in muestras))
    print(nombre, len(muestras) / sr, "s")


class Biquad:
    """Filtro pasa banda / pasa bajos (RBJ)."""

    def __init__(self, tipo, f, q, sr=SR):
        self.x1 = self.x2 = self.y1 = self.y2 = 0.0
        self.set(tipo, f, q, sr)

    def set(self, tipo, f, q, sr=SR):
        w0 = 2 * math.pi * f / sr
        a = math.sin(w0) / (2 * q)
        c = math.cos(w0)
        if tipo == "bp":
            b0, b1, b2 = a, 0.0, -a
        elif tipo == "lp":
            b0, b1, b2 = (1 - c) / 2, 1 - c, (1 - c) / 2
        else:  # hp
            b0, b1, b2 = (1 + c) / 2, -(1 + c), (1 + c) / 2
        a0 = 1 + a
        self.b0, self.b1, self.b2 = b0 / a0, b1 / a0, b2 / a0
        self.a1, self.a2 = -2 * c / a0, (1 - a) / a0

    def __call__(self, x):
        y = self.b0 * x + self.b1 * self.x1 + self.b2 * self.x2 - self.a1 * self.y1 - self.a2 * self.y2
        self.x2, self.x1, self.y2, self.y1 = self.x1, x, self.y1, y
        return y


def golpe(seed, dur=0.35, f0=140, f1=45, cuerpo=1.0, chasquido=0.6, corte=2500, decaimiento=18):
    """Golpe de guante: un 'thump' grave que baja de tono más un chasquido de ruido filtrado."""
    rnd = random.Random(seed)
    lp = Biquad("lp", corte, 0.7)
    lp2 = Biquad("lp", corte * 0.6, 0.7)
    n = int(dur * SR)
    fase = 0.0
    out = []
    for i in range(n):
        t = i / SR
        f = f1 + (f0 - f1) * math.exp(-t * 30)
        fase += 2 * math.pi * f / SR
        thump = math.sin(fase) * math.exp(-t * decaimiento) * cuerpo
        ruido = lp2(lp(rnd.uniform(-1, 1))) * math.exp(-t * 60) * chasquido * 3
        ataque = min(1.0, t / 0.002)
        out.append((thump + ruido) * ataque)
    return out


def latido(dur=0.55):
    """Un solo latido (lub-dub): dos golpes graves y sordos, el segundo más corto y suave."""
    n = int(dur * SR)
    out = [0.0] * n
    for inicio, f, amp, dec in ((0.0, 55, 1.0, 22), (0.2, 70, 0.7, 30)):
        fase = 0.0
        for i in range(int(inicio * SR), n):
            t = i / SR - inicio
            fase += 2 * math.pi * (f + 25 * math.exp(-t * 40)) / SR
            out[i] += math.sin(fase) * math.exp(-t * dec) * amp * min(1.0, t / 0.004)
    return out


def campana(toques=1, separacion=0.3, dur=2.2, f0=1180):
    """Campana del ring: parciales inarmónicos que se apagan a distinto ritmo, más el golpe del badajo al inicio."""
    rnd = random.Random(8)
    parciales = ((1.0, 1.0, 2.2), (1.51, 0.6, 3.0), (2.03, 0.45, 3.8), (2.74, 0.3, 5.5), (3.42, 0.2, 7.5))
    lp = Biquad("lp", 5000, 0.7)
    n = int((dur + separacion * (toques - 1)) * SR)
    out = [0.0] * n
    for toque in range(toques):
        inicio = int(toque * separacion * SR)
        for i in range(inicio, n):
            t = (i - inicio) / SR
            sonido = sum(a * math.sin(2 * math.pi * f0 * r * t) * math.exp(-t * d) for r, a, d in parciales)
            badajo = lp(rnd.uniform(-1, 1)) * math.exp(-t * 90) * 0.8
            out[i] += (sonido + badajo) * min(1.0, t / 0.001)
    return out


def balbuceo(seed, dur, voces, vocal_ahh=False, crescendo=False):
    """Multitud: muchas 'voces' de ruido pasadas por formantes que cambian a ritmo de sílabas."""
    rnd = random.Random(seed)
    n = int(dur * SR)
    mezcla = [0.0] * n
    vocales = [(730, 1090), (530, 1840), (270, 2290), (570, 840), (440, 1020), (300, 870), (660, 1700)]
    for v in range(voces):
        f_esc = rnd.uniform(0.85, 1.35)
        b1 = Biquad("bp", 500, 6)
        b2 = Biquad("bp", 1500, 8)
        silaba = rnd.uniform(0.12, 0.3)
        prox = rnd.uniform(0, silaba)
        objetivo = 0.0
        amp = 0.0
        ganancia = rnd.uniform(0.4, 1.0)
        for i in range(n):
            t = i / SR
            if t >= prox:
                prox = t + rnd.uniform(0.6, 1.4) * silaba
                fa, fb = (730, 1090) if vocal_ahh else rnd.choice(vocales)
                b1.set("bp", fa * f_esc * rnd.uniform(0.9, 1.1), 6)
                b2.set("bp", fb * f_esc * rnd.uniform(0.9, 1.1), 8)
                objetivo = 1.0 if vocal_ahh else (rnd.random() < 0.75) * rnd.uniform(0.4, 1.0)
            amp += (objetivo - amp) * (0.0015 if vocal_ahh else 0.004)
            x = rnd.uniform(-1, 1)
            mezcla[i] += (b1(x) + 0.5 * b2(x)) * amp * ganancia
        print("  voz", v + 1, "/", voces, end="\r")
    print()
    # Rumor de fondo (ruido grave) para que no se oigan las voces sueltas
    lp = Biquad("lp", 400, 0.7)
    for i in range(n):
        mezcla[i] += lp(rnd.uniform(-1, 1)) * 0.6
    if crescendo:
        for i in range(n):
            t = i / n
            env = min(1.0, t / 0.25) * (1 - max(0.0, (t - 0.55) / 0.45)) ** 1.5
            mezcla[i] *= env
    return mezcla


# Sonidos de la voz del réferi: F1, F2, F3 (Hz), sonoridad, ruido y centro del ruido (Hz)
FONEMAS = {
    "a": (720, 1240, 2500, 1.0, 0.0, 0),
    "e": (470, 1880, 2550, 1.0, 0.0, 0),
    "i": (300, 2260, 2950, 0.9, 0.0, 0),
    "o": (480, 930, 2500, 1.0, 0.0, 0),
    "u": (320, 760, 2400, 0.85, 0.0, 0),
    "w": (320, 720, 2300, 0.7, 0.0, 0),
    "y": (300, 2200, 2900, 0.7, 0.0, 0),
    "n": (270, 1500, 2500, 0.35, 0.0, 0),
    "r": (420, 1500, 2300, 0.2, 0.0, 0),  # vibrante simple: un corte breve de la voz
    "b": (340, 900, 2300, 0.4, 0.0, 0),
    "s": (400, 1700, 2600, 0.0, 0.4, 5600),
    "ch": (300, 2000, 2800, 0.0, 0.55, 3400),
    "cierre": (300, 1500, 2500, 0.0, 0.0, 0),  # silencio antes de una oclusiva
    "barra": (250, 1400, 2500, 0.15, 0.0, 0),  # cierre con voz, antes de la d
    "t": (400, 1700, 2600, 0.0, 0.9, 3900),
    "d": (400, 1700, 2600, 0.3, 0.5, 3000),
    "k": (400, 1300, 2300, 0.0, 0.9, 1900),
}

# Cada número como lista de (sonido, milisegundos); todos deben durar menos de un segundo de la cuenta
NUMEROS = (
    (("u", 170), ("n", 80), ("o", 230)),
    (("barra", 30), ("d", 10), ("o", 240), ("s", 170)),
    (("cierre", 15), ("t", 25), ("r", 35), ("e", 220), ("s", 170)),
    (("cierre", 10), ("k", 30), ("w", 70), ("a", 180), ("cierre", 55), ("t", 20), ("r", 35), ("o", 190)),
    (("s", 130), ("i", 130), ("n", 70), ("cierre", 45), ("k", 25), ("o", 220)),
    (("s", 120), ("e", 120), ("i", 120), ("s", 170)),
    (("s", 120), ("y", 70), ("e", 150), ("cierre", 55), ("t", 20), ("e", 190)),
    (("o", 180), ("cierre", 55), ("ch", 100), ("o", 220)),
    (("n", 70), ("w", 70), ("e", 160), ("b", 60), ("e", 200)),
    (("barra", 30), ("d", 10), ("y", 70), ("e", 190), ("s", 190)),
)


GANANCIA_RUIDO = 1.6


def suavizar(valores, radio):
    """Media móvil: convierte los saltos entre sonidos en transiciones."""
    suma = [0.0]
    for v in valores:
        suma.append(suma[-1] + v)
    n = len(valores)
    return [(suma[min(n, i + radio + 1)] - suma[max(0, i - radio)]) / (min(n, i + radio + 1) - max(0, i - radio)) for i in range(n)]


def voz(seed, segmentos, f0_inicio=165, f0_fin=108):
    """Voz de hombre con un sintetizador de formantes en cascada: pulsos de la glotis pasados por resonadores."""
    rnd = random.Random(seed)
    # Una trayectoria por milisegundo; los formantes cambian despacio y la intensidad, deprisa
    pistas = [[] for _ in range(6)]
    for sonido, ms in segmentos:
        for pista, valor in zip(pistas, FONEMAS[sonido]):
            pista.extend([float(valor)] * ms)
    f1, f2, f3 = (suavizar(p, 22) for p in pistas[:3])
    sonoridad, ruido = (suavizar(p, 4) for p in pistas[3:5])
    centros = pistas[5]

    n = int(len(f1) * SR / 1000) + int(0.03 * SR)  # un poco de cola
    resonadores = [[0.0, 0.0] for _ in range(5)]
    filtro_ruido = Biquad("bp", 4000, 1.5)
    inclinacion = 0.0
    fase = 0.0
    periodo_jitter = 1.0
    out = []
    for i in range(n):
        ms = min(int(i * 1000 / SR), len(f1) - 1)
        t = i / max(1, n - 1)
        # Entonación de cuenta: arranca alta y cae, con un vibrato pequeño para que no suene a máquina
        f0 = (f0_inicio + (f0_fin - f0_inicio) * t) * (1 + 0.012 * math.sin(2 * math.pi * 5.2 * i / SR))
        fase += f0 * periodo_jitter / SR
        if fase >= 1.0:
            fase -= 1.0
            periodo_jitter = rnd.uniform(0.992, 1.008)
        # Pulso de la glotis (KLGLOTT): abierta el 60 % del periodo
        apertura = fase / 0.6
        pulso = (2 * apertura - 3 * apertura * apertura) if apertura < 1.0 else 0.0
        inclinacion += 0.45 * (pulso - inclinacion)  # pierde agudos, como una voz real
        ruido_blanco = rnd.uniform(-1, 1)
        x = (inclinacion + 0.03 * ruido_blanco) * sonoridad[ms]

        # Cascada de resonadores: F1, F2, F3 y dos formantes altos fijos
        for r, (f, ancho) in zip(resonadores, ((f1[ms], 90), (f2[ms], 110), (f3[ms], 170), (3400, 250), (4100, 300))):
            c = -math.exp(-2 * math.pi * ancho / SR)
            b = 2 * math.exp(-math.pi * ancho / SR) * math.cos(2 * math.pi * f / SR)
            y = (1 - b - c) * x + b * r[0] + c * r[1]
            r[1], r[0] = r[0], y
            x = y

        if centros[ms] > 0 and i % 110 == 0:
            filtro_ruido.set("bp", centros[ms], 1.5)
        out.append((x, filtro_ruido(ruido_blanco) * ruido[ms]))

    # Primero se iguala la voz entre números y después se añade el ruido, para que las consonantes no dependan de la vocal
    pico = max(1e-9, max(abs(v) for v, _ in out))
    out = [math.tanh(1.3 * v / pico) + GANANCIA_RUIDO * r for v, r in out]  # saturación suave: quien grita la cuenta

    # Fundido corto para no terminar en un clic
    for i in range(int(0.02 * SR)):
        out[-1 - i] *= i / (0.02 * SR)
    return out


def bucle(muestras, cruce):
    """Hace que el final empalme con el principio (fundido cruzado) para un loop sin clic."""
    c = int(cruce * SR)
    cuerpo = muestras[: len(muestras) - c]
    cola = muestras[len(muestras) - c:]
    for i in range(c):
        # Fundido de igual potencia: el ruido no correlacionado no baja de volumen en el cruce
        a = math.sin(i / c * math.pi / 2)
        cuerpo[i] = cuerpo[i] * a + cola[i] * math.cos(i / c * math.pi / 2)
    return cuerpo


if __name__ == "__main__":
    guardar("Golpe_Conectado_1", golpe(1))
    guardar("Golpe_Conectado_2", golpe(2, f0=160, f1=55, chasquido=0.8, corte=3200))
    guardar("Golpe_Conectado_3", golpe(3, f0=125, f1=40, cuerpo=1.2, chasquido=0.5, corte=2000, decaimiento=14))
    # Guante contra guante: más agudo, corto y sin cuerpo grave
    guardar("Golpe_Bloqueado", golpe(4, dur=0.22, f0=260, f1=180, cuerpo=0.35, chasquido=1.0, corte=1800, decaimiento=35))
    # Golpe que recibe el jugador: más grave y largo, se siente cerca
    guardar("Golpe_Recibido", golpe(5, dur=0.5, f0=110, f1=35, cuerpo=1.4, chasquido=0.7, corte=2200, decaimiento=10))
    guardar("Latido", latido())
    guardar("Multitud_Loop", bucle(balbuceo(6, 10.5, 28), 1.5))
    guardar("Multitud_Ovacion", balbuceo(7, 3.5, 22, vocal_ahh=True, crescendo=True))
    # Un toque al empezar el round y tres seguidos al terminarlo, para distinguirlos solo de oído
    guardar("Campana_Inicio", campana())
    guardar("Campana_Fin", campana(toques=3))
    # Voz del réferi: del 1 al 10. Se pueden reemplazar por grabaciones sobrescribiendo estos WAV (el .meta los mantiene asignados)
    for numero, segmentos in enumerate(NUMEROS, start=1):
        guardar(f"Voz_Cuenta_{numero:02d}", voz(100 + numero, segmentos))
