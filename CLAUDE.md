# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

VR boxing simulator for Meta Quest, built in **Unity 6000.4.7f1** (migrated from 2022.3). Active build target is **Android**; XR runs through **OpenXR** with the Meta XR SDK (`com.meta.xr.sdk.all`). The user communicates in Spanish; code comments and README are in Spanish.

## Driving the Editor (Unity CLI + Pipeline)

The project includes `com.unity.pipeline`, so a running Editor can be controlled from the terminal with the `unity` CLI. Prefer this over hand-editing `.unity` / `.prefab` / `.asset` YAML.

```bash
unity status                      # Editor must show state "ready"
unity pipeline list               # shows Pipeline/server reachability and Safe Mode
unity command                     # list command groups; `unity list` lists every command with descriptions
unity command console_status --caller plugin --skill unity-cli --result-only   # compile-failure flag + error/warning counts
unity command console --tail 50 --caller plugin --skill unity-cli --result-only
unity command eval --caller plugin --skill unity-cli --result-only 'return UnityEditor.EditorUserBuildSettings.activeBuildTarget;'
```

- Command parameters are passed as `--name value` (e.g. `add_scene_to_build --path Assets/Scenes/Main_Scene.unity --enabled true`).
- If the Pipeline server is unreachable while the Editor is running, the usual cause is C# compile errors: Unity won't reload the domain, so the Pipeline never loads. Fix the errors from the Editor log (`~/Library/Logs/Unity/Editor.log`, grep `error CS`) first.
- File changes made on disk are only picked up when the Unity window regains focus.

Testing: Play mode in the Editor with the **Meta XR Simulator** works even with Android as the target (Play mode uses the Standalone XR loader settings, which also have OpenXR enabled). Device builds go through *File > Build Profiles > Android > Build And Run*. There are no automated tests.

## Architecture

