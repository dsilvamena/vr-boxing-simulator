# Protocolo de las pruebas HEART con usuarios (entrega 5, bloque K)

Guion para la sesión de usabilidad con personas que no han jugado nunca: la adopción del control sin instrucciones (RNF-03), la cinetosis (RNF-05) y las cinco dimensiones HEART de la sección 3.6 del informe. El juego guarda solo lo que necesita (`RegistroSesion`, RF-21, con el tiempo hasta la primera guardia y el primer golpe desde el bloque K); el equipo añade la observación y el cuestionario, y `Tools/tabla_heart.py` lo junta todo por participante. La preparación sin visor está en `docs/Entrega5.md`, bloque K.

Duración estimada: **unos 25 minutos por participante**, más 20 minutos de preparación y 10 de cierre. Con 6 participantes, unas 3 horas. Conviene hacerla **después** de la sesión de rendimiento (`docs/Protocolo_Rendimiento.md`): si allí apareció un problema que se corrige, las pruebas se hacen con el APK corregido.

## Requisitos e indicadores

| Dimensión / requisito | Qué se mide | Dónde | Meta |
|---|---|---|---|
| RNF-03 (Adoption) | Segundos desde la primera campana hasta la primera guardia y hasta el primer golpe conectado, sin instrucciones | `segundosHastaGuardia` y `segundosHastaGolpeConectado` de la sesión (JSON), con el cronómetro de respaldo | El 80 % de los novatos hace las dos cosas en 120 s o menos |
| RNF-05 (Happiness) | Ausencia de cinetosis | Cuestionario, ítems C1 a C3 (Likert 1 a 5, en positivo) | Media de 4 o más |
| Happiness | Satisfacción inmersiva | Cuestionario, ítems H1 a H4 | Sin meta en el informe; se informa la media |
| Engagement | Duración de la sesión, combates y reintentos | `segundosSesion`, `combatesJugados`, `reintento` (RF-21) | Sin meta; se informa |
| Retention (adaptada) | Intención de volver a jugar y continuación voluntaria | Cuestionario R1 y R2; hoja de observación (`siguioJugando`) | Sin meta; se informa |
| Task Success | Ataques bloqueados, precisión, cuentas superadas, victoria en el nivel 1 | `ataquesBloqueados`, `precision`, `cuentasSuperadas`, `resultado` (RF-21) | Sin meta; se informa |

### Definición exacta de la adopción (RNF-03)

- **Reloj:** el tiempo de juego desde la **primera campana** de la sesión (la del round 1 del primer combate), con el tiempo escalado del juego: las pausas no cuentan, los descansos entre rounds sí. Es el mismo reloj que `segundosCombate`. Si el participante abandona o pierde y vuelve a empezar, se suma el tiempo de los combates anteriores desde su campana (por eso se usa el valor de la **sesión**, no el del combate). El tiempo en los menús y en la hoja de resultados no cuenta.
- **Guardia:** las dos manos a menos de 0,4 m de los ojos y por delante de la cara (`SaludJugador.EnGuardia`, la misma condición que bloquea un ataque), **mantenidas al menos 0,5 s**. El valor es el momento en que subió las manos, no el final del medio segundo. Si ya estaba en guardia al sonar la campana, vale 0.
- **Golpe válido:** el primer golpe que **conecta** al rival (`Oponente.AlRecibirGolpe`: ni bloqueado ni esquivado, con la velocidad mínima de `Puno`). Un golpe que el rival bloquea no cuenta, aunque haya sido un golpe bien lanzado; si eso pasa a menudo, se ve en la columna del cronómetro.
- **Por combate** también quedan los dos tiempos (desde la campana de ese combate), en el JSON y en las dos últimas columnas de `combates.csv`. Valen −1 si no ocurrió.
- **Cumple** quien hace las dos cosas en 120 s o menos. RNF-03 se cumple si lo logra al menos el 80 % de los **novatos**: 4 de 5, 5 de 6, 6 de 7 o 7 de 8.

### Adaptación de Retention

