using System.Collections;
using UnityEngine;

/// <summary>
/// Lo que se ve cuando un golpe del jugador llega al rival, además de su animación:
/// - Salpicadura de sudor en el punto de contacto, en la dirección del puño y más abundante cuanto más fuerte el golpe.
/// - Un destello corto y pálido cuando el rival lo para con la guardia (se distingue de un golpe que entra).
/// - Cámara lenta breve en cada caída, más larga en la definitiva (el K.O.), para que el golpe que tumba se saboree.
///   La cabeza y las manos del jugador siguen a velocidad normal: solo se frena el mundo. No toca la pausa (timeScale 0)
///   y siempre devuelve el tiempo a 1.
/// Un solo ParticleSystem creado al empezar, sin luces ni colisiones (barato en el Quest). Va junto al Oponente y solo
/// escucha sus eventos. Lo añade Tools > Boxeo > Poner pulido de combate, que también le asigna el material.
/// </summary>
[RequireComponent(typeof(Oponente))]
public class EfectosGolpe : MonoBehaviour
{
    [Header("Partículas")]
    [Tooltip("Material de las partículas (Art/Efectos/Particula_Suave.mat). Sin él no se emite nada")]
    [SerializeField] private Material material;
    [Tooltip("Gotas que salen con un golpe flojo (x) y con uno completo (y)")]
    [SerializeField] private Vector2Int gotas = new Vector2Int(5, 18);
    [SerializeField] private Color colorSudor = new Color(0.86f, 0.93f, 1f, 0.75f);
    [SerializeField] private Color colorBloqueo = new Color(1f, 0.95f, 0.8f, 0.55f);

    [Header("Cámara lenta")]
    [SerializeField] private bool camaraLenta = true;
    [Tooltip("Escala de tiempo y segundos reales en un knockdown")]
    [SerializeField, Range(0.1f, 1f)] private float escalaKnockdown = 0.45f;
    [SerializeField, Min(0f)] private float segundosKnockdown = 0.45f;
    [Tooltip("Escala de tiempo y segundos reales en la caída definitiva")]
    [SerializeField, Range(0.1f, 1f)] private float escalaKO = 0.28f;
    [SerializeField, Min(0f)] private float segundosKO = 1.1f;
    [Tooltip("Segundos reales que tarda en volver a la velocidad normal")]
    [SerializeField, Min(0.01f)] private float segundosVuelta = 0.35f;

    private Oponente oponente;
    private ParticleSystem particulas;
    private Coroutine lenta;
    private bool tiempoTocado;

    /// <summary>Partículas emitidas en total (para pruebas).</summary>
    public int Emitidas { get; private set; }

    private void Awake()
    {
        oponente = GetComponent<Oponente>();
        if (material != null) CrearParticulas();
    }

    private void OnEnable()
    {
        oponente.AlRecibirGolpe += AlRecibirGolpe;
        oponente.AlBloquear += AlBloquear;
        oponente.AlCaer += AlCaer;
    }

    private void OnDisable()
    {
        oponente.AlRecibirGolpe -= AlRecibirGolpe;
        oponente.AlBloquear -= AlBloquear;
        oponente.AlCaer -= AlCaer;
        RestaurarTiempo();
    }

