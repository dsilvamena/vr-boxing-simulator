# Entrega 5 (semana 14): versión final

Registro de lo implementado según `docs/Plan_Entrega5.md`. Las capturas están en `docs/capturas/`.

## Bloque H: Iron Mike Tyson y el nivel 5 (RF-07, RF-08, RF-09)

`Scene_Ring_Level5.unity` es el último combate: Iron Mike Tyson, el rival más difícil del juego. Se llega desde el nivel 4 o desde el menú, con el nivel 5 bloqueado hasta ganar el 4. Al cerrar el bloque H, ganarlo volvía al menú; desde el bloque I lleva a la escena de resultados con el cinturón.

### Modelo

El equipo riggeó el modelo en Blender (`Source~/mike_rig.blend`) y lo exportó como `Source~/export/mike_rigged_1.78m_v1.fbx`: 1,78 m, en pose T, una malla con un material y la textura embebida. El esqueleto (25 huesos) usa los nombres de Mixamo (`mixamorig:Hips`...), aunque no viene de Mixamo, y no trae animaciones.

| Archivo (`Assets/_Project/Art/Characters/MikeTyson/`) | Qué es |
|---|---|
| `MikeTyson.fbx` | El modelo que usa el juego: Humanoid, avatar propio, una malla y un material |
| `Textures/MikeTyson_BaseColor.jpg` | La textura que venía embebida (2048 px, JPEG). En Android, ASTC 6×6 y como mucho 2048 px |
| `Materials/MikeTyson.mat` | Standard (pipeline integrado), suavidad 0,2. El FBX lo remapea por nombre |
| `MikeTyson.prefab` | El modelo con el controlador de Soda y `ApoyoEnSuelo`, como `MrSandman.prefab` |
| `Source~/` | Los `.blend` del equipo, los FBX exportados, la textura y las capturas del rigging. Unity no importa esta carpeta |

- **Reducción reproducible:** el FBX tenía 31 092 triángulos, el doble que Mr. Sandman. `Tools/reducir_tyson.py` lo abre en Blender sin interfaz, corrige los pesos del hombro (ver abajo), aplica un Decimate antes del Armature (conserva pesos, UV y material), saca la textura y exporta `MikeTyson.fbx` en metros, sin huesos hoja. Queda en 14 999 triángulos (15 366 vértices en Unity, por las costuras de UV). Se regenera con `Blender -b --python Tools/reducir_tyson.py -- "Assets/_Project/Art/Characters/MikeTyson/Source~/export/mike_rigged_1.78m_v1.fbx" Assets/_Project/Art/Characters/MikeTyson 15000`; sobrescribir el FBX conserva su `.meta`.
- **Avatar:** con los nombres de Mixamo, el mapeo automático reconoció los 22 huesos humanoides (`Spine1` → `Chest`, `Spine2` → `UpperChest`). No hizo falta mapear nada a mano. No tiene dedos.
- **Pesos del hombro:** en la primera prueba le salía una "cuchilla" de la axila al subir la guardia. En el rig original, unos 130 vértices de lo alto de cada hombro (junto al cuello, a más de 20 cm del antebrazo) tenían hasta un 94 % de peso en el antebrazo, y al doblar el codo salían estirados hacia él. `reducir_tyson.py` pasa al hueso más cercano (aquí, el hombro) el peso de antebrazo de cualquier vértice a más de 15 cm de ese hueso: 720 pares vértice-peso por los dos lados. Las manos no se tocan: sin dedos, su hueso mide 17 cm y el guante llega a 18 cm de él.
- **Animaciones:** las de Soda (`SodaPopinski.overrideController`), retargeteadas por el avatar humanoide, como Sandman. Escala 1.
- **Pies:** con los clips de Soda las suelas quedaban de 6 a 14 cm bajo la lona (muestreo de cada clip). `ApoyoEnSuelo` lo corrige igual que en Sandman: 4 huesos y 465 puntos de suela, calculados con *Calcular suelas*. En Play sube la cadera 6,7 cm en `Idle` y el vértice más bajo queda a 3 mm de la lona.

### Perfil

`Assets/_Project/Data/Oponentes/MikeTyson.asset`. Comparado con Mr. Sandman:

| Parámetro | Mr. Sandman | Iron Mike Tyson |
|---|---|---|
| Salud | 175 | **250** |
| Bloqueo (base → máxima, por golpe seguido) | 25 % → 55 %, +8 % | **30 % → 65 %, +10 %** |
| Esquive | 45 %, 1,0 s de enfriamiento, 0,35 m | **50 %, 0,9 s**, 0,35 m |
| Esquive: salida y regreso | 0,12 s y 0,3 s | **0,10 s y 0,28 s** |
| Contraataque tras esquivar | 0,25 a 0,5 s | **0,2 a 0,4 s** |
| Velocidad de ataque | ×1,4 | **×1,5** |
| Desplazamiento | 1,1 m/s | **1,2 m/s** |
| Patrón | gancho, barrido, gancho | **gancho izq., gancho der., barrido, gancho izq.** |
| Pausa entre golpes del combo | 0,12 s | **0,10 s** |
| Espera entre ataques | 2,8 a 4 s | **2 a 3 s** |
| Espera tras recibir un golpe | 0,8 a 1,3 s | **0,7 a 1,1 s** |
| Espera tras levantarse | 1,8 s | **1,5 s** |
| Contraataque tras bloquear | 0,35 a 0,7 s | **0,3 a 0,6 s** |
| Daño por golpe (gancho / barrido) | 8 / 12 | **9 / 14** |
| Castigo por guardia baja | +100 % | **+120 %** |
| Altura del golpe | 1,6 m | **1,5 m** (mide 1,78 m; Sandman, 1,88 m) |

Lo demás (guardia de 0,7 s, 15 % de daño al bloquear, cuenta de 10, 3 caídas, alcance de 1,7 m, `Guardia_Provisional`, sonidos) es igual que en Sandman.

**El combo** son los estados `Ataque_Gancho_2`, `Ataque_Gancho`, `Ataque_Uppercut` y `Ataque_Gancho_2` (`combo` = `Gancho2, Gancho, Uppercut, Gancho2`). Termina en `Ataque_Gancho_2` y no en `Ataque_Gancho`, cuyo clip trae un segundo golpe que no cuenta. No usa `Ataque_Jab` (en estos clips es `Ataque_Cross`, con 1,1 s de preparación).

### Tiempos de impacto medidos

Se muestreó cada clip sobre el cuerpo de Tyson a 30 fotogramas por segundo (`AnimationClip.SampleAnimation` en el Editor) y se buscó el momento en que el puño cruza por delante de la cara. Coinciden con los medidos en Soda (entrega 4), así que se mantienen los `retardoImpacto*` de Sandman.

| Clip | Estado | Impacto en el clip | Puño (cabeza) | Tyson ×1,5 |
|---|---|---|---|---|
| `Ataque_Gancho_2` (izquierda) | `Ataque_Gancho_2` | 0,50 s (más adelantado; cruza la cara a 0,53 s) | 1,30 m (1,52 m) | 0,33 s |
| `Ataque_Gancho` (derecha) | `Ataque_Gancho` | 0,80 s (barrido de 0,6 a 0,9 s) | 1,11 m (1,41 m) | 0,53 s |
| `Ataque_Fuerte`, primer barrido | `Ataque_Uppercut` | 0,30 s | 1,02 m (1,37 m) | 0,20 s |

Los cuatro golpes llegan a 0,33; 0,97; 1,27 y 1,70 s desde el inicio del combo. Todos van por debajo de la cara (los de Soda también): la lógica del impacto apunta a la cabeza del jugador.

### Hitboxes y componentes

*Configurar oponente seleccionado* creó `Hitbox_Cabeza` (esfera de 15,4 cm de radio en `mixamorig:Head`) y `Hitbox_Torso` (caja de 0,46 × 0,53 × 0,33 m en `mixamorig:Spine1`) y dejó `Oponente`, `ReaccionAnimada`, `DefensaOponente` y `EsquiveOponente` enlazados al Animator. Se revisaron contra la malla animada en Play: la esfera queda a unos 4 cm del centro de la cabeza (33 × 28 × 33 cm con las orejas) y la caja cubre el tronco de 0,99 a 1,52 m. No se movieron. `AtaqueOponente` ya estaba en la raíz que se conservó del nivel 4.

### Escena y flujo

