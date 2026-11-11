using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Lo que se registra de un combate (RF-21). También es la base de la hoja de resultados del entregable 5 (RF-18).</summary>
[Serializable]
public class DatosCombate
{
    public int nivel;
    public string escena;
    public string rival;
    public string inicio;              // hora local, ISO 8601
    public bool reintento;             // ese nivel ya se había jugado en la sesión
    public string resultado = "Abandonado"; // ResultadoCombate, o Abandonado si se salió antes de terminar
    public int roundAlcanzado;
    public float segundosCombate;      // tiempo de juego desde el primer round (sin pausas)

    [Header("Golpes del jugador")]
    public int golpesLanzados;
    public int golpesConectados;
    public int golpesBloqueadosPorRival;
    public int golpesEsquivadosPorRival;
    public int jabs, crosses, uppercuts, laterales;
    public float precision;            // conectados / lanzados
    public float efectividad;          // conectados / golpes que llegaron al rival (conectados + bloqueados + esquivados)
    public int derribosAlRival;

    [Header("Ataques del rival")]
    public int ataquesRecibidos;
    public int ataquesConectados;
    public int ataquesBloqueados;
    public int ataquesEsquivados;
    public float porcentajeBloqueo;    // bloqueados / recibidos
    public int caidasJugador;
    public int cuentasSuperadas;

    [Header("Final")]
    public float saludFinalJugador;    // 0-1
    public float saludFinalRival;      // 0-1
    public int vibraciones;            // pulsos hápticos pedidos

    [Header("Rendimiento (RNF-01)")]
    public int fotogramas;
    public float fpsMedio;
    public float porcentajeFotogramasLentos; // fotogramas por debajo de fpsObjetivo

    [Header("Latencia háptica (RNF-02)")]
    public float pasoFisicaMs;               // Time.fixedDeltaTime con que se jugó
    public int muestrasLatencia;             // golpes del jugador que vibraron
    public float latenciaHapticaMediaMs;     // de Puno.OnTriggerEnter a la vuelta de OVRInput: solo lo que controla el código
    public float latenciaHapticaP95Ms;
    public float latenciaHapticaMaxMs;
    public int muestrasRetraso;              // golpes con estimación de retraso de detección
    public float retrasoDeteccionMedioMs;    // estimado: del contacto del guante a que lo detecta el paso de física
    public float retrasoDeteccionP95Ms;
    public float retrasoDeteccionMaxMs;

    [Header("Adopción (RNF-03)")]
    // Desde la primera campana del combate, en tiempo de juego sin pausas (el mismo reloj que segundosCombate, con los
    // descansos). -1 si no ocurrió en este combate.
    public float segundosHastaGuardia = -1f;         // primera vez que mantiene la guardia (SaludJugador.EnGuardia) 0,5 s seguidos; cuenta desde que la sube
    public float segundosHastaGolpeConectado = -1f;  // primer golpe que conecta al rival (ni bloqueado ni esquivado)
}

[Serializable]
public class DatosSesion
{
    public string id;
    public string inicio;
    public string fin;
    public float segundosSesion;
    public string dispositivo;
    public string version;
    public int combates;
    public int reintentos;
    public int victorias;
    public List<DatosCombate> combatesJugados = new List<DatosCombate>();
    // RNF-03 por sesión: lo mismo que en cada combate, pero en tiempo de combate acumulado desde la primera campana de la
    // sesión (suma el de los combates anteriores, desde su campana hasta su final). -1 si no ocurrió en toda la sesión.
    public float segundosHastaGuardia = -1f;
    public float segundosHastaGolpeConectado = -1f;
}

/// <summary>
/// Registro de la sesión (RF-21): duración, combates jugados y reintentados, precisión de golpes y golpes bloqueados, para
/// analizarlos después (métricas HEART de engagement y task success, y el tiempo hasta la primera guardia y el primer golpe
/// conectado para la adopción, RNF-03). Se crea solo al arrancar el juego y sobrevive a los
/// cambios de escena, así que no hay que ponerlo en ninguna escena. En cada escena con Oponente sigue el combate por los
/// eventos de los componentes, sin cambiar su lógica.
/// Escribe en Application.persistentDataPath/Sesiones: un JSON por sesión (sesion_aaaaMMdd_HHmmss.json, se reescribe al
/// terminar cada combate, al salir de la escena y al pausar o cerrar la app) y combates.csv, con una fila por combate de
/// todas las sesiones. En el Quest: /sdcard/Android/data/com.utec.vrhola/files/Sesiones (adb pull).
/// Las columnas nuevas se añaden siempre al final del CSV; si combates.csv ya existía con menos columnas, se reescribe solo
/// su cabecera, así las filas viejas siguen siendo válidas (les faltan los últimos campos).
/// </summary>
public class RegistroSesion : MonoBehaviour
{
    private const float FpsObjetivo = 72f; // RNF-01: Meta Quest 2
    private const string Carpeta = "Sesiones";
    private const float SegundosGuardiaSostenida = 0.5f; // RNF-03: menos que esto es pasar las manos por delante, no adoptar la guardia

