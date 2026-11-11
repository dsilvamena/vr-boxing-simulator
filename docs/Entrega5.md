# Entrega 5 (semana 14): versión final

Registro de lo implementado según `docs/Plan_Entrega5.md`. Las capturas están en `docs/capturas/`.

## Bloque H: Iron Mike Tyson y el nivel 5 (RF-07, RF-08, RF-09)

`Scene_Ring_Level5.unity` es el último combate: Iron Mike Tyson, el rival más difícil del juego. Se llega desde el nivel 4 o desde el menú, con el nivel 5 bloqueado hasta ganar el 4. Al ganarlo se vuelve al menú; la escena de resultados y el cinturón son el bloque I.

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
- **`ControlCombate`:** nivel 4 → `Scene_Ring_Level5` con "Siguiente: Nivel 5"; nivel 5 (`nivel` = 5) → `Main_Scene` con "¡Eres el nuevo campeón!". El 4 ya no dice "Volviendo al menú principal".
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
- "¡Eres el nuevo campeón!" sale todavía sin cinturón ni escena de resultados, que son el bloque I.
