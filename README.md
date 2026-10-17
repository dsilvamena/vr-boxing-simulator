# VR Boxing Simulator

Proyecto de Unity para un simulador de boxeo en realidad virtual.

## Requisitos

- Unity Hub y Unity **2022.3.62f3**.
- Git y Git LFS.
- Para compilar para Android: Android Build Support de esta versión de Unity.

## Abrir el proyecto

```bash
git lfs install
git clone https://github.com/dsilvamena/vr-boxing-simulator.git
cd vr-boxing-simulator
git lfs pull
```

En Unity Hub, agrega esta carpeta y abre el proyecto con Unity 2022.3.62f3. Espera a que termine la importación de assets y la resolución de paquetes.

La escena habilitada en Build Settings es `Assets/Scenes/BuildingBlocksScena.unity`.

## Control de versiones

Se incluyen `Assets/` y sus archivos `.meta`, `Packages/` y `ProjectSettings/`. Unity regenera las carpetas `Library/`, `Temp/` y los archivos del IDE, por lo que están excluidos.

Los archivos binarios grandes se gestionan con Git LFS mediante `.gitattributes`. Mantén Git LFS instalado al clonar y subir cambios.
