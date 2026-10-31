using UnityEngine;

/// <summary>
/// RF-16: la multitud alrededor del ring, con audio espacial 3D. Crea varias fuentes en círculo (las gradas) que
/// repiten el murmullo, cada una desde un punto distinto del clip para que no suenen al unísono.
/// Ovaciona cuando el rival cae o se derrumba el jugador, desde las fuentes más cercanas a quien cayó.
/// Va en un objeto propio en el centro del ring, en cada escena de pelea.
/// </summary>
public class AmbienteMultitud : MonoBehaviour
{
    [SerializeField] private AudioClip murmullo;
    [SerializeField] private AudioClip ovacion;
    [Tooltip("Vacío = el Oponente de la escena")]
    [SerializeField] private Oponente oponente;
    [Tooltip("Vacío = el SaludJugador de la escena")]
    [SerializeField] private SaludJugador jugador;

    [Header("Fuentes")]
    [SerializeField, Min(1)] private int fuentes = 4;
    [Tooltip("Distancia de cada fuente al centro del ring, en metros")]
    [SerializeField] private float radio = 6f;
    [Tooltip("Altura de las fuentes sobre este objeto, en metros")]
    [SerializeField] private float altura = 2.5f;
    [SerializeField, Range(0f, 1f)] private float volumenMurmullo = 0.35f;
    [SerializeField, Range(0f, 1f)] private float volumenOvacion = 0.8f;

    private AudioSource[] gradas;

    private void Awake()
    {
        if (oponente == null) oponente = FindAnyObjectByType<Oponente>();
        if (jugador == null) jugador = FindAnyObjectByType<SaludJugador>();

        gradas = new AudioSource[fuentes];
        for (int i = 0; i < fuentes; i++)
        {
            float angulo = (i + 0.5f) / fuentes * Mathf.PI * 2f;
            var go = new GameObject($"Grada_{i + 1}");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(Mathf.Cos(angulo) * radio, altura, Mathf.Sin(angulo) * radio);

            var fuente = go.AddComponent<AudioSource>();
            fuente.spatialBlend = 1f;
            fuente.rolloffMode = AudioRolloffMode.Logarithmic;
            fuente.minDistance = 3f;
            fuente.maxDistance = 30f;
            fuente.dopplerLevel = 0f;
            fuente.playOnAwake = false;
            fuente.loop = true;
            fuente.clip = murmullo;
            fuente.volume = volumenMurmullo;
            gradas[i] = fuente;
        }
    }

    private void Start()
    {
        if (murmullo == null) return;
        foreach (AudioSource fuente in gradas)
        {
            fuente.time = Random.Range(0f, murmullo.length);
            fuente.pitch = Random.Range(0.95f, 1.05f);
            fuente.Play();
        }
    }

    private void OnEnable()
    {
        if (oponente != null) oponente.AlCaer += AlCaerOponente;
        if (jugador != null) jugador.AlQuedarseSinSalud += AlCaerJugador;
    }

    private void OnDisable()
    {
        if (oponente != null) oponente.AlCaer -= AlCaerOponente;
        if (jugador != null) jugador.AlQuedarseSinSalud -= AlCaerJugador;
    }

    private void AlCaerOponente(Golpe golpe) => Ovacionar(oponente.transform.position);

    private void AlCaerJugador() => Ovacionar(jugador.PosicionCabeza);

    /// <summary>La ovación sale de las dos gradas más cercanas al punto, para que se oiga de ese lado.</summary>
    public void Ovacionar(Vector3 punto)
    {
        if (ovacion == null) return;
        System.Array.Sort(gradas, (a, b) =>
            (a.transform.position - punto).sqrMagnitude.CompareTo((b.transform.position - punto).sqrMagnitude));
        // PlayClipAtPoint y no PlayOneShot: así el volumen no queda multiplicado por el del murmullo
        for (int i = 0; i < Mathf.Min(2, gradas.Length); i++)
            AudioSource.PlayClipAtPoint(ovacion, gradas[i].transform.position, volumenOvacion);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        for (int i = 0; i < fuentes; i++)
        {
            float angulo = (i + 0.5f) / fuentes * Mathf.PI * 2f;
            Gizmos.DrawWireSphere(transform.TransformPoint(new Vector3(Mathf.Cos(angulo) * radio, altura, Mathf.Sin(angulo) * radio)), 0.3f);
        }
    }
}
