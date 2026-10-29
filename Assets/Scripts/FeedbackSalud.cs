using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Salud diegética (RF-14): oscurece la visión periférica y suena un latido que aparece y se acelera de forma gradual al bajar la salud.
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
    [Tooltip("Debe ser UN solo latido (lub-dub) corto, de menos de ~0.6 s; si dura más, cada latido corta al anterior")]
    [SerializeField] private AudioClip latido;
    [Tooltip("Volumen máximo del latido (con la salud en cero)")]
    [SerializeField, Range(0f, 1f)] private float volumenLatido = 0.8f;
    [Tooltip("Por debajo de esta fracción de salud empieza a aparecer el latido")]
    [SerializeField, Range(0f, 1f)] private float umbralLatido = 0.6f;
    [Tooltip("Latidos por minuto al cruzar el umbral (ritmo de partida, calmado)")]
    [SerializeField, Min(30f)] private float bpmSuave = 60f;
    [Tooltip("Latidos por minuto con la salud en cero. Moderado: por encima de ~100 suena exagerado")]
    [SerializeField, Min(30f)] private float bpmUrgente = 90f;
    [Tooltip("Volumen al cruzar el umbral, como fracción del volumen máximo")]
    [SerializeField, Range(0f, 1f)] private float volumenInicial = 0.25f;
    [Tooltip("Cuánto sube el ritmo al perder salud (latidos por minuto, por segundo)")]
    [SerializeField, Min(0.5f)] private float subidaRitmo = 8f;
    [Tooltip("Cuánto baja el ritmo al recuperar salud (más bajo que la subida = se calma poco a poco)")]
    [SerializeField, Min(0.5f)] private float bajadaRitmo = 5f;
    [Tooltip("Rapidez con la que el latido gana volumen (fracción del máximo por segundo)")]
    [SerializeField, Min(0.05f)] private float aparicionVolumen = 0.6f;
    [Tooltip("Rapidez con la que el latido se apaga al recuperarse (fracción del máximo por segundo)")]
    [SerializeField, Min(0.05f)] private float desvanecerVolumen = 0.4f;

    private RawImage imagen;
    private Texture2D textura;
    private AudioSource audioSource;
    private float opacidad;
    private float pulso;
    private float bpmActual;
    private float volumenActual;
    private float faseLatido;
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
        audioSource.loop = false;

        bpmActual = bpmSuave;
        float periodoMinimo = 60f / Mathf.Max(bpmSuave, bpmUrgente);
        if (latido != null && latido.length > periodoMinimo)
            Debug.LogWarning($"{name}: el clip de latido dura {latido.length:F2} s y el latido más rápido es cada {periodoMinimo:F2} s; " +
                             "cada latido cortará al anterior. Usa un clip de un solo latido o recórtalo.", this);
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

        ActualizarLatido();
    }

    // Latido gradual: el ritmo y el volumen siguen a la salud con suavizado, así que ni al perder ni al recuperar
    // vida hay saltos. Un solo AudioSource con Play() reinicia el clip en cada latido, por lo que nunca se superponen.
    private void ActualizarLatido()
    {
        if (latido == null) return;
        float dt = Time.unscaledDeltaTime;
        float salud = jugador.SaludNormalizada;
        bool activo = salud < umbralLatido;

        // Gravedad: 0 en el umbral → 1 con la salud en cero; la curva suave evita arranques y topes bruscos
        float gravedad = Mathf.SmoothStep(0f, 1f, 1f - Mathf.InverseLerp(0f, umbralLatido, salud));

        // Ritmo: sigue a la salud con una velocidad limitada (sube rápido, se calma despacio)
        float bpmObjetivo = Mathf.Lerp(bpmSuave, Mathf.Max(bpmSuave, bpmUrgente), gravedad);
        float pasoRitmo = (bpmObjetivo > bpmActual ? subidaRitmo : bajadaRitmo) * dt;
        bpmActual = Mathf.MoveTowards(bpmActual, bpmObjetivo, pasoRitmo);

        // Volumen: aparece poco a poco al cruzar el umbral y se apaga poco a poco al recuperarse
        float volumenObjetivo = activo ? Mathf.Lerp(volumenInicial, 1f, gravedad) : 0f;
        float pasoVolumen = (volumenObjetivo > volumenActual ? aparicionVolumen : desvanecerVolumen) * dt;
        volumenActual = Mathf.MoveTowards(volumenActual, volumenObjetivo, pasoVolumen);

        // En silencio no hay nada que sonar; se deja la fase lista para que el primer latido llegue pronto
        if (volumenActual <= 0.001f)
        {
            faseLatido = 0.6f;
            return;
        }

        // Fase del latido: avanza según el ritmo actual, así un cambio de ritmo se nota enseguida pero sin saltos
        faseLatido += bpmActual / 60f * dt;
        if (faseLatido < 1f) return;
        faseLatido %= 1f;

        if (audioSource.clip != latido) audioSource.clip = latido;
        audioSource.volume = volumenLatido * volumenActual;
        audioSource.Play();
        pulso = volumenActual;
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