- **`Scene_Ring_Level5.unity`** es una copia del nivel 4 con `Arbitro`, `ControlRondas`, `Multitud`, `PauseMenu`, `PausaGuardian`, rig y Canvas. Igual que con Sandman, se conservó la raíz del rival (ahora `MikeTyson`) con todos sus componentes y las referencias que le tienen `Arbitro`, `ControlRondas` y los demás; solo se cambiaron sus hijos (el modelo), el avatar, el perfil, las suelas y las hitboxes. Antes se comprobó que solo `ApoyoEnSuelo` apuntaba a huesos del modelo viejo. Está en Build Settings con el índice 5.
- **`ControlCombate`:** nivel 4 → `Scene_Ring_Level5` con "Siguiente: Nivel 5"; nivel 5 (`nivel` = 5) → `Main_Scene` con "¡Eres el nuevo campeón!". El 4 ya no dice "Volviendo al menú principal". Desde el bloque I, toda victoria pasa antes por `Scene_Results`, y `siguienteEscena` solo se usa si no hay escena de resultados.
- **Menú:** `MainMenu.Levels` tiene las cinco escenas y `levelButtons` cinco botones. `ButtonNivel5` ("NIVEL 5 / IRON MIKE TYSON", `PlayLevel(5)`) va solo en una segunda fila, centrado bajo los otros cuatro, y ATRÁS baja a y = −110: cinco botones en fila ocuparían 1 950 px de los 2 128 del Canvas. Ver `docs/capturas/12_menu_niveles_5.png`.
- **`RegistroSesion`:** no necesitó cambios; los combates del nivel 5 quedan en `combates.csv` con nivel 5 y rival "Iron Mike Tyson".

### Cambio en `AtaqueOponente`: no atacar fuera de alcance

Con la espera de 2 a 3 s, Tyson lanzaba combos enteros al aire al empezar el round, desde 3,75 y 2,8 m, mientras aún caminaba hacia el jugador: en una pelea simulada fallaron así 12 de 48 ataques. Sandman casi nunca llegaba a hacerlo, porque su espera es más larga. Ahora, si la cabeza del jugador queda más lejos que `alcanceAtaque` desde `alturaAtaque` (la misma geometría que el impacto), no lanza y sigue acercándose; el ataque sale en cuanto llega. Afecta a los cinco rivales, pero solo en ese caso: `distanciaCombate` siempre queda dentro del alcance. Con el cambio, ningún ataque de Tyson ni de Sandman se lanzó fuera de alcance.

### Por qué es más difícil que Mr. Sandman

La simulación de la entrega 4 no estaba en el repositorio, así que se reconstruyeron sus dos partes.

- **El modelo** (`Tools/modelo_dificultad.py`, sin dependencias) lee los perfiles y simula 20 000 peleas con la lógica de `RecibirGolpe`, `DefensaOponente` y `EsquiveOponente`, con crosses a la cabeza de 7,5 de daño. Reproduce la tabla de la entrega 4 con 0 a 2 golpes de diferencia (Sandman: 52,8 / 51,6 / 62,4 frente a 52 / 51 / 62) y su daño por segundo exacto. El daño por segundo es el daño del combo entre la espera media más el tiempo del lanzamiento del primer golpe al del último, porque `AtaqueOponente` empieza a contar la espera al lanzar el último.
- **La pelea simulada** es una partida en Play, a ×2, con un jugador programado: la cabeza a 1,65 m, quieta y mirando al rival, las manos siempre en guardia, ráfagas de 5 crosses a la cabeza (cada 0,5 s) cada 6 s, y pulsaciones de A/B/X/Y si cae. Se juega igual en los niveles 4 y 5. Con este arnés Sandman dura 92 s antes del cambio de alcance (95 s en la entrega 4), lo que indica que es comparable.

| Medida | Mr. Sandman | Iron Mike Tyson |
|---|---|---|
| Daño por segundo si todo conecta, guardia arriba (modelo) | 6,65 | 10,60 (+59 %) |
| Golpes para ganarle, a 1 golpe cada 0,5 s (modelo) | 52,8 | 80,9 |
| Golpes para ganarle, en ráfagas de 5 con 6 s de pausa (modelo) | 51,6 | 78,3 |
| Golpes para ganarle, a 1 golpe cada 1,5 s (modelo) | 62,4 | 101,6 |
| Pelea simulada: segundos de round (dos partidas) | 98,1 y 98,6 | 117,6 y 117,1 (+19 %) |
| Pelea simulada: daño que recibe el jugador | 91 y 86 | 150 y 138 (+65 %) |
| Pelea simulada: veces que cae el jugador | 0 y 0 | 1 y 1 |
| Pelea simulada: golpes que lanza el jugador | 50 y 51 | 64 y 63 |
| Pelea simulada: salud final del jugador | 29 y 34 | 10 y 23 (se levantó con 40) |

En las cuatro partidas ganó el jugador por KO: Tyson es el más difícil, pero se le puede ganar.

**Cómo se llegó a estos valores.** La primera versión (salud 190, daño 8 / 12) salía casi igual de difícil que Sandman en la pelea simulada: 90,5 s frente a 92 s, y el jugador acababa con la misma salud (37 frente a 36). El modelo no lo ve porque el rival no ataca en él, pero en Play Tyson pasa cerca del 60 % del tiempo en una animación de ataque (cuatro golpes, más la recuperación del último, cada 2 a 3 s), y en ese estado no bloquea ni esquiva: un jugador que pega sin parar le entra más que a Sandman. Para no tocar el combo largo ni la espera de 2 a 3 s, se subieron el daño (9 / 14) y después la salud: con 220 duraba 94 s; con 250, 117 s.

### Criterio de terminado

| Criterio | Resultado |
|---|---|
| Del menú al nivel 5 en una partida continua | OK: con *Desbloquear todos los niveles* (`nivelSuperado` = 5) el botón del nivel 5 se habilita y carga la escena. Del nivel 4 al 5 al ganar |
| Ganar o perder el nivel 5 vuelve al menú con el progreso correcto | OK: la victoria sube `nivelSuperado` de 4 a 5 y carga `Main_Scene`; la derrota por K.O. carga `Main_Scene` sin cambiarlo |
| Tyson ataca, bloquea, esquiva, cae, se levanta con la cuenta y se deja derrotar | OK: ver las pruebas |
| Medible y claramente más difícil que Mr. Sandman | OK: ver la tabla anterior |
| Sin errores ni warnings propios; compila para Android | OK en el Editor y al compilar los scripts para Android (70 ensamblados), sin generar el APK. Los únicos avisos son de OpenXR por no haber visor |

### Pruebas (Play en el Editor, Unity 6000.4.7f1)

Sin visor, para los ataques se desactivó `OVRCameraRig` en Play y se recolocaron la cabeza y las manos en cada fotograma desde `Application.onBeforeRender`, después de que OVR las reescriba. Las escenas no se guardaron.

| Prueba | Resultado |
|---|---|
| Avatar | Válido, 22 huesos humanoides mapeados automáticamente |
| `Idle` y guardia | OK: guardia arriba, mirando al jugador, botas sobre la lona. Sin la "cuchilla" del hombro tras corregir los pesos (`docs/capturas/11_nivel5_mike_tyson.png`) |
| Ataques | OK: combos de cuatro golpes; con la guardia del jugador arriba, todos `Bloqueado` (36 a 60 por pelea) |
| Bloqueo forzado (*Probar: subir la guardia*) y golpe a la cabeza | OK: `Bloqueado`, 1,1 de daño (7,5 × 0,15) |
| *Probar: esquivar* | OK: se aparta, sin daño, y vuelve a la misma posición. Rechazado con el motivo "está atacando" mientras ataca |
| Esquives y bloqueos en la pelea simulada | OK: 8 a 14 esquives y 9 a 20 bloqueos por pelea |
| *Probar: contar al rival* | OK: `Knockdown` sostenido en 0,44 con la cabeza a 0,57 m, un golpe en el piso devuelve `NoCuenta`, cuenta del 1 al 10 (1 en t = 0,03 s, 10 en t = 9,01 s) y se levanta a los 10,01 s |
| Knockdowns y derrota en la pelea simulada | OK: dos knockdowns con cuenta y la derrota en la tercera caída, `VictoriaPorKO` |
| Menú con `nivelSuperado` = 3 | OK: NIVEL 5 BLOQUEADO, no interactuable, y `PlayLevel(5)` no carga ("Nivel 5 bloqueado: primero gana el nivel 4") |
| Menú con `nivelSuperado` = 4 | OK: "NIVEL 5 / IRON MIKE TYSON" y su `onClick` carga el nivel 5 |
| Sin progreso y con *Desbloquear todos los niveles* | OK: bloqueado; después `nivelSuperado` = 5 y abierto |
| Ganar el nivel 4 (`nivelSuperado` = 3) | OK: sube a 4, "Siguiente: Nivel 5" y se carga el nivel 5 |
| Ganar el nivel 5 (*Probar: ganar el combate*, `nivelSuperado` = 4) | OK: sube a 5, cartel "Derrotaste a Iron Mike Tyson / ¡Eres el nuevo campeón!" y vuelta a `Main_Scene` |
| Perder el nivel 5 por K.O. | OK: vuelta a `Main_Scene`, `nivelSuperado` sin cambios |
| `RegistroSesion` | OK: cada combate del nivel 5 queda en `combates.csv` con nivel 5 y rival Iron Mike Tyson |

### Pendiente de validar con visor

