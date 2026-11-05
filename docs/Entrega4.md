# Entrega 4 (semana 13): avance del 70 %

Registro de lo implementado según `docs/Plan_Entrega4_70.md`. Por ahora cubre los bloques A, B y C; los bloques E y D se añadirán al terminarlos.

## Bloque A: rondas de 3 × 90 s (RF-10, RF-11)

`ControlRondas`, en un objeto propio de cada escena de pelea, corre `Preparacion` → `Round` → `Descanso` → … → `Terminado`.

| Parámetro | Valor | Campo |
|---|---|---|
| Rounds | 3 | `rounds` |
| Duración del round | 90 s | `segundosPorRound` |
| Descanso | 20 s | `segundosDescanso` |
| Preparación | 5 s | `segundosPreparacion` |
| Salud que recupera el jugador en cada descanso | 20 % de la máxima | `fraccionSaludDescanso` |

### Decisiones

- **Empate por salud:** gana el jugador. Repetir el round alargaría un combate que el informe fija en 3 rounds.
- **Duración del descanso:** el informe no la fija. Se eligieron 20 s para no cortar el ritmo de una sesión en VR; se cambia en el componente.
- **Campana:** un toque al empezar el round y tres al terminarlo, para distinguirlos solo de oído. También suena al terminar el round por KO. Los clips (`Campana_Inicio.wav`, `Campana_Fin.wav`) salen de `Tools/sintetizar_audio.py`.
- **Fuera de los rounds no se pelea:** el rival no ataca y los golpes del jugador no cuentan (`Oponente.Protegido`). Si no, se le podría castigar gratis durante el descanso.
- **Vuelta a la posición:** el rival se desliza hasta su punto inicial a su `velocidadDesplazamiento`, igual que cuando se acerca, porque todavía no hay animación de caminar.
- **Cuenta del réferi (bloque B):** `ControlRondas.CuentaEnMarcha` es un `Func<bool>`. Mientras devuelva `true`, un round que llegó a cero espera antes de cerrarse.

### Criterio de terminado

| Criterio | Resultado |
|---|---|
| 3 rounds con campana y descanso entre ellos | OK en el flujo y en los registros; falta escuchar la campana |
| El tiempo se detiene al pausar | OK con `PauseMenu`. `PausaGuardian` usa ese mismo menú |
| El cronómetro no avanza con el jugador caído | OK |
| Un KO termina el combate al instante | OK con el KO del rival (nivel 3) y el del jugador (nivel 1) |
| Sin KO gana quien tenga más salud | OK: victoria 70 % contra 67 % (nivel 1), derrota 20 % contra 60 % (nivel 2), empate 50 % contra 50 % (nivel 1) |
| Ganar desbloquea el siguiente nivel | OK: `nivelSuperado` sube a 1 y a 3, y se carga la escena siguiente |
| Perder lleva al menú con el nivel abierto | OK: se carga `Main_Scene` y `nivelSuperado` no cambia |
| Compila sin errores ni warnings propios | OK |

### Otras pruebas (Play en el Editor, Unity 6000.4.7f1)

| Prueba | Resultado |
|---|---|
| Preparación de 5 s con el rival quieto y protegido; después empieza el round 1 | OK |
| Un round que llega a cero con el jugador caído espera a que se levante | OK |
| Un round que llega a cero con `CuentaEnMarcha` en `true` espera a que pase a `false` | OK |
| Descanso: un golpe al rival devuelve `NoCuenta`, el jugador recupera 20 de salud y el rival vuelve a su posición inicial | OK |
| Combate completo en tiempo real, sin intervenir (nivel 1): la consola marca 90, 20, 90, 20 y 90 s; termina empatado a 100 % y carga el nivel 2 | OK |
| Round acabado justo tras un knockdown (nivel 2): el rival se levanta a los 0,1 s por el descanso, no a los 3,7 s de su temporizador, y conserva la caída | OK |
| Al terminar por salud el rival se queda quieto y deja de atacar | OK |
| `ControlCombate` sin `ControlRondas` en la escena sigue ganando por KO | OK |
| Cartel "¡VICTORIA!" con la línea "Ganaste por salud" y panel "DERROTA" | OK |

### Pendiente de validar

