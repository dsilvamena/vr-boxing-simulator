# Entrega 3 (semana 12): avance del 50 %

Según el cronograma del informe (sección 8.1), la entrega 3 debe mostrar la **IA reactiva de los niveles 1 y 2 (Glass Joe y Bald Bull) con ataque, bloqueo y velocidad**, el **esquive con cabeza y torso** y la **salud diegética con viñeta y pulso cardíaco**. El resultado verificable es: *combates completos contra dos rivales, con la salud percibida sin HUD*.

Rama: `feature/entrega-3-50`.

## Qué incluye

| Área | Qué hace el juego |
|---|---|
| Combate completo, niveles 1 y 2 | Glass Joe y Bald Bull atacan, bloquean, se acercan y caen tres veces (dos knockdowns y la derrota). Al ganar se carga el siguiente nivel. |
| Bloqueo del rival (RF-07) | Cada rival tiene su probabilidad de bloqueo, que sube con cada golpe seguido: el rival "lee" la combinación. Al bloquear sube la guardia (los guantes frente a la cara, por IK), la mantiene un momento y después queda abierto. Mientras ataca no puede bloquear, así que el contragolpe siempre entra. Un golpe bloqueado quita poco y nunca lo tumba. |
| Velocidad e IA reactiva (RF-07, RF-08) | Cada rival tiene su velocidad de ataque y de desplazamiento. Se acerca si el jugador se aleja (sin salir del ring), ataca antes si el jugador baja la guardia y contraataca rápido después de bloquear. |
| Dificultad ascendente (RF-08) | Glass Joe < Bald Bull < Soda Popinski en salud, daño, bloqueo, velocidad y agresividad (tabla de abajo). |
| Datos configurables (RNF-10) | Todo lo anterior está en el asset `PerfilOponente` de cada rival (`Assets/_Project/Data/Oponentes/`), sin tocar el código. |
| Ataque al jugador, bloqueo y esquive (RF-04 a RF-06) | El rival apunta a la cabeza al empezar el ataque: si el jugador la saca de la línea, esquiva; si tiene ambos guantes frente a la cara, bloquea y recibe solo el 25 % del daño. |
| Salud diegética sin HUD (RF-14) | La visión periférica se oscurece al perder salud, y un latido aparece y se acelera. Las barras de depuración empiezan ocultas. El latido ahora es un clip corto de un solo lub-dub (el anterior duraba 25 s y cada latido lo cortaba). |
| Háptica diferenciada (RF-15) | Patrones distintos para golpe conectado (uno por tipo de golpe), **golpe bloqueado por el rival** (dos toques sordos de baja frecuencia), golpe bloqueado por el jugador y golpe recibido. |
| Audio espacial 3D (RF-16) | Golpes conectados y bloqueados en el rival, golpe recibido y bloqueo en la cabeza o los guantes del jugador, murmullo de la multitud desde cuatro puntos de las gradas, y ovación desde el lado donde cae alguien. |
| Desbloqueo de niveles (RF-09) | En el menú, cada nivel aparece como BLOQUEADO hasta ganar el anterior. |
| Pausa por el Guardián (RF-20) | La pelea se pausa si la cabeza sale del área del Guardián, si se abre el menú de Meta o si se quita el visor, con el aviso "VUELVE AL ÁREA SEGURA". Se reanuda sola al volver. |

### Valores de cada rival

| Parámetro | Glass Joe | Bald Bull | Soda Popinski |
|---|---|---|---|
| Salud | 100 | 150 | 200 |
| Probabilidad de bloqueo (base → máxima) | 8 % → 30 % | 20 % → 55 % | 30 % → 65 % |
| Velocidad de ataque | ×0,9 | ×1,15 | ×1,25 |
| Desplazamiento | 0,4 m/s | 0,7 m/s | 0,9 m/s |
| Daño jab / uppercut | 4 / 7 | 8 / 14 | 10 / 18 |
| Castigo por guardia baja | +30 % | +80 % | +100 % |

## Trazabilidad de requisitos

Estado al cierre de esta entrega. La columna *Entregable* es la del informe (sección 8.3).

