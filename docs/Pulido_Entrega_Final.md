# Pulido para la entrega final: personajes, animación y sensación de combate (2026-11-13)

Rama `entrega-final/pulido-combate`, creada desde `entrega5/sin-visor`. **Sin merge ni push.**

Objetivo: que la pelea se vea y se sienta más real y fluida sin cambiar las reglas del combate (salud, daño, rounds, bloqueo y esquive siguen igual). Todo se hizo y se probó en el Editor (Play con el Meta XR Simulator); **nada de esto se ha visto todavía en el visor**.

## Qué cambia para quien juega

| Antes | Ahora | Componente |
|---|---|---|
| El rival miraba al frente o al suelo según el clip | Te mira a los ojos: la cabeza te sigue aunque te agaches o te muevas | `CuerpoVivo` (mirada) |
| Todos los golpes a la cabeza producían la misma reacción | Cada golpe sacude la cabeza o el torso **en la dirección real del puño**: un gancho le gira la cara, un recto se la echa atrás, uno al cuerpo lo dobla, y más cuanto más fuerte pegas | `CuerpoVivo` (impacto) |
| Un golpe bloqueado no movía al rival | Lo empuja un poco (30 % de la sacudida) y salta un destello pálido en los guantes | `CuerpoVivo`, `EfectosGolpe` |
| Al esquivar se deslizaba tieso | Inclina el torso hacia donde se aparta (hasta 24°) | `CuerpoVivo` (esquive) |
| Se veía igual con 100 % que con 5 % de salud | Al perder salud se encorva, baja la cabeza y jadea (respiración más amplia y rápida). Sin HUD, es la forma de leer cómo va el rival (RF-14) | `CuerpoVivo` (cansancio) |
| Esperaba plantado delante de ti | Se desplaza de lado a tu alrededor, cambiando de sentido, dentro de un arco de 35° a cada lado (solo tienes que girar un poco) | `AtaqueOponente` (juego de pies) |
| Los golpes no lo movían del sitio | Cada golpe lo echa atrás unos centímetros y vuelve (Glass Joe 14 cm, Tyson 5 cm) | `AtaqueOponente` (retroceso) |
| Nada al conectar, salvo el sonido y la vibración | Salpicadura de sudor en el punto de contacto, en la dirección del golpe | `EfectosGolpe` |
| La caída del rival pasaba a velocidad normal | Cámara lenta breve en cada caída (0,45 s) y más larga en el K.O. (1,1 s) | `EfectosGolpe` |
| Tus golpes al aire no sonaban | El guante silba al lanzar un golpe, conecte o no | `Puno` |
| Los ataques del rival llegaban en silencio | Resopla al lanzar cada golpe, antes del impacto: también sirve de aviso | `AtaqueOponente`, perfil |
| Todos los golpes conectados sonaban igual de fuerte | El volumen sigue la fuerza del golpe y el tono varía un poco | `Oponente.Sonar` |
| Cuando te tumbaba, se quedaba en guardia | Se burla de ti mientras el réferi te cuenta (Glass Joe no: no va con el personaje) | `AtaqueOponente` (burla) |
| Sandman y Tyson se agachaban al dar pasos, y a Sandman se le torcían los pies | Corregido (era un fallo de los pasos procedurales) | `PasosProcedurales` |
| En el menú había dos modelos estáticos antiguos y Glass Joe | Los cinco rivales reales, en fila por nivel, en guardia, respirando y mirándote | `MenuBuilder`, `CuerpoVivo` |

## Detalle técnico

### `CuerpoVivo` (nuevo, en el objeto del Animator)

Capa procedural que se suma a los clips: gira huesos en `LateUpdate`, después de la animación y la IK, así que no toca ningún clip ni el controlador.