La tabla HEART del informe mide Retention como "número de sesiones en segunda y tercera semana" con un registro de uso por usuario. No se puede medir en la semana 14: los participantes no tienen visor propio y la entrega es esa misma semana. Se sustituye por dos indicadores de **intención**, que hay que presentar como tales en el informe:

1. **Intención de volver:** R1, "Me gustaría volver a jugar" (y R2, usarlo para hacer ejercicio la próxima semana). Se informa la media y cuántos marcan 4 o 5.
2. **Continuación voluntaria:** al terminar la parte guiada se le dice "Ya puedes quitarte el visor, o jugar un combate más si quieres" y se anota si sigue (`siguioJugando`). Es una conducta observada, no una opinión.

El registro de uso por usuario ya existe (`RegistroSesion` guarda cada sesión), así que si algún participante vuelve a jugar con el mismo visor en las semanas siguientes se puede contar de verdad.

## Participantes

- **De 5 a 8 personas novatas**: han usado un visor de RV **3 veces o menos** y nunca han jugado a un juego de boxeo o de fitness en RV. Que no sean del equipo ni hayan visto el juego. Si viene alguien con más experiencia, puede jugar, pero se marca `novato` = no y no cuenta para RNF-03 (sí para el resto).
- **No deben participar** personas con epilepsia o antecedentes de convulsiones, embarazo, lesiones recientes de hombro, brazo o cuello, problemas cardíacos o que se mareen con facilidad en vehículos (las advertencias de salud de Meta). Se pregunta antes, sin anotar el motivo.
- **Identificación:** cada persona recibe un código por orden de llegada (P01, P02...). **No se escribe el nombre en ninguna hoja ni archivo**, ni hay lista que una código y nombre. El consentimiento es verbal (abajo) y se marca en la hoja de observación.

## Roles

| Rol | Quién | Qué hace |
|---|---|---|
| **Guía** | Una persona del equipo | Recibe al participante, lee el consentimiento, ajusta el visor, dice solo las frases del guion, **vigila la seguridad** (se queda junto al área de juego, atento a paredes y muebles) y para la prueba si hace falta |
| **Anotador** | Otra persona | Mira la transmisión del visor, lleva el **cronómetro** y la hoja de observación, y al final saca el id de la sesión con `adb` |
| Técnico (opcional) | La tercera persona | Prepara el visor entre participantes (cerrar y abrir el juego, batería, limpieza). Si no hay, lo hace el anotador |

Guía y anotador no hablan entre ellos de lo que hace el participante mientras juega.

## Material

- Meta Quest 2 cargado y conectado al cargador entre participantes; controles con pilas nuevas y correas de muñeca.
- **El APK de desarrollo generado desde la rama `entrega5/sin-visor`** (`Builds/Android/vrboxing_dev.apk`, 2026-11-11), que ya guarda los tiempos de adopción. El APK anterior no los tiene: con él, el script usa solo el cronómetro. El de desarrollo deja editar los PlayerPrefs con `run-as`.
- Cable USB-C de datos y el Mac con `adb` (ver `docs/Protocolo_Rendimiento.md`, "Material").
- **Transmisión** de lo que ve el participante: app Meta Horizon en un teléfono (*Transmitir*) o la web de transmisión de Meta en un portátil. Solo para mirar: **no se graba**.
- Un cronómetro con vueltas (el del teléfono del anotador).
- Hojas impresas: `docs/heart/Hoja_observacion.md` (una por participante) y `docs/heart/Cuestionario_post_prueba.md` (una por participante), bolígrafos. O el cuestionario en un formulario con las mismas preguntas.
- Toallitas para la espuma facial y los controles, y una funda lavable si hay. Agua. Una silla para el cuestionario.
- Un área despejada de **al menos 2 × 2 m** (el juego pide 1,5 × 1,5 m, RNF-06) con el Guardián en **escala de habitación**.

## Preparación (una vez, antes del primer participante), unos 20 minutos

