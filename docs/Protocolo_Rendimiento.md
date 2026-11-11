# Protocolo de la sesión de rendimiento con visor (entrega 5, bloque J)

Guion para medir en el Meta Quest 2 lo que el Editor no puede medir: los FPS de cada nivel (RNF-01), la latencia háptica (RNF-02) y una sesión continua de 30 minutos (RNF-08). Todo lo que hace falta ya está en el APK: `RegistroSesion` guarda los FPS y la latencia de cada combate, y `Tools/tabla_rendimiento.py` arma las tablas. La preparación sin visor está en `docs/Entrega5.md`, bloque J.

Duración estimada: **1 h 30 min** (15 min de preparación, 35 min de niveles, 35 min de sesión larga y 5 min para sacar los datos). Hacen falta dos personas: una juega y otra apunta y maneja la terminal.

## Requisitos que se miden

| Requisito | Qué se mide | Dónde | Meta |
|---|---|---|---|
| RNF-01 | FPS durante los rounds de cada nivel | `fpsMedio`, `porcentajeFotogramasLentos` de cada combate | Menos del 5 % de fotogramas por debajo de 72 FPS |
| RNF-02 | Del golpe a la vibración | `latenciaHaptica*Ms` (código) + `retrasoDeteccion*Ms` (física) | Menos de 15 ms |
| RNF-08 | Sesión continua sin cierres ni pérdida de seguimiento | `segundosSesion` del JSON, observación y `logcat` | 30 minutos |

## Material

- Meta Quest 2 cargado al 100 % (la sesión larga gasta batería y calienta el visor) y sus dos controles con pilas nuevas.
- Cable USB-C de datos (no solo de carga).
- El Mac con el repositorio en la rama `entrega5/sin-visor` y los APK de `Builds/Android/`:
  - `vrboxing_dev.apk` (193,6 MB): *Development Build*, MSAA 4×. Es el que se usa en todo el protocolo (deja depurar con `adb` y sacar el `logcat` completo).
  - `vrboxing_dev_sinmsaa.apk` (193,6 MB): el mismo APK de desarrollo con el MSAA de *Medium* a 0. Solo para la comparación de "Si no se cumple", sin tener que recompilar.
  - `vrboxing_release.apk` (172,7 MB): el mismo juego sin *Development* (MSAA 4×). Solo para la comparación del paso 5.

  Los tres se generaron el 2026-11-11 desde el mismo código (commit `4009aed` de `entrega5/sin-visor`: los cambios del visto bueno más los tiempos de adopción de RNF-03 en `RegistroSesion`), con `CleanBuildCache`. Entre los dos de desarrollo solo cambia un byte de los ajustes de calidad (el MSAA). Si se regeneran para medir su tamaño, usar `CleanBuildCache`: una build incremental puede dejar huecos de varios MB dentro del APK.
- `adb`: viene con Unity. Para no escribir la ruta entera:

  ```bash
  export PATH="/Applications/Unity/Hub/Editor/6000.4.7f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools:$PATH"
  adb version
  ```

- Opcional: **OVR Metrics Tool** (gratis en la tienda de Meta Horizon), para ver en el visor los FPS, el uso de GPU y la temperatura (paso 4).
- Una hoja impresa o este documento abierto para rellenar las tablas del final.

## Paso 0. Preparar el visor (una sola vez)

1. En la app Meta Horizon del teléfono: *Dispositivos > el visor > Configuración del visor > Modo de desarrollador* activado. La cuenta tiene que pertenecer a una organización de desarrollador (se crea gratis en developers.meta.com).
2. Conectar el cable. Dentro del visor aparece "¿Permitir la depuración USB?": marcar *Permitir siempre desde este ordenador* y aceptar.
3. Comprobar que se ve:

   ```bash
   adb devices            # debe listar el visor como "device", no "unauthorized"
   adb shell getprop ro.product.model
   adb shell dumpsys battery | grep -E "level|temperature"   # temperatura en décimas de °C
   ```

4. En el visor: configurar el Guardián en modo **escala de habitación** (no estacionario), con al menos 2 × 2 m libres. Cerrar las demás apps.

## Paso 1. Instalar

Si ya hay una versión anterior del juego en el visor, **sacar antes sus datos** (paso 6): desinstalar borra la carpeta `Sesiones`.

