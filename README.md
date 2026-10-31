# VR Boxing Simulator

Proyecto de Unity para un simulador de boxeo en realidad virtual para Meta Quest.

## Requisitos

- Unity Hub y Unity **6000.4.7f1**.
- Git y Git LFS.
- Android Build Support de esta versión de Unity (la plataforma activa del proyecto es Android).
- Opcional: [Meta XR Simulator](https://developers.meta.com/horizon/documentation/unity/xrsim-intro/) para probar en el Editor sin visor.

## Tecnologías

- **Meta XR SDK** (`com.meta.xr.sdk.all`) sobre **OpenXR**.
- **TextMesh Pro / uGUI** para el menú.
- **glTFast** para importar los personajes `.glb`.

## Abrir el proyecto

```bash
git lfs install
git clone https://github.com/dsilvamena/vr-boxing-simulator.git
cd vr-boxing-simulator
git lfs pull
```

En Unity Hub, agrega esta carpeta y abre el proyecto con Unity 6000.4.7f1. Espera a que termine la importación de assets y la resolución de paquetes.

Las escenas en Build Settings son `Assets/Scenes/Main_Scene.unity` (menú principal) y las peleas `Scene_Ring_Level1` (Glass Joe), `Scene_Ring_Level2` (Bald Bull) y `Scene_Ring_Level3` (Soda Popinski). Cada nivel se desbloquea al ganar el anterior.

## Probar

- **En el Editor:** abre `Main_Scene` y pulsa Play. Con el Meta XR Simulator activado (*Meta > Meta XR Simulator*) puedes probar sin visor, aunque la plataforma activa sea Android.
- **En el Quest:** conecta el visor con el modo desarrollador activado y usa *File > Build Profiles > Android > Build And Run*.

### Controles

| Acción | Meta Quest | Meta XR Simulator |
|---|---|---|
| Hacer clic en el menú | Gatillo derecho o botón A | Clic izquierdo (con *Point + Click* activado) |
| Pausar / reanudar la pelea | Botón Menu (☰) del control izquierdo | Tecla `M` |
| Golpear | Mover el puño contra la cabeza o el torso del rival | — (usa los menús de prueba de abajo) |
| Bloquear | Ambos guantes frente a la cara | — |
| Esquivar | Sacar la cabeza de la línea del golpe | — |
| Levantarse tras un K.O. | Pulsar A/B/X/Y repetidamente | — |
| Mostrar las barras de salud (solo pruebas) | Botón Y | — |

En pausa, el menú muestra **Reanudar**, **Opciones** y **Volver al menú principal**. La pelea también se pausa sola si sales del área del Guardián o quitas el visor, y se reanuda al volver.

### Probar sin visor

En Play, clic derecho en el título de cada componente:

- `ControlCombate` → *Probar: ganar el combate*.
- `RecuperacionJugador` → *Probar: derribar al jugador*.
- `PruebaAtaques` (en el `OVRCameraRig`) → ataques que conectan, que se esquivan o que se bloquean.
- `DefensaOponente` (en el rival) → *Probar: subir la guardia*.
- `PausaGuardian` (en `PauseMenu`) → *Probar: salir del área segura* / *volver al área segura*.
- `MainMenu` (en el `Canvas` del menú) → desbloquear todos los niveles o reiniciar el progreso.

## Estructura

- `Assets/_Project/`: assets propios del juego (personajes, perfiles de los rivales en `Data/Oponentes/`, fuentes y audio).
- `Assets/Scenes/`: menú principal y los tres rings.
- `Assets/Scripts/`: scripts del juego (combate, IA de los rivales, salud del jugador, menús y audio).
- `Assets/Editor/`: herramientas del menú *Tools > Boxeo* (configurar un rival, poner guantes).
- `Tools/sintetizar_audio.py`: genera los efectos de `Assets/_Project/Audio/Efectos/` (golpes, bloqueo, latido, multitud).
- `docs/Entrega3.md`: estado de la entrega 3 (50 %), trazabilidad de requisitos y pruebas.
- `Assets/ThirdParty/MarpaStudio/`: pack del gimnasio.

## Control de versiones

Se incluyen `Assets/` y sus archivos `.meta`, `Packages/` y `ProjectSettings/`. Unity regenera las carpetas `Library/`, `Temp/` y los archivos del IDE, por lo que están excluidos.

Los archivos binarios grandes se gestionan con Git LFS mediante `.gitattributes`. Mantén Git LFS instalado al clonar y subir cambios.
