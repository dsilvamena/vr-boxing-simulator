using System;
using UnityEngine;

public enum EstadoRondas { Preparacion, Round, Descanso, Terminado }

public enum ResultadoCombate { VictoriaPorKO, VictoriaPorSalud, DerrotaPorKO, DerrotaPorSalud }

/// <summary>Lo que apuntó un juez: puntos del jugador y del rival, round a round sumados.</summary>
[Serializable]
public struct TarjetaJuez
{
    public int jugador;
    public int rival;
    public override string ToString() => $"{jugador}-{rival}";
}

/// <summary>
/// Rondas del combate (RF-10, RF-11): Preparacion → Round → Descanso → ... → Terminado, con campana 3D al inicio y al
/// final de cada round. Un KO termina el combate en el acto. Los rounds solo parten el tiempo: el combate es uno, así
/// que las caídas del rival no se reinician.
/// Si se acaba el último round sin KO deciden los jueces, como en el boxeo: tres tarjetas con el sistema de 10 puntos.
/// En cada round gana 10 puntos quien más daño hizo (salud que le quitó al otro, en fracción de su máximo) y 9 el otro;
/// si la diferencia es mínima, 10-10. Cada caída le resta un punto a quien cayó. Los tres jueces no ven igual un round
/// parejo (cada uno tiene un pequeño sesgo), así que la decisión puede ser unánime, mayoritaria o dividida. Gana quien
/// tenga más jueces a favor y, si empatan, gana el jugador (no hay round extra). El resultado sigue llamándose
/// VictoriaPorSalud / DerrotaPorSalud en ResultadoCombate para no romper los registros ya guardados.
/// En cada descanso el rival se levanta, camina a su esquina (la roja) y baja la guardia, y el jugador recupera salud.
/// Fuera de los rounds el rival no ataca ni recibe golpes. A falta de diez segundos suena la claqueta de aviso.
/// El cronómetro usa tiempo escalado, así que se detiene con la pausa (PauseMenu, PausaGuardian), y no avanza mientras
/// el jugador está en el piso. Si llega a cero con una cuenta del réferi en marcha (CuentaEnMarcha), el round espera a
/// que termine. ControlCombate escucha AlTerminarCombate para cerrar la victoria o la derrota.
/// Va en un objeto propio de cada escena de pelea, junto a ControlCombate.
/// </summary>
public class ControlRondas : MonoBehaviour
{
    [Tooltip("Vacío = el Oponente de la escena")]
    [SerializeField] private Oponente oponente;
    [Tooltip("Vacío = el SaludJugador de la escena")]
    [SerializeField] private SaludJugador jugador;
    [Tooltip("Vacío = el RecuperacionJugador de la escena")]
    [SerializeField] private RecuperacionJugador recuperacion;

    [Header("Tiempos")]
    [SerializeField, Min(1)] private int rounds = 3;
    [SerializeField, Min(1f)] private float segundosPorRound = 90f;
    [Tooltip("Segundos de descanso entre rounds")]
    [SerializeField, Min(0f)] private float segundosDescanso = 20f;
    [Tooltip("Segundos antes de la campana del primer round")]
    [SerializeField, Min(0f)] private float segundosPreparacion = 5f;

    [Header("Entre rounds")]
    [Tooltip("Fracción de la salud máxima que recupera el jugador en cada descanso")]
    [SerializeField, Range(0f, 1f)] private float fraccionSaludDescanso = 0.2f;

    [Header("Campana (RF-10)")]
    [Tooltip("Fuente 3D en una esquina del ring")]
    [SerializeField] private AudioSource fuenteCampana;
    [SerializeField] private AudioClip campanaInicio;
    [SerializeField] private AudioClip campanaFin;
    [Tooltip("Dos golpes de claqueta a falta de diez segundos, como hace el cronometrador; vacío = sin aviso")]
    [SerializeField] private AudioClip avisoDiezSegundos;

    [Header("Jueces (decisión)")]
    [Tooltip("Diferencia de daño (en fracción de salud) por debajo de la cual un juez da el round por empatado, 10-10")]
    [SerializeField, Range(0f, 0.2f)] private float margenRoundParejo = 0.015f;
    [Tooltip("Sesgo de cada juez a favor del jugador, en fracción de salud: hace que no puntúen igual un round parejo")]
    [SerializeField] private float[] sesgoJueces = { -0.02f, 0f, 0.02f };

    [Header("Esquina del rival")]
    [Tooltip("Metros que deja hasta cada cuerda cuando descansa en su esquina")]
    [SerializeField, Min(0f)] private float separacionEsquina = 0.8f;