| ID | Requisito | Entregable | Estado |
|---|---|---|---|
| RF-01 | Primera persona y guantes con 6DoF | 1 | Hecho |
| RF-02 | Colisión guante–oponente | 2 | Hecho |
| RF-03 | Clasificación jab, cross, uppercut | 2 | Hecho (también ganchos laterales) |
| RF-04 | Daño según tipo de golpe y velocidad | 3 | Hecho |
| RF-05 | Bloqueo del jugador con reducción de daño | 3 | Hecho |
| RF-06 | Esquive con cabeza y torso | 3 | Hecho |
| RF-07 | Rivales con patrón de ataque, velocidad y bloqueo propios | 3, 4 y 5 | Hecho para los niveles 1 y 2 (y el 3). Faltan Mr. Sandman e Iron Mike Tyson, y el esquive activo del rival |
| RF-08 | Dificultad ascendente | 3, 4 y 5 | Hecho para los niveles 1 a 3 |
| RF-09 | Desbloqueo del siguiente nivel | 2 y 3 | Hecho |
| RF-10 | 3 rounds de 90 s con campana | 4 | Pendiente |
| RF-11 | Ganador por KO o por salud al final | 4 | Hecho por KO; falta por salud (depende de los rounds) |
| RF-12 | Cuenta de 10 s y button mashing | 4 | Adelantado: hecho sin el modelo del réferi |
| RF-13 | Derrota y reintento del nivel | 4 | Adelantado: vuelve al menú con el nivel abierto |
| RF-14 | Salud diegética sin HUD | 3 y 4 | Hecho |
| RF-15 | Háptica conectado, bloqueado y recibido | 2, 3 y 4 | Hecho |
| RF-16 | Audio 3D de golpes, multitud y réferi | 3 y 4 | Hecho para golpes y multitud; falta la voz del réferi |
| RF-17 | Menú diegético Jugar, Opciones, Salir | 1 y 2 | Hecho con puntero láser, no con contacto de los guantes |
| RF-18 | Estadísticas al terminar el combate | 5 | Pendiente (el rival ya cuenta golpes conectados y bloqueados) |
| RF-19 | Cinturón de campeón | 5 | Pendiente |
| RF-20 | Pausa por el Guardián | 1 y 2 | Hecho |
| RF-21 | Registro de métricas por sesión | 4 y 5 | Pendiente |

### Diferencias con el informe

- **Guantes:** siguen a los controles como hijos de los anclajes de las manos del `OVRCameraRig`, sin `HandGrabbable`. Así no se separan del control ni en golpes rápidos.
- **Guardia del rival:** se hace con IK de manos sobre el Animator Humanoid en lugar de secuencias grabadas con Hand Grab Recorder. La sección 6.3 deja el Recorder para el entregable 4.
- **Audio:** los efectos están sintetizados con `Tools/sintetizar_audio.py` en vez de venir de un banco de sonidos. Se pueden reemplazar por grabaciones sin cambiar el código: basta con asignar otros clips en los perfiles y en las escenas.

## Pruebas realizadas (Meta XR Simulator, Unity 6000.4.7f1)

| Prueba | Resultado |
|---|---|
| Compilación sin errores ni warnings propios | OK |
| Menú: con el progreso en 0 solo el nivel 1 está abierto; `PlayLevel(2)` no carga; con el nivel 1 ganado se abre el 2 | OK |
| Nivel 1: el rival camina desde 4 m hasta unos 1,15 m del jugador y ataca | OK |
| Probabilidad de bloqueo medida en 2000 intentos: 7,0 % (perfil: 8 %); sube 8 → 12 → 16 → 20 % con golpes seguidos; 0 % mientras ataca | OK |
| Golpe físico: el `Puno` derecho se mueve contra la hitbox de la cabeza a 4 m/s; 12 golpes = 11 conectados (Cross, cabeza) y 1 bloqueado, con dos knockdowns | OK |
| Combate completo nivel 1 (*Probar: ganar el combate*): bloqueos, dos knockdowns, derrota, cartel de victoria y carga del nivel 2 | OK |
| Combate completo nivel 2: Bald Bull bloquea muchas más veces (31 bloqueos frente a unos pocos de Glass Joe); victoria, carga del nivel 3, `nivelSuperado` = 2 | OK |
| Ataques al jugador (`PruebaAtaques`): conectado, esquivado y bloqueado, con sus sonidos 3D | OK |
| Guardia por IK visible (captura del Game View con *Probar: subir la guardia*) | OK |
| Derribo del jugador: ovación 3D de las dos gradas más cercanas, murmullo en las cuatro gradas, latido nuevo sin warnings | OK |
| Pausa por el Guardián: pausa con aviso, se vuelve a abrir si el jugador la cierra estando fuera, se reanuda al volver; la pausa manual sigue mostrando "PAUSA" | OK |
| Nivel 3: Soda se acerca, ataca más rápido y el Animator vuelve a la velocidad normal tras cada ataque | OK |

Sin visor no se pudieron probar la intensidad real de la vibración, el área del Guardián del Quest ni el rendimiento (RNF-01). Conviene validarlos con un *Build And Run* en el Quest.