    public static RegistroSesion Instancia { get; private set; }

    /// <summary>
    /// Combate en curso, o el último si ya terminó; null si aún no se ha jugado ninguno. Se conserva al salir de la pelea
    /// (ya cerrado) hasta que empieza otro: de aquí lee Scene_Results (PantallaResultados) los datos que muestra.
    /// </summary>
    public DatosCombate CombateActual => combate;
    public DatosSesion Sesion => sesion;
    public string RutaSesion { get; private set; }
    public string RutaCsv { get; private set; }

    /// <summary>Terminó un combate y ya está guardado.</summary>
    public event Action<DatosCombate> AlTerminarCombate;

    private DatosSesion sesion;
    private DatosCombate combate;
    private bool combateCerrado;
    private float inicioSesion;
    private float inicioCombate = -1f; // Time.time del primer round
    private readonly HashSet<int> nivelesJugados = new HashSet<int>();
    private int fotogramasLentos;
    private double sumaDelta;
    private readonly List<double> latencias = new List<double>(); // ms
    private readonly List<double> retrasos = new List<double>();  // ms
    private bool cabeceraRevisada;
    private float inicioGuardia = -1f;      // Time.time en que el jugador subió la guardia que se está midiendo
    private double tiempoCombatesPrevios;   // segundos de combate (desde su campana) de los combates ya cerrados de la sesión

    private Oponente oponente;
    private SaludJugador jugador;
    private RecuperacionJugador recuperacion;
    private ControlRondas rondas;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Crear()
    {
        if (Instancia != null) return;
        var go = new GameObject("RegistroSesion");
        DontDestroyOnLoad(go);
        go.AddComponent<RegistroSesion>();
    }