- **Mirada:** `Animator.SetLookAtPosition` hacia la cámara principal (peso 0,75, torso 0,12, recorrido limitado), suavizada 0,12 s. Cede al 30 % durante los clips de reacción y se apaga en el piso.
- **Impacto:** dos muelles amortiguados (cabeza y torso; rigidez 110, amortiguación 11), en el espacio de la raíz. El eje del giro es el par que hace el golpe: brazo de palanca (del cuello o la cintura al punto de contacto) × dirección del puño. La amplitud va con `Golpe.intensidad` (36° la cabeza y 16° el torso con un golpe completo). Escucha `Oponente.AlRecibirGolpe` y `AlBloquear`.
- **Esquive:** lee `EsquiveOponente.Desplazamiento` (propiedad nueva) e inclina columna y pecho hacia ahí.
- **Cansancio:** `1 − SaludNormalizada`, suavizado. Encorva 8°, baja la cabeza 7° y la respiración pasa de 0,5° a 0,28 Hz a 2,6° a 0,95 Hz.
- Las hitboxes cuelgan de los huesos y siguen la sacudida.
- Sin `Oponente` (los rivales de adorno del menú) solo mira y respira, y desfasa su `Idle` para que los que comparten clip no se muevan a la vez.
- Menús contextuales en Play: *Probar: gancho a la cabeza*, *Probar: recto a la cabeza*.

### Juego de pies y retroceso (`AtaqueOponente`, `PerfilOponente`)

- `MoverPies` sustituye a `Acercarse`: sigue acercándose hasta `distanciaCombate` y, cuando ya está a distancia de pelea, se desplaza por la tangente a `velocidadLateral`. Cada 1,2–3,2 s elige sentido (o quedarse quieto, 30 %). No pasa de `arcoLateral` (35°) respecto a la línea jugador-rival del principio y da la vuelta si topa con las cuerdas. Solo en `Idle`, así que durante los ataques no se mueve. Los pasos los pone `PasosProcedurales`.
- Retroceso: un muelle con amortiguación crítica lo desplaza `retrocesoPorGolpe` metros (según la intensidad; la mitad si bloqueó) y lo devuelve en unos 0,4 s. Se aplica como diferencia entre fotogramas, igual que el esquive. **Lección:** la primera versión solo lo empujaba y dejaba que volviera caminando; como solo camina en `Idle`, bajo una serie de golpes se iba alejando (de 1,1 m a más de 1,5 m) y el jugador tendría que perseguirlo en su espacio real.
- Campos nuevos del perfil: `velocidadLateral`, `retrocesoPorGolpe`, `probabilidadBurla`, `sonidosAtaque`.

| Rival | `velocidadLateral` (m/s) | `retrocesoPorGolpe` (m) | Burla |
|---|---|---|---|
| Glass Joe | 0,12 | 0,14 | no |
| Bald Bull | 0,16 | 0,08 | sí |
| Soda Popinski | 0,20 | 0,07 | sí |
| Mr. Sandman | 0,26 | 0,07 | sí |
| Iron Mike Tyson | 0,36 | 0,05 | sí |

- Burla: estado `Burla` nuevo en `GlassJoe.controller` (clip `SodaPopinski@Burla.fbx`, que estaba importado y sin usar; vuelve a `Idle` al 90 %). 0,9 s después de tumbar al jugador, una vez por caída; si el jugador se levanta antes, vuelve a la guardia.

### `EfectosGolpe` (nuevo, junto al `Oponente`)

- Un `ParticleSystem` creado al empezar (96 partículas como mucho, sin luces, sombras ni colisiones). De 5 a 18 gotas por golpe según la intensidad (60 % si es al cuerpo), estiradas según su velocidad. Material `Art/Efectos/Particula_Suave.mat` (*Mobile/Particles/Alpha Blended*, textura de 64 px generada por la herramienta).
- Cámara lenta: `Time.timeScale` a 0,45 durante 0,45 s reales en un knockdown y a 0,28 durante 1,1 s en la caída definitiva, con 0,35 s de vuelta. La cabeza y las manos del jugador siguen a velocidad real. Si alguien cambia el tiempo mientras tanto (la pausa lo pone a 0), lo suelta sin tocarlo; al desactivarse devuelve el tiempo a 1. Se puede apagar con el campo `camaraLenta`.
- `Puno` mide ahora la velocidad con el tiempo real entre pasos de física (`fixedDeltaTime / timeScale`): en cámara lenta los pasos llegan más espaciados y un gesto normal habría medido como un golpe rapidísimo.