Estas pruebas se hicieron sin visor ni Meta XR Simulator, con los menús contextuales y con llamadas directas a los componentes. Quedan por probar con visor:

- Cómo suena la campana y si se ubica bien en 3D.
- Un combate completo con golpes reales de los puños.
- La pausa real del Guardián y la del botón Menu durante un round.

## Bloque B: réferi y cuenta regresiva (RF-12, RF-13, RF-16)

`Arbitro`, en un objeto propio de cada escena de pelea, cuenta en voz alta del 1 al 10. La voz sale de su hijo `Voz`, un `AudioSource` 3D en (-2,2; 0,48; -0,8), a un lado del ring. El modelo del réferi se pondrá ahí sin tocar código.

| Parámetro | Valor | Campo |
|---|---|---|
| Número en el que se levanta el rival | 10 en los tres rivales | `cuentaKnockdown` (`PerfilOponente`) |
| Segundos por número, cuenta del rival | 1 s | `segundosPorNumero` (`Arbitro`) |
| Duración de la cuenta del jugador | 10 s | `segundosParaLevantarse` (`RecuperacionJugador`) |
| Tiempo sin recibir golpes al incorporarse | 1,3 s | `segundosAlLevantarse` (`PerfilOponente`) |
| Punto del clip en el que espera en el piso | 0,44 | `momentoPiso` (`ReaccionAnimada`) |
| Knockdown sin réferi en la escena | 3,7 s | `segundosKnockdown` (`PerfilOponente`) |

### Decisiones

- **Duración de la cuenta del rival:** dura 10 s en los tres rivales, como pide el plan. Es un valor por rival (`cuentaKnockdown`), así que se puede acortar sin tocar código si resulta largo al jugarlo. El reloj del round sigue corriendo durante esta cuenta, igual que en el bloque A; solo se detiene con el jugador en el piso.
- **Qué significa llegar a 10:** con el jugador es K.O.; con el rival, no. Sus caídas por umbrales de salud deciden la derrota, así que se levanta al terminar la cuenta aunque haya llegado a 10.
- **La caída final no se cuenta:** al llegar a `Derrotado` el combate termina en el acto (bloque A).
- **Ritmo:** cada número suena al empezar su segundo, de modo que el 10 suena en t = 9 s y la cuenta acaba en t = 10 s.
- **Animación en el piso:** el clip `Knockdown` cae, se queda abajo y se levanta en 3,7 s. Con réferi se sostiene en `momentoPiso` y, al terminar la cuenta, sigue desde ahí. Se fija el tiempo del estado y no `animator.speed`, porque `AtaqueOponente` devuelve la velocidad a 1. `ReaccionProcedural` no necesitó cambios: se queda inclinado mientras dure `Caido`.
- **Incorporarse:** `Caido` pasa a falso al terminar la cuenta, cuando el clip aún tarda 1,3 s en ponerlo de pie. En ese tiempo los golpes no cuentan, porque si no un golpe cortaría la animación.
- **Pausa:** un `AudioSource` no se detiene con `Time.timeScale` = 0, así que un número que estuviera sonando seguiría hasta acabar. `Arbitro` lo pausa y lo reanuda a mano desde el mismo punto.
- **Sin HUD:** se quitó el panel con número y barra. En el piso, la viñeta, el parpadeo y el latido siguen a `Progreso` y no a la salud, porque con 20 de salud objetivo la mejora casi no se notaría. Cada pulsación vibra más fuerte (de 0,25 a 1) y al levantarse hay dos vibraciones fuertes. Los carteles de K.O. y DERROTA se quedan.
- **Voces:** `Tools/sintetizar_audio.py` genera `Voz_Cuenta_01..10.wav` con un sintetizador de formantes (solo biblioteca estándar). Regenerar deja idénticos los demás clips. Para usar grabaciones basta con sobrescribir los WAV o arrastrar otros clips a `numeros`; volver a ejecutar el script sobrescribiría las grabaciones con el mismo nombre.

### Criterio de terminado

