# Video de demostración

Video de respaldo de la demo (entregable 6): una partida completa grabada en el Editor desde los ojos del jugador, con el audio del juego y subtítulos que explican cada función. Lo graba y lo juega solo `Assets/Scripts/DirectorVideo.cs` (solo para el Editor: no está en ninguna escena ni entra en el APK).

El archivo final queda en `Builds/Video/demo_simulador_boxeo.mp4`, fuera del repositorio (`Builds/` está ignorada); los subtítulos también van aparte, en `Builds/Video/demo_simulador_boxeo.srt`.

## Qué muestra

| Tramo | Funciones |
|---|---|
| Menú | Menú dentro de la arena, puntero láser, opciones de volumen y vibración, selección de los cinco niveles |
| Nivel 1, Glass Joe | Campana, rival que se acerca dando pasos, bloqueo con la guardia, esquive con la cabeza, golpes por física (jab, cross, gancho y uppercut), dos knockdowns con la cuenta del réferi (que espera a que el rival esté en la lona), K.O., cartel de victoria y hoja de resultados |
| Nivel 2, Bald Bull | Barra de vida de prueba (botón Y), golpes recibidos (viñeta y latido), caída del jugador sin pulsar: cuenta, K.O. y hoja de derrota con REINTENTAR; reintento ganado |
| Nivel 3, Soda Popinski | Combo bloqueado, pausa con el botón Menú, pausa del Guardián al salir del área y vuelta, knockdown y victoria a los puntos (los jueces) al final del último round |
| Nivel 4, Mr. Sandman | El rival esquiva y bloquea, knockdown, fin del round (tres campanadas), descanso de 20 s (el rival vuelve a su esquina) y round 2, caída del jugador y *button mashing* para levantarse, K.O. |
| Nivel 5, Iron Mike Tyson | Combo de cuatro golpes bloqueado, dos knockdowns, K.O., "¡Eres el nuevo campeón!", hoja de campeón con el cinturón, vuelta al menú |

### Versión corta (niveles 1 y 5)

`DirectorVideo.Empezar(carpeta, corto: true)` graba solo la primera y la última pelea (unos 4 minutos), y es la que está en `Builds/Video/demo_simulador_boxeo_corto.mp4`:

| Tramo | Funciones |
|---|---|
| Menú | Láser, opciones, panel de niveles con los niveles 2–5 bloqueados |
| Nivel 1, Glass Joe | Instrucciones del réferi, bloqueo, esquive, los cuatro tipos de golpe por física, knockdown con la cuenta, pausa con el botón Menú, pausa del Guardián, fin del round (tres campanadas y «¡Tiempo!»), descanso con el rival en su esquina, round 2, caída del jugador y *button mashing*, otras dos caídas y K.O., hoja de resultados |
| Menú | Vuelta al menú; los niveles 2–4 se dan por ganados para saltar al campeón (el progreso real se restaura al final) |
| Nivel 5, Iron Mike Tyson | Combo de cuatro golpes bloqueado, dos knockdowns, K.O., hoja de campeón con el cinturón, vuelta al menú |

### Solo las peleas de los niveles 3 y 4

`DirectorVideo.EmpezarPeleas(carpeta)` entra desde el menú (con el nivel 3 desbloqueado) y graba solo los tramos de Soda Popinski y Mr. Sandman de la versión completa, con sus hojas de resultados (unos 2,5 minutos). Está en `Builds/Video/peleas_soda_sandman.mp4` (y `.srt`).

## Qué no es como en el visor

- **El jugador es simulado.** La cabeza y las manos se colocan por código cada fotograma y los golpes son trayectorias del guante que pasan por la física igual que en el visor. Los movimientos son más regulares que los de una persona.
- **Combates acortados.** Antes de cada golpe se baja la salud del rival hasta cerca de su siguiente caída, para que caiga con pocos golpes (cada caída la provoca siempre un golpe que se ve). Los primeros cuatro golpes del nivel 1 van sin acortar.
- **Pruebas internas.** La pausa del Guardián, el fin del round, la victoria a los puntos y las pulsaciones de la cuenta usan los menús de prueba de los componentes, porque en el Editor no hay Guardián ni botones físicos.
- **Imagen.** Es una cámara de un solo ojo de ~98° de campo horizontal, en 16:9; el visor ve con los dos ojos y otro campo. Grabado en el Editor (calidad *Ultra*), no en el Quest.
- **Sin vibración**, evidentemente: los subtítulos dicen cuándo vibra y cómo.

