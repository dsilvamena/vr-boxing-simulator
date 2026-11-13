# Resumen del trabajo de la entrega 5 sin visor (2026-11-11 a 2026-11-13)

Rama `entrega5/sin-visor`, creada desde `entrega5/visto-bueno`. **Sin merge ni push.** 17 commits. El detalle técnico está en `docs/Entrega5.md` (al final: lo nuevo del bloque J y los bloques K, L y M).

## En una frase

Todo lo que no necesita visor ni usuarios está hecho, y los problemas que viste en el visor (zona segura, personajes que se deslizan, réferi rígido y que contaba antes de tiempo) están corregidos. Lo que falta solo se puede hacer con el visor y con personas: medir FPS y latencia, la sesión de 30 minutos, las pruebas HEART y grabar la guardia real. **El 100 % de la entrega depende de esas sesiones**: el informe tiene 32 huecos en rojo esperando esos datos.

## Lo que pediste el 2026-11-13 y cómo quedó

| Problema que viste | Causa | Corrección | Validar en el visor |
|---|---|---|---|
| "VUELVE AL ÁREA SEGURA" estando dentro, no se quitaba ni con el joystick ni redibujando el Guardián | El área del Guardián que da el Quest (OpenXR) está centrada en el centro del área dibujada; el juego medía la cabeza desde donde estaba el visor al arrancar o al recentrar. Si no empezabas justo en el centro, el área quedaba desplazada. Además solo se leía al empezar la escena y la pausa no tenía salida | Se convierte el área al sistema de la cabeza, se relee al recentrar, al volver del menú del Quest y cada 2 s, hace falta estar fuera 0,25 s, y **si pulsas REANUDAR estando "fuera", el juego deja de usar esa área** (el Guardián del Quest sigue avisando). Nunca te deja atrapado | Empezar lejos del centro, recentrar, redibujar: no debe saltar dentro y sí al salir |
| El joystick movía al jugador | `DebugLocomotion` estaba activo en las cinco peleas y en el APK (incumplía RNF-04) | Solo funciona en el Editor | Los sticks no mueven ni giran |
| El réferi y los rivales se deslizan, no dan pasos | No hay ninguna animación de caminar en el proyecto (tampoco de Glass Joe) | Componente nuevo `PasosProcedurales`: cada pie se clava en la lona y da un paso en arco cuando el cuerpo se aleja o gira, en cualquier dirección (también en el esquive y al girar). Quieto, manda la animación | Que se vea natural a escala real |
| El brazo del réferi al contar es tieso | Era un brazo recto bajando en línea recta | Brazo con un muelle (baja rápido, se pasa un poco y se asienta), antebrazo con retraso tipo látigo, codo algo doblado arriba, torso y cabeza que acompañan cada número. Además acelera y frena al moverse | Ídem |
| El réferi empieza a contar mientras el rival aún se está cayendo | La cuenta empezaba en el instante de la caída; el clip tarda 1,6 s en dejarlo en el suelo | Espera a que el rival esté tendido y medio segundo más (el "1" llega unos 2,1 s después de la caída); mientras, el réferi camina hacia él | Ídem |

Otras cosas que no se ven naturales y **no** corregí (las anoto para el bloque L): los pies de Soda en los ganchos (durante los ataques mandan los clips), y el esquive de los rivales sigue siendo un desplazamiento sin animación propia (ahora con pasos).

## Lo que pedía la tarea original