- **Nadie ha jugado este combate.** Los números son de un jugador programado y de un modelo. Un jugador real baja la guardia para pegar, y Tyson lo castiga un 120 % más rápido.
- Si el barrido de `Ataque_Fuerte` a ×1,5 (0,20 s hasta el impacto) da tiempo a reaccionar, y si el combo de cuatro golpes se puede defender. Si es demasiado, bajar `velocidadAtaque` a 1,4 o quitar el barrido del combo.
- Si los clips de Soda le quedan bien a sus proporciones, y si los guantes (que no tienen dedos ni hueso propio) se deforman bien al cerrar el puño.
- El rendimiento (bloque J): 14 999 triángulos y 15 366 vértices skinneados, más que Sandman (9 034 vértices). Si hace falta, se baja el objetivo de `reducir_tyson.py`.
- El botón del nivel 5 con el puntero láser: se invocó su `onClick` desde código.
- Cambiar `Guardia_Provisional` por una guardia grabada (bloque L).
- ~~"¡Eres el nuevo campeón!" sale todavía sin cinturón ni escena de resultados~~: resuelto en el bloque I.

### Decisiones y visto bueno

Revisadas el 2026-11-11: se aprobaron tal como estaban, salvo la que se indica.

- **La dificultad de Tyson** (salud 250, daño 9 / 14, combo de cuatro golpes cada 2 a 3 s): **pendiente de confirmar con el visor**. Los valores no se tocan hasta que alguien juegue el combate.
- **El cartel del nivel 5** ("Derrotaste a Iron Mike Tyson / ¡Eres el nuevo campeón!"): aprobado.
- **El botón del nivel 5 solo en una segunda fila** del menú de niveles: aprobado.
- **El cambio de `AtaqueOponente`** (no atacar con el jugador fuera de alcance, en los cinco rivales): aprobado.

## Bloque I: escena de resultados y cinturón de campeón (RF-18, RF-19)

Al terminar cualquier combate de los niveles 1 a 5, ganado o perdido, el juego muestra antes el cartel de siempre (¡VICTORIA!, K.O. o DERROTA) y después carga `Scene_Results`: una hoja con la precisión de golpes, los golpes bloqueados y el tiempo total, y dos botones para seguir. Al ganar a Iron Mike Tyson, la hoja cambia a "¡CAMPEÓN!", con el texto de cierre del arco narrativo y el cinturón de campeón girando a su lado.

### Cómo llega el resultado a la escena

No hay estado nuevo. `RegistroSesion` vive en `DontDestroyOnLoad` y cierra el combate (`CerrarCombate`) en cuanto `ControlRondas` lanza `AlTerminarCombate`, que en los cinco niveles llega antes del cartel y del cambio de escena. `CombateActual` conserva ese combate cerrado hasta que empieza otro, así que `PantallaResultados` lo lee en su `Start`:

| Dato de `DatosCombate` | Para qué lo usa la hoja |
|---|---|
| `resultado` (`VictoriaPorKO`, `VictoriaPorSalud`, `DerrotaPorKO`, `DerrotaPorSalud`) | Título, línea de cómo terminó y si se ganó |
| `nivel` | Siguiente nivel (`MainMenu.Levels[nivel]`) y si es el último (cinturón) |
| `escena`, `rival`, `roundAlcanzado` | REINTENTAR / REPETIR cargan la misma escena; subtítulo |
| `precision`, `golpesConectados`, `golpesLanzados` | Estadística 1 |
| `ataquesBloqueados`, `ataquesRecibidos`, `porcentajeBloqueo` | Estadística 2 |
| `segundosCombate` | Estadística 3 |
| `golpesBloqueadosPorRival`, `golpesEsquivadosPorRival`, `derribosAlRival`, `caidasJugador` | Línea de detalle |

`RegistroSesion` no cambió: ni sus métricas ni el JSON ni el CSV. Solo se corrigió el comentario de `CombateActual`, que decía que era null fuera de las peleas. Ya tenía todos los datos que pide RF-18.

"Golpes bloqueados" son los ataques del rival que el jugador bloqueó (su defensa). Los golpes del jugador que paró el rival van en la línea de detalle. El tiempo total es el de `RegistroSesion`: desde la campana del primer round hasta el final, con los descansos y sin las pausas.

### Flujo

| Situación | Antes | Ahora | Botón principal | Otro botón |
|---|---|---|---|---|
| Ganar los niveles 1 a 4 | ¡VICTORIA! → siguiente nivel | ¡VICTORIA! → `Scene_Results` | SIGUIENTE NIVEL (nivel + 1) | MENÚ |
| Ganar el nivel 5 | ¡VICTORIA! → `Main_Scene` | ¡VICTORIA! → `Scene_Results` con cinturón | REPETIR (nivel 5) | MENÚ |
| Perder por K.O. o por salud | K.O. / DERROTA → `Main_Scene` | K.O. / DERROTA → `Scene_Results` | REINTENTAR (mismo nivel) | MENÚ |
| Abrir `Scene_Results` sin combate (solo en el Editor) | — | "Todavía no has terminado ningún combate" | (oculto) | MENÚ, centrado |

- **`ControlCombate`:** nuevo campo `escenaResultados` (por defecto `Scene_Results`). Si está vacío, carga `siguienteEscena` como antes. `nivelSuperado` se sigue guardando al ganar, antes del cartel.
- **`RecuperacionJugador`:** nuevo campo `escenaResultados` (por defecto `Scene_Results`), con `escenaAlPerder` como respaldo. Vale para el K.O. y para `PerderPorSalud`.
- Los campos nuevos toman su valor por defecto en las cinco escenas sin volver a guardarlas, así que las escenas de pelea no cambiaron.
- La pausa no lleva a resultados: VOLVER AL MENÚ sigue yendo a `Main_Scene`, y `RegistroSesion` registra ese combate como `Abandonado`.
- Un reintento desde la hoja vuelve a cargar la escena del combate. `RegistroSesion` lo marca como `reintento` porque ese nivel ya se jugó en la sesión.

### La escena

`Scene_Results.unity` es una copia de `Main_Scene` (índice 6 en Build Settings): la misma arena de MarpaStudio con su iluminación, el `OVRCameraRig` en el mismo punto que en las peleas (0, −1,22, −3), `EventSystem` con `OVRInputModule`, `LaserPointer` con `LaserCursor` y `MenuMusic`. Se quitaron los tres boxeadores de decoración, `GrabadoraGuardia` del rig y el componente `MainMenu` con sus paneles.

El Canvas World Space (el del menú, con `OVRRaycaster`) queda a 1,5 m del jugador, con el centro a 1,57 m del suelo, y tiene un solo panel, `Resultados` (1 300 × 700 px, unos 2,1 × 1,1 m):

| Elemento | Qué muestra |
|---|---|
| `Titulo` | ¡VICTORIA! (dorado), DERROTA (rojo) o ¡CAMPEÓN! (dorado), en Bangers |
| `Subtitulo` | "Nivel N: rival" y cómo terminó ("Ganaste por K.O. en el round 2", "Perdiste por salud al terminar el último round"...), o el texto de cierre |
| `Estadisticas/Precision` | "48 %" y "33 de 69 golpes" |
| `Estadisticas/Bloqueos` | "27" y "de 27 ataques (100 %)" |
| `Estadisticas/Tiempo` | "0:59" y "1 round" |
| `Detalle` | "El rival bloqueó 30 y esquivó 6 de tus golpes · Derribos: 3 al rival, 0 tuyos" |
| `BotonPrincipal`, `BotonMenu` | Los botones del menú con su estilo, enlazados a `PantallaResultados.Continuar` e `IrAlMenu` |

Al abrir la hoja de una victoria suena una vez `Multitud_Ovacion.wav` (fuente 2D en `PantallaResultados/Ovacion`). No hay HUD durante el combate (RF-14): la hoja solo aparece fuera de él.

### Cinturón de campeón (RF-19)

En el repositorio no había ningún modelo de cinturón, y el plan pedía no descargar el de Sketchfab sin verificar su licencia. Se usó el respaldo que prevé el plan: un cinturón propio y sencillo, generado por código en Blender sin interfaz.

| Archivo (`Assets/_Project/Art/CinturonCampeon/`) | Qué es |
|---|---|
| `CinturonCampeon.fbx` | El modelo: una malla con 3 materiales, 1 540 triángulos y 2 294 vértices en Unity, sin UV ni texturas |
| `Materials/Cinturon_Cuero.mat`, `Cinturon_Oro.mat`, `Cinturon_Esmalte.mat` | Standard (pipeline integrado). El oro es metálico (0,7) y deja `_EMISSION` activa en negro para el brillo; el esmalte rojo tiene una emisión fija suave porque la arena es oscura |
| `CinturonCampeon.prefab` | El modelo sin sombras, con `CinturonCampeon` en la raíz |
| `Source~/cinturon_campeon.blend` | El `.blend` que deja el script (Unity no lo importa) |
| `Tools/crear_cinturon.py` | El script que lo construye y lo exporta |