    private void CrearParticulas()
    {
        var go = new GameObject("ParticulasGolpe");
        // Sin padre con escala: los rivales están escalados y deformarían las gotas
        go.transform.SetParent(transform.parent, false);
        particulas = go.AddComponent<ParticleSystem>();
        particulas.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule principal = particulas.main;
        principal.playOnAwake = false;
        principal.loop = false;
        principal.simulationSpace = ParticleSystemSimulationSpace.World;
        principal.maxParticles = 96;
        principal.gravityModifier = 1.4f;
        principal.startLifetime = 0.5f;
        principal.startSpeed = 0f;

        ParticleSystem.EmissionModule emision = particulas.emission;
        emision.enabled = false;
        ParticleSystem.ShapeModule forma = particulas.shape;
        forma.enabled = false;

        // Se encogen y se apagan al final de su vida
        ParticleSystem.SizeOverLifetimeModule tamano = particulas.sizeOverLifetime;
        tamano.enabled = true;
        tamano.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.6f, 0.8f), new Keyframe(1f, 0f)));
        ParticleSystem.ColorOverLifetimeModule color = particulas.colorOverLifetime;
        color.enabled = true;
        var degradado = new Gradient();
        degradado.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0f, 1f) });
        color.color = degradado;

        var render = particulas.GetComponent<ParticleSystemRenderer>();
        render.sharedMaterial = material;
        render.renderMode = ParticleSystemRenderMode.Stretch; // gotas alargadas según su velocidad
        render.velocityScale = 0.022f;
        render.lengthScale = 1f;
        render.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        render.receiveShadows = false;
        particulas.Play();
    }

    private void AlRecibirGolpe(Golpe golpe)
    {
        if (particulas == null) return;
        float intensidad = Mathf.Clamp01(golpe.intensidad);
        bool cabeza = golpe.zona == null || golpe.zona.Zona == TipoZona.Cabeza;
        int cuantas = Mathf.RoundToInt(Mathf.Lerp(gotas.x, gotas.y, intensidad) * (cabeza ? 1f : 0.6f));
        Vector3 direccion = golpe.velocidad.sqrMagnitude > 0.0001f ? golpe.velocidad.normalized : -transform.forward;
        // Sale hacia donde iba el puño y algo hacia arriba, abierto en abanico
        Emitir(golpe.punto, (direccion + Vector3.up * 0.45f).normalized, cuantas,
            Mathf.Lerp(1.2f, 3.2f, intensidad), 0.75f, colorSudor, new Vector2(0.012f, 0.028f), new Vector2(0.3f, 0.6f));
    }

    private void AlBloquear(Golpe golpe)
    {
        if (particulas == null) return;
        // Rebota hacia el jugador: un soplo corto y ancho, sin gotas que vuelen
        Vector3 direccion = golpe.velocidad.sqrMagnitude > 0.0001f ? -golpe.velocidad.normalized : transform.forward;
        Emitir(golpe.punto, direccion, 6, 0.7f, 1.2f, colorBloqueo, new Vector2(0.03f, 0.06f), new Vector2(0.12f, 0.22f));
    }

    private void Emitir(Vector3 punto, Vector3 direccion, int cuantas, float rapidez, float apertura, Color color, Vector2 tamano, Vector2 vida)
    {
        var p = new ParticleSystem.EmitParams();
        for (int i = 0; i < cuantas; i++)
        {
            Vector3 d = (direccion + Random.insideUnitSphere * apertura).normalized;
            p.position = punto + Random.insideUnitSphere * 0.03f;
            p.velocity = d * (rapidez * Random.Range(0.5f, 1.15f));
            p.startSize = Random.Range(tamano.x, tamano.y);
            p.startLifetime = Random.Range(vida.x, vida.y);
            p.startColor = color;
            particulas.Emit(p, 1);
        }
        Emitidas += cuantas;
    }

    private void AlCaer(Golpe golpe)
    {
        if (!camaraLenta || !isActiveAndEnabled) return;
        if (lenta != null) StopCoroutine(lenta);
        lenta = StartCoroutine(oponente.Derrotado ? Lenta(escalaKO, segundosKO) : Lenta(escalaKnockdown, segundosKnockdown));
    }

    private IEnumerator Lenta(float escala, float segundos)
    {
        // En pausa no se toca nada
        if (Time.timeScale <= 0f) { lenta = null; yield break; }
        Time.timeScale = escala;
        tiempoTocado = true;

        float t = 0f;
        while (t < segundos)
        {
            // Alguien abrió la pausa o cambió el tiempo: ya no es nuestro
            if (!Mathf.Approximately(Time.timeScale, escala)) { tiempoTocado = false; lenta = null; yield break; }
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        t = 0f;
        float anterior = escala;
        while (t < segundosVuelta)
        {
            if (!Mathf.Approximately(Time.timeScale, anterior)) { tiempoTocado = false; lenta = null; yield break; }
            t += Time.unscaledDeltaTime;
            anterior = Mathf.Lerp(escala, 1f, Mathf.Clamp01(t / segundosVuelta));
            Time.timeScale = anterior;
            yield return null;
        }
        Time.timeScale = 1f;
        tiempoTocado = false;
        lenta = null;
    }

    // Si la escena cambia o el componente se apaga a media cámara lenta, el tiempo no se queda frenado.
    // La pausa (0) se respeta: la quita quien la puso.
    private void RestaurarTiempo()
    {
        if (lenta != null) StopCoroutine(lenta);
        lenta = null;
        if (tiempoTocado && Time.timeScale > 0f) Time.timeScale = 1f;
        tiempoTocado = false;
    }

    private void OnDestroy()
    {
        if (particulas != null) Destroy(particulas.gameObject);
    }
}