    [SerializeField] private bool registrar = true;

    /// <summary>Empieza un round; el número va de 1 a rounds.</summary>
    public event Action<int> AlIniciarRound;
    /// <summary>Termina un round, también si fue por KO.</summary>
    public event Action<int> AlTerminarRound;
    public event Action<ResultadoCombate> AlTerminarCombate;

    public EstadoRondas Estado { get; private set; } = EstadoRondas.Preparacion;
    /// <summary>Round en curso o, en el descanso, el último jugado; 0 antes del primero.</summary>
    public int RoundActual { get; private set; }
    /// <summary>Lo que le queda al estado actual (preparación, round o descanso).</summary>
    public float SegundosRestantes { get; private set; }
    /// <summary>
    /// El réferi la asigna para avisar de que está contando: el round no se cierra mientras devuelva true.
    /// </summary>
    public Func<bool> CuentaEnMarcha { get; set; }

    /// <summary>Las tarjetas de los jueces con los rounds puntuados hasta ahora (vacías antes del primero).</summary>
    public TarjetaJuez[] Tarjetas => (TarjetaJuez[])tarjetas.Clone();
    /// <summary>Las tarjetas en una línea, puntos del jugador primero: "29-28, 29-28, 28-29".</summary>
    public string TarjetasTexto => string.Join(", ", Array.ConvertAll(tarjetas, t => t.ToString()));
    /// <summary>Cómo fue la decisión si el combate llegó a las tarjetas: "unánime", "mayoritaria", "dividida" o "empate".
    /// Vacío mientras no haya decisión.</summary>
    public string Decision { get; private set; } = "";

    private AtaqueOponente ataque;
    private DefensaOponente defensa;
    private EsquiveOponente esquive;
    private Vector3 posicionInicialRival;
    private Vector3 esquinaRival;
    private TarjetaJuez[] tarjetas = new TarjetaJuez[0];
    // Lo que va del round: daño hecho por cada uno (fracción de salud) y caídas
    private float danoDelJugador, danoDelRival;
    private float saludRivalAnterior, saludJugadorAnterior;
    private int caidasRivalRound, caidasJugadorRound;
    private bool roundPuntuado = true;
    private bool avisoDado;

    private void Awake()
    {
        if (oponente == null) oponente = FindAnyObjectByType<Oponente>();
        if (jugador == null) jugador = FindAnyObjectByType<SaludJugador>();
        if (recuperacion == null) recuperacion = FindAnyObjectByType<RecuperacionJugador>();
        if (oponente == null || jugador == null)
        {
            Debug.LogWarning($"{name}: ControlRondas necesita un Oponente y un SaludJugador en la escena.", this);
            enabled = false;
            return;
        }
        ataque = oponente.GetComponent<AtaqueOponente>();
        defensa = oponente.GetComponent<DefensaOponente>();
        esquive = oponente.GetComponent<EsquiveOponente>();
        SegundosRestantes = segundosPreparacion;
        tarjetas = new TarjetaJuez[Mathf.Max(1, sesgoJueces != null ? sesgoJueces.Length : 0)];
    }

    private void OnEnable()
    {
        if (oponente != null)
        {
            oponente.AlCaer += AlCaerRival;
            oponente.AlCambiarSalud += AlCambiarSaludRival;
        }
        if (jugador != null)
        {
            jugador.AlCambiarSalud += AlCambiarSaludJugador;
            jugador.AlQuedarseSinSalud += AlCaerJugador;
        }
        if (recuperacion != null) recuperacion.AlPerderPorKO += AlPerderPorKO;
    }

    private void OnDisable()
    {
        if (oponente != null)
        {
            oponente.AlCaer -= AlCaerRival;
            oponente.AlCambiarSalud -= AlCambiarSaludRival;
        }
        if (jugador != null)
        {
            jugador.AlCambiarSalud -= AlCambiarSaludJugador;
            jugador.AlQuedarseSinSalud -= AlCaerJugador;
        }
        if (recuperacion != null) recuperacion.AlPerderPorKO -= AlPerderPorKO;
    }

    private void Start()
    {
        posicionInicialRival = oponente.transform.position;
        var ring = FindAnyObjectByType<LimiteRing>();
        esquinaRival = ring != null ? ring.EsquinaRoja(separacionEsquina, posicionInicialRival.y) : posicionInicialRival;
        saludRivalAnterior = oponente.SaludNormalizada;
        saludJugadorAnterior = jugador.SaludNormalizada;
        DetenerPelea(posicionInicialRival);
    }