- **Forma:** una correa de cuero negro en lazo elíptico (0,40 × 0,30 m, 10 cm de alto), como un cinturón cerrado, con filetes dorados en los bordes; delante, la placa dorada (27 × 21 cm) con un óvalo de esmalte rojo, una estrella dorada en relieve y una corona; a cada lado, dos placas octogonales doradas con el centro de esmalte. Ver `docs/capturas/15_resultados_campeon.png`.
- **Se regenera con** `Blender -b --python Tools/crear_cinturon.py -- Assets/_Project/Art/CinturonCampeon`. Sobrescribir el FBX conserva su `.meta` (sin animación ni rig, materiales remapeados por nombre). La conversión de ejes va horneada en la malla (`bake_space_transform`), así que el nodo no lleva rotación y el frente mira a +Z.
- **Animación (`CinturonCampeon`):** al activarse sube 0,6 m creciendo desde el 20 % con un pequeño rebote (1,6 s) mientras gira cuatro veces más rápido; después gira a 35 °/s. El oro pulsa la emisión (0,2 a 0,75, a 0,7 Hz) con un `MaterialPropertyBlock` y da un destello al terminar de subir. Usa tiempo sin escalar. No lleva luz propia: en el Quest cada luz en tiempo real añade una pasada por objeto, y la emisión y el reflejo del metal bastan.
- **Colocación:** en la escena, desactivado, a la izquierda del jugador (−0,72 m), 1,2 m por delante y a 1,5 m del suelo, con escala 1,4. Al ganar el nivel 5, `PantallaResultados` lo activa y aparta el Canvas 0,45 m a la derecha, girado 15° hacia el jugador y al 80 % de su tamaño, para que los dos quepan en la vista.
- **Coste:** 1 540 triángulos y 3 draw calls, sin texturas. Solo existe en esta escena, donde no hay ningún rival.

**Texto de cierre** (el desenlace de la tabla del arco narrativo del informe, `s01_05_introduccion_requisitos_marco.tex`): "¡CAMPEÓN!" y "Derrotaste a Iron Mike Tyson, el campeón invicto. El cinturón de campeón regional es tuyo." Retoma lo que dice el informe: la meta es "el cinturón de campeón regional" y Tyson es "el campeón invicto". El cartel del nivel 5 sigue diciendo "Derrotaste a Iron Mike Tyson / ¡Eres el nuevo campeón!" antes de cargar la hoja. El texto está en el campo `textoCampeon` de `PantallaResultados`.

### Criterio de terminado

| Criterio | Resultado |
|---|---|
| Al ganar a Tyson se ven el cinturón y las tres estadísticas | OK: ¡CAMPEÓN!, texto de cierre, cinturón desplegado y girando, precisión 46 %, 4 de 4 ataques bloqueados y 0:33 (`docs/capturas/15_resultados_campeon.png`) |
| Tras cualquier combate de los niveles 1 a 5 se muestran precisión, bloqueos y tiempo, y los botones van a donde deben | OK en los cinco niveles: ver las pruebas |
| Menú → nivel 5 → resultados → menú, con `nivelSuperado` correcto | OK: `nivelSuperado` = 5 sin cambios en todo el recorrido |
| Nivel 4 → resultados → nivel 5, con `nivelSuperado` correcto | OK: con `nivelSuperado` = 3, ganar el 4 lo sube a 4, SIGUIENTE NIVEL carga el 5 y ganarlo lo sube a 5 |
| Sin errores ni warnings propios; compila para Android | OK en el Editor y al compilar los scripts para Android (70 ensamblados), sin generar el APK. Los únicos avisos son de OpenXR y OVRPlugin por no haber visor |

### Pruebas (Play en el Editor, Unity 6000.4.7f1)

Todo en una sola sesión de Play encadenada, empezando en el nivel 4, con los botones de la hoja y del menú pulsados invocando su `onClick` (lo mismo que hace `OVRInputModule` con el puntero láser). Para que el jugador aguantara, se desactivó `OVRCameraRig` en cada pelea y se fijaron, desde `Application.onBeforeRender`, la cabeza a 1,65 m y las dos manos en guardia; el delegado se quita solo al destruirse el rig. En esta sesión el Meta XR Simulator ponía la cabeza a 1,70 m también sin el arnés, y los ataques llegaban. `nivelSuperado` valía 5 antes de empezar: se bajó a 3 para la prueba del nivel 4 y quedó en 5 al terminar. Ninguna escena se guardó durante las pruebas.

| Prueba | Resultado |
|---|---|
| Nivel 4 sin guardia (Sandman tumba al jugador y no se pulsa en la cuenta) | OK: K.O. → DERROTA, "Perdiste por K.O. en el round 1", 0 de 11 ataques bloqueados, 0:20, REINTENTAR → `Scene_Ring_Level4` |
| Nivel 4 en guardia y *Probar: ganar el combate* (`nivelSuperado` = 3) | OK: sube a 4; ¡VICTORIA!, 48 % (33 de 69), 27 de 27 bloqueados, 0:59; SIGUIENTE NIVEL → `Scene_Ring_Level5` (`docs/capturas/13_resultados_victoria.png`) |
| Nivel 5 en guardia, ganando a los 30 s | Tyson tumbó antes al jugador (los bloqueos aún quitan el 15 %): DERROTA, 40 % (14 de 35), 39 de 39, 0:48; REINTENTAR → nivel 5 (`docs/capturas/14_resultados_derrota.png`) |
| Nivel 5 reintentado, ganando desde la campana | OK: `reintento` = true en el registro; `nivelSuperado` 4 → 5; ¡CAMPEÓN!, cinturón, REPETIR → `Scene_Ring_Level5`; MENÚ → `Main_Scene` |
| Menú → botón NIVEL 5 → victoria → MENÚ | OK: `nivelSuperado` = 5 sin cambios, cinturón otra vez |
| Nivel 1, *Probar: ganar por salud* | OK: "Ganaste por salud al terminar el último round", la ovación suena, SIGUIENTE NIVEL → `Scene_Ring_Level2` |
| Nivel 2, *Probar: perder por salud* | OK: cartel DERROTA y luego la hoja, sin ovación, REINTENTAR → nivel 2 |
| Nivel 2 reintentado, *Probar: derribar al jugador* sin pulsar | OK: K.O. a los 10 s y la hoja con "Derribos: 0 al rival, 1 tuyos" |
| Nivel 3, *Probar: ganar el combate* | OK: 40 % (34 de 84), SIGUIENTE NIVEL → `Scene_Ring_Level4` |
| `Scene_Results` abierta directamente | OK: "Todavía no has terminado ningún combate", sin botón principal y MENÚ centrado |
| Singular | OK: "1 de 1 golpe", "de 1 ataque (100 %)", "1 round" |
| Menús contextuales de `PantallaResultados` (*victoria en el nivel 2*, *derrota en el nivel 3*, *campeón*) | OK: rellenan la hoja con datos inventados, para revisarla sin jugar |
| Consola | Sin errores ni warnings de los scripts del juego |

Con *Probar: ganar el combate*, los golpes llegan directamente a `RecibirGolpe`, sin `Puno`. `RegistroSesion` los cuenta como lanzados porque "un golpe que llega al rival también se lanzó", y la precisión baja por los que el rival bloquea o esquiva.

### Pendiente de validar con visor

- **Los botones con el puntero láser.** Se probaron invocando su `onClick`, que es lo que hace `OVRInputModule`.
- **Si se leen** los textos pequeños (los detalles de las tarjetas y la línea de abajo, a 28 px, unos 4,5 cm de alto a 1,5 m) y si el panel, de unos 70° de ancho, cabe sin girar la cabeza. Las capturas tienen un campo de visión más ancho que el del visor.
- **El cinturón en el Quest:** si el oro se ve dorado con la luz de la arena (en el Editor se oscurecía y se le subió la emisión), si el despliegue y el giro marean, y si conviene acercarlo o agrandarlo.
- **El rendimiento** de la escena (bloque J): la arena completa más 1 540 triángulos del cinturón; es la escena más ligera del juego.
- **El volumen** de la ovación (0,8) junto a la música del menú, que sigue sonando.

### Decisiones y visto bueno

Revisadas el 2026-11-11: se aprobaron tal como estaban, salvo la que se indica.

- **El cinturón propio en vez del modelo de Sketchfab:** aprobado. El cinturón queda como el modelo que genera `Tools/crear_cinturon.py`, y se corrigió el informe, que en la tabla de assets de `s06_08_desarrollo_plan_cronograma.tex` citaba el "Championship Belt" de Sketchfab: ahora dice modelo propio generado con Blender por script, sin licencia de terceros. Si algún día se usa otro modelo, basta con poner su prefab en el campo `cinturon` de `PantallaResultados`; `CinturonCampeon` sirve para cualquier modelo (`indiceOro` indica qué material brilla).
- **El texto de cierre** ("Derrotaste a Iron Mike Tyson, el campeón invicto. El cinturón de campeón regional es tuyo."): aprobado.
- **Los botones** (tras ganar el nivel 5, REPETIR vuelve a jugar contra Tyson): aprobado.
- **Todas las derrotas pasan por la hoja**, también las del nivel 1, antes de reintentar: aprobado.
- **La música del menú sigue sonando en la hoja**, también tras una derrota: **pendiente de confirmar con el visor**, junto con el volumen de la ovación.