**Opción A, con el APK ya generado** (recomendada: no recompila nada):

```bash
cd vr-boxing-simulator
adb install -r Builds/Android/vrboxing_dev.apk
```

Si responde `INSTALL_FAILED_UPDATE_INCOMPATIBLE` (otra firma), sacar los datos, `adb uninstall com.utec.vrhola` y repetir.

**Opción B, Build And Run:** en Unity, *File > Build Profiles > Android*, marcar *Development Build*, elegir el visor en *Run Device* y pulsar *Build And Run*. Tarda de 2 a 6 minutos (6 la primera vez en un equipo, unos 2 con la caché de IL2CPP) y deja el APK donde se le indique (usar `Builds/Android/`, que git ignora). Solo hace falta si el código cambió después de generar los APK.

El juego aparece en la biblioteca, en *Orígenes desconocidos*, con el nombre **Simulador de Boxeo RV** (hasta el 2026-11-11 se llamaba *vrhola*; el paquete sigue siendo `com.utec.vrhola`, así que `install -r` actualiza la misma app y conserva sus datos).

## Paso 2. Comprobar que arranca y empezar a registrar

1. En una terminal aparte, guardar el log de Unity y el del compositor de Meta durante toda la sesión:

   ```bash
   mkdir -p datos_visor
   adb logcat -c
   adb logcat -v time -s Unity VrApi OVRPlugin AndroidRuntime > datos_visor/logcat_$(date +%Y%m%d_%H%M).txt
   ```

   `VrApi` escribe una línea por segundo con `FPS=72/72` y otros datos del compositor: sirve de segunda medida, independiente de `RegistroSesion`. `AndroidRuntime` recoge los cierres inesperados.
2. Abrir el juego. Comprobar: aparece la arena y el menú; el láser apunta y el gatillo pulsa; *OPCIONES* tiene la **háptica por encima de 0** (sin vibración no hay muestras de latencia) y el volumen alto.
3. Anotar en la tabla del paso 7 la hora de inicio, la batería y la temperatura (`adb shell dumpsys battery`).

En el log de Unity debe aparecer `Registro: combate nuevo contra ...` al entrar en cada pelea; `RegistroSesion` escribe en `/sdcard/Android/data/com.utec.vrhola/files/Sesiones`.

## Paso 3. FPS y latencia por nivel (RNF-01, RNF-02), unos 35 minutos

Una instalación nueva solo tiene abierto el nivel 1: se juegan **en orden, del 1 al 5**, y cada victoria abre el siguiente. En cada nivel:

| Qué hacer | Por qué |
|---|---|
| Jugar **al menos dos rounds completos** (3 min de round). Si se pierde antes, REINTENTAR y seguir. | `RegistroSesion` solo cuenta los fotogramas de los rounds. |
| Pegar sin parar, con las dos manos y todos los tipos de golpe: **al menos 30 golpes que toquen al rival** por nivel. | Cada golpe que conecta, es bloqueado o esquivado vibra y deja una muestra de latencia. |
| Una vez por round, **girarse 10 s hacia las gradas** (la parte con más luces y geometría) y volver. | Es el peor caso de render; con el rival delante se ve menos arena. |
| Moverse por el ring y esquivar con la cabeza. | Es el uso normal, y deja comprobar que el seguimiento no se pierde. |
| No pausar durante el round. | La pausa no cuenta para los FPS, pero mezcla la observación. |

La persona que apunta anota, por nivel: hora, si hubo tirones visibles (saltos, imagen que tiembla), si se perdió el seguimiento de un control y si el visor avisó de algo.

**Si no se consigue ganar un nivel** (por ejemplo, Tyson), se puede abrir el siguiente sin jugarlo. Con el juego cerrado y el APK de desarrollo:

```bash
adb shell am force-stop com.utec.vrhola
adb shell run-as com.utec.vrhola ls shared_prefs        # debe aparecer com.utec.vrhola.v2.playerprefs.xml
adb shell run-as com.utec.vrhola cat shared_prefs/com.utec.vrhola.v2.playerprefs.xml
```

Si el archivo tiene la línea `<int name="nivelSuperado" value="N" />`, cambiarla por el valor que haga falta (4 abre el nivel 5):