### Sonido

`Tools/sintetizar_audio.py` tiene dos sonidos nuevos: `aire` (ruido pasa banda cuyo tono sube y baja con el brazo: `Golpe_Aire_1..3.wav`) y `esfuerzo` (soplo con dos formantes y un fondo de voz grave: `Rival_Esfuerzo_1..3.wav`). Ahora acepta prefijos: `python3 Tools/sintetizar_audio.py Assets/_Project/Audio/Efectos Golpe_Aire Rival_` genera solo esos y no pisa grabaciones puestas sobre los `Voz_Cuenta`.

### Corrección de `PasosProcedurales`

Dos fallos que ya existían y que el juego de pies habría dejado a la vista todo el tiempo:

1. **El objetivo de IK de un pie no es el hueso.** Se le pasaba a la IK el giro y la posición del hueso; Unity define el objetivo con su propia orientación y algo desplazado, distinto en cada esqueleto. En los de Mixamo casi coinciden, pero a Mr. Sandman le volteaba el pie (la suela quedaba 10 cm *por encima* del tobillo). Ahora, con la IK apagada y el cuerpo quieto, se mide el desfase entre objetivo y hueso y se suma al clavar el pie.
2. **Choque con `ApoyoEnSuelo`.** La postura se guardaba con la subida de cadera ya aplicada; al dar pasos, `ApoyoEnSuelo` veía las suelas en su sitio, no subía la cadera y Sandman daba los pasos 24 cm agachado (Tyson, 6–14 cm). Ahora la postura se guarda a la altura del clip.

Medido en Play (Sandman, esquive): antes, suela a −1,04 m con el tobillo a −1,14 m y la cadera 24 cm baja; ahora, suela a −1,22 m (la lona) y cadera a su altura.

### Menú

*Tools > Boxeo > Poner rivales en el menú* quita `bald_bull` y `mr_sandman` (.glb sin esqueleto) de `Main_Scene` y coloca los prefabs con esqueleto de los cinco rivales en arco, en orden de nivel, con `CuerpoVivo`. Los `.glb` antiguos siguen en el proyecto pero ya no los usa ninguna escena.

### Herramientas

- *Tools > Boxeo > Poner pulido de combate* (`PulidoBuilder`): añade `CuerpoVivo` y `EfectosGolpe`, asigna el material y los sonidos, pone los valores de la tabla en los perfiles y crea el estado `Burla`. Se puede repetir; no cambia lo que ya está.
- *Tools > Boxeo > Poner rivales en el menú* (`MenuBuilder`).

## Pruebas (Editor, Play con Meta XR Simulator)

- Golpes reales por `Oponente.RecibirGolpe` en los niveles 1, 2 y 5: conectan, se bloquean y se esquivan como antes; 0 errores en la consola. Capturas revisadas a mano del gancho (la cara gira y sale la salpicadura), del bloqueo y de la mirada (con y sin `CuerpoVivo`).
- Cámara lenta: al derribar, `timeScale` 0,45 → 1 en menos de un segundo.
- Juego de pies: el rival recorre el arco y cambia de sentido; con el retroceso elástico la distancia se mantiene (1,15 m con Glass Joe bajo golpes seguidos).
- Burla: Soda pasa a `Burla` 1 s después del derribo del jugador y vuelve a `Idle`.
- Menú: los cinco rivales en guardia, sin errores.

## APK (`Builds/Android/`, fuera del repositorio)

Regenerados el 2026-11-13 con todo lo de esta rama (código del commit `e76dc0f`), sin errores de compilación:

| APK | Tamaño | Para qué |
|---|---|---|
| `vrboxing_dev.apk` | 193,6 MB | El de las pruebas (MSAA 4×) |
| `vrboxing_dev_sinmsaa.apk` | 193,7 MB | Comparar FPS sin MSAA |
| `vrboxing_release.apk` | 173,1 MB | Sin *Development* |

Son builds incrementales: para medir el tamaño con exactitud hay que compilar con `CleanBuildCache`.

## Sin probar: hace falta el visor

1. **Comodidad de la cámara lenta** en el visor (si molesta: campo `camaraLenta` de `EfectosGolpe` en cada pelea).
2. **Juego de pies a escala real:** que girar para seguir al rival sea cómodo dentro del Guardián; se ajusta con `arcoLateral` y `velocidadLateral`.
3. **Sonidos nuevos:** están sintetizados y no se han oído con auriculares; volumen del silbido en `Puno.volumenAire`.
4. **Rendimiento:** la mirada es IK del Animator y las partículas son como mucho 96 quads; no debería notarse, pero hay que medir FPS otra vez.
5. **Dificultad:** un rival que se mueve es algo más difícil de acertar. Los valores de daño y salud no se tocaron.

## Qué no cambió

Las reglas del combate, `RegistroSesion` (mismos campos), los protocolos de rendimiento y HEART, y el informe. El video de demostración (`Builds/Video/`) es anterior a estos cambios.

---

# Segunda pasada (2026-11-14): presentación, animaciones y reglamento

Misma rama, `entrega-final/pulido-combate`. **Sin merge ni push.** Objetivo: que el juego se vea y se comporte como un producto terminado: el menú, la arena, las reacciones a los golpes, el réferi y las reglas de un combate de boxeo de verdad. Todo se hizo con el Editor abierto, manejándolo con el CLI de Unity, y se probó en Play con el Meta XR Simulator; **nada de esto se ha visto todavía en el visor**.

## Qué cambia para quien juega

| Antes | Ahora | Dónde |
|---|---|---|
| En el Editor, Bald Bull, Mr. Sandman y Tyson se veían en T (solo animaban en Play) y tres rivales del menú compartían la misma guardia | Cada rival del menú tiene su pose: Glass Joe nervioso, Bald Bull choca los guantes, Soda en guardia, Sandman con los guantes en la cintura, Tyson balanceándose en su guardia cerrada. También quedan posados fuera de Play, en el menú y en las peleas | `MenuBuilder`, `Maniqui` |
| Arena plana y gris: todo en la sombra de una luz "de sol" tapada por el techo | Ring iluminado desde arriba con sombras de los púgiles en la lona, gradas en penumbra, luces de relleno cálidas por delante y azules por detrás del rival | `ArenaBuilder` |
| Lona gris, esquinas negras, cuerdas blancas | Lona clara con el emblema del juego, esquina roja (rival), azul (jugador) y dos neutrales blancas, cuerdas roja, blanca y azul | `ArenaBuilder` |
| Gradas vacías con el murmullo de la multitud sonando | 834 espectadores en los asientos; saltan y disparan las cámaras cuando alguien cae | `PublicoBuilder`, shader `Boxeo/Publico` |
| Un gancho de izquierda y uno de derecha producían la misma reacción; todo golpe al cuerpo, la misma | La cabeza gira hacia donde va el guante y el cuerpo la sigue; un golpe al costado lo dobla sobre ese lado y baja el codo a cubrirse | `ReaccionesBuilder`, `ReaccionAnimada` |
| Nada avisaba de que el rival estaba a punto de caer | Se tambalea, le cuelga la cabeza y se le bajan los guantes cuando le queda poco para la próxima caída | `CuerpoVivo` (tocado) |
| El rival se levantaba al llegar al 10 | Está de pie antes del 10 (cada rival en su número) y la cuenta sigue hasta 8, la cuenta de protección; el réferi ordena "¡Box!" para seguir | `Arbitro` |
| Tras tumbarte se burlaba encima de ti | Se retira a la esquina neutral y se burla desde allí | `AtaqueOponente` |
| En el descanso se quedaba en el centro | Camina a su esquina, la roja | `ControlRondas`, `AtaqueOponente` |
| Sin K.O. ganaba quien tuviera más salud | Deciden tres jueces con tarjetas de 10 puntos por round; la decisión puede ser unánime, mayoritaria o dividida y las tarjetas se ven en el cartel y en resultados | `ControlRondas` |
| El réferi solo contaba | Da la salida de cada round, manda a la esquina neutral, para el combate agitando los brazos y señala al ganador | `CuerpoArbitro` |
| No había forma de saber cuánto quedaba de round | Marcador luminoso colgado de la estructura de focos con round, reloj y nombres, y claqueta a falta de diez segundos | `MarcadorRing`, `ControlRondas` |
| Cuando ganaba, el rival se quedaba en guardia | Levanta los brazos y celebra | `ReaccionAnimada` (estado `Victoria`) |