1. **Pruebas HEART (bloque K):** el juego guarda el tiempo hasta la primera guardia y el primer golpe conectado (RNF-03); `docs/Protocolo_HEART.md` (guion completo), cuestionario, hoja de observación y plantillas en `docs/heart/`, y `Tools/tabla_heart.py`, que lo junta todo por participante y dice si se cumplen RNF-03 y RNF-05.
2. **Día del visor (bloque J):** `vrboxing_dev_sinmsaa.apk` para comparar el MSAA sin recompilar, y el menú *Tools > Boxeo > Rendimiento* para aplicar y deshacer los recortes 1 a 3 con un clic (comprobado: aplicar y deshacer deja `git diff` vacío). Ninguno aplicado.
3. **Informe (bloque M):** `docs/informe/Informe_SimuladorBox_Semana_14.tex` con la sección 12 (entregable 5), pirámide de Freytag dibujada, Retention adaptada, Gantt con estado, trazabilidad de RF y RNF, y la comparación de los bocetos con capturas nuevas. Compila sin errores (55 páginas). No subí su PDF porque tiene pendientes.
4. **Verificación sin visor de RNF-04, RNF-06 y RNF-07** (en `docs/Entrega5.md`).
5. **Documentación:** `docs/Entrega5.md`, los dos protocolos, `CLAUDE.md` y la memoria del proyecto.

## Medidas de RNF-03 (definición)

- **Guardia:** las dos manos donde bloquean (a menos de 0,4 m de los ojos y por delante de la cara) mantenidas 0,5 s seguidos; cuenta el momento en que las subió.
- **Golpe válido:** el primer golpe que conecta (ni bloqueado ni esquivado).
- **Reloj:** tiempo de juego desde la primera campana, sin pausas (los descansos sí cuentan). Por combate y por sesión; el de sesión suma los combates anteriores (si alguien abandona y vuelve a empezar). Se usa el de sesión.
- **Cumple** quien hace las dos cosas en 120 s o menos; RNF-03 se cumple si lo logra el 80 % de los novatos.

## APK (`Builds/Android/`, fuera del repositorio)

| APK | Tamaño | Para qué |
|---|---|---|
| `vrboxing_dev.apk` | 193,6 MB | El de todas las pruebas (MSAA 4×) |
| `vrboxing_dev_sinmsaa.apk` | 193,6 MB | Comparar FPS sin MSAA |
| `vrboxing_release.apk` | 172,8 MB | Comparar con la build sin *Development* |

Los tres son del commit `b5f824d` (con todas las correcciones), paquete `com.utec.vrhola`, nombre "Simulador de Boxeo RV", solo Quest 2. **Los APK que tenías antes no traen las correcciones de la zona segura ni de las animaciones: instala estos.**

## Decisiones que tomé (revísalas)

- **Pasos procedurales en vez de un clip de caminar**: un clip resbalaría igual al ir de lado o girar, y no hay clips de caminar de Mixamo en el proyecto. Si consigues uno, se puede combinar.
- **Válvula de seguridad del Guardián**: si la geometría vuelve a fallar en algún visor, REANUDAR la desactiva para esa sesión en lugar de dejarte atrapado. El precio: en ese caso solo avisa el Guardián del propio Quest.
- **Joystick solo en el Editor** (RNF-04), en vez de quitarlo: sirve para probar sentado con el simulador.
- **Golpe válido = conectado** para RNF-03, y **Retention = intención de volver + si sigue jugando** cuando se le ofrece parar (las semanas 2 y 3 no se pueden medir).
- **El nivel 4 en las pruebas HEART** (parte C, opcional) para observar combos y el roce del esquive, porque un novato en 12 minutos no suele llegar.
- **Informe:** copias propias `_s14` de las secciones que cambian, para no tocar el de la semana 13.

## Cosas que tienes que revisar

1. **Cambios tuyos sin commit en el informe de la semana 13** (del 2026-11-11, 21:00): cambiaste "Unity 6 (6000.4.7f1)" por "Unity 2022 LTS" en `s01_05`, `s06_08`, `s09_10` y `s11`, y recompilaste el PDF; también hay `docs/informe/*.aux/.out/.toc` y `Informe_SimuladorBox_Entrega4.pdf` en la raíz. **No los toqué ni los incluí en mis commits.** En la semana 14 seguí el mismo criterio ("Unity 2022 LTS"), pero ojo: el proyecto está en Unity 6 (`ProjectVersion.txt`), así que el informe dice algo distinto a lo real. Decide tú cuál queda.
2. **RNF-07**: 68 de 70 golpes detectados (97 %); los dos fallos fueron a 10 y 12 m/s cerca del borde de la cabeza. Cumple, pero no averigüé la causa exacta.
3. **Unity cambió tu licencia** Personal por una de estudiante durante la sesión (sale en la consola como "License revoked" seguido de "Student license has been added"). No afecta al proyecto.
4. **`DefensaOponente`** lee la pose animada de las manos con `GetIKPosition`, que (descubrí) devuelve el objetivo del fotograma anterior. Funciona porque ese desfase se borra durante la subida de la guardia, pero si algún día ves la guardia rara, es lo primero que miraría.
5. La **dificultad de Tyson** sigue sin confirmar con alguien jugando de verdad.

