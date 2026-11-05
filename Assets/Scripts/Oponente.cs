using System;
using UnityEngine;

/// <summary>
/// Recibe los golpes de las ZonaGolpe hijas y los convierte en daño sobre su salud.
/// Cae al cruzar los umbrales de salud (knockdowns); el último umbral (0) es la derrota.
/// Cómo se ve la reacción lo decide otro componente (ReaccionAnimada o ReaccionProcedural).
/// Si tiene DefensaOponente, esta decide qué golpes bloquea: un golpe bloqueado quita poca salud, nunca lo tumba
/// y lanza AlBloquear en vez de AlRecibirGolpe (así no hay animación de reacción).
/// Si tiene EsquiveOponente, primero le pregunta si esquiva: un golpe esquivado no daña ni cuenta como golpe seguido
/// para DefensaOponente, y lanza AlEsquivar.
/// Tras un knockdown se levanta cuando termina la cuenta del réferi (Arbitro) o, si no hay réferi, a los segundosKnockdown.
/// </summary>
public class Oponente : MonoBehaviour
{
    [SerializeField] private PerfilOponente perfil;
    [Tooltip("Fuente 3D (spatialBlend 1) para los golpes; vacío = suena en el punto del golpe")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("Escribe cada golpe en la consola (tipo, zona, velocidad, daño, salud)")]
    [SerializeField] private bool registrarGolpes = true;

    public event Action<Golpe> AlRecibirGolpe;
    /// <summary>Paró un golpe con la guardia.</summary>
    public event Action<Golpe> AlBloquear;
    /// <summary>Esquivó un golpe: no le hizo daño.</summary>
    public event Action<Golpe> AlEsquivar;
    /// <summary>En cada caída, también en la derrota (mirar Derrotado).</summary>
    public event Action<Golpe> AlCaer;
    public event Action AlLevantarse;
    /// <summary>Salud actual y salud máxima, cada vez que cambia.</summary>
    public event Action<float, float> AlCambiarSalud;

    public PerfilOponente Perfil => perfil;
    /// <summary>Nombre que ve el jugador: el del perfil o, si está vacío, el del objeto.</summary>
    public string Nombre => perfil != null && !string.IsNullOrEmpty(perfil.nombre) ? perfil.nombre : name;
    /// <summary>Golpes conectados en total (para la precisión, RF-18).</summary>
    public int Golpes { get; private set; }
    /// <summary>Golpes del jugador que bloqueó.</summary>
    public int Bloqueos { get; private set; }
    /// <summary>Golpes del jugador que esquivó.</summary>
    public int Esquives { get; private set; }
    public int Caidas { get; private set; }
    public bool Caido { get; private set; }
    /// <summary>Cayó por última vez: ya no se levanta.</summary>
    public bool Derrotado { get; private set; }
    public float Salud { get; private set; }
    public float SaludNormalizada => perfil.saludMaxima > 0f ? Salud / perfil.saludMaxima : 0f;
    /// <summary>Los golpes no cuentan: ControlRondas lo activa fuera de los rounds.</summary>
    public bool Protegido { get; set; }
    /// <summary>Hay réferi: se levanta cuando él lo decide y no con segundosKnockdown. Lo mantiene Arbitro.</summary>
    public bool LevantaArbitro { get; set; }
    /// <summary>Aún no recibe otro golpe: acaba de recibir uno o se está incorporando.</summary>
    public bool Invulnerable => Time.time < invulnerableHasta;

    private float invulnerableHasta;
    private float levantarseEn;
    private DefensaOponente defensa;
    private EsquiveOponente esquive;

    private void Awake()
    {
        if (perfil == null)
        {
            Debug.LogWarning($"{name}: Oponente sin PerfilOponente, se usan valores por defecto.", this);
            perfil = ScriptableObject.CreateInstance<PerfilOponente>();
        }
        Salud = perfil.saludMaxima;
        defensa = GetComponent<DefensaOponente>();
        esquive = GetComponent<EsquiveOponente>();
    }

    private void Update()
    {
        if (Caido && !Derrotado && !LevantaArbitro && Time.time >= levantarseEn) Levantarse();
    }