## Bloque J (preparación sin visor): build, física y latencia (RNF-01, RNF-02, RNF-08)

Sin visor no se puede cerrar el bloque J: los FPS y la latencia del Editor no valen para el Quest. Esta parte deja preparado todo lo demás, para que la sesión con el visor consista en instalar, jugar y sacar los datos siguiendo `docs/Protocolo_Rendimiento.md`. **No hay aquí ninguna cifra de FPS ni de latencia del Quest.** Las del Editor que aparecen sirven para comprobar que la medida funciona y para comparar escenas entre sí.

### Primera build completa para Android

| Dato | APK de desarrollo | APK sin *Development* |
|---|---|---|
| Archivo (`Builds/`, ignorada por git) | `Builds/Android/vrboxing_dev.apk` | `Builds/Android/vrboxing_release.apk` |
| Tamaño | 201,8 MB (192,5 MiB) | 178,9 MB (170,6 MiB) |
| Tiempo de build | 5 min 42 s la primera vez; 1 min 55 s al regenerarla | 2 min 38 s (incremental) |
| Errores / avisos | 0 / 1 (el del Pipeline, ver abajo) | 0 / 1 (el mismo) |

Las 7 escenas de Build Settings van en el APK. Descomprimido ocupa 780 MB; el informe de Unity da 564 MB de assets propios, de los que **532 MB son texturas** (94 %): unas 220 texturas de 2 048 px de la arena de MarpaStudio, a 2,4 MB cada una en ASTC 6×6, y las del glb de los guantes (106,7 MiB, ver los recortes; en el APK, comprimidas por el zip, ocupaban unos 10 MiB). Estas cifras del informe de Unity son MiB. El código nativo (`libil2cpp.so`, `libunity.so`, `libOVRPlugin.so`) suma unos 140 MB más.

**Ajustes que importan** (en esta primera build no se cambió ninguno; los que cambiaron después están en "Cambios tras el visto bueno"):

| Ajuste | Valor | Comentario |
|---|---|---|
| Render pipeline | Built-in | |
| Scripting backend / arquitectura | IL2CPP (configuración Release) / ARM64 | Stripping *Minimal* |
| Graphics API | Solo Vulkan (automático desactivado) | |
| API mínima / objetivo | 32 / 34 | |
| Espacio de color | Linear | |
| Render multihilo, GPU skinning, graphics jobs, *Optimized Frame Pacing* | Activados | |
| OpenXR en Android | `OpenXRLoader`, *Render Mode* `SinglePassInstanced` | En Android/Vulkan, OpenXR lo implementa como **Multiview**. *Latency Optimization*: *Prioritize Rendering* |
| Funciones de OpenXR | Meta XR, Meta XR Foveation, Subsampled Layout, SpaceWarp, perfiles Touch | La foveación está disponible pero no se fija ningún nivel (el *Project Setup Tool* lo da como opción) |
| Compresión de texturas | ASTC por defecto | Las texturas que importa Unity usan ASTC (194 en el nivel 1, una con ASTC 6×6 fijado a mano); las 6 del glb de los guantes no (las importa glTFast sin comprimir). **Corregido tras el visto bueno:** los guantes usan texturas propias en ASTC |
| Calidad en Android | *Medium* (nivel 2) | MSAA **desactivado** (4× tras el visto bueno), sombras solo duras, distancia de sombra 20 m, 1 cascada, **1 luz por píxel**, texturas a resolución completa, sin reflection probes en tiempo real |
| `OVRManager` | Resolución dinámica activada (Quest 2: 0,7 a 1,3) | Si la GPU no llega, el runtime baja la resolución antes de perder fotogramas: hay que mirar las dos cosas |
| Cámara del rig | HDR desactivado, plano lejano 1 000 m | |
| Manifiesto | Paquete `com.utec.vrhola`, nombre "vrhola" ("Simulador de Boxeo RV" tras el visto bueno), versión 0.1 (1), `com.oculus.intent.category.VR`, `focusaware`, `supportedDevices` = `quest2` | |

**Avisos:**

| Aviso | Qué se hizo |
|---|---|
| Script perdido en `Assets/Resources/UnityPlayerAccountSettings.asset` | **Corregido.** Era un resto de Unity Player Accounts, cuyo paquete se quitó con los de multijugador. Estaba en `Resources`, así que entraba en el APK. Tenía el `clientId` vacío y ningún código lo usaba: se borró |
| `Pipeline: No RuntimePipelineConfig asset found ... Pipeline will be disabled in Player builds` | Correcto así: `com.unity.pipeline` es el control del Editor desde la terminal y no debe ir en el juego |
| En `Editor.log`: `metal-objdump` / "missing Metal Toolchain" | Del entorno del Mac (Xcode sin el componente Metal): Unity intenta compilar shaders de Metal para el Editor. No afecta al APK de Android |
| En `Editor.log`: avisos de IL2CPP de TextMeshPro ("given its own cpp file") y de `clang` (`-x c++`) | Informativos |
| *Project Setup Tool* de Meta (Android) | Nada obligatorio ni recomendado pendiente, salvo las dos tareas de la Platform SDK, que no aplican. Opcional: `OVROverlayCanvas` para el texto |
| Permisos del manifiesto que el juego no usa: `RECORD_AUDIO`, `INTERNET`, `BLUETOOTH`, `FOREGROUND_SERVICE_MEDIA_PROJECTION`, hand tracking, anclas y escena | Los añaden los paquetes de `com.meta.xr.sdk.all` (voz, MR Utility Kit...). Un permiso del manifiesto no muestra ningún diálogo si nadie lo pide en ejecución; **comprobar en el visor** que no sale ninguno al abrir el juego |
| `supportedDevices` solo dice `quest2` | **Decidido** (2026-11-11): solo Quest 2, sin declarar Quest 3. Para RNF-09 basta con anotar si el APK funciona en un Quest 3, si alguna vez hay uno |

### Física a 72 Hz (RNF-02)

El *Fixed Timestep* pasa de 0,02 s (50 Hz) a **1/72 s** (13,9 ms), exactamente: Unity 6 lo guarda como fracción y queda en 1 960 000 / 141 120 000. Con 72 fps en el Quest 2 hay un paso de física por fotograma, y el impacto se detecta como mucho un fotograma tarde. Antes podía tardar hasta dos (unos 11 ms de media y hasta ~28 ms), porque solo el 69 % de los fotogramas tenía un paso.

**Lo que depende del paso**, revisado:

| Qué | Efecto de 1/72 | Cambio |
|---|---|---|
| `Puno`: velocidad de la mano = desplazamiento del paso / `Time.fixedDeltaTime` | Está en m/s, no depende del paso. Con 50 Hz y el render a 72 fps, unos pasos veían un fotograma de desplazamiento y otros dos, y la velocidad oscilaba un ±40 % | Ninguno |
| `Puno`: suavizado `Lerp(..., 0,5)` en cada paso | Con más pasos por segundo, el filtro reaccionaría más rápido | **Sí:** el factor se ajusta al paso (`1 − (1 − 0,5)^(paso / 0,02)`), así que en segundos es el mismo filtro calibrado a 50 Hz; con 0,02 s da exactamente 0,5 |
| Umbral de golpe lanzado (1,5 m/s), `velocidadMinima`, `rapidezVibracionMaxima` | En m/s; no dependen del paso | Ninguno |
| Enfriamiento, invulnerabilidad, rondas, ataques | Usan `Time.time`, no `FixedUpdate` | Ninguno |
| Rigidbodies | Los `Puno` (cinemáticos desde `Awake`, `ContinuousSpeculative`) y uno cinemático en cada rival con las hitboxes como triggers. En `Main_Scene` hay dos dinámicos (las figuras `.glb` de decoración): se simulan más a menudo, sin otro efecto | Ninguno |
| `OVRInput.FixedUpdate` | Solo usa `fixedDeltaTime` con la API antigua de Oculus, no con OpenXR | Ninguno |
| Animators | Ninguno anima en física (*Animate Physics*) | Ninguno |
| `OVRCameraRig` | Mueve los anchors en `Update`/`onBeforeRender`, no en `FixedUpdate` (*Use Fixed Update For Tracking* desactivado) | Ninguno |

**Verificación en Play.** `PruebaAtaques` y *Probar: ganar el combate* no pasan por la física (llaman a `RecibirAtaque` y `RecibirGolpe` directamente), así que no detectarían un cambio del paso. Para probar el camino real (trigger → `Puno` → `Oponente` → `Haptica`), un arnés en `Application.onBeforeRender` fijó la cabeza a 0,9 m del rival y movió el guante derecho por trayectorias de mínimo jerk contra la hitbox de la cabeza de Glass Joe (rival quieto: `AtaqueOponente`, `DefensaOponente`, `EsquiveOponente` y `LimiteRing` desactivados en Play, y su salud repuesta antes de cada golpe), y después golpes al aire hacia fuera de la cabeza. Mismas trayectorias a 50 y a 72 Hz, con el Editor a ~72 fps:

