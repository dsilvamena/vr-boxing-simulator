"""Sintetiza los efectos de sonido del juego (golpes, bloqueo, latido, multitud) como WAV mono 16 bits.

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
