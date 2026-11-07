# Borrador para el informe: Entregable 4 (semana 13)

> Texto para copiar al informe del curso (`Informe_SimuladorDeBoxeo.docx`) como sección 11, a continuación de la sección 10 (Entregable 3). Las figuras están en `docs/capturas/`. Al final hay una propuesta para las dos observaciones del docente en la revisión de la semana 12 y una lista de datos del informe que no coinciden con el proyecto.

---

## 11. Implementación del Entregable 4: avances de la semana 13 (70 % de avance acumulado)

### 11.1 Resumen del hito y resultado verificable

Según el cronograma (sección 8.1), el Entregable 4 comprende la IA de los niveles 3 y 4 (Soda Popinski y Mr. Sandman), el réferi con cuenta regresiva y button mashing tras el KO, las rondas de 3 × 90 s, el Hand Grab Recorder en los bloqueos y la háptica diferenciada. Su resultado verificable es: **cuatro rivales jugables y ciclo de KO funcionando**. Se cumplió: el jugador puede recorrer los niveles 1 a 4 en una partida continua, cada combate se organiza en rounds con campana, cualquiera de los dos contendientes puede caer y ser contado por el réferi, y el combate termina por KO o por salud al final del tercer round.

Además se adelantó el registro de métricas por sesión (RF-21, previsto para las semanas 13 y 14), que servirá para las pruebas HEART de la semana 14.

Todo se implementó en Unity 6000.4.7f1 para Meta Quest (OpenXR, Android) y se probó en el Editor. Por no contar todavía con los modelos 3D del réferi, de Mr. Sandman y de Iron Mike Tyson, se usaron modelos provisionales que se reemplazan sin modificar el código.

### 11.2 Rondas de 3 × 90 segundos (RF-10, RF-11)

El script `ControlRondas` organiza cada combate en las fases de preparación (5 s), round (90 s), descanso (20 s) y final. Una campana espacial 3D en una esquina del ring suena una vez al empezar cada round y tres veces al terminarlo, para distinguir ambos momentos solo de oído. El cronómetro usa tiempo escalado: se detiene con el menú de pausa y con la pausa del Guardián, y no avanza mientras el jugador está en la lona. Fuera de los rounds el rival no ataca ni recibe golpes. En cada descanso vuelve a su esquina y el jugador recupera el 20 % de su salud.

Un KO termina el combate al instante. Si se completan los tres rounds, gana quien conserve más salud en porcentaje; en caso de empate gana el jugador, para no alargar un combate que el diseño limita a tres rounds por la fatiga física (sección 7.2).

### 11.3 Réferi, cuenta regresiva y button mashing (RF-12, RF-13, RF-16)

El réferi (script `Arbitro`) cuenta en voz alta del 1 al 10 con una voz espacial 3D que sale de su cabeza. Las diez grabaciones de los números se sintetizaron con un sintetizador de formantes (`Tools/sintetizar_audio.py`) y pueden sustituirse por grabaciones reales sin tocar el código.

- **Caída del jugador:** al quedarse sin salud empieza una cuenta de 10 s. Pulsar rápido los botones A, B, X o Y lo levanta. Cada pulsación aclara la visión periférica, calma el latido y vibra más fuerte. Si la cuenta termina, el jugador pierde por K.O. y vuelve al menú con el nivel todavía disponible para reintentarlo (RF-13).
- **Caída del rival:** el réferi cuenta y el rival se levanta al terminar la cuenta. La animación de caída se sostiene en el piso hasta ese momento. La caída final (la tercera) no se cuenta: termina el combate.