1. **Instalar** el APK si no está ya (`adb install -r Builds/Android/vrboxing_dev.apk`). Antes, sacar los datos que haya del visor (`docs/Protocolo_Rendimiento.md`, paso 6): no se borran, pero así quedan separados.
2. **Opciones del juego:** volumen alto y háptica al máximo (*OPCIONES* del menú). Se guardan y valen para todos.
3. **Niveles abiertos:** la parte C usa el nivel 4. Si en el visor aún no se ha ganado el nivel 3, abrirlo como en `docs/Protocolo_Rendimiento.md`, paso 3 (con `nivelSuperado` = 3; vale cualquier valor de 3 a 5). Los participantes juegan desde el nivel 1 aunque haya más abiertos: la guía les dice cuál elegir.
4. **Ensayo:** una persona del equipo juega un combate del nivel 1, se quita el visor, y el anotador comprueba que el JSON nuevo trae los campos:

   ```bash
   adb shell ls -t /sdcard/Android/data/com.utec.vrhola/files/Sesiones | head -3
   adb shell cat /sdcard/Android/data/com.utec.vrhola/files/Sesiones/sesion_<id>.json | grep -E "segundosHasta"
   ```

   Si no aparecen `segundosHastaGuardia` y `segundosHastaGolpeConectado`, el APK instalado es el anterior: reinstalar el correcto.
5. **Transmisión** funcionando en el teléfono o portátil del anotador, y el sonido del visor audible (la campana es la señal para el cronómetro).
6. Hojas numeradas P01, P02... y el área despejada.

## Con cada participante, unos 25 minutos

### 1. Bienvenida y consentimiento (3 min)

La guía lee:

> "Gracias por venir. Estamos probando un juego de boxeo en realidad virtual para un curso de la universidad. Vas a jugar unos diez o doce minutos y luego te haremos unas preguntas. Lo que evaluamos es el juego, no a ti: no hay forma de hacerlo mal. No guardamos tu nombre: serás el participante P__. El juego guarda datos de la partida, como el tiempo o los golpes, sin nada que te identifique. Puede que te canses o que te marees un poco: si te sientes mal, dilo y paramos en ese momento, sin dar explicaciones. ¿Tienes epilepsia, alguna lesión en brazos, hombros o cuello, o te mareas con facilidad? [si responde que sí a algo, se le agradece y no juega] ¿Aceptas participar?"

Marcar el consentimiento en la hoja. Preguntar y anotar la experiencia previa con RV, si ha jugado boxeo o fitness en RV, la experiencia en boxeo y la mano dominante. Con eso se marca si es novato.

### 2. Preparar el visor (2 min)

1. El técnico cierra y vuelve a abrir el juego, para que cada participante tenga **su propia sesión** (un `sesion_<id>.json` nuevo):

   ```bash
   adb shell am force-stop com.utec.vrhola
   adb shell am start -n com.utec.vrhola/com.unity3d.player.UnityPlayerGameActivity
   ```

2. Quitar relojes y pulseras; poner las correas de muñeca. Ajustar la correa del visor y la distancia entre lentes hasta que vea nítido. Con gafas, el separador.

### 3. Menú (máx. 2 min)

El menú no es parte de RNF-03 (es RF-17), así que sí se explica. La guía dice:

> "Con el control derecho apuntas como con un láser y eliges con el gatillo. Entra en JUGAR y elige NIVEL 1."

Si no lo logra en un minuto, se le ayuda y se anota.

### 4. Parte A: juego libre en el nivel 1 (RNF-03)

La guía dice **solo**:

> "Estás en un ring. Juega como quieras. Puedes moverte dentro del área. Si te mareas o quieres parar, dilo."

El anotador **pone el cronómetro al oír la primera campana** (suena 5 s después de entrar) y marca una vuelta cuando el participante sube las dos manos delante de la cara y las mantiene, y otra cuando su primer golpe toca al rival y lo hace reaccionar. Si el juego se pausa (por ejemplo, al salir del Guardián), pausa también el cronómetro. Es el respaldo de los campos del registro, y sirve para ver si la definición del juego coincide con lo que ve una persona.

**Qué no se dice** (es lo que mide RNF-03), aunque pregunte:

| No se dice | Si pregunta... |
|---|---|
| Cómo golpear, bloquear o esquivar | "Haz lo que harías en un ring." |
| Que en la lona hay que pulsar A, B, X o Y para levantarse | "Haz lo que creas." (el cartel del réferi lo dice) |
| Que el botón Menú pausa | "Si quieres parar, dímelo." |
| Cómo va de salud o cuánto le queda al round | "Lo que veas y oigas es todo lo que hay." |
| Que el rival puede esquivar o bloquear | — |