| Criterio | Resultado |
|---|---|
| La cuenta de 10 s funciona con la caída del jugador y la del rival | OK: del 1 al 10 con el clip de cada número, en 10,00 s (rival) y 10,01 s (jugador). Falta escucharla |
| Se puede provocar con los menús contextuales | OK: *Probar: contar al rival* y *Probar: contar al jugador* |
| El rival se levanta al terminar la cuenta | OK en los niveles 1, 2 y 3 |
| El round no se cierra en mitad de una cuenta | OK: el round llegó a cero en t = 2 s y se cerró en t = 10,00 s, también el último (la decisión por salud esperó) |
| La pausa detiene la cuenta | OK con el rival y con el jugador |
| Flujo del bloque A | OK: rounds, KO, decisión por salud, desbloqueo y vuelta al menú (ver abajo) |
| Compila sin errores ni warnings propios | OK |

### Otras pruebas (Play en el Editor, Unity 6000.4.7f1)

| Prueba | Resultado |
|---|---|
| El rival se sostiene en `Knockdown` a 0,44 toda la cuenta: cabeza a 0,43 m del piso (nivel 1), 0,49 m (nivel 2) y 0,59 m (nivel 3); de pie, 1,26, 1,56 y 1,71 m | OK |
| Knockdown por un golpe real a `RecibirGolpe`: empieza la cuenta | OK |
| Al incorporarse: un golpe a los 0,5 s no cuenta y uno a los 1,5 s conecta | OK |
| Un golpe durante la cuenta devuelve `NoCuenta` y no cambia la salud | OK |
| Pausa con un clip sonando: `isPlaying` pasa a falso, `timeSamples` se queda en 3292 durante 2 s, reanuda desde ahí, y la cuenta dura 10,01 s escalados (12,08 s reales) | OK |
| `Levantarse()` externo con un clip sonando, durante la pausa y por `EmpezarDescanso`: voz parada, cuenta cancelada y `CuentaEnMarcha` en falso; al reanudar no vuelve la voz | OK |
| *Probar: ganar el combate* (nivel 2) cancela la cuenta en cada knockdown y termina en `VictoriaPorKO` | OK |
| `ReaccionProcedural` añadido en Play (nivel 1): unos 80° de inclinación en t = 3 s y t = 9 s, y vuelve a 0° al levantarse | OK |
| `Arbitro` desactivado a mitad de cuenta: el rival no se queda en el piso. Con `Arbitro` desactivado, se levanta a los 3,70 s sin sostener la animación | OK |
| Mashing: en la caída 1, 0 → 6 → 11 pulsaciones lleva la viñeta de 0,95 a 0,67 y 0,43, el latido de 86 a 76 y 65 lpm, y su volumen de 1,00 a 0,63 y 0,26 | OK |
| La vibración se pide al pulsar y al levantarse, y no se pide con `"haptica"` = 0 | OK en código; sin visor no hay vibración real |
| Sin panel durante la cuenta (también en captura). Cartel de K.O. al acabarse el tiempo y al caer sin oportunidades | OK |
| K.O. del jugador con la cuenta del rival en marcha: la cuenta termina, el rival se levanta y no queda voz | OK |
| Caen los dos: la voz cuenta al jugador; al levantarse él, sigue la del rival | OK |
| Ganar por salud y por KO sube `nivelSuperado` a 2 y 3 y carga la escena siguiente; perder por salud o K.O. carga `Main_Scene` sin cambiarlo | OK |

### Pendiente de validar

Estas pruebas se hicieron sin visor ni Meta XR Simulator, con los menús contextuales y llamadas directas a los componentes. Se comprobó qué clip suena y cuándo, pero **no se ha escuchado ninguno**. Quedan por probar con visor:

- Si las voces sintetizadas se entienden. Si no, conviene grabarlas.
- Cómo se ubica la voz en 3D y si se oye sobre la multitud.
- La háptica real de las pulsaciones y al levantarse.
- Si la viñeta y el latido bastan para notar el avance, y si la viñeta cubre todo el campo visual (en la pantalla plana del Editor se ven sus bordes).
- **Sin el panel, nada le dice al jugador que debe pulsar A / B / X / Y.** Hay que explicarlo en otro sitio o añadir una señal; el bloque E puede sumar la háptica de la cuenta.
- El button mashing con botones reales: las pulsaciones se simularon con *Probar: pulsar una vez*.
- Si 10 s por knockdown se hacen largos al jugar; se ajusta con `cuentaKnockdown`.

## Bloque C: IA de los niveles 3 y 4 (RF-07, RF-08)