| Golpe (rapidez pico) | Resultado a 50 Hz | Resultado a 72 Hz | Rapidez medida a 50 Hz (m/s) | Rapidez medida a 72 Hz (m/s) |
|---|---|---|---|---|
| A la cabeza, 0,4 m/s (×3) | No cuenta | No cuenta | — | — |
| A la cabeza, 1 m/s (×3) | Cross a la cabeza | Cross a la cabeza | 1,18 / 1,33 / 1,17 | 1,12 / 0,99 / 1,01 |
| A la cabeza, 2 m/s (×3) | Ídem | Ídem | 2,29 / 1,60 / 2,24 | 1,96 / 2,40 / 1,97 |
| A la cabeza, 3 m/s (×3) | Ídem | Ídem | 3,31 / 3,32 / 3,32 | 2,88 / bloqueado / 2,90 |
| A la cabeza, 5 m/s (×3) | Ídem | Ídem | 4,19 / 5,02 / 4,35 | bloqueado / 4,43 / 4,57 |
| A la cabeza, 8 m/s (×3) | Ídem | Ídem | 6,06 / 7,46 / bloqueado | 6,66 / 6,63 / bloqueado |
| Al aire, 1,30 m/s (×3) | 1 de 3 cuenta como lanzado | 0 de 3 | | |
| Al aire, 1,45 m/s (×3) | 3 de 3 | 0 de 3 | | |
| Al aire, 1,60 / 1,75 / 2,50 m/s (×3) | 3 de 3 | 3 de 3 | | |

Los golpes conectan, no conectan y se clasifican igual. Los "bloqueado" son el azar de `DefensaOponente.Bloquea`, que decide aunque el componente esté desactivado. Lo que cambia es la medida: a 72 Hz la rapidez oscila menos y el umbral de 1,5 m/s corta limpio. A 50 Hz contaban como lanzados golpes de 1,30 y 1,45 m/s por la oscilación (sobrestimaba hasta un 39 % cuando el paso veía dos fotogramas). La rapidez solo cambia el daño por debajo de 2,5 m/s (`rapidezGolpeCompleto`), y el daño de un mismo golpe ahora varía menos.

En la misma sesión, *Atacar de frente / con la cabeza desplazada / con guardia* de `PruebaAtaques` dieron `Conectado`, `Esquivado` y `Bloqueado`, y *Probar: ganar el combate* terminó en `VictoriaPorKO` y cargó `Scene_Results`.

### Latencia háptica medida en el juego (RNF-02)

| Pieza | Qué hace |
|---|---|
| `Puno.OnTriggerEnter` | Lo primero que hace es tomar la marca de tiempo (`Haptica.Ahora()`, `Stopwatch` de alta resolución) y pasarla al primer pulso del patrón. La corrutina de vibración corre hasta su primer `yield` dentro de la misma llamada, así que ese pulso sale antes de volver de `OnTriggerEnter` |
| `Haptica.Fijar(..., origenImpacto)` | Al volver de `OVRInput.SetControllerVibration` publica los milisegundos transcurridos en el evento nuevo `AlMedirLatencia`. Sin origen (todas las demás vibraciones) no mide nada. `AlVibrar` sigue igual |
| `Puno.AlEstimarRetrasoDeteccion` | Evento nuevo. Tras vibrar (para no sumarlo a la latencia), estima lo que se perdió por el paso de física: la profundidad con que el guante ya estaba dentro de la hitbox (`Physics.ComputePenetration`) entre la rapidez con que entraba. Se descarta si la rapidez de entrada es menor que `velocidadMinima` o el resultado pasa de 0,1 s (el rival se metió en el guante, o el guante ya estaba dentro) |
| `RegistroSesion` | Por combate, campos nuevos al final de `DatosCombate`: `pasoFisicaMs`, `muestrasLatencia`, `latenciaHapticaMediaMs`, `latenciaHapticaP95Ms`, `latenciaHapticaMaxMs`, `muestrasRetraso`, `retrasoDeteccionMedioMs`, `retrasoDeteccionP95Ms`, `retrasoDeteccionMaxMs` (p95 por rango más cercano). En `combates.csv`, nueve columnas nuevas al final con esos datos |

**Compatibilidad.** El JSON solo gana campos. En el CSV, si `combates.csv` ya existía con la cabecera vieja, `RegistroSesion` reescribe solo la primera línea con las columnas nuevas al final: las filas viejas quedan intactas (con 24 campos) y `csv.DictReader` o una hoja de cálculo las leen con los campos nuevos vacíos. Se comprobó con el `combates.csv` del Editor (37 filas): las 37 iguales byte a byte y la fila nueva con 33 campos.

**Solo cuenta lo que el código controla.** La latencia completa tiene tres tramos:

1. **Detección**, del contacto según el seguimiento al paso de física que lo ve: de media medio fotograma (~7 ms a 72 Hz) y como mucho uno (~14 ms), más si se pierden fotogramas. Se estima en cada golpe (`retrasoDeteccion*`).
2. **Código**, de `OnTriggerEnter` a la vuelta de `OVRInput`: resolver el golpe en `Oponente` (con su `Debug.Log`), sonidos, eventos de reacción y registro, y la llamada a la vibración. Se mide en cada golpe (`latenciaHaptica*`).
3. **Runtime de OpenXR y control**: el envío por Bluetooth y el arranque del motor. **No se puede medir desde el código**; haría falta instrumentación externa (un acelerómetro en el control, sincronizado con el juego).

Para RNF-02 se informará el tramo 1 más el tramo 2 medidos en el Quest, diciendo que falta el tercero. `Tools/tabla_rendimiento.py` suma los p95.

**En el Editor (Mono, no vale como cifra del Quest)**, con el arnés de 15 golpes a 72 Hz: el tramo 2 fue de 0,24 a 0,74 ms, salvo el primer golpe de la sesión (9,5 ms: compilación JIT de Mono y primera reproducción de sonido; con IL2CPP no hay JIT). El tramo 1 dio de 0,2 a 16,6 ms, con media de 8,6 ms: repartido a lo largo de un fotograma, como se esperaba (por encima de 13,9 ms cuando el Editor perdió fotogramas). El combate quedó registrado con `muestrasLatencia` = 15, media de 1,22 ms, p95 y máximo de 9,25 ms (el primer golpe), y retraso medio de 7,8 ms con p95 de 16,7 ms.

### Auditoría de coste por escena

Dos medidas, ninguna del Quest:

- **Inventario estático** (Edit mode, escena sin guardar): renderers activos dentro de un frustum con el campo de visión combinado del Quest 2 (~104° × 93°), con el ojo a 1,65 m sobre el rig y mirando al rival. Triángulos y vértices de esas mallas, y submallas, que son las draw calls sin batching (una por material). Sin sombras ni multiview.
- **`UnityStats` en Play**, forzando la calidad *Medium* de Android (el Editor usa *Ultra*, la de Standalone), con la cabeza del Meta XR Simulator a 1,70 m en el rig, 3,5 s después de cargar cada escena (antes de la campana). **El simulador renderiza en MultiPass**: cada ojo se dibuja aparte y las sombras se suman. En el Quest, con Multiview, los dos ojos van en una sola draw call. Sirve para comparar escenas, no como cifra absoluta.

| Escena | Triángulos visibles | Vértices visibles | Submallas visibles | Personajes con skinning (vértices) | Draw calls (Editor) | SetPass (Editor) | Triángulos (Editor) | Objetos en sombras (Editor) |
|---|---|---|---|---|---|---|---|---|
| `Main_Scene` | 119 940 | 114 844 | 624 | Glass Joe 6 026 | 2 380 | 1 033 | 1 025 191 | 1 322 |
| Nivel 1 | 125 696 | 116 373 | 584 | Glass Joe 6 026 + réferi 14 316 | 2 167 | 970 | 1 277 904 | 1 184 |
| Nivel 2 | 127 680 | 117 525 | 584 | Bald Bull 7 178 + réferi | 2 167 | 969 | 1 293 776 | 1 184 |
| Nivel 3 | 126 467 | 117 631 | 605 | Soda Popinski 7 284 **en 36 piezas** + réferi | 2 293 | 1 011 | 1 284 072 | 1 268 |
| Nivel 4 | 133 175 | 119 807 | 591 | Mr. Sandman 9 460 + réferi | 2 209 | 983 | 1 337 736 | 1 212 |
| Nivel 5 | 134 043 | 125 713 | 570 | Iron Mike Tyson 15 366 + réferi | 2 083 | 941 | 1 344 680 | 1 128 |
| `Scene_Results` | 99 532 | 96 732 | 572 | — | 2 057 | 921 | 848 290 | 1 103 |

