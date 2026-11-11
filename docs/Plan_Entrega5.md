# Plan de la entrega 5 (semana 14): versión final, 100 %

> Hecho a partir del cronograma del informe (secciones 8.1 a 8.3), de `docs/Entrega4.md` y del estado del repositorio al 2026-11-11. El entregable 6 (semana 15) es solo la presentación: informe final, demo y video, así que todo lo que falte del juego debe quedar aquí.

## Qué pide el informe

| Fuente | Entregable 5 |
|---|---|
| Actividades (8.1) | IA del nivel 5 (Iron Mike Tyson). `Scene_Results` con cinturón y estadísticas. Opciones de volumen y háptica. Optimización a 72-90 FPS y háptica menor a 15 ms. Pruebas HEART y corrección de errores. |
| Resultado verificable (8.1) | Versión final con los 5 niveles y resultados de las pruebas de usabilidad. |
| Gantt (8.2), semana 14 | IA nivel 5 (RF-07, RF-08); resultados, cinturón y estadísticas (RF-18, RF-19); optimización (RNF-01, RNF-02); pruebas HEART y correcciones (RNF-03, RNF-05); informe final; registro de métricas (RF-21, segunda mitad). |
| Trazabilidad (8.3) | RF-07 y RF-08 (nivel 5), RF-18, RF-19 y RF-21 cierran aquí. |

## Punto de partida

- **Listo:** niveles 1 a 4, rondas, réferi, háptica, guardias grabadas, registro de sesión (`RegistroSesion`) y opciones de volumen y háptica.
- **Tyson:** el FBX riggeado está en `Art/Characters/MikeTyson/Source~/export/` (fuera de Unity). Aún no hay `Scene_Ring_Level5`, `PerfilOponente` ni cinturón.
- **Resultados:** los datos ya los calcula `RegistroSesion` (`CombateActual`, `AlTerminarCombate`); falta la pantalla.
- **Rendimiento:** la física corre a 50 Hz (`fixedDeltaTime` 0,02), por encima de los 15 ms de RNF-02. Las cifras de FPS del Editor no valen para el Quest.

## Bloques

| Bloque | Contenido | Requisitos | Prioridad |
|---|---|---|---|
| H | Iron Mike Tyson y nivel 5 | RF-07, RF-08, RF-09 | Alta |
| I | Escena de resultados y cinturón de campeón | RF-18, RF-19 | Alta |
| J | Optimización y latencia háptica (con visor) | RNF-01, RNF-02, RNF-08 | Alta |
| K | Pruebas HEART con usuarios | RNF-03, RNF-05, RF-21 | Alta |
| L | Correcciones y cierre de pendientes del 70 % | varios | Media |
| M | Informe final | (documentación) | Media |

### Bloque H: Iron Mike Tyson (nivel 5)

1. Importar `export/mike_rigged_1.78m_v1.fbx` a `Art/Characters/MikeTyson/` (Humanoid, mapeo manual si hace falta, como con Sandman), texturas y materiales Standard; reducir la malla con Blender si pasa de ~15k triángulos (`Tools/reducir_*.py` como referencia).
2. Animaciones: reutilizar las de Soda por avatar humanoide (como Sandman) con `AnimatorOverrideController`, y comprobar `ApoyoEnSuelo` para las botas o suelas.
3. `PerfilOponente` `MikeTyson.asset`, el más difícil del juego: más ritmo que Sandman (×1,5 o más), combos más largos (p. ej. gancho, gancho, uppercut, gancho), bloqueo y esquive altos, espera entre ataques de ~2 a 3 s. Salud a calibrar con el modelo de simulación de la entrega 4, para que supere a Sandman (6,65 de daño por segundo; 95 s en la pelea simulada).
4. `Scene_Ring_Level5.unity` copiando el nivel 4 (la ficha avisa: rig, Canvas y pausa se cambian en las cinco escenas). Añadirla a Build Settings, al botón del menú (`PlayLevel(5)`, `MainMenu.Levels`) y al desbloqueo (`nivelSuperado` hasta 5).
5. `ControlCombate`: ganar el nivel 5 carga `Scene_Results` (hoy el nivel 4 vuelve al menú). Quitar el mensaje que evita decir "campeón".
6. *Configurar oponente seleccionado* para hitboxes, `DefensaOponente` y `EsquiveOponente`; ajustar `retardoImpacto*` midiendo cada clip como en el bloque C.
7. Guardia: usar la guardia grabada con visor (ver bloque L).

**Terminado cuando:** se puede jugar del menú al nivel 5 en una partida continua, y Tyson es medible y claramente más difícil que Sandman.

### Bloque I: resultados y cinturón (RF-18, RF-19)

1. `Scene_Results.unity` en la misma arena, sin menús planos: estadísticas en un Canvas World Space (misma pila `OVRInputModule`/`OVRRaycaster`).
2. Datos al terminar cada combate (RF-18): precisión de golpes, golpes bloqueados y tiempo total, leídos de `RegistroSesion.Instancia.CombateActual`. Mostrarlos también tras cada victoria o derrota de los niveles 1 a 4, antes de volver al menú (tal como pide el requisito, "al terminar cada combate").
3. Cinturón de campeón (RF-19): modelo `Championship Belt` (Sketchfab, licencia Creative Commons a verificar), con peso en el Quest (materiales Standard, texturas ≤ 2048 px) y una animación sencilla (giro y brillo). Solo al ganar el nivel 5; el texto de resolución del arco narrativo coincide con el informe.
4. Botones: volver al menú y repetir el nivel, y reintento cuando se pierde.
5. Capturas para el informe.

**Terminado cuando:** al ganar a Tyson se ve el cinturón y las tres estadísticas, y tras cualquier combate se muestran precisión, bloqueos y tiempo.

