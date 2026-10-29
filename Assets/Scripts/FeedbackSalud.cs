using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Salud diegética (RF-14): oscurece la visión periférica y suena un latido que se acelera al bajar la salud.
/// Va en el OVRCameraRig, junto a SaludJugador. No usa post-procesado (caro en Quest): es un gradiente
/// generado por código sobre un canvas pegado a la cámara.
/// Con poca vida (umbralParpadeo puntos o menos) la pantalla se oscurece y se aclara en bucle, con transiciones suaves;
/// es solo presentación: no toca la salud ni la lógica del combate.
/// </summary>
public class FeedbackSalud : MonoBehaviour
{
    [SerializeField] private SaludJugador jugador;
    [Header("Viñeta")]
    [Tooltip("Opacidad máxima de la viñeta con la salud en cero (0-1)")]
    [SerializeField, Range(0f, 1f)] private float intensidadMaxima = 0.95f;
    [Tooltip("Tamaño del gradiente en metros, a distanciaVineta de la cámara; si los bordes no se oscurecen, súbelo")]
    [SerializeField] private float tamano = 0.9f;
    [SerializeField] private float distanciaVineta = 0.3f;
    [Tooltip("Rapidez con la que la viñeta sigue a la salud (opacidad por segundo)")]
    [SerializeField] private float velocidadCambio = 1.5f;
    [Tooltip("Cuánto se aprieta la viñeta con cada latido")]
    [SerializeField, Range(0f, 0.5f)] private float intensidadPulso = 0.15f;

    [Header("Parpadeo de poca vida")]
    [Tooltip("Con esta salud o menos (en puntos) la pantalla se oscurece y se aclara en bucle")]
    [SerializeField] private float umbralParpadeo = 20f;
    [Tooltip("Oscuridad máxima del parpadeo (0-1)")]
    [SerializeField, Range(0f, 0.9f)] private float oscuridadParpadeo = 0.6f;
    [Tooltip("Segundos de un ciclo completo claro-oscuro-claro (más alto = más lento; evita valores bajos)")]
    [SerializeField, Min(0.8f)] private float periodoParpadeo = 1.6f;
    [Tooltip("Segundos que tarda el efecto en aparecer y en desaparecer")]
    [SerializeField, Min(0.05f)] private float tiempoTransicion = 0.4f;

    [Header("Latido")]
    [SerializeField] private AudioClip latido;
    [SerializeField, Range(0f, 1f)] private float volumenLatido = 0.8f;
    [Tooltip("Por debajo de esta fracción de salud empieza a sonar el latido")]
    [SerializeField, Range(0f, 1f)] private float umbralLatido = 0.6f;
    [Tooltip("Segundos entre latidos con el umbral y con la salud en cero")]
    [SerializeField] private float intervaloLento = 1.1f;
    [SerializeField] private float intervaloRapido = 0.45f;

    private RawImage imagen;
    private Texture2D textura;
    private AudioSource audioSource;
    private float opacidad;
    private float pulso;
    private float proximoLatido;
    private Image oscurecedor;
    private float pesoParpadeo;
    private float faseParpadeo;
    private float alfaParpadeo;

    private void Awake()
    {
        if (jugador == null) jugador = GetComponent<SaludJugador>();
        var rig = GetComponentInParent<OVRCameraRig>();
        if (rig == null) { Debug.LogWarning($"{name}: FeedbackSalud necesita el OVRCameraRig.", this); enabled = false; return; }

        CrearVineta(rig.centerEyeAnchor);

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0f;
        audioSource.playOnAwake = false;
    }

    private void OnDestroy()
    {
        if (textura != null) Destroy(textura);
    }