## Qué hacer, en orden

**El día del visor** (`docs/Protocolo_Rendimiento.md`):

1. Sacar los datos que haya en el visor y luego `adb install -r Builds/Android/vrboxing_dev.apk`.
2. Comprobar lo del bloque L: zona segura (empezar lejos del centro, recentrar, redibujar), sticks sin efecto, pasos y gesto del réferi.
3. Jugar los niveles 1 a 5 (pasos 2 y 3 del protocolo) y sacar las tablas con `Tools/tabla_rendimiento.py`.
4. Si no llega a 72 FPS: instalar `vrboxing_dev_sinmsaa.apk` y repetir el nivel peor; después, los recortes 1 a 3 del menú, de uno en uno, compilando y midiendo el nivel peor.
5. Sesión de 30 minutos (paso 7).
6. Grabar la guardia real (`GrabadoraGuardia` en `Main_Scene`, los dos sticks) y probar la dificultad de Tyson.

**El día de las pruebas con usuarios** (`docs/Protocolo_HEART.md`):

1. Imprimir las hojas (una por participante) y el cuestionario; tener la transmisión del visor en un teléfono.
2. Preparación: APK correcto, háptica al máximo, niveles abiertos hasta el 4, ensayo que compruebe los campos `segundosHasta...` en el JSON.
3. De 5 a 8 novatos, unos 25 minutos cada uno.
4. `adb pull` de las sesiones, pasar hojas y cuestionarios a CSV, `python3 Tools/tabla_heart.py ...`.
5. Pegar las tablas en `docs/Entrega5.md`, rellenar los 32 `\pendiente` del informe, compilar y subir el PDF.

## Commits (del más antiguo al más nuevo)

```
c2a48c0 Registrar el tiempo hasta la primera guardia y el primer golpe conectado (RNF-03)
082f92e Añadir el cuestionario, la hoja de observación y las plantillas CSV de las pruebas HEART
1a08d62 Añadir Tools/tabla_heart.py para cruzar sesiones, observación y cuestionario por participante
9f687ba Añadir FoveacionFija, que fija la foveación al iniciar si se añade a una escena
4009aed Añadir Tools > Boxeo > Rendimiento para aplicar y deshacer los recortes 1 a 3
d8ca49f Ignorar las carpetas de datos que se sacan del visor en las pruebas
4554ee6 Añadir docs/Protocolo_HEART.md con el guion de las pruebas con usuarios
6b01f40 Poner en el protocolo de rendimiento el APK sin MSAA y el menú de recortes
27527c9 Corregir la pausa del Guardián que avisaba estando dentro del área
6be4114 Desactivar la locomoción por joystick fuera del Editor (RNF-04)
edeab6b Empezar la cuenta del rival cuando ya está tendido en la lona
e8dc48b Hacer más natural el gesto de la cuenta y el desplazamiento del réferi
b5f824d Dar pasos a los rivales y al réferi al desplazarse en vez de deslizarse
c25ec89 Añadir capturas de la viñeta con poca salud y en la cuenta del jugador
9082389 Preparar el informe de la semana 14 con la sección del entregable 5
aa8fda9 Documentar en Entrega5 los bloques K, L y M y lo nuevo del bloque J
2a9a320 Actualizar CLAUDE.md con lo de la entrega 5 sin visor y las correcciones del visor
```

(más el commit de este resumen)