Soda Popinski lanza combinaciones de ganchos, Mr. Sandman esquiva y contraataca, y el nivel 4 ya se puede jugar con un modelo provisional. Los valores van en `PerfilOponente`, en los assets de `Assets/_Project/Data/Oponentes/`.

### Combos

`AtaqueOponente` lanza en cada ataque la secuencia `combo` del perfil. Con la secuencia vacía sigue el ciclo de `uppercutCada`, así que Glass Joe y Bald Bull no cambian.

- **Encadenado:** el siguiente golpe arranca `segundosEntreGolpesCombo` después del impacto del anterior, sin pasar por `Idle`. Cada golpe vuelve a apuntar a la cabeza del jugador: moverse esquiva uno, no todo el combo.
- **Qué lo cancela:** un golpe conectado, una caída (también la que no viene de un golpe, `Derribar`), el jugador en el piso, `Pausar` (fin del round o del combate) y desactivar el componente. Cancelar borra el resto del combo y el impacto pendiente.
- **Qué no lo cancela:** un golpe que el jugador bloquea, porque el rival sigue presionando sobre la guardia. La pausa tampoco: el impacto usa tiempo escalado y llega al reanudar.
- **Entre dos golpes** `Atacando` sigue en verdadero, así que el rival no bloquea ni esquiva. Contragolpear en mitad del combo siempre entra y lo corta, igual que con un golpe suelto.

### Soda Popinski: combinaciones rápidas de ganchos

Su combo es izquierda, derecha, izquierda: `Gancho2`, `Gancho`, `Gancho2`. Sube a ×1,3 de velocidad y cada gancho quita menos que sus golpes anteriores. La espera entre combos es algo mayor, porque cada ataque ahora son tres golpes.

- **Estados nuevos:** `Ataque_Gancho` y `Ataque_Gancho_2`, con la misma transición a `Idle` que los otros ataques. Están en `EsAtaque`, y de ahí dependen `Atacando`, `DefensaOponente` y el freno contra la guardia.
- **Sin huecos en los override controllers:** un override sustituye clips, no estados, así que un estado nuevo no podía compartir clip base con otro. Por eso estos dos usan directamente los clips de Soda: Soda y Bald Bull los reciben sin override y sus listas no tienen entradas vacías. Glass Joe y Bald Bull no los usan.
- **Desplazamiento:** los clips hacen avanzar al personaje 1,33 m (`Ataque_Gancho`) y 0,71 m (`Ataque_Gancho_2`), lo que a la escala del nivel 3 serían 1,06 m y 0,57 m: desde 1,2 m se metería en el jugador. `LimiteRing` no lo evitaría, porque mide desde la raíz y el avance va en la pose. Se quitó en la importación, desactivando *Bake Into Pose* XZ de esos dos clips; con `applyRootMotion` apagado, el cuerpo se queda en su sitio. En Play las caderas se separan como mucho 0,19 m de la raíz y la cabeza del rival no baja de 1,09 m de la del jugador. El guante sigue llegando a la cara, como se ve en las capturas.
- **Segundo golpe de `Ataque_Gancho`:** el clip trae una derecha y después una izquierda baja. Solo cuenta la derecha. El combo la corta antes de la izquierda, por eso no debe terminar en `Gancho`.

### Tiempos de impacto medidos

Se muestreó cada clip a 30 fotogramas por segundo. En los ganchos, el impacto es el momento en que el puño cruza por delante de la cara; se divide por `velocidadAtaque` igual que los anteriores.

| Clip (a escala 1) | Estado | Impacto en el clip | Altura del puño | Soda ×1,3 | Sandman ×1,4 |
|---|---|---|---|---|---|
| `Ataque_Gancho_2` (izquierda) | `Ataque_Gancho_2` | 0,50 s | 1,93 m (cabeza 2,04 m) | 0,38 s | 0,36 s |
| `Ataque_Gancho` (derecha) | `Ataque_Gancho` | 0,80 s | 1,62 m (cabeza 1,84 m) | 0,62 s | no lo usa |
| `Ataque_Fuerte`, primer barrido | `Ataque_Uppercut` | 0,30 s | 1,34 m (cabeza 1,82 m) | no lo usa | 0,21 s |

En Play, los impactos de Soda llegaron a +0,40, +1,14 y +1,65 s desde el inicio del combo; el tiempo del clip en cada impacto coincidió con la tabla, con una diferencia de un fotograma como máximo.

