# Entrega 4 (semana 13): avance del 70 %

Registro de lo implementado según `docs/Plan_Entrega4_70.md`. Por ahora cubre el bloque A; los bloques B, C, E y D se añadirán al terminarlos.

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

## Trazabilidad de requisitos

Solo los requisitos que cambian respecto a `docs/Entrega3.md`.

| ID | Requisito | Entregable | Estado |
|---|---|---|---|
| RF-10 | 3 rounds de 90 s con campana | 4 | Hecho; falta validarlo con visor |
| RF-11 | Ganador por KO o por salud al final | 4 | Hecho; falta validarlo con visor |
| RF-16 | Audio 3D de golpes, multitud y réferi | 3 y 4 | Hecho para golpes, multitud y campana; falta la voz del réferi |
