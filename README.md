# VR Boxing Simulator

Proyecto de Unity para un simulador de boxeo en realidad virtual para Meta Quest.

## Requisitos

- Unity Hub y Unity **6000.3.23f1**.
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

En Unity Hub, agrega esta carpeta y abre el proyecto con Unity 6000.3.23f1. Espera a que termine la importación de assets y la resolución de paquetes.

La escena habilitada en Build Settings es `Assets/Scenes/Main_Scene.unity`.

## Probar

- **En el Editor:** abre `Main_Scene` y pulsa Play. Con el Meta XR Simulator activado (*Meta > Meta XR Simulator*) puedes probar sin visor, aunque la plataforma activa sea Android.
- **En el Quest:** conecta el visor con el modo desarrollador activado y usa *File > Build Profiles > Android > Build And Run*.

## Estructura

- `Assets/_Project/`: assets propios del juego (personajes y fuentes).
- `Assets/Scenes/`: escena principal.
- `Assets/Scripts/`: scripts del juego (`MainMenu`, `LaserCursor`).
- `Assets/ThirdParty/MarpaStudio/`: pack del gimnasio.

## Control de versiones

Se incluyen `Assets/` y sus archivos `.meta`, `Packages/` y `ProjectSettings/`. Unity regenera las carpetas `Library/`, `Temp/` y los archivos del IDE, por lo que están excluidos.

Los archivos binarios grandes se gestionan con Git LFS mediante `.gitattributes`. Mantén Git LFS instalado al clonar y subir cambios.
