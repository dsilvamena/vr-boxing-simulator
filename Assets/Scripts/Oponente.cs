using System;
using UnityEngine;

/// <summary>
/// Recibe los golpes de las ZonaGolpe hijas, lleva la vida y avisa (golpe, KO, levantarse).
/// Cómo se ve la reacción lo decide otro componente (ReaccionProcedural, y más adelante una por Animator).
/// </summary>
public class Oponente : MonoBehaviour
{
    [SerializeField] private PerfilOponente perfil;
    [SerializeField] private AudioSource audioSource;
    [Tooltip("Escribe cada golpe en la consola (tipo, zona, velocidad, daño)")]
    [SerializeField] private bool registrarGolpes = true;

    public event Action<Golpe> AlRecibirGolpe;
    public event Action<Golpe> AlCaer;
    public event Action AlLevantarse;

    public PerfilOponente Perfil => perfil;
    public float Vida { get; private set; }
    public bool Caido { get; private set; }

    private float invulnerableHasta;
    private float levantarseEn;

    private void Awake()
    {
        if (perfil == null)
        {
            Debug.LogWarning($"{name}: Oponente sin PerfilOponente, se usan valores por defecto.", this);
            perfil = ScriptableObject.CreateInstance<PerfilOponente>();
        }
        Vida = perfil.vidaMaxima;
    }

    private void Update()
    {
        if (Caido && perfil.segundosKO > 0f && Time.time >= levantarseEn) Levantarse();
    }

    /// <summary>Devuelve false si el golpe no cuenta (está caído o aún invulnerable).</summary>
    public bool RecibirGolpe(Golpe golpe)
    {
        if (Caido || Time.time < invulnerableHasta) return false;
        invulnerableHasta = Time.time + perfil.invulnerabilidad;

        float mult = golpe.zona != null ? golpe.zona.Multiplicador : 1f;
        // Satura en rapidezGolpeCompleto: no hace falta pegar con toda la fuerza
        golpe.intensidad = Mathf.Clamp01(golpe.Rapidez / perfil.rapidezGolpeCompleto);
        if (golpe.tipo == TipoGolpe.Lateral) mult *= perfil.multiplicadorLateral;
        golpe.danio = perfil.danioGolpe * golpe.intensidad * mult;
        Vida = Mathf.Max(0f, Vida - golpe.danio);

        if (registrarGolpes)
            Debug.Log($"{name}: {golpe.tipo} ({golpe.mano}) en {(golpe.zona != null ? golpe.zona.Zona.ToString() : "-")},{golpe.Rapidez:F1} m/s → {golpe.danio:F0} de daño, vida {Vida:F0}", this);

        Sonar(perfil.sonidosGolpe, golpe.punto);
        AlRecibirGolpe?.Invoke(golpe);

        if (Vida <= 0f)
        {
            Caido = true;
            levantarseEn = Time.time + perfil.segundosKO;
            if (perfil.sonidoKO != null) Sonar(new[] { perfil.sonidoKO }, golpe.punto);
            AlCaer?.Invoke(golpe);
        }
        return true;
    }

    public void Levantarse()
    {
        Caido = false;
        Vida = perfil.vidaMaxima;
        AlLevantarse?.Invoke();
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