### Bloque J: rendimiento y latencia (RNF-01, RNF-02, RNF-08)

Requiere visor y *Build And Run* en Android; sin eso no se puede cerrar.

1. **Primera compilación para el Quest** del proyecto completo (hasta ahora solo se compilaron los scripts) y revisar errores de plataforma: shaders, tamaño de texturas, memoria.
2. **FPS:** medir con `RegistroSesion` (`fpsMedio`, `fotogramasLentos`) en cada nivel. Meta: menos del 5 % de fotogramas bajo 72 FPS. Si no se cumple, por orden: bajar texturas y calidad en Android, reducir mallas (el réferi ya está en 20k, Sandman 14k), fijar la resolución de render, revisar luces y sombras de la arena de MarpaStudio, agrupar draw calls y revisar el coste de los `SkinnedMeshRenderer`.
3. **Latencia háptica (RNF-02):** bajar `fixedDeltaTime` a 1/72 s (o 1/90) y medir desde la detección de colisión hasta la llamada a `Haptica`. Documentar el método y el resultado.
4. **Sesión de 30 minutos (RNF-08):** dejar una sesión continua pasando por varios combates sin cierres ni pérdida de seguimiento, mirando el log de `RegistroSesion`.
5. Si se ve que Meta Quest 3 funciona igual (RNF-09), anotarlo.

**Terminado cuando:** hay una tabla con los FPS por nivel y la latencia háptica medida en el visor, y la sesión larga no se cae.

### Bloque K: pruebas HEART (RNF-03, RNF-05)

1. Preparar el protocolo: 5 a 8 usuarios novatos, sin explicarles el control (RNF-03: guardia y primer golpe válido en 2 minutos; 80 % de éxito). Cronometrar la adopción.
2. Instrumentos del informe 3.6: cuestionario Likert 1-5 para Happiness y cinetosis (RNF-05: promedio mínimo 4), observación (Adoption), `combates.csv` y los JSON de sesión (Engagement y Task Success), y la pregunta de si volverían a jugar (Retention).
3. Cada tester usa su sesión con `RegistroSesion`; sacar los archivos con `adb pull` y juntar los CSV en una hoja.
4. Observaciones que deben quedar escritas: si se entiende que hay que pulsar A, B, X o Y en la cuenta, si el roce háptico del esquive se nota y si los combos son defendibles.
5. Priorizar las correcciones que salgan y aplicarlas en el bloque L.

**Terminado cuando:** hay resultados por dimensión HEART y una lista priorizada de problemas.

### Bloque L: correcciones y pendientes heredados

- Asignar a los cuatro perfiles (y al de Tyson) una guardia grabada de verdad (*GrabadoraGuardia* en `Main_Scene`) en lugar de `Guardia_Provisional`; aplicar las rotaciones de mano si salen bien calibradas.
- Revisar con el visor lo que el 70 % dejó anotado: umbral de golpe lanzado (1,5 m/s) para la precisión, altura del barrido de Sandman, esquive decidido al contacto (¿conviene la anticipación?) y pies resbalando en los ganchos de Soda.
- Sustituir los materiales lisos de Soda si aparecen sus texturas.
- Voces del réferi: si no se entienden en el visor, grabar las diez y sobrescribir `Voz_Cuenta_01..10.wav`.
- Pausa y salida: comprobar la pausa del Guardián y del botón Menú en el Quest.
- Cualquier problema de las pruebas HEART.
- Limpiar el repositorio: los pendientes de Git LFS y los archivos fuera del repositorio a propósito (`hola.pdf`, PDF anotado, cambios de `Bangers SDF.asset`).

### Bloque M: informe final

1. Sección 12 (Entregable 5) en `docs/informe/` con el mismo formato: Tyson, resultados y cinturón, rendimiento, HEART y verificación. Dejar los números reales medidos.
2. Actualizar la trazabilidad (8.3) y el Gantt al 100 %.
3. Preparar la comparación con los bocetos para el entregable 6: las figuras 3 y 4 frente a las capturas reales.
4. Actualizar `CLAUDE.md` y los documentos de `docs/`.

## Orden de trabajo

| Orden | Paso | Con visor |
|---|---|---|
| 1 | H: Tyson y el nivel 5 | No |
| 2 | I: resultados y cinturón | No |
| 3 | Primera compilación Android y *Build And Run* | Sí |
| 4 | J: FPS, latencia háptica y sesión de 30 minutos | Sí |
| 5 | L: guardia real y correcciones del visor | Sí |
| 6 | K: pruebas HEART con usuarios | Sí |
| 7 | L: correcciones de las pruebas | Sí |
| 8 | M: informe final | No |

Los pasos 1 y 2 se pueden hacer sin visor y conviene terminarlos primero, para que el tiempo con visor se dedique a medir y a las pruebas con usuarios.

## Riesgos

| Riesgo | Mitigación |
|---|---|
| El modelo de Tyson no se importa o pesa demasiado | Reducir la malla en Blender; usar el mapeo de avatar manual como con Sandman. |
| No se llega a 72 FPS en Quest 2 | Plan de recortes por orden de coste (bloque J, paso 2); medir pronto, no al final. |
| Poco tiempo con visor y usuarios | Fijar la fecha de las pruebas desde ya; preparar protocolo, cuestionario y APK antes. |
| El cinturón es demasiado pesado o no tiene licencia | Modelo simple propio (malla baja con textura plana) como respaldo. |
| La latencia háptica no baja de 15 ms | Documentar la medición y la causa (el pipeline del SDK); el requisito se informa con su cifra real. |

## Fuera de alcance

Nuevos rivales o niveles, multijugador, Unity Analytics (el registro local basta), animaciones de caminar del réferi y de los rivales, y el seguimiento de piernas.