```bash
adb shell "run-as com.utec.vrhola sed -i 's/name=\"nivelSuperado\" value=\"[0-9]*\"/name=\"nivelSuperado\" value=\"4\"/' shared_prefs/com.utec.vrhola.v2.playerprefs.xml"
```

Si no tiene esa línea (aún no se ganó ningún nivel), es más rápido ganar el nivel 1. Anotar en la hoja que el nivel se abrió así.

## Paso 4. Opcional: OVR Metrics Tool

Sirve para saber **por qué** baja el FPS (CPU o GPU) si `RegistroSesion` da más del 5 % de fotogramas lentos.

1. Instalar *OVR Metrics Tool* desde la tienda del visor y abrirlo.
2. Activar *Enable Persistent Overlay* (los datos en pantalla mientras se juega) y, en *Record*, *Record all captured metrics to CSV files*.
3. Volver al juego y repetir un round del nivel más pesado (el que dé peor FPS en el paso 3).
4. Al terminar, sacar los CSV:

   ```bash
   adb shell ls /sdcard/Android/data/com.oculus.ovrmonitormetricsservice/files/CapturedMetrics/
   adb pull /sdcard/Android/data/com.oculus.ovrmonitormetricsservice/files/CapturedMetrics/ datos_visor/ovrmetrics/
   ```

   Las columnas que importan son el FPS, `GPU Utilization`, `App GPU Time`, `CPU Level` / `GPU Level` y los *stale frames*. Si la ruta no existe en esta versión del sistema, el propio OVR Metrics Tool la muestra en su pantalla de grabación.

## Paso 5. Opcional: el APK sin *Development*

Para saber cuánto pesa la build de desarrollo en los FPS. **Antes, sacar los datos (paso 6)** por si acaso, aunque las dos versiones tienen la misma firma de depuración y `install -r` conserva los datos:

```bash
adb install -r Builds/Android/vrboxing_release.apk
```

Jugar dos rounds del nivel más pesado y comparar su `fpsMedio` con el del paso 3. Volver después al de desarrollo (`adb install -r Builds/Android/vrboxing_dev.apk`) para la sesión larga, o seguir con este si la diferencia es grande y anotarlo.

## Paso 6. Sacar los datos

Con el juego cerrado o en el menú (`RegistroSesion` reescribe el JSON al terminar cada combate, al salir de una escena y al pausar la app):

```bash
adb shell ls -l /sdcard/Android/data/com.utec.vrhola/files/Sesiones
adb pull /sdcard/Android/data/com.utec.vrhola/files/Sesiones datos_visor/
python3 Tools/tabla_rendimiento.py datos_visor/Sesiones --dispositivo Quest
```

El script imprime en Markdown la tabla de sesiones (con la duración para RNF-08) y la de niveles (FPS, fotogramas lentos y latencia), lista para pegar en `docs/Entrega5.md`. Con `--sesion <id>` se limita a una sesión; los id son los nombres `sesion_<id>.json`. `--dispositivo Quest` deja fuera las sesiones del Editor (`Mac...`) si alguien copió esos archivos en la misma carpeta. Comprobar antes el campo `dispositivo` de un JSON del visor (`SystemInfo.deviceModel`, que en el Quest 2 debería contener "Quest") y usar ese texto.

Guardar `datos_visor/` fuera del repositorio o en una carpeta ignorada: tiene la hora de juego de cada persona. Para el informe basta con las tablas.

## Paso 7. Sesión continua de 30 minutos (RNF-08), unos 35 minutos

Una sola ejecución del juego, sin cerrarlo, de al menos 30 minutos de reloj:

1. Anotar la hora de inicio, la batería y la temperatura. Abrir el juego desde la biblioteca (nueva sesión: nuevo `sesion_<id>.json`).
2. Jugar los niveles en orden, con sus reintentos, pasando por la hoja de resultados después de cada combate. Si se acaban, volver a empezar por el nivel 1.
3. Durante la sesión, una vez cada cosa, y anotar la hora:
   - Pausar con el botón *Menú* del control izquierdo, esperar 20 s y reanudar.
   - Salir del área del Guardián: debe salir "VUELVE AL ÁREA SEGURA" y la pausa; volver y comprobar que el juego sigue.
   - Quitarse el visor 15 s y volver a ponérselo: el juego debe pausarse y seguir.
   - Volver al menú principal desde la pausa y entrar a otro nivel.