    private void Update()
    {
        if (jugador == null) return;
        float dano = 1f - jugador.SaludNormalizada;

        opacidad = Mathf.MoveTowards(opacidad, dano * intensidadMaxima, velocidadCambio * Time.unscaledDeltaTime);
        pulso = Mathf.MoveTowards(pulso, 0f, 3f * Time.unscaledDeltaTime);
        Color c = imagen.color;
        c.a = Mathf.Clamp01(opacidad + pulso * intensidadPulso * dano);
        imagen.color = c;

        ActualizarParpadeo();

        if (latido != null && jugador.SaludNormalizada < umbralLatido && Time.unscaledTime >= proximoLatido)
        {
            audioSource.PlayOneShot(latido, volumenLatido);
            pulso = 1f;
            // Menos salud = latido más rápido
            float t = Mathf.InverseLerp(0f, umbralLatido, jugador.SaludNormalizada);
            proximoLatido = Time.unscaledTime + Mathf.Lerp(intervaloRapido, intervaloLento, t);
        }
    }

    // Poca vida: oscurece toda la vista y la devuelve a su brillo en bucle. Se apaga con el menú de pausa.
    private void ActualizarParpadeo()
    {
        float dt = Time.unscaledDeltaTime;
        bool activo = jugador.Salud <= umbralParpadeo && Time.timeScale > 0f;
        pesoParpadeo = Mathf.MoveTowards(pesoParpadeo, activo ? 1f : 0f, dt / tiempoTransicion);

        float alfa = 0f;
        if (pesoParpadeo > 0f)
        {
            faseParpadeo += dt / periodoParpadeo;
            // 0 → 1 → 0 con coseno: empieza con el brillo normal y los cambios son suaves
            float onda = 0.5f - 0.5f * Mathf.Cos(faseParpadeo * 2f * Mathf.PI);
            alfa = oscuridadParpadeo * pesoParpadeo * onda;
        }
        else
        {
            faseParpadeo = 0f;
        }

        // Solo se toca la imagen cuando cambia
        if (Mathf.Approximately(alfa, alfaParpadeo)) return;
        alfaParpadeo = alfa;
        oscurecedor.color = new Color(0f, 0f, 0f, alfa);
    }

    private void CrearVineta(Transform camara)
    {
        var go = new GameObject("VinetaSalud");
        go.transform.SetParent(camara, false);
        go.transform.localPosition = new Vector3(0f, 0f, distanciaVineta);
        go.transform.localRotation = Quaternion.identity;

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 100;
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(100f, 100f);
        rt.localScale = Vector3.one * (tamano / 100f);

        // Capa negra que cubre toda la vista, por debajo de la viñeta; su opacidad la mueve ActualizarParpadeo
        var capa = new GameObject("Parpadeo", typeof(RectTransform), typeof(Image));
        capa.transform.SetParent(rt, false);
        var crt = (RectTransform)capa.transform;
        crt.anchorMin = Vector2.zero;
        crt.anchorMax = Vector2.one;
        crt.offsetMin = new Vector2(-100f, -100f);
        crt.offsetMax = new Vector2(100f, 100f);
        oscurecedor = capa.GetComponent<Image>();
        oscurecedor.color = new Color(0f, 0f, 0f, 0f);
        oscurecedor.raycastTarget = false;

        var hijo = new GameObject("Gradiente", typeof(RectTransform), typeof(RawImage));
        hijo.transform.SetParent(rt, false);
        var hrt = (RectTransform)hijo.transform;
        hrt.anchorMin = Vector2.zero;
        hrt.anchorMax = Vector2.one;
        hrt.offsetMin = hrt.offsetMax = Vector2.zero;

        textura = CrearTextura(128);
        imagen = hijo.GetComponent<RawImage>();
        imagen.texture = textura;
        imagen.raycastTarget = false;
        imagen.color = new Color(1f, 1f, 1f, 0f);
    }

    // Transparente en el centro, negro hacia los bordes
    private static Texture2D CrearTextura(int n)
    {
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f;
                float dy = (y + 0.5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float t = Mathf.Clamp01(Mathf.InverseLerp(0.35f, 1f, r));
                tex.SetPixel(x, y, new Color(0f, 0f, 0f, t * t * (3f - 2f * t)));
            }
        tex.Apply();
        return tex;
    }
}