**Común a las siete escenas** (la arena de MarpaStudio es la misma):

| Elemento | Estado |
|---|---|
| Arena (`Building`) | ~1 250 renderers, ~99 000 triángulos y ~570 submallas visibles; **ninguno con *Batching Static***; todos proyectan sombra |
| Luces | **25 en tiempo real, todas con sombras suaves**: 21 focos de relleno (intensidad 0,5, alcance 159,5 m), 2 focos fuertes (10, 14,2 m), 1 puntual (2, 15 m) y la direccional (*Mixed*). Con *Medium*: una luz por píxel por objeto (las demás por vértice) y sombras duras. Los ~29 cambios de render target por fotograma en el Editor apuntan a muchos mapas de sombra de focos |
| Iluminación horneada | GI horneado activado (*Shadowmask*, Progressive CPU) pero **nunca horneado**: 0 lightmaps; 8 light probes. Las siete escenas comparten el `LightingData` de `Main_Scene` |
| Reflection probe | Una, *Realtime*, *On Awake*, 128 px HDR. En *Medium* las probes en tiempo real están desactivadas: en el Quest no cuesta nada (ni refleja nada) |
| Materiales (nivel 1) | 56, todos Standard salvo los del glb (glTF); **35 con parallax** (mapa de altura) y oclusión, 37 con mapa normal y de metal |
| Texturas (nivel 1) | 200, 175 de ellas a 2 048 px: unos **420 MB** en ASTC 6×6 con mipmaps, más las 6 del glb de los guantes **sin comprimir** (RGB24/ARGB32, 2 048 px: 106,7 MiB en memoria y en los datos de la build; el zip del APK las dejaba en unos 10 MiB). Sustituidas tras el visto bueno |
| Otros | Niebla activada, skybox procedural, sin *occlusion culling* horneado, plano lejano a 1 000 m, MSAA desactivado en *Medium* (4× tras el visto bueno) |
| Réferi | 20 000 triángulos, 14 316 vértices, 54 huesos, con sombra: más que cualquier rival |

### Recortes posibles, ordenados por coste y beneficio (no aplicados)

Se deciden con los FPS del Quest: si `porcentajeFotogramasLentos` < 5 % en todos los niveles, no hace falta ninguno. Si no, aplicarlos de uno en uno, desde arriba, y repetir la medida del nivel peor.

| # | Recorte | Beneficio esperado | Coste | Efecto visual |
|---|---|---|---|---|
| 1 | Quitar la sombra (*Shadow Type: No Shadows*) a las 24 luces que no son la direccional, o a todas menos una que alumbre el ring | Muy alto en GPU: cada foco con sombra vuelve a dibujar sus objetos en un mapa de sombra (~1 100–1 300 objetos en sombras por fotograma en el Editor) | Muy bajo: un ajuste por luz, en 7 escenas | Se pierden las sombras de la arena; las de los púgiles en la lona se pueden conservar con una luz |
| 2 | Bajar las texturas en Android: *Mipmap Limit* de la calidad *Medium* a 1 (o *Max Size* 1024 en las de MarpaStudio) | Alto en memoria (~420 → ~105 MB) y en ancho de banda de GPU | Muy bajo: un ajuste de calidad | Arena algo más borrosa de cerca; casi siempre se ve de lejos |
| 3 | Fijar el *foveated rendering* (nivel alto o dinámico) al iniciar | Medio en GPU: baja la resolución de la periferia | Muy bajo: la función de OpenXR ya está activada | Periferia algo borrosa |
| 4 | Marcar la arena como *Batching Static* | Alto en CPU: de ~570 draw calls de la arena a unas decenas de lotes (56 materiales) | Bajo; más memoria y APK por la malla combinada | Ninguno |
| 5 | Texturas de los guantes: materiales Standard con texturas ASTC en vez de las de glTFast | Memoria (~100 MiB menos); poco en FPS | Bajo | Ninguno. **Aplicado** tras el visto bueno |
| 6 | Réferi a ~10 000 triángulos (`Tools/reducir_referi.py` ya existe) y sin sombra | Medio-bajo en GPU y skinning | Bajo | Mínimo |
| 7 | Quitar el parallax de los 35 materiales que lo usan | Medio en GPU (una textura y un cálculo menos por píxel) | Bajo; son materiales de terceros: copiarlos o modificarlos | Menos relieve |
| 8 | Hornear la iluminación: luces de relleno a *Baked* y lightmaps | Alto: 24 luces gratis en ejecución | Medio: horneado largo, UV2 de MarpaStudio, lightmaps en el APK | Puede cambiar el aspecto: revisar |
| 9 | Plano lejano de la cámara a ~150 m (7 escenas) | Bajo (algo de culling y precisión de profundidad) | Muy bajo | Ninguno si la arena cabe |
| 10 | Soda Popinski en una sola malla (36 → 1 `SkinnedMeshRenderer`) | Medio en CPU, solo en el nivel 3 (+35 draw calls y skinning) | Medio (Blender) | Ninguno |
| 11 | *Occlusion culling* horneado | Bajo-medio (gradas detrás de muros) | Medio | Ninguno |
| 12 | Shaders móviles en lugar de Standard | Alto en GPU | Alto, y cambio visual grande | Último recurso |

Aparte de RNF-01: con MSAA desactivado los bordes se ven dentados en el visor. Activar 4× MSAA en *Medium* cuesta poco en la GPU del Quest (*tile-based*). Se activó tras el visto bueno, y es lo primero que se compara si no se llega a 72 FPS.

### Herramientas para el día del visor

- **`docs/Protocolo_Rendimiento.md`:** el guion completo. Instalar (`adb install -r` o *Build And Run*), qué jugar en cada nivel y durante cuánto tiempo, `logcat` con `VrApi` como segunda medida de FPS, OVR Metrics Tool (opcional), la sesión de 30 minutos con pausa, Guardián y visor quitado, `adb pull` de `/sdcard/Android/data/com.utec.vrhola/files/Sesiones`, y las tablas vacías para rellenar.
- **`Tools/tabla_rendimiento.py`** (sin dependencias): lee los `sesion_*.json` (o `combates.csv` si no hay JSON) y escribe en Markdown la tabla de sesiones (duración frente a los 30 minutos) y la de niveles: FPS (fotogramas totales entre tiempo total), fotogramas lentos, latencia (media ponderada por muestras, p95 del peor combate, máximo) y retraso de detección. `--sesion` y `--dispositivo` filtran.

### Criterio de terminado

| Criterio | Resultado |
|---|---|
| El APK de desarrollo se genera sin errores, fuera del repositorio, y está documentado | OK: 201,8 MB, 0 errores, en `Builds/` (ignorada por git); también el APK sin *Development* |
| Tabla de coste por escena y lista ordenada de recortes | OK, arriba |
| Física a 1/72 s, verificada en Play sin cambios de comportamiento en los golpes | OK: mismos resultados en las mismas trayectorias, con una medida de rapidez más estable |
| `RegistroSesion` registra la latencia háptica por combate, con JSON y CSV compatibles | OK: 9 campos y columnas nuevos al final; cabecera ampliada sin tocar las filas viejas |
| Guion de la sesión con visor | OK: `docs/Protocolo_Rendimiento.md` |
| Sin errores ni warnings propios; compila para Android | OK: consola sin avisos de los scripts del juego (solo los de OpenXR y OVRPlugin del simulador); los dos APK compilan los scripts con IL2CPP |

### Pruebas (Unity 6000.4.7f1, Editor con el Meta XR Simulator)

| Prueba | Resultado |
|---|---|
| Arnés de golpes por física a 50 Hz y a 72 Hz | Ver la tabla de la física |
| `PruebaAtaques` a 72 Hz | OK: `Conectado`, `Esquivado`, `Bloqueado` |
| *Probar: ganar el combate* a 72 Hz | OK: `VictoriaPorKO`, carga `Scene_Results`, `nivelSuperado` sin cambios (5) |
| Latencia en un combate real (15 golpes por física, luego *ganar el combate*) | OK: el JSON y la fila del CSV traen las 9 medidas; `pasoFisicaMs` = 13,889 |
| Muestra a muestra (otros 15 golpes) | OK: ver la latencia; el máximo es siempre el primer golpe |
| `combates.csv` existente con la cabecera vieja | OK: cabecera ampliada, 37 filas viejas iguales, fila nueva con 33 campos |
| `Tools/tabla_rendimiento.py` con los JSON del Editor, con solo el CSV y con `docs/ejemplos` (sin latencia) | OK en los tres casos |
| Build de desarrollo y sin *Development* | OK, 0 errores |
| Manifiesto del APK (`aapt2 dump`) | OK: paquete, categoría VR, `focusaware`, `quest2` |
| *Project Setup Tool* de Meta para Android | OK: nada obligatorio ni recomendado pendiente que aplique |
| Escenas y `nivelSuperado` | Ninguna escena guardada ni modificada; `nivelSuperado` = 5, igual que al empezar |

