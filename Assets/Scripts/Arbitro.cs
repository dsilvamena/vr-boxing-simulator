using System;
using UnityEngine;

/// <summary>
/// Réferi (RF-12, RF-16): cuenta en voz alta del 1 al 10, en 3D desde su posición, cuando cae el jugador y cuando cae
/// el rival en un knockdown. La caída final del rival (Derrotado) no se cuenta: termina el combate en el acto.
/// Jugador: la cuenta, el tiempo y el button mashing siguen siendo de RecuperacionJugador; aquí solo se pone la voz.
/// Rival: mientras hay réferi, el rival no se levanta con su temporizador (Oponente.LevantaArbitro), sino cuando llega
/// al número cuentaKnockdown de su perfil. El round que llega a cero espera a esa cuenta (ControlRondas.CuentaEnMarcha).
/// Si alguien más lo levanta (el descanso, una prueba), la cuenta se cancela y la voz se calla.
/// La cuenta usa tiempo escalado y la voz se pausa a mano, porque un AudioSource no se detiene con timeScale = 0.
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

    [Header("Cuenta del rival")]
    [Tooltip("Segundos entre números. La del jugador va al ritmo de segundosParaLevantarse de RecuperacionJugador")]
    [SerializeField, Min(0.1f)] private float segundosPorNumero = 1f;

    [SerializeField] private bool registrar = true;

    /// <summary>Dice un número de la cuenta (1 a 10); el bool es true si cuenta al jugador. CuerpoArbitro hace el gesto con él.</summary>
    public event Action<int, bool> AlDecirNumero;

    public bool ContandoRival { get; private set; }
    public bool ContandoJugador => recuperacion != null && recuperacion.Contando;
    /// <summary>Último número dicho en la cuenta actual; 0 si no hay cuenta.</summary>
    public int NumeroActual { get; private set; }

    private float segundosRival;
    private float duracionRival;
    private int ultimoNumeroRival;
    private bool vozPausada;
    private bool combateTerminado;

    private void Awake()
    {
        if (oponente == null) oponente = FindAnyObjectByType<Oponente>();
        if (jugador == null) jugador = FindAnyObjectByType<SaludJugador>();
        if (recuperacion == null) recuperacion = FindAnyObjectByType<RecuperacionJugador>();
        if (rondas == null) rondas = FindAnyObjectByType<ControlRondas>();
        if (voz == null) voz = GetComponentInChildren<AudioSource>();
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
        if (recuperacion != null) recuperacion.AlLevantarse += Callar;
        if (rondas != null)
        {
            rondas.CuentaEnMarcha = HayCuentaDelRival;
            rondas.AlTerminarCombate += AlTerminarCombate;
        }
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
        if (recuperacion != null) recuperacion.AlLevantarse -= Callar;
        if (rondas != null)
        {
            rondas.CuentaEnMarcha = null;
            rondas.AlTerminarCombate -= AlTerminarCombate;
        }
    }

    private void Update()
    {
        PausarVoz(Time.timeScale == 0f);
        if (Time.timeScale == 0f) return;

        if (ContandoRival)
        {
            segundosRival += Time.deltaTime;
            if (segundosRival >= duracionRival) TerminarCuentaRival();
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
        if (ContandoRival) return Mathf.Min(ultimoNumeroRival, Mathf.FloorToInt(segundosRival / segundosPorNumero) + 1);
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
        ultimoNumeroRival = Mathf.Clamp(oponente.Perfil.cuentaKnockdown, 1, NumerosPorCuenta);
        duracionRival = ultimoNumeroRival * segundosPorNumero;
        segundosRival = 0f;
        ContandoRival = true;
        Callar();
        if (registrar) Debug.Log($"Réferi: cuenta a {oponente.Nombre} hasta {ultimoNumeroRival} ({duracionRival:F0} s)", this);
    }

    private void TerminarCuentaRival()
    {
        ContandoRival = false;
        if (registrar) Debug.Log($"Réferi: {oponente.Nombre} se levanta al terminar la cuenta", this);
        oponente.Levantarse();
    }

    // Se levantó con la cuenta a medias (descanso, prueba): la cuenta ya no tiene sentido
    private void AlLevantarseRival()
    {
        if (!ContandoRival) return;
        ContandoRival = false;
        Callar();
        if (registrar) Debug.Log($"Réferi: cuenta a {oponente.Nombre} cancelada", this);
    }

    private void AlTerminarCombate(ResultadoCombate resultado)
    {
        combateTerminado = true;
        Callar();
        if (ContandoRival) TerminarCuentaRival();
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