    private void Update()
    {
        switch (Estado)
        {
            case EstadoRondas.Preparacion:
                SegundosRestantes -= Time.deltaTime;
                if (SegundosRestantes <= 0f) EmpezarRound(1);
                break;

            case EstadoRondas.Round:
                if (!jugador.Derribado) SegundosRestantes = Mathf.Max(0f, SegundosRestantes - Time.deltaTime);
                if (!avisoDado && SegundosRestantes <= 10f && segundosPorRound > 20f)
                {
                    avisoDado = true;
                    Sonar(avisoDiezSegundos);
                }
                if (SegundosRestantes <= 0f && !HayCuenta) TerminarRound();
                break;

            case EstadoRondas.Descanso:
                SegundosRestantes -= Time.deltaTime;
                if (SegundosRestantes <= 0f) EmpezarRound(RoundActual + 1);
                break;
        }
    }

    private bool HayCuenta => jugador.Derribado || (CuentaEnMarcha != null && CuentaEnMarcha());

    private void EmpezarRound(int numero)
    {
        RoundActual = numero;
        Estado = EstadoRondas.Round;
        SegundosRestantes = segundosPorRound;
        oponente.Protegido = false;
        if (ataque != null) ataque.Reanudar();
        EmpezarAPuntuar();
        Sonar(campanaInicio);
        if (registrar) Debug.Log($"Round {numero} de {rounds}: {segundosPorRound:F0} s", this);
        AlIniciarRound?.Invoke(numero);
    }

    private void TerminarRound()
    {
        Sonar(campanaFin);
        if (registrar) Debug.Log($"Fin del round {RoundActual} de {rounds}", this);
        PuntuarRound();
        AlTerminarRound?.Invoke(RoundActual);

        if (RoundActual >= rounds) Terminar(DecidirPorTarjetas());
        else EmpezarDescanso();
    }

    // ---------- Tarjetas de los jueces ----------

    private void EmpezarAPuntuar()
    {
        danoDelJugador = danoDelRival = 0f;
        caidasRivalRound = caidasJugadorRound = 0;
        saludRivalAnterior = oponente.SaludNormalizada;
        saludJugadorAnterior = jugador.SaludNormalizada;
        roundPuntuado = false;
        avisoDado = false;
    }

    // Solo cuenta la salud que baja durante un round: la que el jugador recupera en el descanso o al levantarse no resta
    private void AlCambiarSaludRival(float salud, float maxima)
    {
        float ahora = maxima > 0f ? salud / maxima : 0f;
        if (Estado == EstadoRondas.Round && ahora < saludRivalAnterior) danoDelJugador += saludRivalAnterior - ahora;
        saludRivalAnterior = ahora;
    }

    private void AlCambiarSaludJugador(float salud, float maxima)
    {
        float ahora = maxima > 0f ? salud / maxima : 0f;
        if (Estado == EstadoRondas.Round && ahora < saludJugadorAnterior) danoDelRival += saludJugadorAnterior - ahora;
        saludJugadorAnterior = ahora;
    }

    private void AlCaerJugador()
    {
        if (Estado == EstadoRondas.Round) caidasJugadorRound++;
    }

    // Sistema de 10 puntos: 10 para quien ganó el round y 9 para el otro (10-10 si fue parejo), menos uno por caída
    private void PuntuarRound()
    {
        if (roundPuntuado) return;
        roundPuntuado = true;
        for (int j = 0; j < tarjetas.Length; j++)
        {
            float sesgo = sesgoJueces != null && j < sesgoJueces.Length ? sesgoJueces[j] : 0f;
            float ventaja = danoDelJugador - danoDelRival + sesgo;
            int puntosJugador = ventaja < -margenRoundParejo ? 9 : 10;
            int puntosRival = ventaja > margenRoundParejo ? 9 : 10;
            tarjetas[j].jugador += Mathf.Max(6, puntosJugador - caidasJugadorRound);
            tarjetas[j].rival += Mathf.Max(6, puntosRival - caidasRivalRound);
        }
        if (registrar)
            Debug.Log($"Round {RoundActual} puntuado: daño del jugador {danoDelJugador:P0}, del rival {danoDelRival:P0}, caídas {caidasJugadorRound}/{caidasRivalRound} → tarjetas {TarjetasTexto}", this);
    }