    private void Awake()
    {
        Instancia = this;
        inicioSesion = Time.realtimeSinceStartup;
        DateTime ahora = DateTime.Now;
        sesion = new DatosSesion
        {
            id = ahora.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture),
            inicio = ahora.ToString("s", CultureInfo.InvariantCulture),
            dispositivo = SystemInfo.deviceModel,
            version = Application.version,
        };
        string carpeta = Path.Combine(Application.persistentDataPath, Carpeta);
        RutaSesion = Path.Combine(carpeta, $"sesion_{sesion.id}.json");
        RutaCsv = Path.Combine(carpeta, "combates.csv");
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += AlCargarEscena;
        SceneManager.sceneUnloaded += AlDescargarEscena;
        Puno.AlLanzarGolpe += AlLanzarGolpe;
        Haptica.AlVibrar += AlVibrar;
        Haptica.AlMedirLatencia += AlMedirLatencia;
        Puno.AlEstimarRetrasoDeteccion += AlEstimarRetraso;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= AlCargarEscena;
        SceneManager.sceneUnloaded -= AlDescargarEscena;
        Puno.AlLanzarGolpe -= AlLanzarGolpe;
        Haptica.AlVibrar -= AlVibrar;
        Haptica.AlMedirLatencia -= AlMedirLatencia;
        Puno.AlEstimarRetrasoDeteccion -= AlEstimarRetraso;
        Desconectar();
    }

    // AfterSceneLoad llega después del sceneLoaded de la primera escena
    private void Start() => EmpezarCombate(SceneManager.GetActiveScene());

    private void AlCargarEscena(Scene escena, LoadSceneMode modo) => EmpezarCombate(escena);

    private void AlDescargarEscena(Scene escena)
    {
        if (combate == null || combate.escena != escena.name) return;
        CerrarCombate(null);
        Desconectar();
    }

    private void OnApplicationPause(bool pausada)
    {
        if (pausada) Guardar();
    }

    private void OnApplicationQuit()
    {
        if (combate != null && !combateCerrado) CerrarCombate(null);
        else Guardar();
    }

    private void Update()
    {
        if (combate == null || combateCerrado) return;
        VigilarGuardia();
        if (rondas == null || rondas.Estado != EstadoRondas.Round) return;
        // Solo cuenta el rendimiento durante los rounds, que es lo que pide RNF-01 ("durante el combate")
        float dt = Time.unscaledDeltaTime;
        combate.fotogramas++;
        sumaDelta += dt;
        if (dt > 1f / FpsObjetivo * 1.05f) fotogramasLentos++;
    }

    private void EmpezarCombate(Scene escena)
    {
        Desconectar();
        oponente = FindAnyObjectByType<Oponente>();
        if (oponente == null) return; // menú u otra escena sin pelea
        jugador = FindAnyObjectByType<SaludJugador>();
        recuperacion = FindAnyObjectByType<RecuperacionJugador>();
        rondas = FindAnyObjectByType<ControlRondas>();
        var control = FindAnyObjectByType<ControlCombate>();

        int nivel = control != null ? control.Nivel : 0;
        combate = new DatosCombate
        {
            nivel = nivel,
            escena = escena.name,
            rival = oponente.Nombre,
            inicio = DateTime.Now.ToString("s", CultureInfo.InvariantCulture),
            reintento = !nivelesJugados.Add(nivel),
        };
        combateCerrado = false;
        inicioCombate = rondas == null ? Time.time : -1f;
        fotogramasLentos = 0;
        sumaDelta = 0;
        inicioGuardia = -1f;
        latencias.Clear();
        retrasos.Clear();
        sesion.combatesJugados.Add(combate);

        oponente.AlRecibirGolpe += AlConectar;
        oponente.AlBloquear += AlBloquearRival;
        oponente.AlEsquivar += AlEsquivarRival;
        oponente.AlCaer += AlCaerRival;
        oponente.AlCambiarSalud += AlCambiarSaludRival;
        if (jugador != null)
        {
            jugador.AlResolverAtaque += AlResolverAtaque;
            jugador.AlCambiarSalud += AlCambiarSaludJugador;
        }
        combate.saludFinalRival = Redondear(oponente.SaludNormalizada);
        if (jugador != null) combate.saludFinalJugador = Redondear(jugador.SaludNormalizada);
        if (recuperacion != null)
        {
            recuperacion.AlDerribarse += AlDerribarseJugador;
            recuperacion.AlLevantarse += AlLevantarseJugador;
        }
        if (rondas != null)
        {
            rondas.AlIniciarRound += AlIniciarRound;
            rondas.AlTerminarCombate += AlTerminarCombateRondas;
        }
        Debug.Log($"Registro: combate {(combate.reintento ? "reintentado" : "nuevo")} contra {combate.rival} (nivel {nivel}) → {RutaSesion}", this);
    }

    private void Desconectar()
    {
        if (oponente != null)
        {
            oponente.AlRecibirGolpe -= AlConectar;
            oponente.AlBloquear -= AlBloquearRival;
            oponente.AlEsquivar -= AlEsquivarRival;
            oponente.AlCaer -= AlCaerRival;
            oponente.AlCambiarSalud -= AlCambiarSaludRival;
        }
        if (jugador != null)
        {
            jugador.AlResolverAtaque -= AlResolverAtaque;
            jugador.AlCambiarSalud -= AlCambiarSaludJugador;
        }
        if (recuperacion != null)
        {
            recuperacion.AlDerribarse -= AlDerribarseJugador;
            recuperacion.AlLevantarse -= AlLevantarseJugador;
        }
        if (rondas != null)
        {
            rondas.AlIniciarRound -= AlIniciarRound;
            rondas.AlTerminarCombate -= AlTerminarCombateRondas;
        }
        oponente = null;
        jugador = null;
        recuperacion = null;
        rondas = null;
    }

    private bool Registrando => combate != null && !combateCerrado;

    private void AlLanzarGolpe(Puno puno)
    {
        if (Registrando) combate.golpesLanzados++;
    }

    private void AlConectar(Golpe golpe)
    {
        if (!Registrando) return;
        combate.golpesConectados++;
        if (combate.segundosHastaGolpeConectado < 0f && inicioCombate >= 0f)
        {
            float t = Time.time - inicioCombate;
            combate.segundosHastaGolpeConectado = Redondear(t);
            if (sesion.segundosHastaGolpeConectado < 0f) sesion.segundosHastaGolpeConectado = Redondear((float)(tiempoCombatesPrevios + t));
        }
        switch (golpe.tipo)
        {
            case TipoGolpe.Jab: combate.jabs++; break;
            case TipoGolpe.Cross: combate.crosses++; break;
            case TipoGolpe.Uppercut: combate.uppercuts++; break;
            default: combate.laterales++; break;
        }
    }

    private void AlBloquearRival(Golpe golpe)
    {
        if (Registrando) combate.golpesBloqueadosPorRival++;
    }

    private void AlEsquivarRival(Golpe golpe)
    {
        if (Registrando) combate.golpesEsquivadosPorRival++;
    }

    private void AlCaerRival(Golpe golpe)
    {
        // La última caída cierra el combate antes de llegar aquí: al cerrar se completa con Oponente.Caidas
        if (Registrando) combate.derribosAlRival++;
        // Sin ControlRondas el combate termina con la derrota del rival
        if (rondas == null && oponente != null && oponente.Derrotado) CerrarCombate(ResultadoCombate.VictoriaPorKO.ToString());
    }

    private void AlResolverAtaque(ResultadoAtaque resultado)
    {
        if (!Registrando) return;
        combate.ataquesRecibidos++;
        switch (resultado)
        {
            case ResultadoAtaque.Conectado: combate.ataquesConectados++; break;
            case ResultadoAtaque.Bloqueado: combate.ataquesBloqueados++; break;
            case ResultadoAtaque.Esquivado: combate.ataquesEsquivados++; break;
        }
    }

    // La salud se sigue por eventos para que un combate abandonado también la tenga
    private void AlCambiarSaludRival(float salud, float maxima)
    {
        if (Registrando) combate.saludFinalRival = Redondear(maxima > 0f ? salud / maxima : 0f);
    }

    private void AlCambiarSaludJugador(float salud, float maxima)
    {
        if (Registrando) combate.saludFinalJugador = Redondear(maxima > 0f ? salud / maxima : 0f);
    }

    private void AlDerribarseJugador()
    {
        if (Registrando) combate.caidasJugador++;
    }

    private void AlLevantarseJugador()
    {
        if (Registrando) combate.cuentasSuperadas++;
    }

    private void AlIniciarRound(int numero)
    {
        if (!Registrando) return;
        if (inicioCombate < 0f) inicioCombate = Time.time;
        combate.roundAlcanzado = numero;
    }

    private void AlVibrar(OVRInput.Controller control, float frecuencia, float amplitud, float momento)
    {
        if (Registrando && amplitud > 0f) combate.vibraciones++;
    }

    private void AlMedirLatencia(OVRInput.Controller control, double milisegundos)
    {
        if (Registrando) latencias.Add(milisegundos);
    }

    private void AlEstimarRetraso(Puno puno, float segundos)
    {
        if (Registrando) retrasos.Add(segundos * 1000.0);
    }

    // RNF-03: la guardia cuenta cuando las dos manos están donde bloquean (SaludJugador.EnGuardia) durante
    // SegundosGuardiaSostenida seguidos, desde la primera campana; se guarda el momento en que la subió. Solo lee la pose.
    private void VigilarGuardia()
    {
        if (combate.segundosHastaGuardia >= 0f || jugador == null || inicioCombate < 0f || Time.timeScale == 0f) return;
        if (!jugador.EnGuardia)
        {
            inicioGuardia = -1f;
            return;
        }
        if (inicioGuardia < 0f) inicioGuardia = Time.time;
        if (Time.time - inicioGuardia < SegundosGuardiaSostenida) return;
        float t = inicioGuardia - inicioCombate;
        combate.segundosHastaGuardia = Redondear(t);
        if (sesion.segundosHastaGuardia < 0f) sesion.segundosHastaGuardia = Redondear((float)(tiempoCombatesPrevios + t));
    }

    private void AlTerminarCombateRondas(ResultadoCombate resultado) => CerrarCombate(resultado.ToString());

    private void CerrarCombate(string resultado)
    {
        if (combate == null || combateCerrado) return;
        combateCerrado = true;
        if (resultado != null) combate.resultado = resultado;
        if (rondas != null && combate.roundAlcanzado == 0) combate.roundAlcanzado = rondas.RoundActual;
        combate.segundosCombate = inicioCombate >= 0f ? Mathf.Round((Time.time - inicioCombate) * 10f) / 10f : 0f;
        if (inicioCombate >= 0f) tiempoCombatesPrevios += Time.time - inicioCombate;
        // Si se sale de la escena los componentes ya no existen y quedan los conteos por eventos
        if (oponente != null) combate.derribosAlRival = Mathf.Max(combate.derribosAlRival, oponente.Caidas);
        if (recuperacion != null) combate.caidasJugador = Mathf.Max(combate.caidasJugador, recuperacion.Derribos);

        // Un golpe que llega al rival también se lanzó, aunque el detector no lo viera
        int llegaron = combate.golpesConectados + combate.golpesBloqueadosPorRival + combate.golpesEsquivadosPorRival;
        combate.golpesLanzados = Mathf.Max(combate.golpesLanzados, llegaron);
        combate.precision = Proporcion(combate.golpesConectados, combate.golpesLanzados);
        combate.efectividad = Proporcion(combate.golpesConectados, llegaron);
        combate.porcentajeBloqueo = Proporcion(combate.ataquesBloqueados, combate.ataquesRecibidos);
        if (jugador != null) combate.saludFinalJugador = Redondear(jugador.SaludNormalizada);
        if (oponente != null) combate.saludFinalRival = Redondear(oponente.SaludNormalizada);
        combate.fpsMedio = sumaDelta > 0 ? Redondear((float)(combate.fotogramas / sumaDelta)) : 0f;
        combate.porcentajeFotogramasLentos = Proporcion(fotogramasLentos, combate.fotogramas);
        combate.pasoFisicaMs = Redondear(Time.fixedDeltaTime * 1000f);
        combate.muestrasLatencia = latencias.Count;
        combate.latenciaHapticaMediaMs = Media(latencias);
        combate.latenciaHapticaP95Ms = Percentil(latencias, 0.95);
        combate.latenciaHapticaMaxMs = Percentil(latencias, 1.0);
        combate.muestrasRetraso = retrasos.Count;
        combate.retrasoDeteccionMedioMs = Media(retrasos);
        combate.retrasoDeteccionP95Ms = Percentil(retrasos, 0.95);
        combate.retrasoDeteccionMaxMs = Percentil(retrasos, 1.0);

        if (combate.reintento) sesion.reintentos++;
        if (combate.resultado.StartsWith("Victoria")) sesion.victorias++;
        sesion.combates = sesion.combatesJugados.Count;
        Guardar();
        AgregarCsv(combate);
        Debug.Log($"Registro: {combate.resultado} contra {combate.rival} en {combate.segundosCombate:F0} s. Golpes {combate.golpesConectados}/{combate.golpesLanzados} " +
                  $"(precisión {combate.precision:P0}), bloqueados por el rival {combate.golpesBloqueadosPorRival}, esquivados {combate.golpesEsquivadosPorRival}; " +
                  $"ataques bloqueados {combate.ataquesBloqueados}/{combate.ataquesRecibidos}. Guardado en {RutaSesion}", this);
        AlTerminarCombate?.Invoke(combate);
    }

    private static float Proporcion(int parte, int total) => total > 0 ? Redondear((float)parte / total) : 0f;
    private static float Redondear(float valor) => Mathf.Round(valor * 1000f) / 1000f;

    private static float Media(List<double> valores)
    {
        if (valores.Count == 0) return 0f;
        double suma = 0;
        foreach (double v in valores) suma += v;
        return Redondear((float)(suma / valores.Count));
    }

    // Percentil por rango más cercano (p = 1 es el máximo)
    private static float Percentil(List<double> valores, double p)
    {
        if (valores.Count == 0) return 0f;
        var ordenados = new List<double>(valores);
        ordenados.Sort();
        int i = Mathf.Clamp((int)Math.Ceiling(p * ordenados.Count) - 1, 0, ordenados.Count - 1);
        return Redondear((float)ordenados[i]);
    }

    /// <summary>Escribe el JSON de la sesión. Se puede llamar en cualquier momento.</summary>
    public void Guardar()
    {
        sesion.fin = DateTime.Now.ToString("s", CultureInfo.InvariantCulture);
        sesion.segundosSesion = Mathf.Round(Time.realtimeSinceStartup - inicioSesion);
        sesion.combates = sesion.combatesJugados.Count;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(RutaSesion));
            File.WriteAllText(RutaSesion, JsonUtility.ToJson(sesion, true));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Registro: no se pudo guardar la sesión en {RutaSesion}: {e.Message}", this);
        }
    }

    private static readonly string[] Columnas =
    {
        "sesion", "inicio", "nivel", "rival", "reintento", "resultado", "round", "segundos", "lanzados", "conectados",
        "bloqueadosPorRival", "esquivadosPorRival", "precision", "efectividad", "ataquesRecibidos", "ataquesBloqueados",
        "ataquesEsquivados", "porcentajeBloqueo", "caidasJugador", "cuentasSuperadas", "saludJugador", "saludRival",
        "fpsMedio", "fotogramasLentos",
        // Entrega 5, bloque J: siempre al final, para no romper los CSV anteriores
        "pasoFisicaMs", "muestrasLatencia", "latenciaMediaMs", "latenciaP95Ms", "latenciaMaxMs", "muestrasRetraso",
        "retrasoDeteccionMedioMs", "retrasoDeteccionP95Ms", "retrasoDeteccionMaxMs",
        // Entrega 5, bloque K (RNF-03)
        "segundosHastaGuardia", "segundosHastaGolpeConectado",
    };

    private void AgregarCsv(DatosCombate c)
    {
        var inv = CultureInfo.InvariantCulture;
        string[] valores =
        {
            sesion.id, c.inicio, c.nivel.ToString(inv), c.rival, c.reintento ? "1" : "0", c.resultado, c.roundAlcanzado.ToString(inv),
            c.segundosCombate.ToString("F1", inv), c.golpesLanzados.ToString(inv), c.golpesConectados.ToString(inv),
            c.golpesBloqueadosPorRival.ToString(inv), c.golpesEsquivadosPorRival.ToString(inv), c.precision.ToString(inv),
            c.efectividad.ToString(inv), c.ataquesRecibidos.ToString(inv), c.ataquesBloqueados.ToString(inv),
            c.ataquesEsquivados.ToString(inv), c.porcentajeBloqueo.ToString(inv), c.caidasJugador.ToString(inv),
            c.cuentasSuperadas.ToString(inv), c.saludFinalJugador.ToString(inv), c.saludFinalRival.ToString(inv),
            c.fpsMedio.ToString(inv), c.porcentajeFotogramasLentos.ToString(inv),
            c.pasoFisicaMs.ToString(inv), c.muestrasLatencia.ToString(inv), c.latenciaHapticaMediaMs.ToString(inv),
            c.latenciaHapticaP95Ms.ToString(inv), c.latenciaHapticaMaxMs.ToString(inv), c.muestrasRetraso.ToString(inv),
            c.retrasoDeteccionMedioMs.ToString(inv), c.retrasoDeteccionP95Ms.ToString(inv), c.retrasoDeteccionMaxMs.ToString(inv),
            c.segundosHastaGuardia.ToString(inv), c.segundosHastaGolpeConectado.ToString(inv),
        };
        try
        {
            bool nuevo = !File.Exists(RutaCsv);
            if (!nuevo) ActualizarCabecera();
            var linea = new StringBuilder();
            if (nuevo) linea.AppendLine(string.Join(",", Columnas));
            linea.AppendLine(string.Join(",", Array.ConvertAll(valores, v => v.Contains(",") ? $"\"{v}\"" : v)));
            File.AppendAllText(RutaCsv, linea.ToString());
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Registro: no se pudo escribir {RutaCsv}: {e.Message}", this);
        }
    }

    // Un combates.csv de una versión anterior tiene menos columnas: se completa su cabecera (las nuevas van al final)
    private void ActualizarCabecera()
    {
        if (cabeceraRevisada) return;
        cabeceraRevisada = true;
        string cabecera = string.Join(",", Columnas);
        string[] lineas = File.ReadAllLines(RutaCsv);
        if (lineas.Length == 0 || lineas[0] == cabecera || !cabecera.StartsWith(lineas[0] + ",", StringComparison.Ordinal)) return;
        lineas[0] = cabecera;
        File.WriteAllLines(RutaCsv, lineas);
        Debug.Log($"Registro: cabecera de {RutaCsv} ampliada con las columnas nuevas", this);
    }

    // Para probar sin visor: en Play, seleccionar el objeto RegistroSesion (DontDestroyOnLoad) y clic derecho en el componente
    [ContextMenu("Probar: guardar ahora")]
    private void ProbarGuardar()
    {
        Guardar();
        Debug.Log($"Registro guardado en {RutaSesion}", this);
    }
}
