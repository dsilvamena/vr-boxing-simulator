using System;
using UnityEngine;

/// <summary>
/// Recibe los golpes de las ZonaGolpe hijas y los convierte en daño sobre su salud.
/// Cae al cruzar los umbrales de salud (knockdowns); el último umbral (0) es la derrota.
/// Cómo se ve la reacción lo decide otro componente (ReaccionAnimada o ReaccionProcedural).
/// </summary>
public class Oponente : MonoBehaviour
{
    [SerializeField] private PerfilOponente perfil;
    [SerializeField] private AudioSource audioSource;
    [Tooltip("Escribe cada golpe en la consola (tipo, zona, velocidad, daño, salud)")]
    [SerializeField] private bool registrarGolpes = true;

    public event Action<Golpe> AlRecibirGolpe;
    /// <summary>En cada caída, también en la derrota (mirar Derrotado).</summary>
    public event Action<Golpe> AlCaer;
    public event Action AlLevantarse;
    /// <summary>Salud actual y salud máxima, cada vez que cambia.</summary>
    public event Action<float, float> AlCambiarSalud;

    public PerfilOponente Perfil => perfil;
    /// <summary>Nombre que ve el jugador: el del perfil o, si está vacío, el del objeto.</summary>
    public string Nombre => perfil != null && !string.IsNullOrEmpty(perfil.nombre) ? perfil.nombre : name;
    /// <summary>Golpes recibidos en total (para la precisión, RF-18).</summary>
    public int Golpes { get; private set; }
    public int Caidas { get; private set; }
    public bool Caido { get; private set; }
    /// <summary>Cayó por última vez: ya no se levanta.</summary>
    public bool Derrotado { get; private set; }
    public float Salud { get; private set; }
    public float SaludNormalizada => perfil.saludMaxima > 0f ? Salud / perfil.saludMaxima : 0f;

    private float invulnerableHasta;
    private float levantarseEn;

    private void Awake()
    {
        if (perfil == null)
        {
            Debug.LogWarning($"{name}: Oponente sin PerfilOponente, se usan valores por defecto.", this);
            perfil = ScriptableObject.CreateInstance<PerfilOponente>();
        }
        Salud = perfil.saludMaxima;
    }

    private void Update()
    {
        if (Caido && !Derrotado && Time.time >= levantarseEn) Levantarse();
    }

    /// <summary>Devuelve false si el golpe no cuenta (está caído o aún invulnerable).</summary>
    public bool RecibirGolpe(Golpe golpe)
    {
        if (Caido || Time.time < invulnerableHasta) return false;
        invulnerableHasta = Time.time + perfil.invulnerabilidad;

        // Satura en rapidezGolpeCompleto: no hace falta pegar con toda la fuerza
        golpe.intensidad = Mathf.Clamp01(golpe.Rapidez / perfil.rapidezGolpeCompleto);
        Golpes++;

        float dano = CalcularDano(golpe);
        Salud = Mathf.Max(0f, Salud - dano);

        if (registrarGolpes)
            Debug.Log($"{name}: {golpe.tipo} ({golpe.mano}) en {(golpe.zona != null ? golpe.zona.Zona.ToString() : "-")}, {golpe.Rapidez:F1} m/s → daño {dano:F1}, salud {Salud:F0}/{perfil.saludMaxima:F0}, caídas {Caidas}/{perfil.caidasParaDerrota}", this);

        Sonar(perfil.sonidosGolpe, golpe.punto);
        AlRecibirGolpe?.Invoke(golpe);
        AlCambiarSalud?.Invoke(Salud, perfil.saludMaxima);

        // Umbral de la próxima caída: con 3 caídas → 66 %, 33 % y 0 %
        float umbral = perfil.saludMaxima * (1f - (Caidas + 1f) / perfil.caidasParaDerrota);
        if (Salud <= umbral)
        {
            Salud = umbral; // un golpe fuerte no se salta umbrales
            Caidas++;
            Caido = true;
            Derrotado = Caidas >= perfil.caidasParaDerrota;
            levantarseEn = Time.time + perfil.segundosKnockdown;
            if (registrarGolpes) Debug.Log($"{name}: {(Derrotado ? "derrotado" : $"knockdown {Caidas}")}", this);
            if (perfil.sonidoKO != null) Sonar(new[] { perfil.sonidoKO }, golpe.punto);
            AlCaer?.Invoke(golpe);
            AlCambiarSalud?.Invoke(Salud, perfil.saludMaxima);
        }
        return true;
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
        AlLevantarse?.Invoke();
    }

    /// <summary>Vuelve al estado inicial (otro combate o ronda).</summary>
    public void Reiniciar()
    {
        Golpes = 0;
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