- **Scenes:** two scenes, both in Build Settings. `Assets/Scenes/Main_Scene.unity` holds the main menu (a World Space `Canvas`) inside the MarpaStudio boxing arena, with an `OVRCameraRig`, and plays the menu music from a looping `AudioSource` on `MenuMusic`. `Assets/Scenes/Scene_Ring_Level1.unity` started as a copy of it: same arena and Canvas, but only Glass Joe and no music.
- **Pause menu:** in `Scene_Ring_Level1`, `PauseMenu` (on its own GameObject, not the Canvas, so hiding the Canvas doesn't disable it) toggles the Canvas and laser on `OVRInput.Button.Start` (left controller Menu button, key `M` in the Meta XR Simulator) and sets `Time.timeScale` to 0/1. There, the Canvas buttons are rewired: `ButtonReanudar` → `PauseMenu.Hide`, `ButtonVolverMenu` → `PauseMenu.BackToMainMenu`.
- **Menu input:** VR pointer interaction uses the Meta Core legacy UI stack (`OVRInputModule` + `OVRRaycaster` on the canvas; the input module accepts the right index trigger as well as A, which is what the simulator's Point + Click left mouse button sends). `LaserCursor` derives from `OVRCursor` and draws the ray with a `LineRenderer`, moving a dot to the hit point.
- **`MainMenu`** switches between the main and options panels and stores settings in `PlayerPrefs` under the keys `"volumen"` (applied to `AudioListener.volume`) and `"haptica"`. Nothing reads `"haptica"` yet.
- **`PlayGame()`** loads `"Scene_Ring_Level1"`.
- **Hitting opponents:** `Puno` (kinematic, on each hand anchor) measures hand velocity relative to the `TrackingSpace`, so joystick locomotion and snap turns don't count as punches. On `OnTriggerEnter` with a `ZonaGolpe` (trigger hitbox: `Cabeza` or `Torso`), it sends a `Golpe` to the `Oponente`. `Oponente` counts hits instead of tracking health. Every `golpesPorCaida` hits (10) of any type he falls. The falls before `caidasParaDerrota` (3) are knockdowns and he gets up after `segundosKnockdown`. The last one is the defeat (`Derrotado`), and he stays down until `Reiniciar()`. Hits don't count while he is down. `Oponente` raises the events `AlRecibirGolpe`, `AlCaer` and `AlLevantarse`, and two components react to them:
  - `ReaccionAnimada` drives an Animator by state name. For Glass Joe: Jab, Cross and Lateral to the head → `Jab`; Uppercut to the head → `Uppercut_Face`; anything to the torso → `Uppercut_Stomach`; then `Knockdown` / `Defeat`. The controller's exit-time transitions bring the reactions and `Knockdown` back to `Stance`.
  - `ReaccionProcedural` is for the remaining `.glb` characters, which have no skeleton: it leans and pushes the model from the feet with a spring.

  Per-character values live in a `PerfilOponente` asset (`Assets/_Project/Data/Oponentes/`). *Tools > Boxeo > Configurar oponente seleccionado* sets up a character. With a Humanoid Animator, it adds `ReaccionAnimada` and builds each hitbox from the vertices skinned to the head and the spine bones, parented to those bones so the hitboxes follow the animation. Without one, it uses heights. Adjust the hitboxes by eye afterwards.
- **Glass Joe** (`Assets/_Project/Art/Characters/GlassJoe/`) is a Mixamo rig, 1.70 m in bind pose. `GlassJoe.fbx` holds the model, the Humanoid avatar and the `Stance` clip. `Animations/GlassJoe@<Clip>.fbx` contribute only clips: their avatar is copied from the model, they import no materials, and root motion is baked into the pose, with `applyRootMotion` off. `Animations/GlassJoe.controller` holds the states, and `Materials/` holds the Standard materials that the FBX remaps by name, each with its texture from `Textures/`. The FBX only references the textures by file name, so new versions are copied over the existing files to keep their `.meta` settings. Bald Bull and Mr Sandman are still `.glb` files in `Characters/`.
- **Punch types:** `Golpe.Clasificar` sorts each hit by the hand's direction relative to where the head faces, with a fixed orthodox stance. Mostly upward is `Uppercut`; straight forward is `Jab` with the left hand and `Cross` with the right; anything else is `Lateral` (hooks). Every type counts as one hit; Lateral gets a soft vibration. Each type has its own vibration pattern in `Puno`, scaled by the `"haptica"` setting. The procedural reaction's `intensidad` saturates at `rapidezGolpeCompleto` (2.5 m/s), so punching harder doesn't add anything. `Oponente` logs every hit to the console (`registrarGolpes`).
- **Gloves:** `Assets/_Project/Art/boxing-gloves/source/Boxing gloves.glb` contains both gloves in one skinned mesh with a squash & stretch rig and animations, none of which are used. *Tools > Boxeo > Poner guantes en las manos* splits the mesh by bone side into the static meshes `Guante_Izquierdo`/`Guante_Derecho.asset` and replaces each `Puno`'s sphere with a `Guante` child. That child is rolled ±90° so the thumb points up while holding a controller. The gloves need glTFast ≥ 6.20: 6.14 fails to import this skinned mesh (`SortAndNormalizeBoneWeightsJob`).
- **Asset layout:** game-owned assets live in `Assets/_Project/` (characters under `Art/Characters/`: one folder per rigged character, the `.glb` ones imported with glTFast; the Bangers TMP font, and audio under `Audio/`; long music clips use the *Streaming* load type). Third-party art lives in `Assets/ThirdParty/MarpaStudio/`, and the scenes use all of it.

## Repository conventions

- Large binaries (png, fbx, wav, zip, dll, …) are tracked with **Git LFS** via `.gitattributes`. On a new machine install Git LFS (`winget install GitHub.GitLFS` on Windows) and run `git lfs install` before cloning, or `git lfs pull` afterwards. Without it the clone contains only text pointers and the arena (textures, meshes) shows up broken or empty; if Unity was already opened, delete `Library/` and reopen.
- Meta Avatars SDK, the multiplayer packages (Netcode, Lobby, Multiplayer Services), Visual Scripting and Timeline were removed on purpose as unused. Don't reintroduce them without a reason. Stale `Oculus.AvatarSDK2*.csproj` files at the root are generated and git-ignored.
- The Meta Project Setup Tool items about the Platform SDK (Data Use Checkup, application ID) don't apply, because the game doesn't use the Platform SDK.
