# Plan de la entrega 4 (semana 13): avance del 70 %

> `docs/Entrega4.md` cita este archivo, pero no estaba en el repositorio. Se reconstruyó a partir del informe del curso (sección 8: cronograma, Gantt y trazabilidad) y de lo que el registro ya decía de cada bloque. Los bloques A, B y C son los que ya estaban hechos; D a G son los de esta segunda parte.

## Qué pide el informe

| Fuente | Entregable 4 |
|---|---|
| Actividades (8.1) | IA de los niveles 3 y 4 (Soda Popinski y Mr. Sandman). Réferi con cuenta regresiva y button mashing tras el KO. Rondas de 3 × 90 s. Hand Grab Recorder en los bloqueos y háptica diferenciada. |
| Resultado verificable (8.1) | Cuatro rivales jugables y ciclo de KO funcionando. |
| Gantt (8.2), semana 13 | Háptica diferenciada (RF-15), salud diegética (RF-14), Hand Grab Recorder para los bloqueos, IA de los niveles 3 y 4 (RF-07, RF-08), réferi, KO y rondas (RF-10 a RF-13), optimización (RNF-01, RNF-02, semanas 13 y 14), audio 3D (RF-16) y registro de métricas de sesión (RF-21, semanas 13 y 14). |
| Trazabilidad (8.3) | RF-10 a RF-13 en el entregable 4; RF-07, RF-08, RF-14, RF-15, RF-16 y RF-21 se reparten entre el 3, el 4 y el 5. |

## Bloques

| Bloque | Contenido | Requisitos | Estado |
|---|---|---|---|
| A | Rondas de 3 × 90 s con campana y ganador por KO o por salud | RF-10, RF-11 | Hecho |
| B | Réferi: cuenta de 10 s en voz 3D y button mashing sin HUD | RF-12, RF-13, RF-16 | Hecho |
| C | IA de los niveles 3 y 4: combos de Soda, esquive y contraataque de Sandman, nivel 4 jugable | RF-07, RF-08, RF-09 | Hecho, con el modelo definitivo de Sandman (`MrSandman.fbx`, riggeado por el equipo) |
| D | Hand Grab Recorder: grabar guardias con el visor y usarlas en los bloqueos del rival | RF-07 (presentación) | Hecho, con una guardia provisional sintetizada hasta grabar una real |
| E | Háptica diferenciada completa: golpe esquivado por el rival, golpe que lo derriba y tic de la cuenta | RF-15 | Hecho |
| F | Réferi visible en 3D que se coloca, cuenta con el brazo y muestra los botones | RF-12 | Hecho, con el modelo definitivo (`Referee.fbx`, reducido para el Quest) |
| G | Registro de métricas de la sesión (JSON y CSV) y FPS durante los rounds | RF-21, base de RNF-01 y RF-18 | Hecho; falta medirlo en el Quest |

Fuera de esta entrega, según el cronograma: Iron Mike Tyson y el nivel 5, la escena de resultados con el cinturón (RF-18, RF-19), la optimización a 72–90 FPS y la latencia háptica medidas en el visor (RNF-01, RNF-02) y las pruebas HEART (semana 14).

## Restricción

No hay visor. Los modelos del réferi y de Mr. Sandman llegaron al final de la entrega y ya están montados; el de Iron Mike Tyson también llegó y quedó guardado en `Art/Characters/MikeTyson/Source~` para el nivel 5. Todo se probó en Play en el Editor, con los menús contextuales y llamadas por código.