## `Maniqui`: animaciones humanoides por código

No hay animador en el equipo ni clips nuevos de Mixamo, así que las poses y reacciones nuevas se generan en el Editor (`Assets/Editor/Maniqui.cs`):

1. Se instancia una copia oculta del personaje.
2. Se coloca en una pose: se muestrea un clip (su guardia) o se parte de una pose de pie, y se mueven huesos con giros en el espacio del personaje y con IK analítica de dos huesos para brazos (`Mano`) y piernas (`MoverCadera` vuelve a plantar los pies donde estaban, así que bajar la cadera dobla las rodillas).
3. `HumanPoseHandler` convierte esa pose en músculos (`Capturar`).
4. `EscribirClip` escribe las fotos como fotogramas clave de un clip humanoide, con tangentes suaves.

Como el clip queda en espacio de músculos, se retargetea solo a cualquier esqueleto Humanoid y se mezcla con los clips de Mixamo en el Animator. Las medidas se dan en "anchos de hombros" para que valgan en cuerpos distintos. Dos detalles que costaron: con todos los músculos a cero la cabeza de Bald Bull mira al techo (depende de cómo se definió su avatar), así que la pose de pie toma la cabeza de su guardia; y al regenerar, la guardia de referencia tiene que salir del controlador del prefab, no del que ya tiene puesto la escena.

Clips generados:

| Clip | Personajes | Uso |
|---|---|---|
| `Menu/Menu_BaldBull`, `Menu_MrSandman`, `Menu_MikeTyson` | uno cada uno | Poses del menú, en bucle, cada una en un controlador de un solo estado |
| `<Rival>/Animations/Generadas/<Rival>@Cabeza_Derecha`, `Cabeza_Izquierda`, `Cuerpo_Derecha`, `Cuerpo_Izquierda` | Glass Joe, Bald Bull, Soda (Sandman y Tyson usan los de Soda) | Reacciones según la dirección del golpe |
| `GlassJoe/Animations/Generadas/GlassJoe@Victoria` | todos (sin override) | Celebración al ganar |

Los estados nuevos están en `GlassJoe.controller`; los clips de Bull y Soda, en sus override controllers. `ReaccionAnimada` elige el estado lateral cuando al menos la mitad de la velocidad del puño va de lado en el espacio del rival (`umbralLateral`); de frente siguen los clips de siempre.

## Arena (`Tools > Boxeo > Vestir arena`)

**Iluminación.** La escena tenía 25 luces en tiempo real con sombra y, con la calidad *Medium* de Android (una luz por píxel), la que mandaba era la direccional, que el techo tapaba: todo quedaba en sombra y el mapa de sombras dibujaba más de mil objetos por fotograma. Ahora:

- La direccional es la luz del ring (casi cenital, cálida, la única por píxel y con sombra) y solo alumbra lo que no está en la capa nueva **`Arena`**, a la que pasa todo el edificio menos el ring. Ni lo ilumina ni entra en su mapa de sombras.
- Los focos del edificio quedan como relleno por vértice, fríos y sin sombra; la puntual del centro se apaga.
- Cuatro luces por vértice en las esquinas del ring (`LucesRing`).
- Ambiente en tres tonos, niebla oscura.
- Sombras de *Medium*: distancia 20 → 12 m y resolución *Low* → *Medium*, para que no se vean a bloques.