Se anota cada pregunta en la hoja (`pidioAyuda`). Si a los **3 minutos** de juego no ha subido la guardia o no ha conectado ningún golpe, ya no cumple RNF-03; la guía puede decir una sola frase de ayuda ("prueba a pegarle") para que la prueba siga, y se anota.

Se juega hasta el final del combate (contra Glass Joe suele ser de 1 a 5 minutos). Al terminar sale la hoja de resultados.

### 5. Parte B: seguir jugando (unos 5 min)

- Si ganó: "Elige SIGUIENTE NIVEL." (nivel 2, Bald Bull, que ataca más y es más probable que lo tumbe: así se observa si entiende la cuenta del réferi).
- Si perdió: "Elige REINTENTAR."

Sin más instrucciones. La guía corta a los 5 minutos de esta parte, o antes si termina el combate.

### 6. Continuación voluntaria (Retention)

Al terminar el combate de la parte B (o al cortar), la guía dice:

> "Ya puedes quitarte el visor, o jugar un combate más si quieres."

El anotador marca `siguioJugando`. Si sigue, pasa a la parte C. Si no, se le puede pedir igualmente la parte C ("¿nos ayudas con dos minutos más contra otro rival?"); la respuesta a la oferta ya quedó anotada.

### 7. Parte C: combos y esquive (unos 3 min, si hay tiempo)

> "Pulsa MENÚ, entra en JUGAR y elige NIVEL 4."

Mr. Sandman lanza combos de tres golpes y esquiva el 45 % de los golpes. El anotador observa si se defiende de los combos (bloquea, se aparta o se los come todos) y cuántas veces el rival esquiva. La guía corta a los 3 minutos o al primer K.O.

### 8. Quitar el visor y sacar el id de la sesión

Al quitarse el visor el juego se pausa y escribe el JSON. El anotador ejecuta:

```bash
adb shell ls -t /sdcard/Android/data/com.utec.vrhola/files/Sesiones | head -3
```

y copia el id del `sesion_<id>.json` más reciente en la hoja (`sesion`). Si no aparece uno nuevo, volver a ponerse el visor un momento con el juego en el menú y repetir.

### 9. Cuestionario y preguntas de cierre (7 min)

Sentado, el participante rellena el cuestionario solo (la guía no lo mira mientras). Después, la guía hace las cuatro preguntas de la sección 4 de la hoja de observación, sin sugerir la respuesta: si notó algo en el control cuando el rival se apartaba (el roce háptico del esquive), cómo supo qué hacer en la lona y si pudo defenderse de las combinaciones.

### 10. Entre participantes (3 min)

Limpiar la espuma y los controles, comprobar la batería (`adb shell dumpsys battery | grep level`; cargar si baja del 30 %) y volver al paso 1 con el siguiente.

## Observaciones que pide el plan

| Observación | Cómo se obtiene | Columna |
|---|---|---|
| ¿Se entiende que hay que pulsar A, B, X o Y en la cuenta? | Si cae: si pulsa rápido sin ayuda (sí), si tarda o pulsa poco (parcial) o si no pulsa (no). Más la pregunta de cierre. El registro da `caidasJugador` y `cuentasSuperadas` | `entendioPulsar` |
| ¿Se nota el roce háptico del esquive? | Solo en la parte C (Sandman esquiva). Pregunta de cierre sin decir qué vibración era | `notoRoceEsquive` |
| ¿Los combos se pueden defender? | Parte C (y el nivel 3 si llega). Observación; el registro da `ataquesBloqueados` y `ataquesEsquivados` del nivel 4 | `combosDefendibles` |

Si nadie cae en las partes A y B, la observación de la cuenta queda sin datos: se dice así en el informe, sin forzar caídas.

## Sacar los datos y hacer las tablas

Al terminar el día:

```bash
mkdir -p datos_heart
adb pull /sdcard/Android/data/com.utec.vrhola/files/Sesiones datos_heart/
```

