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

## Sin probar: hace falta el visor

1. **Comodidad de la cámara lenta** en el visor (si molesta: campo `camaraLenta` de `EfectosGolpe` en cada pelea).
2. **Juego de pies a escala real:** que girar para seguir al rival sea cómodo dentro del Guardián; se ajusta con `arcoLateral` y `velocidadLateral`.
3. **Sonidos nuevos:** están sintetizados y no se han oído con auriculares; volumen del silbido en `Puno.volumenAire`.
4. **Rendimiento:** la mirada es IK del Animator y las partículas son como mucho 96 quads; no debería notarse, pero hay que medir FPS otra vez.
5. **Dificultad:** un rival que se mueve es algo más difícil de acertar. Los valores de daño y salud no se tocaron.

## Qué no cambió

Las reglas del combate, `RegistroSesion` (mismos campos), los protocolos de rendimiento y HEART, y el informe. El video de demostración (`Builds/Video/`) es anterior a estos cambios.
