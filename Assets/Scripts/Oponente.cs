using System;
using UnityEngine;

/// <summary>
/// Recibe los golpes de las ZonaGolpe hijas, cuenta los golpes y las caídas y avisa (golpe, caída, levantarse).
/// Cada golpesPorCaida golpes se cae; las primeras son knockdowns y se levanta, la de caidasParaDerrota es la derrota.
/// Cómo se ve la reacción lo decide otro componente (ReaccionAnimada o ReaccionProcedural).
/// </summary>
public class Oponente : MonoBehaviour
{
    [SerializeField] private PerfilOponente perfil;
    [SerializeField] private AudioSource audioSource;
    [Tooltip("Escribe cada golpe en la consola (tipo, zona, velocidad, conteo)")]
    [SerializeField] private bool registrarGolpes = true;

    public event Action<Golpe> AlRecibirGolpe;
    /// <summary>En cada caída, también en la derrota (mirar Derrotado).</summary>
    public event Action<Golpe> AlCaer;
    public event Action AlLevantarse;

    public PerfilOponente Perfil => perfil;
    /// <summary>Golpes recibidos desde la última caída.</summary>
    public int Golpes { get; private set; }
    public int Caidas { get; private set; }
    public bool Caido { get; private set; }
    /// <summary>Cayó por última vez: ya no se levanta.</summary>
    public bool Derrotado { get; private set; }

    private float invulnerableHasta;
    private float levantarseEn;

    private void Awake()
    {
        if (perfil == null)
        {
            Debug.LogWarning($"{name}: Oponente sin PerfilOponente, se usan valores por defecto.", this);
            perfil = ScriptableObject.CreateInstance<PerfilOponente>();
        }
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

        if (registrarGolpes)
            Debug.Log($"{name}: {golpe.tipo} ({golpe.mano}) en {(golpe.zona != null ? golpe.zona.Zona.ToString() : "-")}, {golpe.Rapidez:F1} m/s → golpe {Golpes}/{perfil.golpesPorCaida}, caídas {Caidas}/{perfil.caidasParaDerrota}", this);

        Sonar(perfil.sonidosGolpe, golpe.punto);
        AlRecibirGolpe?.Invoke(golpe);

        if (Golpes >= perfil.golpesPorCaida)
        {
            Golpes = 0;
            Caidas++;
            Caido = true;
            Derrotado = Caidas >= perfil.caidasParaDerrota;
            levantarseEn = Time.time + perfil.segundosKnockdown;
            if (registrarGolpes) Debug.Log($"{name}: {(Derrotado ? "derrotado" : $"knockdown {Caidas}")}", this);
            if (perfil.sonidoKO != null) Sonar(new[] { perfil.sonidoKO }, golpe.punto);
            AlCaer?.Invoke(golpe);
        }
        return true;
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