### EsquiveOponente

Va junto a `DefensaOponente` y solo está montado en el nivel 4. *Configurar oponente seleccionado* también lo añade, pero no hace nada mientras `probabilidadEsquive` sea 0, que es el valor por defecto y el de los otros tres perfiles.

**Dónde se decide:** al contacto, en `Oponente.RecibirGolpe`, y no anticipando con la mano. Solo al entrar en una `ZonaGolpe` se sabe que hay un golpe de verdad, con su tipo y su zona, así que un amago no lo hace saltar. Además sigue el mismo camino que el bloqueo y se puede probar sin visor. El costo es que el guante ya toca la hitbox cuando él empieza a moverse: para disimularlo sale deprisa desde el primer fotograma. Si con visor se ve mal, se puede añadir la anticipación sin cambiar el resultado ni el evento.

**Orden con el bloqueo:**

1. Con la guardia arriba, bloquea. No esquiva.
2. En la abertura tras bajar la guardia, no hace ninguna de las dos cosas: es el premio del jugador por no rendirse.
3. Si no, tira el esquive y, si falla, tira el bloqueo. Fuera del enfriamiento, la probabilidad real de bloquear es (1 − esquive) × bloqueo.

**Golpes seguidos de `DefensaOponente`:** un golpe esquivado no llegó a tocarlo, así que no los aumenta ni los reinicia. La ventana de 1,5 s puede vencer durante el esquive.

**Resultado:** `ResultadoGolpe.Esquivado`. No quita salud, no hay sonido de golpe, no hay animación de reacción y `Puno` no vibra (la háptica propia es del bloque E). Sí activa el enfriamiento del puño, para que el mismo golpe no conecte después en la otra zona. `Oponente` cuenta `Esquives` y dispara `AlEsquivar`.

**Cuándo no esquiva:** mientras ataca, cae, se incorpora o está `Protegido`, con la guardia ocupada o durante `enfriamientoEsquive`.

**Ventana de contraataque:** se interpretó como el contraataque del rival: `AlEsquivar` fija su próximo ataque dentro de `ventanaContraataque`, igual que `contraataqueTrasBloqueo`. Con la guardia del jugador baja, `castigoGuardiaBaja` la acorta. También le deja una ventana al jugador: mientras vuelve a su sitio se le puede pegar, y si ya está contraatacando, el golpe entra seguro.

**Movimiento:** sin animación de esquive, se mueve el transform.

- Se aparta al lado contrario del guante o, en el 30 % de los casos, hacia atrás. Si una dirección lo deja contra las cuerdas, prueba otra.
- El desvío se aplica como diferencia en cada fotograma, así que se suma a cualquier otro movimiento. `Acercarse` espera a que termine, y la vuelta del descanso sigue su camino y llega igual.
- Al terminar no queda desvío, y si desactivan el componente a mitad, lo deshace.
- Las hitboxes cuelgan de los huesos y se mueven con él.
- Nunca sale del ring: `LimiteRing.DentroDelRing`, el mismo límite que usa `Acercarse`.

### Mr. Sandman, nivel 4

`Scene_Ring_Level4.unity` es una copia de la 3 con `Arbitro`, `ControlRondas`, `PausaGuardian`, `PauseMenu` y `Multitud`. Tiene `nivel` = 4 y vuelve a `Main_Scene`. El nivel 3 ahora carga el 4 con el mensaje "Siguiente: Nivel 4". El mensaje del 4 ya no dice "eres el campeón", porque según el informe ese título llega con Iron Mike Tyson.

**Modelo provisional:** la misma instancia de Soda con cuatro materiales lisos propios (`MrSandman/Materials/Sandman_*.mat`), su `AnimatorOverrideController` y `MrSandman.asset`. No se usó `mr_sandman.glb`: no tiene esqueleto y `AtaqueOponente` necesita un Animator. Para cambiarlo sin tocar código: poner el modelo nuevo en la escena, usar *Tools > Boxeo > Configurar oponente seleccionado*, añadirle `AtaqueOponente`, asignarle `MrSandman.asset` y volver a medir los `retardoImpacto`.