4. Al pasar los 30 minutos, terminar el combate en curso, volver al menú y anotar la hora, la batería y la temperatura.
5. Sacar los datos (paso 6).

Se da por cumplido si: la app no se cerró sola (el `logcat` no tiene `FATAL EXCEPTION` ni el proceso se reinició), `segundosSesion` ≥ 1800 en el JSON de esa sesión, no hubo pérdidas de seguimiento que dejaran el control o la cabeza congelados, y los FPS de los últimos combates no caen respecto a los primeros (el calor puede bajar los relojes del visor).

Para buscar cierres en el log:

```bash
grep -nE "FATAL EXCEPTION|SIGSEGV|ANR in|Process com.utec.vrhola.*(died|has died)" datos_visor/logcat_*.txt
grep -c "FPS=" datos_visor/logcat_*.txt     # líneas de VrApi: una por segundo
```

## Qué mide la latencia y qué no

La latencia háptica tiene tres tramos. El juego registra los dos primeros en cada golpe:

| Tramo | Qué es | Cómo se obtiene | Columna |
|---|---|---|---|
| 1. Detección | Del momento en que el guante toca la hitbox (según el seguimiento) al paso de física que lo detecta. Con el *Fixed Timestep* de 1/72 s hay un paso por fotograma: de media medio fotograma (~7 ms) y como mucho uno (~14 ms), más si se pierden fotogramas. | Estimado en cada golpe: lo que el guante ya había entrado en la hitbox, dividido por la rapidez con que entraba (`Physics.ComputePenetration`). | `retrasoDeteccion*Ms` |
| 2. Código | De `Puno.OnTriggerEnter` a la vuelta de `OVRInput.SetControllerVibration`: resolver el golpe en `Oponente`, sonidos, eventos y la llamada a la vibración. | Medido con `Stopwatch` en cada golpe que vibra. | `latenciaHaptica*Ms` |
| 3. Runtime y control | Del runtime de OpenXR al control por Bluetooth y lo que tarda el motor en arrancar. | **No se puede medir desde el código.** Haría falta instrumentación externa (un acelerómetro en el control sincronizado con el juego). | — |

La cifra que se informa para RNF-02 es **tramo 1 + tramo 2** (el script suma los p95), indicando que falta el tramo 3. En el Editor, con Mono, el tramo 2 fue de unos 0,25 ms salvo el primer golpe (9,5 ms, compilación JIT); en el Quest, con IL2CPP, no hay JIT, pero `Debug.Log` del golpe sí escribe en el `logcat` en la build de desarrollo, y por eso conviene comparar con el APK sin *Development* (paso 5).

## Tablas para rellenar

### Sesión

| Dato | Valor |
|---|---|
| Fecha | |
| Visor y versión del sistema (`adb shell getprop ro.build.version.incremental`) | |
| APK (dev / release) y commit | |
| Persona que juega / persona que anota | |
| Hora de inicio y fin de la medición por niveles | |
| Batería y temperatura al empezar / al terminar | |

### FPS por nivel (RNF-01)

Columnas `fpsMedio` y `porcentajeFotogramasLentos` de `Tools/tabla_rendimiento.py`; `VrApi` es la media de las líneas `FPS=` del `logcat` durante ese nivel, como contraste.

| Nivel | Rival | Combates | Segundos de round | FPS medio | Fotogramas lentos | < 5 % | FPS según VrApi | Tirones o avisos observados |
|---|---|---|---|---|---|---|---|---|
| 1 | Glass Joe | | | | | | | |
| 2 | Bald Bull | | | | | | | |
| 3 | Soda Popinski | | | | | | | |
| 4 | Mr. Sandman | | | | | | | |
| 5 | Iron Mike Tyson | | | | | | | |
| — | `Scene_Results` y menú (sin registro; solo observación y VrApi) | | | | | | | |

### Latencia háptica (RNF-02)

