# Video de demostración

Video de respaldo de la demo (entregable 6): una partida completa grabada en el Editor desde los ojos del jugador, con el audio del juego y subtítulos que explican cada función. Lo graba y lo juega solo `Assets/Scripts/DirectorVideo.cs` (solo para el Editor: no está en ninguna escena ni entra en el APK).

El archivo final queda en `Builds/Video/demo_simulador_boxeo.mp4`, fuera del repositorio (`Builds/` está ignorada); los subtítulos también van aparte, en `Builds/Video/demo_simulador_boxeo.srt`.

## Qué muestra

| Tramo | Funciones |
|---|---|
| Menú | Menú dentro de la arena, puntero láser, opciones de volumen y vibración, selección de los cinco niveles |
| Nivel 1, Glass Joe | Campana, rival que se acerca dando pasos, bloqueo con la guardia, esquive con la cabeza, golpes por física (jab, cross, gancho y uppercut), dos knockdowns con la cuenta del réferi (que espera a que el rival esté en la lona), K.O., cartel de victoria y hoja de resultados |
| Nivel 2, Bald Bull | Barra de vida de prueba (botón Y), golpes recibidos (viñeta y latido), caída del jugador sin pulsar: cuenta, K.O. y hoja de derrota con REINTENTAR; reintento ganado |
| Nivel 3, Soda Popinski | Combo bloqueado, pausa con el botón Menú, pausa del Guardián al salir del área y vuelta, knockdown y victoria por salud al final del último round |
| Nivel 4, Mr. Sandman | El rival esquiva y bloquea, knockdown, fin del round (tres campanadas), descanso de 20 s (el rival vuelve a su esquina) y round 2, caída del jugador y *button mashing* para levantarse, K.O. |
| Nivel 5, Iron Mike Tyson | Combo de cuatro golpes bloqueado, dos knockdowns, K.O., "¡Eres el nuevo campeón!", hoja de campeón con el cinturón, vuelta al menú |

## Qué no es como en el visor

- **El jugador es simulado.** La cabeza y las manos se colocan por código cada fotograma y los golpes son trayectorias del guante que pasan por la física igual que en el visor. Los movimientos son más regulares que los de una persona.
- **Combates acortados.** Antes de cada golpe se baja la salud del rival hasta cerca de su siguiente caída, para que caiga con pocos golpes (cada caída la provoca siempre un golpe que se ve). Los primeros cuatro golpes del nivel 1 van sin acortar.
- **Pruebas internas.** La pausa del Guardián, el fin del round, la victoria por salud y las pulsaciones de la cuenta usan los menús de prueba de los componentes, porque en el Editor no hay Guardián ni botones físicos.
- **Imagen.** Es una cámara de un solo ojo de ~98° de campo horizontal, en 16:9; el visor ve con los dos ojos y otro campo. Grabado en el Editor (calidad *Ultra*), no en el Quest.
- **Sin vibración**, evidentemente: los subtítulos dicen cuándo vibra y cómo.

## Cómo volver a grabarlo

1. En Unity, abrir `Assets/Scenes/Main_Scene.unity` sin cambios pendientes y anotar `nivelSuperado` (el director lo restaura al final).
2. Entrar en Play con el Meta XR Simulator y, en la consola de `eval` o en un script de Editor:

   ```csharp
   DirectorVideo.Empezar("/ruta/a/una/carpeta/video");
   ```

   La carpeta debe estar **fuera del proyecto** (son unos 3 GB de JPG). Graba en tiempo real: **el Editor tiene que estar al frente** todo el rato (en segundo plano va a 5 fps). La partida dura unos 7 minutos. `DirectorVideo.Instancia.Terminado` pasa a `true` al acabar.
3. Armar el MP4 con ffmpeg (los fotogramas llevan su duración en `lista.txt`, así que el audio queda sincronizado aunque el Editor haya ido irregular):

   ```bash
   cd /ruta/a/una/carpeta/video
   ffmpeg -f concat -safe 0 -i lista.txt -i audio.wav -vf "fps=30,format=yuv420p" \
          -c:v libx264 -preset medium -crf 21 -c:a aac -b:a 160k -shortest demo_simulador_boxeo.mp4
   ```

**Por qué en tiempo real:** lo ideal sería `Time.captureFramerate` (tiempo de juego fijo) con `AudioRenderer`, pero en este Editor `AudioRenderer` no devuelve muestras, así que el audio sale de la mezcla final del `AudioListener` (`GrabadorAudioVideo`, `OnAudioFilterRead`), que solo existe en tiempo real. Los subtítulos se dibujan dentro del juego (el ffmpeg de Homebrew no trae los filtros de texto).