    /// <summary>Devuelve NoCuenta si está caído, protegido o aún invulnerable; si no, si el golpe conectó, lo bloqueó o lo esquivó.</summary>
    public ResultadoGolpe RecibirGolpe(Golpe golpe)
    {
        if (Caido || Protegido || Invulnerable) return ResultadoGolpe.NoCuenta;
        // Antes de fijar la invulnerabilidad: EsquiveOponente la consulta para no esquivar mientras se incorpora
        bool esquivado = esquive != null && esquive.Esquiva(golpe);
        invulnerableHasta = Time.time + perfil.invulnerabilidad;

        if (esquivado)
        {
            Esquives++;
            if (registrarGolpes)
                Debug.Log($"{name}: {golpe.tipo} ({golpe.mano}) en {(golpe.zona != null ? golpe.zona.Zona.ToString() : "-")}, {golpe.Rapidez:F1} m/s → ESQUIVADO, salud {Salud:F0}/{perfil.saludMaxima:F0}", this);
            AlEsquivar?.Invoke(golpe);
            return ResultadoGolpe.Esquivado;
        }

        // Satura en rapidezGolpeCompleto: no hace falta pegar con toda la fuerza
        golpe.intensidad = Mathf.Clamp01(golpe.Rapidez / perfil.rapidezGolpeCompleto);
        bool bloqueado = defensa != null && defensa.Bloquea(golpe);

        float dano = CalcularDano(golpe) * (bloqueado ? perfil.danoTrasBloqueo : 1f);
        float umbral = UmbralProximaCaida;
        // Bloqueado resta un poco pero nunca cruza el umbral: solo los golpes que entran tumban
        Salud = bloqueado ? Mathf.Max(Salud - dano, Mathf.Min(Salud, umbral + 1f)) : Mathf.Max(0f, Salud - dano);

        if (registrarGolpes)
            Debug.Log($"{name}: {golpe.tipo} ({golpe.mano}) en {(golpe.zona != null ? golpe.zona.Zona.ToString() : "-")}, {golpe.Rapidez:F1} m/s → {(bloqueado ? "BLOQUEADO, " : "")}daño {dano:F1}, salud {Salud:F0}/{perfil.saludMaxima:F0}, caídas {Caidas}/{perfil.caidasParaDerrota}", this);

        if (bloqueado)
        {
            Bloqueos++;
            Sonar(new[] { perfil.sonidoBloqueo }, golpe.punto);
            AlBloquear?.Invoke(golpe);
            AlCambiarSalud?.Invoke(Salud, perfil.saludMaxima);
            return ResultadoGolpe.Bloqueado;
        }

        Golpes++;
        Sonar(perfil.sonidosGolpe, golpe.punto);
        AlRecibirGolpe?.Invoke(golpe);
        AlCambiarSalud?.Invoke(Salud, perfil.saludMaxima);

        if (Salud <= umbral) Caer(golpe);
        return ResultadoGolpe.Conectado;
    }

    /// <summary>Lo tumba en la siguiente caída, sin golpe (para probar la cuenta del réferi).</summary>
    public void Derribar()
    {
        if (Caido) return;
        Caer(new Golpe { tipo = TipoGolpe.Cross, velocidad = -transform.forward, punto = transform.position + Vector3.up * perfil.alturaAtaque });
    }

    // Umbral de la próxima caída: con 3 caídas → 66 %, 33 % y 0 %
    private float UmbralProximaCaida => perfil.saludMaxima * (1f - (Caidas + 1f) / perfil.caidasParaDerrota);

    private void Caer(Golpe golpe)
    {
        Salud = UmbralProximaCaida; // un golpe fuerte no se salta umbrales
        Caidas++;
        Caido = true;
        Derrotado = Caidas >= perfil.caidasParaDerrota;
        levantarseEn = Time.time + perfil.segundosKnockdown;
        if (registrarGolpes) Debug.Log($"{name}: {(Derrotado ? "derrotado" : $"knockdown {Caidas}")}", this);
        if (perfil.sonidoKO != null) Sonar(new[] { perfil.sonidoKO }, golpe.punto);
        AlCaer?.Invoke(golpe);
        AlCambiarSalud?.Invoke(Salud, perfil.saludMaxima);
    }

    private float CalcularDano(Golpe golpe)
    {
        float baseDano = golpe.tipo switch
        {
            TipoGolpe.Jab => perfil.danoJab,
            TipoGolpe.Cross => perfil.danoCross,
            TipoGolpe.Uppercut => perfil.danoUppercut,
            _ => perfil.danoLateral,
        };

        float factorRapidez = Mathf.Lerp(perfil.danoMinimoPorRapidez, 1f, golpe.intensidad);
        bool cabeza = golpe.zona != null && golpe.zona.Zona == TipoZona.Cabeza;
        return baseDano * factorRapidez * (cabeza ? perfil.multiplicadorCabeza : 1f);
    }

    public void Levantarse()
    {
        Caido = false;
        // Tras una cuenta la animación sale del piso a partir de aquí: mientras se incorpora, no recibe golpes
        if (LevantaArbitro) invulnerableHasta = Time.time + perfil.segundosAlLevantarse;
        AlLevantarse?.Invoke();
    }

    /// <summary>Fija la salud sin caídas ni reacción (para probar la decisión por salud).</summary>
    public void EstablecerSalud(float valor)
    {
        Salud = Mathf.Clamp(valor, 0f, perfil.saludMaxima);
        AlCambiarSalud?.Invoke(Salud, perfil.saludMaxima);
    }

    /// <summary>Vuelve al estado inicial (otro combate o ronda).</summary>
    public void Reiniciar()
    {
        Golpes = 0;
        Bloqueos = 0;
        Esquives = 0;
        Caidas = 0;
        Derrotado = false;
        Salud = perfil.saludMaxima;
        AlCambiarSalud?.Invoke(Salud, perfil.saludMaxima);
        if (Caido) Levantarse();
    }

    private void Sonar(AudioClip[] clips, Vector3 punto)
    {
        if (clips == null || clips.Length == 0) return;
        AudioClip clip = clips[UnityEngine.Random.Range(0, clips.Length)];
        if (clip == null) return;
        if (audioSource != null) audioSource.PlayOneShot(clip);
        else AudioSource.PlayClipAtPoint(clip, punto);
    }
}