| Nivel | Golpes con vibración | Código: media / p95 / máx. (ms) | Detección: media / p95 / máx. (ms) | Tramo 1 + 2, p95 (ms) | < 15 ms |
|---|---|---|---|---|---|
| 1 | | | | | |
| 2 | | | | | |
| 3 | | | | | |
| 4 | | | | | |
| 5 | | | | | |
| Todos | | | | | |

### Sesión de 30 minutos (RNF-08)

| Dato | Valor |
|---|---|
| Id de la sesión (`sesion_<id>.json`) y `segundosSesion` | |
| Combates jugados / reintentos / victorias | |
| Pausa con el botón Menú (hora, ¿siguió bien?) | |
| Salida del Guardián (hora, ¿pausó y siguió?) | |
| Visor quitado (hora, ¿pausó y siguió?) | |
| Cierres de la app (`FATAL EXCEPTION`, reinicios) | |
| Pérdidas de seguimiento observadas | |
| `fpsMedio` del primer y del último combate | |
| Batería y temperatura al empezar / al terminar | |
| ¿Cumple? | |

### OVR Metrics Tool (si se usó)

| Nivel | FPS | GPU Utilization | App GPU Time | CPU / GPU Level | Stale frames |
|---|---|---|---|---|---|
| | | | | | |

## Si no se cumple

- **FPS**, en este orden, repitiendo siempre **solo el nivel peor** (dos rounds, como en el paso 3) y anotando cada intento en una fila nueva de la tabla de FPS:
  1. **El MSAA.** Se activó 4× en la calidad *Medium* tras el visto bueno, sin medirlo en el visor. Sacar los datos (paso 6), instalar el APK que ya está hecho sin MSAA y repetir el nivel peor:

     ```bash
     adb install -r Builds/Android/vrboxing_dev_sinmsaa.apk
     ```

     Si con 0 se llega a 72 FPS y con 4× no, decidir entre bajarlo a 2× (*Project Settings > Quality > Medium > Anti Aliasing*) o quitarlo, o mantenerlo y aplicar recortes. Volver después al APK que se decida.
  2. **Los recortes**, de uno en uno y por orden (la lista del bloque J en `docs/Entrega5.md`). Los tres primeros se aplican con un clic en Unity, en *Tools > Boxeo > Rendimiento*:

     | Menú | Qué cambia |
     |---|---|
     | *Recorte 1: quitar sombras (salvo la direccional)* | Sin sombra en las 33 luces no direccionales de cada escena (24 activas), en las 7 escenas |
     | *Recorte 2: Mipmap Limit 1 en Medium* | Texturas a la mitad de lado en Android |
     | *Recorte 3: foveación fija al iniciar* | Pone `FoveacionFija` (nivel *High*, fijo) en el objeto de `OVRManager` de las 7 escenas |
     | *Deshacer recorte 1 / 2 / 3* | Lo devuelve exactamente como estaba |
     | *Ver recortes aplicados* | Escribe en la consola qué hay aplicado y el MSAA de *Medium* |

     Para cada uno: guardar o descartar los cambios de las escenas abiertas, aplicar el recorte, generar el APK de desarrollo (paso 1, opción B, o `unity command build --target Android --outputPath Builds/Android/vrboxing_dev.apk --options '["Development"]' --confirm true`), instalarlo y repetir el nivel peor. Si no basta, aplicar el siguiente sin deshacer el anterior. Lo aplicado queda apuntado en `ProjectSettings/RecortesRendimiento.json` (el valor anterior de cada ajuste); si se decide quedárselo, se hace commit de las escenas o ajustes y de ese archivo juntos, y si no, *Deshacer* deja el repositorio igual que antes. Del recorte 4 en adelante no hay menú: se hacen a mano.

  OVR Metrics Tool (paso 4) dice si el cuello es la GPU (luces, sombras, MSAA, texturas, resolución: recortes 1 a 3) o la CPU (draw calls, skinning: recortes 4, 6 y 10).
- **Latencia:** si el tramo 2 es alto, mirar qué se ejecuta entre `RecibirGolpe` y la vibración (el `Debug.Log` de `Oponente.registrarGolpes`, los sonidos). Si el tramo 1 es alto, es que se pierden fotogramas: se arregla con los FPS.
- **Sesión larga:** guardar el `logcat` y el JSON de esa sesión; un cierre se diagnostica con la traza de `AndroidRuntime` o de `Unity`.