Su combo es `Gancho2`, `Uppercut`, `Gancho2`, que en este modelo son un gancho, un barrido de `Ataque_Fuerte` (el primero, a 0,30 s) y otro gancho. No usa los golpes de `Ataque_Jab` porque en este rig son lentos (1,13 s de preparación). Tampoco los de Glass Joe: traen un paso adelante que, adaptado a este cuerpo, lo metería en el jugador, y quitarlo cambiaría también a Glass Joe.

### Valores de cada rival

Actualiza la tabla de `docs/Entrega3.md`. En negrita, lo que cambió en Soda.

| Parámetro | Glass Joe | Bald Bull | Soda Popinski | Mr. Sandman |
|---|---|---|---|---|
| Salud | 100 | 150 | 200 | 175 |
| Bloqueo (base → máxima) | 8 % → 30 % | 20 % → 55 % | 30 % → 65 % | 25 % → 55 % |
| Esquive | no | no | no | 45 %, 1,0 s de enfriamiento, 0,35 m |
| Contraataque tras esquivar | no | no | no | 0,25 a 0,5 s |
| Velocidad de ataque | ×0,9 | ×1,15 | **×1,3** | ×1,4 |
| Desplazamiento | 0,4 m/s | 0,7 m/s | 0,9 m/s | 1,1 m/s |
| Patrón | jab ×3, uppercut | jab ×2, uppercut | **izquierda, derecha, izquierda** | gancho, barrido, gancho |
| Pausa entre golpes del combo | no | no | **0,12 s** | 0,12 s |
| Espera entre ataques | 4 a 6 s | 3,5 a 5 s | **3,2 a 4,6 s** | 2,8 a 4 s |
| Daño por golpe | 4 / 7 | 8 / 14 | **9** | 8 / 12 |
| Castigo por guardia baja | +30 % | +80 % | +100 % | +100 % |

### Por qué sube la dificultad

Sandman tiene menos salud que Soda, como pide "salud intermedia". Lo compensan el esquive y un ritmo más alto, y cada uno de sus golpes quita menos.

| Medida | Glass Joe | Bald Bull | Soda Popinski | Mr. Sandman |
|---|---|---|---|---|
| Daño por segundo si todo conecta, guardia arriba (calculado) | 0,95 | 2,35 | 5,24 (antes 3,6) | 6,65 |
| Golpes para ganarle, a 1 golpe cada 0,5 s (modelo) | 19 | 36 | 51 | 52 |
| Golpes para ganarle, en ráfagas de 5 con 6 s de pausa (modelo) | 18 | 33 | 48 | 51 |
| Golpes para ganarle, a 1 golpe cada 1,5 s (modelo) | 18 | 31 | 46 | 62 |
| Pelea simulada en Play: segundos de juego | 30 | 64 | 83 | 95 |
| Pelea simulada en Play: salud final del jugador | 99 | 85 | 39 | 7 |

- El **modelo** es una simulación con 20 000 peleas que copia la lógica de `RecibirGolpe`, `DefensaOponente` y `EsquiveOponente`, con crosses a la cabeza de 7,5 de daño. Es una estimación, no una medida de juego.
- La **pelea simulada** es una sola partida por rival, a velocidad ×2, con un jugador programado: ráfagas de 5 golpes y la guardia siempre arriba. Sirve para comparar, no para fijar el balance real. La de Glass Joe y Bald Bull se jugó con las mismas condiciones y sus perfiles no cambiaron.
- La primera versión de Sandman (35 % de esquive, 20 % de bloqueo) salió **más fácil** que Soda en la pelea simulada, 79 s contra 95 s, y por eso se subieron sus defensas y se acortó su espera.
- **Glass Joe sigue pegando casi tan pronto como Sandman:** su clip tiene poca preparación (0,39 s hasta el impacto, frente a 0,36 s). Lo que lo hace fácil es el ritmo, la salud y el daño. No se cambió, porque Glass Joe debía quedar igual.

### Criterio de terminado