Esto coincide con el recorte 1 de la auditoría de `docs/Entrega5.md` (quitar sombras a las luces que no son la direccional), que ya no hace falta aplicar aparte: *Tools > Boxeo > Rendimiento* lo encontrará sin nada que cambiar. Los recortes 2 y 3 siguen igual.

**Ring.** La lona es un plano 4 mm por encima del piso original con una textura de 1024 px que pinta el propio Editor (el texto sale del atlas SDF de Bangers). Los materiales propios están en `Art/Arena/`; los de MarpaStudio no se tocan. De paso se devolvió a su esquina el cuarto pilar de la estructura de focos, que en la escena original estaba en mitad de un lado y hundido en el suelo.

**Público.** Los 1 012 asientos de la arena son planos de 1 m con la butaca pintada. `PublicoBuilder` sienta gente en el 82 %: una sola malla de 1 866 triángulos (una draw call) con una textura de 512 × 64 (5 KB) y un shader sin luces. El shader mece las siluetas y, cuando `AmbienteMultitud` ovaciona, las hace saltar y abre destellos de cámara (`_PublicoAnimo`, global). Para quitarlo basta con desactivar el objeto `Publico` de cada escena.

## Reglamento

- **Cuenta al rival.** Nadie se levanta "en el 10": eso es nocaut. `cuentaKnockdown` del perfil es ahora el número en el que ya está de pie (Glass Joe 9, Bald Bull 8, Soda 8, Sandman 7, Tyson 6). Empieza a incorporarse 2 s antes, la cuenta sigue hasta 8 como mínimo y hasta el "¡Box!" queda protegido y sin atacar. La caída queda en 8–9 s de cuenta más la espera de la esquina, parecido a los 10 s de antes.
- **Tres caídas.** La tercera caída del rival sigue parando el combate sin cuenta: es la regla de las tres caídas.
- **Esquinas neutrales.** Las da `LimiteRing.EsquinaNeutralLejana`. El jugador no puede ser obligado a ir (es su espacio real); el réferi se la señala.
- **Jueces.** En cada round, 10 puntos para quien más salud le quitó al otro (en fracción de su máximo) y 9 para el otro; 10-10 si la diferencia es menor de 1,5 %; un punto menos por caída. Tres jueces con sesgos de −2 %, 0 y +2 %. Gana quien tenga más jueces; el empate sigue siendo del jugador. **Esto cambia RF-11 respecto al informe**, que decía "gana quien tenga más salud": hay que actualizar esa frase. `ResultadoCombate` conserva `VictoriaPorSalud` / `DerrotaPorSalud` para no romper los registros ni `Tools/tabla_heart.py`; `RegistroSesion` añade `tarjetas` y `decision` al final.
- **Lo que no se cambió:** 3 rounds de 90 s con 20 s de descanso (un asalto de 3 min con el visor puesto cansa demasiado), el daño, la salud y las probabilidades de bloqueo y esquive.

## Pasos

Los pasos procedurales (`PasosProcedurales`) ya clavaban cada pie entre paso y paso; se comprobó de nuevo con Mr. Sandman, el esqueleto más delicado. Lo que cambia: caminando deprisa o un tramo largo (el rival a su esquina o a la neutral, el réferi cruzando el ring) la zancada se alarga y el pie sube más (`zancada`, hasta ×1,5 a 1 m/s), en vez de recorrer tres metros a pasitos de pelea. Medido en el nivel 4, de camino a la esquina (2,5 m): antes los pies hacían 1,6 y 1,1 m de ese recorrido a menos de 2 cm de la lona; ahora 0,6 y 0,9 m en 22 pasos. Por debajo de 0,35 m/s (el juego de pies de la pelea) todo sigue igual. Además, en el descanso el rival camina mirando hacia su esquina en vez de ir de espaldas.

