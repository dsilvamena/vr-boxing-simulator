"""Sintetiza los efectos de sonido del juego (golpes, bloqueo, latido, multitud, campana, claqueta) como WAV mono 16 bits.

Uso: python3 Tools/sintetizar_audio.py Assets/_Project/Audio/Efectos [prefijo ...]

Con prefijos solo se escriben los sonidos cuyo nombre empieza por alguno (p. ej. "Golpe_Aire Rival_"), y el resto de
los WAV no se toca. La voz del réferi ya no sale de aquí: la genera Tools/voces_arbitro.py con Voice Studio.
"""
import math
import random
import struct
import sys
import wave

SR = 22050
OUT = sys.argv[1]
SOLO = sys.argv[2:]


def guardar(nombre, crear, sr=SR):
    """crear: función sin argumentos que devuelve las muestras (no se llama si el nombre queda fuera de SOLO)."""
    if SOLO and not any(nombre.startswith(p) for p in SOLO):
        return
    muestras = crear()
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


def aire(seed, dur=0.22, f0=500, f1=2600, q=1.6):
    """Silbido del guante al cortar el aire: ruido pasa banda cuyo tono sube y baja con la velocidad del brazo."""
    rnd = random.Random(seed)
    bp = Biquad("bp", f0, q)
    hp = Biquad("hp", 250, 0.7)
    n = int(dur * SR)
    out = []
    for i in range(n):
        x = i / n
        # La mano acelera y frena: envolvente en campana, algo adelantada
        env = math.sin(math.pi * x ** 0.8) ** 2
        if i % 16 == 0:
            bp.set("bp", f0 + (f1 - f0) * env, q)
        out.append(hp(bp(rnd.uniform(-1, 1))) * env)
    return out


def esfuerzo(seed, dur=0.28, tono=120, aspereza=0.5):
    """Resoplido de un boxeador al lanzar un golpe: un soplo de aire con dos formantes y un fondo de voz grave."""
    rnd = random.Random(seed)
    f1 = Biquad("bp", 950, 2.0)
    f2 = Biquad("bp", 2300, 2.5)
    lp = Biquad("lp", 700, 0.8)
    n = int(dur * SR)
    fase = 0.0
    out = []
    for i in range(n):
        t = i / SR
        x = i / n
        # Ataque brusco (el aire sale de golpe) y caída más lenta
        env = min(1.0, t / 0.012) * math.exp(-x * 4.5)
        r = rnd.uniform(-1, 1)
        soplo = f1(r) * 1.0 + f2(r) * 0.6
        fase += 2 * math.pi * tono * (1 - 0.25 * x) / SR
        # Voz: una onda rica en armónicos, filtrada, que se mezcla poco
        voz_grave = lp(math.sin(fase) + 0.5 * math.sin(2 * fase) + 0.3 * math.sin(3 * fase)) * aspereza
        out.append((soplo * 2.2 + voz_grave) * env)
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


def claqueta(seed=31):
    """Aviso de los diez segundos: dos golpes secos de madera, como la claqueta del cronometrador."""
    rnd = random.Random(seed)
    n = int(0.42 * SR)
    out = [0.0] * n
    for inicio, fuerza in ((0.0, 1.0), (0.17, 0.85)):
        filtro = Biquad("bp", 1350, 5.0)
        cuerpo = Biquad("bp", 620, 6.0)
        i0 = int(inicio * SR)
        for i in range(int(0.2 * SR)):
            t = i / SR
            golpe_seco = rnd.uniform(-1, 1) * math.exp(-t * 160)
            out[i0 + i] += fuerza * (2.2 * filtro(golpe_seco) + 1.2 * cuerpo(golpe_seco)) * math.exp(-t * 28)
    pico = max(1e-9, max(abs(v) for v in out))
    return [0.9 * v / pico for v in out]


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
    guardar("Golpe_Conectado_1", lambda: golpe(1))
    guardar("Golpe_Conectado_2", lambda: golpe(2, f0=160, f1=55, chasquido=0.8, corte=3200))
    guardar("Golpe_Conectado_3", lambda: golpe(3, f0=125, f1=40, cuerpo=1.2, chasquido=0.5, corte=2000, decaimiento=14))
    # Guante contra guante: más agudo, corto y sin cuerpo grave
    guardar("Golpe_Bloqueado", lambda: golpe(4, dur=0.22, f0=260, f1=180, cuerpo=0.35, chasquido=1.0, corte=1800, decaimiento=35))
    # Golpe que recibe el jugador: más grave y largo, se siente cerca
    guardar("Golpe_Recibido", lambda: golpe(5, dur=0.5, f0=110, f1=35, cuerpo=1.4, chasquido=0.7, corte=2200, decaimiento=10))
    guardar("Latido", lambda: latido())
    guardar("Multitud_Loop", lambda: bucle(balbuceo(6, 10.5, 28), 1.5))
    guardar("Multitud_Ovacion", lambda: balbuceo(7, 3.5, 22, vocal_ahh=True, crescendo=True))
    # Un toque al empezar el round y tres seguidos al terminarlo, para distinguirlos solo de oído
    guardar("Campana_Inicio", lambda: campana())
    guardar("Campana_Fin", lambda: campana(toques=3))
    guardar("Aviso_Diez_Segundos", lambda: claqueta())
    # Silbido del guante del jugador al lanzar un golpe (Puno) y resoplido del rival al lanzar el suyo (AtaqueOponente)
    guardar("Golpe_Aire_1", lambda: aire(201))
    guardar("Golpe_Aire_2", lambda: aire(202, dur=0.26, f0=420, f1=2100))
    guardar("Golpe_Aire_3", lambda: aire(203, dur=0.19, f0=600, f1=3000, q=1.9))
    guardar("Rival_Esfuerzo_1", lambda: esfuerzo(211))
    guardar("Rival_Esfuerzo_2", lambda: esfuerzo(212, dur=0.24, tono=135, aspereza=0.35))
    guardar("Rival_Esfuerzo_3", lambda: esfuerzo(213, dur=0.33, tono=105, aspereza=0.65))
