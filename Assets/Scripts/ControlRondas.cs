using System;
using UnityEngine;

public enum EstadoRondas { Preparacion, Round, Descanso, Terminado }

public enum ResultadoCombate { VictoriaPorKO, VictoriaPorSalud, DerrotaPorKO, DerrotaPorSalud }

/// <summary>
/// Rondas del combate (RF-10, RF-11): Preparacion → Round → Descanso → ... → Terminado, con campana 3D al inicio y al
/// final de cada round. Un KO termina el combate en el acto; si se acaba el último round sin KO, gana quien tenga más
/// salud en porcentaje y, si empatan, gana el jugador. Los rounds solo parten el tiempo: el combate es uno, así que las
/// caídas del rival no se reinician.
/// En cada descanso el rival se levanta, vuelve a su posición inicial y baja la guardia, y el jugador recupera salud.
/// Fuera de los rounds el rival no ataca ni recibe golpes.
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

    private AtaqueOponente ataque;
    private DefensaOponente defensa;
    private Vector3 posicionInicialRival;

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
        SegundosRestantes = segundosPreparacion;
    }

    private void OnEnable()
    {
        if (oponente != null) oponente.AlCaer += AlCaerRival;
        if (recuperacion != null) recuperacion.AlPerderPorKO += AlPerderPorKO;
    }

    private void OnDisable()
    {
        if (oponente != null) oponente.AlCaer -= AlCaerRival;
        if (recuperacion != null) recuperacion.AlPerderPorKO -= AlPerderPorKO;
    }

    private void Start()
    {
        posicionInicialRival = oponente.transform.position;
        DetenerPelea(volverAlInicio: true);
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
        Sonar(campanaInicio);
        if (registrar) Debug.Log($"Round {numero} de {rounds}: {segundosPorRound:F0} s", this);
        AlIniciarRound?.Invoke(numero);
    }

    private void TerminarRound()
    {
        Sonar(campanaFin);
        if (registrar) Debug.Log($"Fin del round {RoundActual} de {rounds}", this);
        AlTerminarRound?.Invoke(RoundActual);

        if (RoundActual >= rounds) Terminar(DecidirPorSalud());
        else EmpezarDescanso();
    }

    private void EmpezarDescanso()
    {
        Estado = EstadoRondas.Descanso;
        SegundosRestantes = segundosDescanso;
        DetenerPelea(volverAlInicio: true);
        if (oponente.Caido && !oponente.Derrotado) oponente.Levantarse();
        if (defensa != null) defensa.Reiniciar();
        jugador.EstablecerSalud(jugador.Salud + jugador.SaludMaxima * fraccionSaludDescanso);
        if (registrar) Debug.Log($"Descanso de {segundosDescanso:F0} s: jugador con {jugador.Salud:F0}/{jugador.SaludMaxima:F0} de salud", this);
    }

    // El empate es del jugador: repetir el round alargaría un combate que el informe fija en 3 rounds
    private ResultadoCombate DecidirPorSalud() =>
        jugador.SaludNormalizada >= oponente.SaludNormalizada ? ResultadoCombate.VictoriaPorSalud : ResultadoCombate.DerrotaPorSalud;

    private void AlCaerRival(Golpe golpe)
    {
        if (oponente.Derrotado) TerminarPorKO(ResultadoCombate.VictoriaPorKO);
    }

    private void AlPerderPorKO() => TerminarPorKO(ResultadoCombate.DerrotaPorKO);

    private void TerminarPorKO(ResultadoCombate resultado)
    {
        if (Estado == EstadoRondas.Terminado) return;
        if (Estado == EstadoRondas.Round)
        {
            Sonar(campanaFin);
            AlTerminarRound?.Invoke(RoundActual);
        }
        Terminar(resultado);
    }

    private void Terminar(ResultadoCombate resultado)
    {
        Estado = EstadoRondas.Terminado;
        SegundosRestantes = 0f;
        DetenerPelea(volverAlInicio: false);
        if (registrar)
            Debug.Log($"Combate terminado en el round {RoundActual}: {resultado} (jugador {jugador.SaludNormalizada:P0}, {oponente.Nombre} {oponente.SaludNormalizada:P0})", this);
        AlTerminarCombate?.Invoke(resultado);
    }

    private void DetenerPelea(bool volverAlInicio)
    {
        oponente.Protegido = true;
        if (ataque != null) ataque.Pausar(volverAlInicio ? posicionInicialRival : null);
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

    // Fija las saludes y deja el último round a cero, para que se cierre y se decida por el camino normal
    private void ProbarFinal(float saludJugador, float saludRival)
    {
        if (!PuedeProbar()) return;
        jugador.EstablecerSalud(jugador.SaludMaxima * saludJugador);
        oponente.EstablecerSalud(oponente.Perfil.saludMaxima * saludRival);
        RoundActual = rounds;
        Estado = EstadoRondas.Round;
        SegundosRestantes = 0f;
    }

    private bool PuedeProbar()
    {
        if (Estado != EstadoRondas.Terminado && !jugador.Derribado) return true;
        Debug.LogWarning($"{name}: no se puede probar con el combate terminado o el jugador en el piso.", this);
        return false;
    }
}