    // Gana quien tenga más jueces a favor. El empate es del jugador: repetir el round alargaría un combate que el
    // informe fija en 3 rounds
    private ResultadoCombate DecidirPorTarjetas()
    {
        int aFavor = 0, enContra = 0;
        foreach (TarjetaJuez t in tarjetas)
        {
            if (t.jugador > t.rival) aFavor++;
            else if (t.rival > t.jugador) enContra++;
        }
        int total = tarjetas.Length;
        if (aFavor == enContra) Decision = "empate";
        else if (Mathf.Max(aFavor, enContra) == total) Decision = "unánime";
        else if (Mathf.Min(aFavor, enContra) == 0) Decision = "mayoritaria";
        else Decision = "dividida";
        return aFavor >= enContra ? ResultadoCombate.VictoriaPorSalud : ResultadoCombate.DerrotaPorSalud;
    }

    private void EmpezarDescanso()
    {
        Estado = EstadoRondas.Descanso;
        SegundosRestantes = segundosDescanso;
        DetenerPelea(esquinaRival);
        if (oponente.Caido && !oponente.Derrotado) oponente.Levantarse();
        if (defensa != null) defensa.Reiniciar();
        if (esquive != null) esquive.Reiniciar();
        jugador.EstablecerSalud(jugador.Salud + jugador.SaludMaxima * fraccionSaludDescanso);
        if (registrar) Debug.Log($"Descanso de {segundosDescanso:F0} s: jugador con {jugador.Salud:F0}/{jugador.SaludMaxima:F0} de salud", this);
    }

    private void AlCaerRival(Golpe golpe)
    {
        if (Estado == EstadoRondas.Round) caidasRivalRound++;
        if (oponente.Derrotado) TerminarPorKO(ResultadoCombate.VictoriaPorKO);
    }

    private void AlPerderPorKO() => TerminarPorKO(ResultadoCombate.DerrotaPorKO);

    private void TerminarPorKO(ResultadoCombate resultado)
    {
        if (Estado == EstadoRondas.Terminado) return;
        if (Estado == EstadoRondas.Round)
        {
            Sonar(campanaFin);
            PuntuarRound();
            AlTerminarRound?.Invoke(RoundActual);
        }
        Terminar(resultado);
    }

    private void Terminar(ResultadoCombate resultado)
    {
        Estado = EstadoRondas.Terminado;
        SegundosRestantes = 0f;
        DetenerPelea(null);
        if (registrar)
            Debug.Log($"Combate terminado en el round {RoundActual}: {resultado} (jugador {jugador.SaludNormalizada:P0}, {oponente.Nombre} {oponente.SaludNormalizada:P0}; tarjetas {TarjetasTexto}{(Decision != "" ? ", decisión " + Decision : "")})", this);
        AlTerminarCombate?.Invoke(resultado);
    }

    private void DetenerPelea(Vector3? destinoRival)
    {
        oponente.Protegido = true;
        if (ataque != null) ataque.Pausar(destinoRival);
    }

    private void Sonar(AudioClip clip)
    {
        if (fuenteCampana != null && clip != null) fuenteCampana.PlayOneShot(clip);
    }

    // Para probar sin visor: en Play, clic derecho en el título del componente
    [ContextMenu("Probar: acabar el round")]
    private void ProbarAcabarRound() => SegundosRestantes = 0f;

    [ContextMenu("Probar: saltar al round 3")]
    private void ProbarSaltarUltimoRound()
    {
        if (PuedeProbar()) EmpezarRound(rounds);
    }

    [ContextMenu("Probar: ganar por salud")]
    private void ProbarGanarPorSalud() => ProbarFinal(0.8f, 0.4f);

    [ContextMenu("Probar: perder por salud")]
    private void ProbarPerderPorSalud() => ProbarFinal(0.2f, 0.6f);

    // Fija las saludes y deja el último round a cero, para que se cierre y lo decidan los jueces por el camino normal
    private void ProbarFinal(float saludJugador, float saludRival)
    {
        if (!PuedeProbar()) return;
        // Como si esas saludes fueran el daño del último round: así los jueces lo puntúan por el camino normal
        RoundActual = rounds;
        Estado = EstadoRondas.Round;
        EmpezarAPuntuar();
        jugador.EstablecerSalud(jugador.SaludMaxima * saludJugador);
        oponente.EstablecerSalud(oponente.Perfil.saludMaxima * saludRival);
        SegundosRestantes = 0f;
    }

    private bool PuedeProbar()
    {
        if (Estado != EstadoRondas.Terminado && !jugador.Derribado) return true;
        Debug.LogWarning($"{name}: no se puede probar con el combate terminado o el jugador en el piso.", this);
        return false;
    }
}