| Criterio | Resultado |
|---|---|
| Se puede jugar contra los cuatro rivales | OK: partida continua del nivel 1 al 4 con el jugador programado, hasta volver al menú. Falta jugarla con visor |
| El nivel 4 aparece en el menú, bloqueado hasta ganar el 3 | OK: con `nivelSuperado` = 2 dice BLOQUEADO y `PlayLevel(4)` no carga; con 3 se abre y el botón carga la escena |
| Soda lanza las combinaciones de su perfil | OK: `Gancho_2`, `Gancho`, `Gancho_2`, cada una con tres impactos |
| Un golpe o una caída las interrumpen sin impactos fantasma | OK: ver la tabla siguiente |
| Sandman esquiva según su perfil, sin daño y con `AlEsquivar` | OK: 35,2 % en 4000 intentos con el perfil entonces en 35 %; 0 de 4000 con probabilidad 0. Salud sin cambios y `AlEsquivar` una vez |
| No sale del ring y tiene ventana de contraataque | OK: 15 esquives junto a las cuerdas y en las esquinas sin pasar el margen; el contraataque arrancó a los 0,48 s con una espera sorteada de 0,48 s |
| Se puede provocar con el menú contextual | OK: *Probar: esquivar* |
| Flujo de los bloques A y B en los cuatro niveles | OK: ver "Otras pruebas" |
| Compila sin errores ni warnings propios | OK en el Editor y al compilar los scripts para Android, sin generar el APK. Los únicos avisos son de OpenXR por no haber visor |

### Otras pruebas (Play en el Editor, Unity 6000.4.7f1)

Sin visor, la cabeza se queda en el piso y los ataques no llegan. Para estas pruebas se desactivó `OVRCameraRig` en Play y se colocaron la cabeza y las manos a mano. La escena no se guardó.

| Prueba de los combos (nivel 3) | Resultado |
|---|---|
| Combo completo con la guardia baja: tres impactos conectados, 100 → 73 de salud; vuelve a `Idle` a velocidad normal | OK |
| Golpe entre dos golpes del combo: lo corta; 1 impacto en total | OK |
| Golpe con el segundo en camino, a 0,43 s de llegar: lo corta; 1 impacto en total | OK |
| `Derribar` con el segundo en camino: ningún impacto en el piso; el réferi empieza a contar | OK |
| Knockdown por un golpe a mitad de combo: la cuenta dura 10,00 s; 1 impacto en total | OK |
| K.O. del rival con el segundo golpe en camino: 0 impactos después; victoria | OK (nivel 4) |
| El jugador cae entre dos golpes: 1 impacto en total; al levantarse sigue la pelea | OK |
| Pausa con el segundo en camino: el reloj escalado no avanza en 1,5 s reales; al reanudar llegan los dos golpes que faltaban | OK |
| Guardia arriba todo el combo: tres bloqueos, el guante frena en cada uno, el combo sigue y el Animator vuelve a velocidad 1 | OK |
| El jugador saca la cabeza después de cada lanzamiento: tres esquives y sin daño | OK |
| Fin del round con el segundo en camino: `Descanso`, 1 impacto, vuelve a `Idle`; el round siguiente empieza un combo desde el primer golpe | OK |
| Mismo estado dos veces seguidas (jab, jab, uppercut con un perfil de prueba): tres impactos, 10 + 10 + 18 | OK |
| Secuencia vacía en ese perfil: jab, jab, uppercut por `uppercutCada`. Bald Bull: jab, jab, uppercut en toda la pelea | OK |

| Prueba del esquive (nivel 4) | Resultado |
|---|---|
| Se aparta 0,350 m y vuelve con 0,0000 m de desvío; la hitbox se separa 0,055 m de su sitio respecto a la raíz, por la animación | OK |
| No esquiva atacando, entre golpes del combo, caído, incorporándose ni `Protegido`, aunque se fuerce saltando el azar | OK |
| Guardia arriba: resultado `Bloqueado`. Abertura tras la guardia: `Conectado` | OK |
| Un esquive no cambia `seguidos` ni el último golpe de `DefensaOponente`; el golpe siguiente sí pasa por el bloqueo | OK |
| Enfriamiento: el motivo para no esquivar es "todavía está en un esquive" y, al acabar el movimiento, "está en enfriamiento"; en la pelea, nunca hubo dos esquives a menos de 1,01 s (enfriamiento 1,0 s) | OK |
| Esquive con el round acabando: llega a la posición inicial con 0,0000 m de error | OK |
| `Acercarse` espera al esquive: 0,0000 m de avance durante el esquive; después vuelve a caminar | OK |
| Golpe real de un `Puno` contra la hitbox: esquivado sin vibración; sin esquive, conectado y con vibración pedida | OK; sin visor no hay vibración real |
| Pausa a medio esquive: 0,0000 m de movimiento en pausa; termina al reanudar | OK |

