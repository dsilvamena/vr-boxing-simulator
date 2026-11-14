# Voces del réferi

Todas las frases del réferi están en `Assets/_Project/Audio/Arbitro/`, un WAV por frase (mono, 24 kHz, 16 bits).
Se generaron el 2026-11-14 con **Voice Studio** (API local en `http://127.0.0.1:3900`, motor OmniVoice) a partir de
una sola voz clonada, el perfil «Réferi Boxeo»: hombre de mediana edad, voz grave, diseñada con las etiquetas
`male, middle-aged, low pitch` (semilla 1) diciendo la frase de referencia de `Source~/Referencia_Voz.wav`.
Clonar esa referencia en todas las frases hace que suenen a la misma persona.

## Regenerar

```bash
python3 Tools/voces_arbitro.py Assets/_Project/Audio/Arbitro                    # todas
python3 Tools/voces_arbitro.py Assets/_Project/Audio/Arbitro Voz_Tiempo Voz_Box_2  # solo esos prefijos
```

El script recorta el silencio de los extremos, normaliza y transcribe cada frase con el Whisper de Voice Studio; si no
se entiende o pasa de su duración máxima, prueba otra semilla (las tres últimas, un 12 % más deprisa) y marca
`REVISAR` lo que siga fallando. Whisper escribe «Knockout» y «Vox» cuando el réferi dice «nocaut» y «box»: el script
los da por buenos. Para cambiar un texto o añadir una frase se edita la tabla `LINEAS` del script; una frase nueva con
el prefijo de su momento entra en el juego con *Tools > Boxeo > Poner voces del réferi*. Para usar una grabación real
basta con sobrescribir el WAV (el `.meta` mantiene la referencia) y no volver a generar ese prefijo.

## Guion

`Arbitro` elige al azar una frase del grupo, sin repetir la última que dijo de ese grupo.

| Archivo | Frase | Duración | Cuándo |
|---|---|---|---|
| `Voz_Instrucciones_1.wav` | ¡Pelea limpia, caballeros! ¡Protéjanse en todo momento! | 3,16 s | Al cargar la pelea, en la preparación (antes de la primera campana) |
| `Voz_Instrucciones_2.wav` | ¡Quiero una pelea limpia! ¡Obedezcan mis órdenes! | 2,77 s | Ídem |
| `Voz_Box_1.wav` | ¡Box! | 0,63 s | 0,5 s después de la campana que abre cada round |
| `Voz_Box_2.wav` | ¡A pelear! | 0,89 s | Ídem |
| `Voz_Box_3.wav` | ¡Adelante! ¡Box! | 1,33 s | Ídem |
| `Voz_Esquina_1.wav` | ¡Esquina neutral! | 1,04 s | El rival quedó tendido: manda al jugador a la esquina neutral antes de contar |
| `Voz_Esquina_2.wav` | ¡A la esquina! | 0,90 s | Ídem |
| `Voz_Esquina_3.wav` | ¡Esquina, ya! | 0,87 s | Ídem |
| `Voz_Cuenta_01.wav` … `Voz_Cuenta_10.wav` | ¡Uno! … ¡Diez! | 0,50–0,84 s | La cuenta, un número por segundo (del rival y del jugador) |
| `Voz_Seguir_1.wav` | ¿Puede seguir? ¡Box! | 1,56 s | Reanuda tras una cuenta superada (del rival o del jugador) |
| `Voz_Seguir_2.wav` | ¡Levante los guantes! ¡Box! | 1,82 s | Ídem |
| `Voz_Seguir_3.wav` | ¡Muy bien, sigan! | 1,16 s | Ídem |
| `Voz_Tiempo_1.wav` | ¡Tiempo! ¡A sus esquinas! | 1,48 s | 1 s después de la campana de fin de round, si el combate sigue |
| `Voz_Tiempo_2.wav` | ¡Alto! ¡Tiempo! | 1,33 s | Ídem |
| `Voz_Tiempo_3.wav` | ¡Se acabó el round! | 1,10 s | Ídem |
| `Voz_Nocaut_1.wav` | ¡Se acabó! ¡Nocaut! | 1,52 s | Nocaut de cualquiera de los dos (con la seña de agitar los brazos) |
| `Voz_Nocaut_2.wav` | ¡Fuera! ¡Es nocaut! | 1,48 s | Ídem |
| `Voz_Nocaut_3.wav` | ¡Nocaut! | 0,60 s | Ídem |
| `Voz_Decision_1.wav` | ¡Se acabó! ¡Vamos a las tarjetas! | 1,84 s | Fin del último round sin nocaut: deciden los jueces |
| `Voz_Decision_2.wav` | ¡Tiempo! ¡Deciden los jueces! | 1,89 s | Ídem |

Límites que impone el juego: cada número dura menos de 1 s (el siguiente lo corta), las frases de esquina menos de
1,3 s (`pausaAntesDeContar`, luego llega el «¡Uno!») y las instrucciones menos de ~4,5 s (la preparación dura 5 s y el
primer «¡Box!» llega 0,5 s después de la campana).

Probado en Play (nivel 1, sin visor): instrucciones → campana → «¡Box!» → *Probar: contar al rival* (esquina, 1–9,
frase de seguir) → *Probar: acabar el round* («¡Tiempo!») → *saltar al round 3* + *perder por salud* (decisión, sin
«¡Tiempo!»). Queda por escuchar en el visor si se entienden con el público de fondo y a la distancia del réferi.