**Réferi visible (modelo provisional).** El script `CuerpoArbitro` da cuerpo al réferi (figura 6). Durante el round se mantiene a 2 m de la línea de la pelea, en la periferia de la vista. En la cuenta del rival se acerca a él y lo señala. En la cuenta del jugador se coloca delante de este, ligeramente a un lado para no superponerse al rival, como en el boceto del estado de knockout (figura 4 de la sección 4.4). Con cada número baja el brazo y lo vuelve a subir, sincronizado con la voz. Sobre su cabeza aparece el cartel "¡ARRIBA! PULSA A B X Y", que indica cómo levantarse. Está anclado al réferi dentro del mundo y no a la cámara, de modo que se mantiene el principio de no usar HUD (figura 7). El modelo provisional está hecho con primitivas (camisa blanca, pantalón y pajarita negros). El modelo definitivo de la tabla de assets se coloca en su lugar sin modificar el código.

### 11.4 IA de los niveles 3 y 4 (RF-07, RF-08)

- **Soda Popinski (nivel 3), combinaciones rápidas de ganchos:** cada ataque encadena tres golpes (izquierda, derecha, izquierda) con 0,12 s entre ellos. Cada golpe vuelve a apuntar a la cabeza del jugador, así que se puede esquivar uno sin esquivar el combo entero. Un golpe conectado o una caída del rival cortan la combinación sin dejar impactos pendientes.
- **Mr. Sandman (nivel 4), alta velocidad y esquive activo:** decide al contacto si esquiva, antes que el bloqueo, con un 45 % de probabilidad y un enfriamiento de 1 s. Se aparta 0,35 m hacia el lado contrario del guante o hacia atrás sin salir del ring, y contraataca entre 0,25 y 0,5 s después. No esquiva mientras ataca ni justo después de bajar la guardia: ese momento es la ventana del jugador. Usa provisionalmente el modelo de Soda con otros colores.

Los parámetros de los cuatro rivales están en un asset `PerfilOponente` por personaje (RNF-10). La dificultad sube del nivel 1 al 4 (tabla 11.1).

*Tabla 11.1. Parámetros de cada rival.*

| Parámetro | Glass Joe | Bald Bull | Soda Popinski | Mr. Sandman |
|---|---|---|---|---|
| Salud | 100 | 150 | 200 | 175 |
| Bloqueo (base → máxima) | 8 % → 30 % | 20 % → 55 % | 30 % → 65 % | 25 % → 55 % |
| Esquive | — | — | — | 45 % |
| Velocidad de ataque | ×0,9 | ×1,15 | ×1,3 | ×1,4 |
| Patrón | jab ×3, uppercut | jab ×2, uppercut | gancho izq., der., izq. | gancho, barrido, gancho |
| Espera entre ataques | 4 a 6 s | 3,5 a 5 s | 3,2 a 4,6 s | 2,8 a 4 s |

### 11.5 Hand Grab Recorder en los bloqueos

Para que los bloqueos de los rivales no sean un movimiento rígido en línea recta, se implementó un grabador de guardias:

- **Grabación:** con el visor puesto y las manos abajo, el jugador pulsa a la vez los dos sticks. Tras una cuenta atrás háptica, el script `GrabadoraGuardia` registra durante 2 s la posición y la rotación de ambos controladores respecto a la cabeza y conserva solo el tramo en que se sube la guardia. El resultado es un asset `GrabacionGuardia`.
- **Reproducción:** cuando un rival bloquea, `DefensaOponente` lleva sus guantes por esa trayectoria mediante cinemática inversa (IK) de manos, hacia delante al subir la guardia y al revés al bajarla. La trayectoria parte de la pose animada del rival y se escala a su estatura.

Mientras no se disponga de una grabación hecha con el visor, los cuatro rivales usan una trayectoria provisional sintetizada. Además, el escalado por estatura corrigió un defecto de Soda Popinski, cuyos guantes atravesaban su cara con la guardia fija anterior (figuras 9 y 10).

### 11.6 Háptica diferenciada (RF-15)

Se completó el repertorio de patrones de vibración (tabla 11.2) para que el jugador distinga cada evento sin mirar.

*Tabla 11.2. Patrones hápticos añadidos en esta entrega.*