| Flujo de los bloques A y B | Resultado |
|---|---|
| Nivel 1 → 2 → 3 → 4 → menú ganando por KO: `nivelSuperado` sube de 1 a 4 y se carga la escena siguiente en cada paso | OK |
| Nivel 4: `VictoriaPorSalud` sube a 4 y va al menú | OK |
| Nivel 4: `DerrotaPorSalud` y K.O. del jugador van al menú sin cambiar `nivelSuperado` | OK |
| Nivel 4: cuenta del rival del 1 al 10 en 10,00 s y se levanta. Cuenta del jugador con el cronómetro detenido | OK |
| Nivel 4: el round llega a cero con una cuenta en marcha y se cierra a los 10,00 s | OK |
| Nivel 4: pausa del Guardián con "VUELVE AL ÁREA SEGURA", cronómetro detenido y reanudación | OK |
| Ningún impacto con el rival caído o el jugador en el piso, y ninguno a menos de 1,83 s de un golpe conectado, en las cuatro peleas | OK |
| Panel de niveles en captura: cuatro botones en fila desde la posición del jugador, con el cuarto atenuado | OK |
| Capturas de Soda y Sandman en el impacto y durante el esquive: sin T-pose, con el guante cerca de la cara | OK |

### Pendiente de validar

Estas pruebas se hicieron sin visor ni Meta XR Simulator. Los ataques se provocaron por código y los golpes llamaron directamente a `RecibirGolpe`, salvo el del `Puno`. **Nadie ha jugado todavía estos combates.** Quedan por probar con visor:

- Si los combos se sienten rápidos y se pueden defender, y si la pausa de 0,12 s entre golpes es buena.
- Si el golpe de 0,21 s de Sandman da tiempo a reaccionar. El barrido de `Ataque_Fuerte` es muy rápido y va a la altura del pecho, aunque la lógica apunta a la cabeza.
- Si el esquive se ve bien decidiéndose al contacto, porque el guante llega a tocarlo antes de que se mueva. Si no, conviene la anticipación.
- Si 0,35 m bastan para que se note, y si desplazarse sin animación se ve aceptable.
- Si los pies se ven resbalar en los ganchos, porque el clip da un paso y el cuerpo se queda en su sitio.
- El balance real: los números son de un jugador programado y de un modelo. Un jugador con la guardia baja recibe mucho más daño, por `castigoGuardiaBaja`.
- El botón del nivel 4 con el puntero láser: se invocó su `onClick` desde código.
- No se volvió a medir un round completo de 90 s en el nivel 4, aunque usa el mismo componente que los otros niveles. El rendimiento en el Quest (RNF-01) y el *Build And Run* siguen pendientes.

## Trazabilidad de requisitos

Solo los requisitos que cambian respecto a `docs/Entrega3.md`.

| ID | Requisito | Entregable | Estado |
|---|---|---|---|
| RF-07 | Rivales con patrón de ataque, velocidad y bloqueo o esquive propios | 3, 4 y 5 | Hecho para los niveles 1 a 4, con Mr. Sandman en modelo provisional; falta Iron Mike Tyson y validarlo con visor |
| RF-08 | Dificultad ascendente | 3, 4 y 5 | Hecho para los niveles 1 a 4; falta el nivel 5 y validarlo con visor |
| RF-09 | Desbloqueo del siguiente nivel | 2 y 3 | Hecho para los cuatro niveles |
| RF-10 | 3 rounds de 90 s con campana | 4 | Hecho; falta validarlo con visor |
| RF-11 | Ganador por KO o por salud al final | 4 | Hecho; falta validarlo con visor |
| RF-12 | Cuenta de 10 s y button mashing | 4 | Hecho con voz 3D y sin HUD, sin el modelo del réferi; falta validarlo con visor |
| RF-13 | Derrota y reintento del nivel | 4 | Hecho: el K.O. y la derrota por salud vuelven al menú con el nivel abierto; falta validarlo con visor |
| RF-16 | Audio 3D de golpes, multitud y réferi | 3 y 4 | Hecho; falta escuchar la campana y las voces con visor |
