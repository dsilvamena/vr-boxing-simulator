# Entrega 4 (semana 13): avance del 70 %

Registro de lo implementado según `docs/Plan_Entrega4_70.md`. Por ahora cubre los bloques A y B; los bloques C, E y D se añadirán al terminarlos.

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

## Trazabilidad de requisitos

Solo los requisitos que cambian respecto a `docs/Entrega3.md`.

| ID | Requisito | Entregable | Estado |
|---|---|---|---|
| RF-10 | 3 rounds de 90 s con campana | 4 | Hecho; falta validarlo con visor |
| RF-11 | Ganador por KO o por salud al final | 4 | Hecho; falta validarlo con visor |
| RF-12 | Cuenta de 10 s y button mashing | 4 | Hecho con voz 3D y sin HUD, sin el modelo del réferi; falta validarlo con visor |
| RF-13 | Derrota y reintento del nivel | 4 | Hecho: el K.O. y la derrota por salud vuelven al menú con el nivel abierto; falta validarlo con visor |
| RF-16 | Audio 3D de golpes, multitud y réferi | 3 y 4 | Hecho; falta escuchar la campana y las voces con visor |