## Herramientas nuevas (todas se pueden repetir)

| Menú | Qué hace |
|---|---|
| *Tools > Boxeo > Poner rivales en el menú* | Ahora también genera las tres poses y deja posados a los cinco |
| *Tools > Boxeo > Posar rivales en las peleas* | Deja a cada rival en su guardia dentro de las escenas de pelea |
| *Tools > Boxeo > Vestir arena* | Iluminación, capa `Arena`, lona, esquinas, cuerdas y público en las siete escenas |
| *Tools > Boxeo > Regenerar la lona del ring* / *Regenerar el público* | Rehacen la textura de la lona o la malla y la textura del público |
| *Tools > Boxeo > Crear reacciones a los golpes* | Genera los clips de reacción y `Victoria` y los monta en los controladores |
| *Tools > Boxeo > Poner reglamento* | Asigna las órdenes de voz y la claqueta, monta el marcador y fija los perfiles |

Sonidos nuevos (`python3 Tools/sintetizar_audio.py Assets/_Project/Audio/Efectos Voz_Orden Aviso_`): `Voz_Orden_Box`, `Voz_Orden_Esquina`, `Voz_Orden_Nocaut` y `Aviso_Diez_Segundos`.

## Pruebas (Editor, Play con Meta XR Simulator)

- Menú: los cinco animan, miran al jugador, consola sin errores.
- Reacciones: golpes por `Oponente.RecibirGolpe` a cámara lenta en los niveles 1 y 2, vistos desde una cámara externa (gancho a cada lado y golpe al costado).
- Reglamento (niveles 1, 3 y 5): salida con "¡Box!"; cuenta completa a Glass Joe ("¡Esquina!", 1 a 9, de pie en el 9, "¡Box!"); derribo del jugador con el rival yendo a la esquina neutral; nocaut del jugador ("¡Nocaut!"); descanso con Soda en (2,15; 2,15), su esquina; derrota por decisión unánime con las tarjetas en `Scene_Results`; Tyson celebrando con el réferi señalándolo.
- En las pruebas se usó un arnés que fija cabeza y manos y rellena la salud del jugador, así que las tarjetas de esas peleas no son representativas.

## APK (`Builds/Android/`, fuera del repositorio)

Regenerados el 2026-11-14 con todo lo de esta pasada, con builds incrementales y sin errores (el shader `Boxeo/Publico` compila para Android):

| APK | Tamaño | Notas |
|---|---|---|
| `vrboxing_dev.apk` | 198,0 MB | El de las pruebas (MSAA 4×). La primera build de esta pasada pesó 193,6 MB, igual que antes de ella: la lona, el público, los clips y las voces no llegan a medio MB. Los 4 MB de más de la segunda son huecos del empaquetado incremental, no contenido |
| `vrboxing_release.apk` | 169,1 MB | Sin *Development* |
| `vrboxing_dev_sinmsaa.apk` | 193,7 MB | **No regenerado**: sigue siendo el del 2026-11-13 (commit `e76dc0f`), sin nada de esta pasada |

## Sin probar: hace falta el visor

1. **Shader del público en multiview.** Lleva las macros de estéreo, pero el simulador renderiza en MultiPass: hay que confirmar que las siluetas salen en los dos ojos.
2. **FPS.** La iluminación nueva debería costar menos que la anterior (un mapa de sombras pequeño en vez de más de mil objetos), y el público es una draw call, pero hay que medirlo.
3. **Brillo.** Gradas en penumbra y lona clara: comprobar en las lentes que la lona no deslumbra y que el público se distingue.
4. **Marcador.** Que se lea levantando la vista desde el centro del ring.
5. **Voces nuevas** ("¡Box!", "¡Esquina!", "¡Nocaut!"): son del mismo sintetizador que los números; se pueden sustituir por grabaciones sobrescribiendo los WAV.
6. **Dificultad.** La cuenta de protección da al jugador unos segundos más de respiro tras cada derribo, y la decisión por jueces premia ganar rounds, no solo conservar salud.