| Evento | Patrón | Sensación |
|---|---|---|
| El rival esquiva el golpe | 1 pulso de 0,15 durante 30 ms | Roce leve: el puño no tocó nada sólido |
| El golpe derriba al rival | 3 pulsos de 1,0 (80, 80 y 220 ms) | Recompensa clara, distinta de cualquier golpe |
| Cada número de la cuenta del jugador | Pulso de 30 ms en ambos controles, de 0,2 a 0,6 | Marca el paso del tiempo e invita a pulsar |
| Pulsación de A/B/X/Y en la cuenta | Pulso de 50 ms, de 0,25 a 1,0 | El avance hacia levantarse |
| Levantarse | 2 pulsos fuertes | Ya no hace falta pulsar |

Toda la vibración pasa por un único punto (`Haptica`), lo que permitió verificar cada patrón en el Editor registrando sus llamadas con marca de tiempo.

### 11.7 Registro de métricas por sesión (RF-21)

El script `RegistroSesion` se activa solo al iniciar la aplicación y registra cada combate sin intervenir en la lógica del juego:

- **Combate:** nivel, rival, si es un reintento, resultado y round alcanzado.
- **Golpes del jugador:** tiempo de combate, golpes lanzados y conectados, y precisión.
- **Defensa del jugador:** ataques recibidos y bloqueados, y porcentaje de bloqueo.
- **Caídas:** caídas sufridas y cuentas superadas.
- **Rendimiento:** FPS medios y porcentaje de fotogramas por debajo de 72 FPS durante los rounds (RNF-01).

Los datos se guardan en el visor, en un JSON por sesión y en un CSV con una fila por combate, listos para analizarse en una hoja de cálculo. Cubren los indicadores HEART de *Engagement* (duración, combates reintentados) y *Task Success* (precisión, golpes bloqueados, superación de la cuenta del réferi) de la sección 3.6.

### 11.8 Verificación

Las pruebas se realizaron en Play en el Editor, sin visor, mediante menús contextuales de prueba y llamadas a los componentes:

- Partida continua del nivel 1 al 4 con desbloqueo progresivo.
- Cuenta completa del rival y del jugador, con K.O. y vuelta al menú.
- Golpes reales del guante contra las zonas de impacto, verificando el patrón háptico de cada caso.
- Esquive y combos de los rivales.
- Registro de una sesión con victoria, abandono y reintento.

Los scripts compilan sin errores para Android. Queda pendiente validar con el visor la sensación háptica, la inteligibilidad de la voz sintetizada, la grabación de una guardia real y el rendimiento en el Meta Quest 2.

*Figuras de esta sección (en `docs/capturas/`):*

1. Figura 5. Menú principal diegético dentro de la arena (`01_menu_principal.png`).
2. Figura 6. Nivel 1, Glass Joe, con el réferi provisional a la izquierda, desde los ojos del jugador (`02_nivel1_glass_joe.png`).
3. Figura 7. Cuenta del jugador: el réferi delante, con el brazo en alto y el cartel de los botones (`07_cuenta_del_jugador.png`).
4. Figura 8. Cuenta del rival: el réferi señala a Mr. Sandman en la lona (`08_cuenta_del_rival.png`).
5. Figuras 9 y 10. Guardia de Soda Popinski con la posición fija anterior y con la trayectoria grabada (`09_guardia_fija.png`, `10_guardia_grabada.png`).
6. Figuras 11 a 13. Niveles 2, 3 y 4 (`03_…`, `04_…`, `05_…`).

### 11.9 Resumen de cumplimiento (semana 13)