### Pendiente con visor

- **Toda la medida:** FPS por nivel, latencia en el Quest y la sesión de 30 minutos, siguiendo `docs/Protocolo_Rendimiento.md`. Después, pegar aquí las tablas de `Tools/tabla_rendimiento.py`.
- Si los FPS no llegan: comparar primero con el MSAA desactivado (activado tras el visto bueno), y después aplicar los recortes por orden, generar el APK y repetir el nivel peor.
- Comparar el APK de desarrollo con el que no lo es (los `Debug.Log` de cada golpe escriben en el `logcat` en la build de desarrollo).
- Comprobar que no sale ningún diálogo de permisos al abrir el juego.
- Si el visor se usa a 90 Hz, el *Fixed Timestep* debería ser 1/90 s; el juego no pide ninguna frecuencia, así que corre a 72 Hz.
- RNF-09 (opcional): si alguna vez hay un Quest 3, instalar el mismo APK y anotar si funciona. No se declara en `supportedDevices` (decidido: solo Quest 2).

### Decisiones y visto bueno

Revisadas el 2026-11-11: se aprobaron tal como estaban, con las excepciones que se indican. Lo aplicado está en "Cambios tras el visto bueno", más abajo.

- **El *Fixed Timestep* a 1/72 s** y **el suavizado de `Puno` ajustado al paso:** aprobados.
- **Borrar `UnityPlayerAccountSettings.asset`:** aprobado.
- **Los recortes de rendimiento:** se deciden con los FPS del visor, salvo el 5 (texturas de los guantes), que se aplicó.
- **El MSAA:** se activó 4× en *Medium*; **pendiente de confirmar con los FPS del visor**. Es lo primero que se compara si no se llega a 72 FPS (`docs/Protocolo_Rendimiento.md`).
- **El nombre de la app en el visor:** "Simulador de Boxeo RV" en lugar de "vrhola". El paquete sigue siendo `com.utec.vrhola`.
- **Dispositivos admitidos:** decidido, solo Quest 2 (`supportedDevices` = `quest2`), sin declarar Quest 3.
- **Los permisos que añade la SDK de Meta:** se dejan; **pendiente de comprobar en el visor** que no sale ningún diálogo al abrir el juego.

### Cambios tras el visto bueno (2026-11-11)

En la rama `entrega5/visto-bueno`, creada desde `entrega5/rendimiento`. No se tocaron los valores de Tyson, la escena de resultados, el *Fixed Timestep* ni los demás recortes.

**1. Texturas de los guantes (recorte 5).** Los guantes usaban los dos materiales glTF del glb, cuyas seis texturas de 2 048 px importa glTFast sin comprimir. Ahora usan `Materials/Guante_Cuero.mat` y `Guante_Velcro.mat` (Standard) con texturas que importa Unity, en `Assets/_Project/Art/boxing-gloves/Textures/`:

| Textura | Origen | En Android |
|---|---|---|
| `Glove_BaseColor.jpg`, `Velcro_BaseColor.jpg` | Los JPEG originales del glb, extraídos sin recomprimir | 1 024 px, ASTC 6×6 |
| `Glove_Normal.jpg`, `Velcro_Normal.jpg` | Ídem (mapa normal) | 1 024 px, ASTC 4×4 (las normales sufren más con bloques grandes) |
| `Glove_MetallicSmoothness.png`, `Velcro_MetallicSmoothness.png` | Convertidas del `metallicRoughness` de glTF (G = rugosidad, B = metal): R = metal, A = 1 − rugosidad. El metal es 0 en todo el mapa | 1 024 px, ASTC 6×6, lineal |

- Los dos `.jpeg` que había en `boxing-gloves/textures/` eran dos de esas imágenes (los colores base) **volteadas en vertical**, sin la normal ni el metal; nadie los usaba y se borraron.
- Las cinco escenas de pelea apuntan a los materiales nuevos (son las únicas con guantes). *Tools > Boxeo > Poner guantes en las manos* asigna también los propios, por el nombre del material del glb, y si falta alguno avisa sin cambiar nada.
- `AssetDatabase.GetDependencies`: ninguna de las 7 escenas del build depende ya del glb, que solo sirve de fuente de las mallas en el Editor.
- **Memoria de las texturas de los guantes: de 106,7 MiB a 5,1 MiB.** En la build, los assets empaquetados bajan de ~564 a 456,6 MiB, y las texturas de 532 a 430,4 MiB.
- **Capturas antes y después** (Play en el nivel 1 con la cabeza y las manos fijas, guantes a unos 30 cm del ojo, calidad *Medium*, antes del MSAA): en las zonas de los guantes, diferencia media de 0,14 a 0,49 sobre 255, y menos del 0,1 % de los píxeles cambia más de un 10 %. A esa distancia no se distingue la textura de 1 024 px; se queda así.

**2. MSAA 4×** en la calidad *Medium* (índice 2, la de Android); los demás niveles no cambian y la cámara del rig ya lo permitía (`allowMSAA` en las 7 escenas). Se confirma con los FPS del visor: es lo primero que se compara si no se llega a 72 FPS (`docs/Protocolo_Rendimiento.md`, "Si no se cumple"). El diff de `QualitySettings.asset` trae además la reescritura de Unity 6 (versión de serialización 3 → 5 y campos nuevos con su valor por defecto), porque el asset no se guardaba desde la migración.

**3. Nombre de la app: "Simulador de Boxeo RV".** Es el título de la portada del informe ("Simulador inmersivo de boxeo en realidad virtual (1P vs. IA)") acortado para la biblioteca del Quest, con la sigla RV que usa el propio informe en la cabecera de cada página. Solo cambió `productName`; el paquete sigue siendo `com.utec.vrhola`, del que dependen la carpeta de datos (`/sdcard/Android/data/com.utec.vrhola/files`) y los PlayerPrefs del Quest, así que `adb install -r` actualiza la misma app y conserva sus datos. En el Editor de macOS, en cambio, `productName` decide dónde se guardan los PlayerPrefs y `persistentDataPath`: `nivelSuperado`, `volumen` y `haptica` se copiaron a la ubicación nueva (`~/Library/Application Support/utec/Simulador de Boxeo RV`), y **las sesiones y guardias grabadas antes en el Editor siguen en `~/Library/Application Support/utec/vrhola`**.

**4. Limpieza.** Se descartaron los glifos que TMP añade solo a `Bangers SDF.asset`. `hola.pdf` y `docs/annotated-Informe_SimuladorBox_Semana_12.pdf` eran el mismo archivo (comparados con `cmp`): se borró el primero y el segundo quedó como `docs/informe/Informe_SimuladorBox_Semana_12.pdf`, la versión entregada en la semana 12 (30 páginas). Es la descarga anotada de la corrección: además de las páginas entregadas lleva dos notas del profesor (páginas 7 y 13), invisibles al abrirlo. En el informe se corrigió el origen del cinturón (ver el bloque I).

**APK regenerados** (`Builds/`, ignorada por git):

| Dato | APK de desarrollo | APK sin *Development* |
|---|---|---|
| Antes (bloque J) | 201 833 988 B (201,8 MB) | 178 934 939 B (178,9 MB) |
| Ahora | **193 563 545 B (193,6 MB)**, −8,3 MB | **172 726 330 B (172,7 MB)**, −6,2 MB |
| Descomprimido | 780,7 → 673,1 MB | |
| Tiempo de build | 2 min 20 s (con `CleanBuildCache`, ver abajo) | 2 min 4 s (incremental) |
| Errores / avisos | 0 / 1 (el del Pipeline) | 0 / 1 (el mismo) |
| `aapt2` | `com.utec.vrhola`, etiqueta "Simulador de Boxeo RV", `supportedDevices` = `quest2`, los mismos permisos que antes | Ídem |

El APK baja mucho menos que los 101 MiB de datos que se quitaron porque el zip del APK ya comprimía bien las texturas crudas del glb (casi lisas: unos 10 MiB), mientras que las ASTC casi no se comprimen. **Cuidado al medir el tamaño:** la primera build de desarrollo, incremental, tardó 32 s pero dejó 8,2 MB de huecos dentro del APK (el empaquetado incremental de Gradle no compacta el zip al sustituir entradas) y solo bajó 0,1 MB; se repitió con la opción `CleanBuildCache`, y los dos APK finales no tienen huecos.

**Verificación en Play** (Meta XR Simulator, `nivelSuperado` = 5 antes y después):

| Prueba | Resultado |
|---|---|
| Nivel 1 y nivel 5 cargan; guantes con `Guante_Cuero`/`Guante_Velcro` (Standard), visibles | OK |
| `PruebaAtaques` en los dos niveles | OK: `Conectado`, `Esquivado`, `Bloqueado` |
| *Probar: ganar el combate* en el nivel 5 | OK: `VictoriaPorKO` y `Scene_Results` |
| Consola | 0 errores; solo los avisos conocidos de OpenXR y OVRPlugin del simulador (`GetBoundaryGeometry` incluido) |
