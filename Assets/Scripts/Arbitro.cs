using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Órdenes del réferi que no son números de la cuenta. CuerpoArbitro hace la seña de cada una.</summary>
public enum OrdenArbitro
{
    /// <summary>"¡Box!": a pelear (empieza un round o termina una cuenta).</summary>
    Box,
    /// <summary>"¡Nocaut!": para el combate agitando los brazos.</summary>
    Fin,
}

/// <summary>
/// Réferi (RF-12, RF-16): cuenta en voz alta del 1 al 10, en 3D desde su posición, cuando cae el jugador y cuando cae
/// el rival en un knockdown, y da las órdenes del combate ("¡Box!", "¡Nocaut!"). La caída final del rival
/// (Derrotado) no se cuenta: por la regla de las tres caídas el combate se para en el acto.
/// Jugador: la cuenta, el tiempo y el button mashing siguen siendo de RecuperacionJugador; aquí solo se pone la voz.
/// Rival: mientras hay réferi, el rival no se levanta con su temporizador (Oponente.LevantaArbitro). La cuenta sigue
/// el reglamento: el rival tiene que estar de pie en
/// el número cuentaKnockdown de su perfil (nunca en el 10, que sería nocaut: como mucho en el 9), así que empieza a
/// levantarse segundosParaIncorporarse antes; aunque se levante pronto, la cuenta sigue hasta cuentaProteccion (8, la
/// cuenta de protección obligatoria) y solo entonces, con "¡Box!", se reanuda la pelea. Entre que se levanta y el
/// "¡Box!" el rival ni ataca ni recibe golpes. El round que llega a cero espera a esa cuenta
/// (ControlRondas.CuentaEnMarcha).
/// La cuenta del rival no empieza al caer, sino cuando la animación lo deja tendido (ReaccionAnimada.EnElPiso) y tras
/// pausaAntesDeContar: así el réferi no cuenta a alguien que todavía se está cayendo.
/// Si alguien más lo levanta (el descanso, una prueba), la cuenta se cancela y la voz se calla.
/// La cuenta usa tiempo escalado y la voz se pausa a mano, porque un AudioSource no se detiene con timeScale = 0.
/// Además de las órdenes habla en otros momentos, solo con la voz: las instrucciones antes del primer round, "¡Tiempo!"
/// al terminar un round y, si el combate llega al final sin nocaut, que deciden los jueces. Cada momento tiene varias
/// frases (Audio/Arbitro, de Tools/voces_arbitro.py) y elige una al azar sin repetir la anterior; tras una cuenta
/// superada no dice el "¡Box!" de empezar el round sino una de frasesSeguir ("¿Puede seguir? ¡Box!").
/// Sin este componente en la escena, el rival se levanta con segundosKnockdown, como antes.
/// Va en un objeto propio de cada escena de pelea. El cuerpo visible (CuerpoArbitro) es un hijo con la voz y el modelo,
/// provisional por ahora; se cambia el modelo sin tocar este componente.
/// </summary>
public class Arbitro : MonoBehaviour
{
    private const int NumerosPorCuenta = 10;

    [Tooltip("Vacío = el Oponente de la escena")]
    [SerializeField] private Oponente oponente;
    [Tooltip("Vacío = el SaludJugador de la escena")]
    [SerializeField] private SaludJugador jugador;
    [Tooltip("Vacío = el RecuperacionJugador de la escena")]
    [SerializeField] private RecuperacionJugador recuperacion;
    [Tooltip("Vacío = el ControlRondas de la escena; si no hay, nadie espera a la cuenta")]
    [SerializeField] private ControlRondas rondas;

    [Header("Voz")]
    [Tooltip("Fuente 3D en la posición del réferi")]
    [SerializeField] private AudioSource voz;
    [Tooltip("Del 1 al 10, en orden. Se reemplazan por grabaciones arrastrando otros clips aquí o sobrescribiendo los WAV")]
    [SerializeField] private AudioClip[] numeros = new AudioClip[NumerosPorCuenta];