## Cómo volver a grabarlo

1. En Unity, abrir `Assets/Scenes/Main_Scene.unity` sin cambios pendientes y anotar `nivelSuperado` (el director lo restaura al final).
2. Entrar en Play con el Meta XR Simulator y, en la consola de `eval` o en un script de Editor:

   ```csharp
   DirectorVideo.Empezar("/ruta/a/una/carpeta/video");
   ```

   (o `DirectorVideo.Empezar(carpeta, true)` para la versión corta). La carpeta debe estar **fuera del proyecto** (son unos 3 GB de JPG). Graba en tiempo real: **el Editor tiene que estar al frente** todo el rato (en segundo plano va a 5 fps). La partida dura unos 7 minutos. `DirectorVideo.Instancia.Terminado` pasa a `true` al acabar.
3. Armar el MP4 con ffmpeg (los fotogramas llevan su duración en `lista.txt`, así que el audio queda sincronizado aunque el Editor haya ido irregular):

   ```bash
   cd /ruta/a/una/carpeta/video
   ffmpeg -f concat -safe 0 -i lista.txt -i audio.wav -vf "fps=30,format=yuv420p" \
          -c:v libx264 -preset medium -crf 21 -c:a aac -b:a 160k -shortest demo_simulador_boxeo.mp4
   ```

**Por qué en tiempo real:** lo ideal sería `Time.captureFramerate` (tiempo de juego fijo) con `AudioRenderer`, pero en este Editor `AudioRenderer` no devuelve muestras, así que el audio sale de la mezcla final del `AudioListener` (`GrabadorAudioVideo`, `OnAudioFilterRead`), que solo existe en tiempo real. Los subtítulos se dibujan dentro del juego (el ffmpeg de Homebrew no trae los filtros de texto).

## Video de revisión de animaciones

`Builds/Video/revision_animaciones.mp4` (unos 5 minutos, sin audio) muestra cada animación de los personajes con un rótulo que dice personaje, estado del Animator, clip y duración. Lo graba `Assets/Editor/VideoAnimaciones.cs` en dos partes, como fotogramas JPG a 30 fps en `Builds/Video/animaciones/`:

1. **Clips** (*Tools > Boxeo > Video de animaciones > 1. Grabar clips*, fuera de Play, sin cambios pendientes): en una escena neutra, todos los estados del controlador de cada rival (resueltos a través de su override), las poses del menú de Bald Bull, Mr. Sandman y Tyson, y los clips del réferi (de pie, vigilando, caminar de lado y de frente, paso lateral). Con `ApoyoEnSuelo` sube la cadera como en el juego. Al acabar vuelve a abrir `Main_Scene`.
2. **En juego** (*... > 2. Grabar en juego*, en Play con `Main_Scene`): los cinco rivales del menú vistos desde los ojos del jugador (pose y mirada de `CuerpoVivo`) y la pelea de Bald Bull: el réferi caminando al centro, desplazándose de lado en el round y caminando a contar un knockdown provocado con *Probar: contar al rival*; Bald Bull de perfil, mirando al jugador. Rellena la salud del jugador para que no lo tumben, usa `Time.captureFramerate` y restaura `nivelSuperado`.

Luego, en `Builds/Video/animaciones`:

```bash
for d in 1_clips 2_juego; do ffmpeg -y -framerate 30 -i $d/f_%06d.jpg -c:v libx264 -crf 20 -pix_fmt yuv420p $d.mp4; done
printf "file '1_clips.mp4'\nfile '2_juego.mp4'\n" > lista.txt
ffmpeg -y -f concat -safe 0 -i lista.txt -c copy ../revision_animaciones.mp4
```

El ffmpeg de Homebrew no trae `drawtext`, por eso los rótulos se dibujan en Unity (TextMeshPro colgado de la cámara de grabación).