Pasar las hojas de observación a `datos_heart/observacion.csv` y los cuestionarios a `datos_heart/cuestionario.csv`, copiando las plantillas de `docs/heart/` (una fila por participante; coma o punto y coma como separador; `si`, `parcial`, `no` o vacío). Si se usó un formulario, exportar sus respuestas con las columnas de la plantilla. Después:

```bash
python3 Tools/tabla_heart.py datos_heart/Sesiones --observacion datos_heart/observacion.csv \
    --cuestionario datos_heart/cuestionario.csv --dispositivo Quest
```

El script avisa si un participante no tiene sesión, si su cuestionario no tiene hoja, o si el registro y el cronómetro difieren más de 10 s. Imprime en Markdown la tabla por participante, la tabla HEART, el veredicto de RNF-03 y RNF-05 y el resumen de observaciones, listo para `docs/Entrega5.md` y para el informe.

**`datos_heart/` no va al repositorio** (está fuera del control de versiones o se guarda en otro sitio): aunque no tenga nombres, tiene horas y comentarios de cada persona. En el repositorio quedan solo las tablas.

## Tablas para rellenar

### Sesión

| Dato | Valor |
|---|---|
| Fecha y lugar | |
| Visor, APK y commit | |
| Guía / anotador / técnico | |
| Participantes (novatos / no novatos) | |
| Hora de inicio y de fin | |
| Incidencias generales (batería, transmisión, interrupciones) | |

### Participantes

Las columnas salen de `Tools/tabla_heart.py`; esta tabla es para ir anotando a mano durante el día.

| Código | Novato | Sesión (`sesion_<id>`) | Guardia: crono (s) | Golpe: crono (s) | ≤ 120 s | Niveles jugados | ¿Siguió jugando? | Incidencias |
|---|---|---|---|---|---|---|---|---|
| P01 | | | | | | | | |
| P02 | | | | | | | | |
| P03 | | | | | | | | |
| P04 | | | | | | | | |
| P05 | | | | | | | | |
| P06 | | | | | | | | |
| P07 | | | | | | | | |
| P08 | | | | | | | | |

### Resultados HEART

Pegar la salida de `Tools/tabla_heart.py` ("Por dimensión HEART" y "Requisitos").

| Dimensión | Indicador | Resultado |
|---|---|---|
| Happiness | Satisfacción inmersiva (H1–H4) | |
| Happiness | Ausencia de cinetosis (C1–C3), RNF-05 ≥ 4 | |
| Engagement | Duración de sesión; combates y reintentos | |
| Adoption | Guardia y golpe en ≤ 120 s, RNF-03 ≥ 80 % | |
| Retention (adaptada) | R1 ≥ 4; continuación voluntaria | |
| Task Success | Bloqueos, precisión, cuentas superadas, victoria en el nivel 1 | |

### Problemas encontrados (para el bloque L)

Ordenar por gravedad (3 = impide jugar o marea; 2 = confunde o frustra; 1 = molestia) y por cuántos participantes lo sufrieron.

| # | Problema | Participantes | Gravedad | Propuesta de corrección |
|---|---|---|---|---|
| 1 | | | | |
| 2 | | | | |
| 3 | | | | |

## Si no se cumple

- **RNF-03:** mirar cuál de los dos falla. Si es la guardia, puede que la condición (0,4 m de los ojos) sea más estricta que la guardia natural de un novato: comparar con el cronómetro antes de cambiar nada. Si es el golpe, mirar `golpesLanzados` y `golpesBloqueadosPorRival` (pega pero el rival bloquea) frente a pocos golpes lanzados (no se le ocurre pegar). Las correcciones van al bloque L y se vuelven a probar con otros participantes.
- **RNF-05:** ver en qué momento y nivel empezó el malestar (hoja de observación). Candidatos: el movimiento del rival y del réferi alrededor del jugador, el esquive del rival, los FPS (cruzar con la sesión de rendimiento) y la duración. Se informa la cifra real.
- **Pocos participantes:** con menos de 5, los porcentajes no son fiables; se informa el número y se completa con otra sesión si hay tiempo.