    [Header("Frases (Audio/Arbitro/Voz_*.wav; vacías = solo la seña)")]
    [Tooltip("Al empezar cada round: \"¡Box!\", \"¡A pelear!\"...")]
    [SerializeField] private AudioClip[] frasesBox;
    [Tooltip("Al reanudar tras una cuenta superada (del rival o del jugador); vacías = frasesBox")]
    [SerializeField] private AudioClip[] frasesSeguir;
    [Tooltip("Al parar el combate por nocaut (de cualquiera de los dos)")]
    [SerializeField] private AudioClip[] frasesNocaut;
    [Tooltip("En la preparación, antes de la primera campana. Tienen que caber antes del primer \"¡Box!\"")]
    [SerializeField] private AudioClip[] frasesInstrucciones;
    [Tooltip("Al terminar un round que no acaba el combate")]
    [SerializeField] private AudioClip[] frasesTiempo;
    [Tooltip("Al terminar el último round sin nocaut: deciden los jueces")]
    [SerializeField] private AudioClip[] frasesDecision;
    [Tooltip("Segundos entre la campana y el \"¡Box!\" del principio de cada round")]
    [SerializeField, Min(0f)] private float retrasoBox = 0.5f;
    [Tooltip("Segundos desde que se carga la pelea hasta las instrucciones")]
    [SerializeField, Min(0f)] private float retrasoInstrucciones = 0.4f;
    [Tooltip("Segundos entre la campana de fin de round y \"¡Tiempo!\" (o la decisión), para no pisar los tres toques")]
    [SerializeField, Min(0f)] private float retrasoTiempo = 1f;

    [Header("Cuenta del rival")]
    [Tooltip("Cuenta de protección: aunque el rival se levante antes, se cuenta hasta este número antes de seguir")]
    [SerializeField, Range(1, 9)] private int cuentaProteccion = 8;
    [Tooltip("Segundos que tarda el rival en ponerse de pie desde la lona (lo que dura el final de su clip de Knockdown)")]
    [SerializeField, Min(0f)] private float segundosParaIncorporarse = 2f;
    [Tooltip("Segundos entre números. La del jugador va al ritmo de segundosParaLevantarse de RecuperacionJugador")]
    [SerializeField, Min(0.1f)] private float segundosPorNumero = 1f;
    [Tooltip("Pausa entre que el rival queda tendido y el primer número (el réferi llega y levanta el brazo)")]
    [SerializeField, Min(0f)] private float pausaAntesDeContar = 0.5f;
    [Tooltip("Sin ReaccionAnimada (rivales sin esqueleto), o si la animación no llega al piso: espera máxima antes de contar")]
    [SerializeField, Min(0f)] private float esperaMaximaCaida = 2.5f;

    [SerializeField] private bool registrar = true;

    /// <summary>Dice un número de la cuenta (1 a 10); el bool es true si cuenta al jugador. CuerpoArbitro hace el gesto con él.</summary>
    public event Action<int, bool> AlDecirNumero;
    /// <summary>Da una orden (el punto queda para órdenes que señalen un lugar; ahora ninguna lo usa).</summary>
    public event Action<OrdenArbitro, Vector3> AlOrdenar;

    public bool ContandoRival { get; private set; }
    public bool ContandoJugador => recuperacion != null && recuperacion.Contando;
    /// <summary>Último número dicho en la cuenta actual; 0 si no hay cuenta.</summary>
    public int NumeroActual { get; private set; }

    private float segundosRival;
    private float duracionRival;
    private int ultimoNumeroRival;
    private bool vozPausada;
    private bool combateTerminado;
    private bool esperandoPiso;      // el rival cayó pero aún no está tendido: la cuenta no ha empezado
    private float esperaCaida;
    private ReaccionAnimada reaccionRival;
    private AtaqueOponente ataqueRival;
    private LimiteRing ring;
    private float soltarRivalEn;     // segundo de la cuenta en el que el rival empieza a levantarse
    private bool rivalSoltado;       // ya se está levantando (o está de pie) y la cuenta sigue
    private bool levantandoYo;       // el Levantarse que llega lo pidió esta cuenta: no la cancela
    private float boxEn = -1f;       // cuándo toca el "¡Box!" pendiente (tiempo de juego); -1 = ninguno
    private bool boxTrasCuenta;      // el "¡Box!" pendiente reanuda tras una cuenta: frasesSeguir
    private AudioClip[] frasePendiente; // frase sin seña que toca decir en fraseEn (instrucciones, tiempo, decisión)
    private float fraseEn = -1f;
    private readonly Dictionary<AudioClip[], int> ultimaFrase = new Dictionary<AudioClip[], int>();