| Actividad planificada | Implementación en Unity / scripts C# | Estado |
|---|---|---|
| IA de los niveles 3 y 4 | `AtaqueOponente` (combos), `EsquiveOponente`, perfiles de Soda Popinski y Mr. Sandman | Completado (Mr. Sandman con modelo provisional) |
| Réferi con cuenta regresiva y button mashing | `Arbitro`, `CuerpoArbitro`, `RecuperacionJugador`, voz 3D sintetizada | Completado (modelo del réferi provisional) |
| Rondas de 3 × 90 s | `ControlRondas`, campana 3D | Completado |
| Hand Grab Recorder en los bloqueos | `GrabadoraGuardia`, `GrabacionGuardia`, `DefensaOponente` | Completado (falta grabar una guardia real con el visor) |
| Háptica diferenciada | `Puno`, `SaludJugador`, `FeedbackSalud`, `Haptica` | Completado |
| Registro de métricas de sesión (adelantado) | `RegistroSesion` | Completado (falta probarlo en el Quest) |
| Resultado verificable | Cuatro rivales jugables y ciclo de KO funcionando | Cumplido |

---

## Observaciones del docente en la revisión de la semana 12

El PDF anotado (`docs/annotated-Informe_SimuladorBox_Semana_12.pdf`) tiene dos comentarios del docente.

### 1. Página 8, sección 3.5: "EL ARCO DE ACUERDO A LA TEORIA VISTO EN CLASE"

El arco actual tiene cuatro momentos (detonante, acción ascendente, clímax y resolución). Las estructuras narrativas clásicas tienen cinco o seis: la pirámide de Freytag tiene exposición, acción ascendente, clímax, acción descendente y desenlace, a menudo con el detonante como un punto propio. **Hay que confirmar con las diapositivas del curso qué modelo se vio en clase.** Una propuesta que se ajusta a lo que ya existe en el juego:

| Etapa | En la experiencia |
|---|---|
| Exposición | El jugador entra a la arena del gimnasio underground (menú principal dentro del ring), ve a los rivales y aprende el control con los guantes. |
| Detonante (incidente incitador) | La campana del primer round contra Glass Joe: empieza el camino al cinturón regional. |
| Acción ascendente | Bald Bull, Soda Popinski y Mr. Sandman: cada rival sube la presión (fuerza, combos, esquive) y cada victoria desbloquea al siguiente. Las caídas y la cuenta del réferi son los puntos de tensión. |
| Clímax | El combate contra Iron Mike Tyson (nivel 5): el menor descuido es un KO. |
| Acción descendente | La caída final de Tyson, la cuenta del réferi que no se supera y la ovación de la multitud. |
| Desenlace | El cinturón de campeón y la hoja de estadísticas (RF-18, RF-19). |

### 2. Página 14, sección 6.1: "No se ve en el informe" (junto a las escenas)

El informe enumera las escenas, pero no muestra ninguna. Se propone añadir las capturas de `docs/capturas/` (menú y niveles 1 a 4) en la sección 6.1 o en la 11.8, y corregir la lista:

| El informe dice | En el proyecto |
|---|---|
| `Scene_MainMenu` (escena del vestuario) | `Main_Scene`: el menú está dentro de la misma arena del ring |
| `Scene_Ring_Level1` a `Level4` | Existen y son jugables |
| `Scene_Ring_Level5`, `Scene_Results` | Pendientes (entregable 5) |

## Otros datos del informe que no coinciden con el proyecto

Conviene corregirlos antes de la entrega final:

- **Unity 2022 LTS (secciones 5.1, 6.1, 8.1):** el proyecto está en Unity 6000.4.7f1. La sección 10 ya lo dice.
- **URP (secciones 5.1 y 8.1):** el proyecto usa el pipeline integrado (Built-in), con materiales Standard. `GraphicsSettings.defaultRenderPipeline` está vacío.
- **XR Interaction Toolkit y HandGrabbable (secciones 4.3, 6.3 y 6.4):** no están en el proyecto. Los guantes son hijos de los anchors de las manos del `OVRCameraRig` (`GuantesBuilder`), y la interacción del menú usa `OVRInputModule` y `OVRRaycaster` con un puntero láser.
- **RF-17, selección "mediante contacto de los guantes":** el menú se usa con puntero láser y gatillo, como ya indica la trazabilidad de la entrega 3.
- **Hand Grab Recorder (sección 6.3) como componente del SDK:** lo implementado es un grabador propio (`GrabadoraGuardia`), descrito en 11.5.