    private void Awake()
    {
        if (oponente == null) oponente = FindAnyObjectByType<Oponente>();
        if (jugador == null) jugador = FindAnyObjectByType<SaludJugador>();
        if (recuperacion == null) recuperacion = FindAnyObjectByType<RecuperacionJugador>();
        if (rondas == null) rondas = FindAnyObjectByType<ControlRondas>();
        if (voz == null) voz = GetComponentInChildren<AudioSource>();
        if (oponente != null) reaccionRival = oponente.GetComponent<ReaccionAnimada>();
        if (oponente != null) ataqueRival = oponente.GetComponent<AtaqueOponente>();
        ring = FindAnyObjectByType<LimiteRing>();
        if (oponente == null)
        {
            Debug.LogWarning($"{name}: Arbitro no encontró un Oponente en la escena.", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (oponente == null) return;
        oponente.LevantaArbitro = true;
        oponente.AlCaer += AlCaerRival;
        oponente.AlLevantarse += AlLevantarseRival;
        if (recuperacion != null) recuperacion.AlLevantarse += AlLevantarseJugador;
        if (rondas != null)
        {
            rondas.CuentaEnMarcha = HayCuentaDelRival;
            rondas.AlTerminarCombate += AlTerminarCombate;
            rondas.AlIniciarRound += AlIniciarRound;
            rondas.AlTerminarRound += AlTerminarRound;
        }
    }

    private void Start()
    {
        // Las instrucciones solo al principio de la pelea, mientras corre la preparación
        if (rondas != null && rondas.isActiveAndEnabled && rondas.Estado == EstadoRondas.Preparacion)
            Programar(frasesInstrucciones, retrasoInstrucciones);
    }

    private void OnDisable()
    {
        if (oponente == null) return;
        // Sin réferi el rival vuelve a su temporizador, así que una cuenta cortada aquí no lo deja en el piso
        ContandoRival = false;
        Callar();
        oponente.LevantaArbitro = false;
        oponente.AlCaer -= AlCaerRival;
        oponente.AlLevantarse -= AlLevantarseRival;
        if (recuperacion != null) recuperacion.AlLevantarse -= AlLevantarseJugador;
        if (rondas != null)
        {
            rondas.CuentaEnMarcha = null;
            rondas.AlTerminarCombate -= AlTerminarCombate;
            rondas.AlIniciarRound -= AlIniciarRound;
            rondas.AlTerminarRound -= AlTerminarRound;
        }
    }

    private void Update()
    {
        PausarVoz(Time.timeScale == 0f);
        if (Time.timeScale == 0f) return;

        if (ContandoRival && esperandoPiso)
        {
            // No se cuenta a un rival que todavía está cayendo: primero tiene que quedar tendido
            esperaCaida += Time.deltaTime;
            bool enElPiso = reaccionRival != null ? reaccionRival.EnElPiso : esperaCaida >= 1f;
            if (enElPiso || esperaCaida >= esperaMaximaCaida)
            {
                esperandoPiso = false;
                // Tendido: tras una pausa corta (llega y levanta el brazo) empieza a contar, sin ninguna orden antes
                segundosRival = -pausaAntesDeContar;
            }
        }
        else if (ContandoRival)
        {
            segundosRival += Time.deltaTime;
            if (!rivalSoltado && segundosRival >= soltarRivalEn) SoltarRival();
            if (segundosRival >= duracionRival) TerminarCuentaRival();
        }

        if (boxEn >= 0f && Time.time >= boxEn)
        {
            boxEn = -1f;
            if (!combateTerminado && !ContandoRival && !ContandoJugador) Ordenar(OrdenArbitro.Box, Vector3.zero, boxTrasCuenta);
        }

        if (fraseEn >= 0f && Time.time >= fraseEn)
        {
            fraseEn = -1f;
            if (!ContandoRival && !ContandoJugador) Hablar(frasePendiente);
        }

        int numero = combateTerminado ? 0 : NumeroEnCurso();
        if (numero == NumeroActual) return;
        NumeroActual = numero;
        if (numero > 0) Decir(numero);
    }

    private bool HayCuentaDelRival() => ContandoRival;

    // Una sola voz: si caen los dos (solo pasa en pruebas) manda la del jugador, que puede perder el combate;
    // la del rival sigue corriendo en silencio y vuelve a oírse al terminar
    private int NumeroEnCurso()
    {
        if (ContandoJugador) return NumeroJugador();
        if (ContandoRival) return esperandoPiso || segundosRival < 0f ? 0 : Mathf.Min(ultimoNumeroRival, Mathf.FloorToInt(segundosRival / segundosPorNumero) + 1);
        return 0;
    }

    // El tiempo del jugador lo lleva RecuperacionJugador: la voz solo lo reparte entre los diez números
    private int NumeroJugador()
    {
        float transcurrido = recuperacion.SegundosCuenta - recuperacion.SegundosRestantes;
        float porNumero = recuperacion.SegundosCuenta / NumerosPorCuenta;
        return Mathf.Clamp(Mathf.FloorToInt(transcurrido / porNumero) + 1, 1, NumerosPorCuenta);
    }

    private void AlCaerRival(Golpe golpe)
    {
        if (oponente.Derrotado) return;
        // Tiene que estar de pie antes del 10: como mucho en el 9
        int dePieEn = Mathf.Clamp(oponente.Perfil.cuentaKnockdown, 1, NumerosPorCuenta - 1);
        ultimoNumeroRival = Mathf.Max(dePieEn, Mathf.Min(cuentaProteccion, NumerosPorCuenta - 1));
        duracionRival = ultimoNumeroRival * segundosPorNumero;
        soltarRivalEn = Mathf.Max(0.5f, dePieEn * segundosPorNumero - segundosParaIncorporarse);
        rivalSoltado = false;
        segundosRival = 0f;
        esperandoPiso = true;
        esperaCaida = 0f;
        ContandoRival = true;
        boxEn = -1f;
        Callar();
        if (registrar) Debug.Log($"Réferi: cuenta a {oponente.Nombre}; de pie en el {dePieEn}, cuenta hasta {ultimoNumeroRival}", this);
    }

    // El rival empieza a levantarse con la cuenta en marcha. Hasta el "¡Box!" ni ataca ni recibe golpes
    private void SoltarRival()
    {
        rivalSoltado = true;
        oponente.Protegido = true;
        levantandoYo = true;
        oponente.Levantarse();
        levantandoYo = false;
        if (ataqueRival != null) ataqueRival.Pausar();
        if (registrar) Debug.Log($"Réferi: {oponente.Nombre} se levanta", this);
    }

    private void TerminarCuentaRival()
    {
        ContandoRival = false;
        if (!rivalSoltado) SoltarRival();
        // Si el round ya se acabó (o el combate), ControlRondas lo deja protegido y en pausa: no se reanuda aquí
        if (combateTerminado || (rondas != null && rondas.Estado != EstadoRondas.Round)) return;
        oponente.Protegido = false;
        if (ataqueRival != null) ataqueRival.Reanudar();
        Ordenar(OrdenArbitro.Box, Vector3.zero, true);
    }

    // Alguien más lo levantó con la cuenta a medias (el descanso, una prueba): la cuenta ya no tiene sentido
    private void AlLevantarseRival()
    {
        if (!ContandoRival || levantandoYo) return;
        ContandoRival = false;
        Callar();
        if (registrar) Debug.Log($"Réferi: cuenta a {oponente.Nombre} cancelada", this);
    }

    // El jugador superó la cuenta: se calla el número y manda seguir
    private void AlLevantarseJugador()
    {
        Callar();
        if (combateTerminado) return;
        boxEn = Time.time + 0.6f;
        boxTrasCuenta = true;
    }

    private void AlIniciarRound(int numero)
    {
        boxEn = Time.time + retrasoBox;
        boxTrasCuenta = false;
    }

    // Llega antes que AlTerminarCombate: si el round era el último (o fue nocaut), éste cambia la frase
    private void AlTerminarRound(int numero)
    {
        if (!combateTerminado) Programar(frasesTiempo, retrasoTiempo);
    }

    private void AlTerminarCombate(ResultadoCombate resultado)
    {
        combateTerminado = true;
        boxEn = -1f;
        Callar();
        if (ContandoRival)
        {
            ContandoRival = false;
            if (!rivalSoltado)
            {
                levantandoYo = true;
                oponente.Levantarse();
                levantandoYo = false;
            }
        }
        // Por nocaut (el del jugador o la tercera caída del rival) para el combate agitando los brazos
        if (resultado == ResultadoCombate.VictoriaPorKO || resultado == ResultadoCombate.DerrotaPorKO)
            Ordenar(OrdenArbitro.Fin, Vector3.zero);
        else
            Programar(frasesDecision, retrasoTiempo);
    }

    private void Ordenar(OrdenArbitro orden, Vector3 punto, bool trasCuenta = false)
    {
        if (registrar) Debug.Log($"Réferi: {(orden == OrdenArbitro.Box ? "¡Box!" : "¡Nocaut!")}", this);
        AlOrdenar?.Invoke(orden, punto);
        AudioClip[] frases = orden == OrdenArbitro.Fin ? frasesNocaut
            : trasCuenta && frasesSeguir != null && frasesSeguir.Length > 0 ? frasesSeguir : frasesBox;
        Hablar(frases);
    }

    private void Programar(AudioClip[] frases, float retraso)
    {
        frasePendiente = frases;
        fraseEn = Time.time + retraso;
    }

    // Una frase al azar del grupo, sin repetir la última que se dijo de ese grupo
    private void Hablar(AudioClip[] frases)
    {
        if (voz == null || frases == null || frases.Length == 0) return;
        int i;
        if (frases.Length == 1) i = 0;
        else if (!ultimaFrase.TryGetValue(frases, out int ultima)) i = UnityEngine.Random.Range(0, frases.Length);
        else
        {
            i = UnityEngine.Random.Range(0, frases.Length - 1);
            if (i >= ultima) i++;
        }
        ultimaFrase[frases] = i;
        if (frases[i] == null) return;
        if (registrar) Debug.Log($"Réferi dice: {frases[i].name}", this);
        vozPausada = false;
        voz.clip = frases[i];
        voz.Play();
    }

    private void Decir(int numero)
    {
        if (registrar) Debug.Log($"Réferi: {numero}", this);
        AlDecirNumero?.Invoke(numero, ContandoJugador);
        if (voz == null || numeros == null || numero > numeros.Length || numeros[numero - 1] == null) return;
        vozPausada = false;
        voz.clip = numeros[numero - 1];
        voz.Play();
    }

    private void Callar()
    {
        NumeroActual = 0;
        fraseEn = -1f;
        vozPausada = false;
        if (voz != null) voz.Stop();
    }

    // Un clip que ya está sonando no se entera de timeScale = 0: hay que pausarlo y reanudarlo a mano
    private void PausarVoz(bool pausar)
    {
        if (voz == null) return;
        if (pausar && voz.isPlaying)
        {
            voz.Pause();
            vozPausada = true;
        }
        else if (!pausar && vozPausada)
        {
            voz.UnPause();
            vozPausada = false;
        }
    }

    // Para probar sin visor: en Play, clic derecho en el título del componente
    [ContextMenu("Probar: contar al rival")]
    private void ProbarContarRival()
    {
        if (oponente.Caido || oponente.Caidas + 1 >= oponente.Perfil.caidasParaDerrota)
        {
            Debug.LogWarning($"{name}: {oponente.Nombre} ya está en el piso o la próxima caída sería la derrota, que no se cuenta.", this);
            return;
        }
        oponente.Derribar();
    }

    [ContextMenu("Probar: contar al jugador")]
    private void ProbarContarJugador()
    {
        if (jugador == null || recuperacion == null)
        {
            Debug.LogWarning($"{name}: hacen falta SaludJugador y RecuperacionJugador en la escena.", this);
            return;
        }
        jugador.Derribar();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(voz != null ? voz.transform.position : transform.position, 0.2f);
    }
}
