using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// SOLO PARA PRUEBAS con visor. Muestra únicamente tu salud y la de Glass Joe en un panel
/// FIJO en la escena, a un lado del ring (no sigue a la cabeza ni al movimiento del rig). El botón Y del mando
/// izquierdo lo muestra u oculta.
/// El informe pide combate sin HUD (RF-14): quitar este componente del OVRCameraRig antes de la entrega.
/// Va en el OVRCameraRig, junto a SaludJugador.
/// </summary>
public class DebugSalud : MonoBehaviour
{
    [SerializeField] private SaludJugador jugador;
    [SerializeField] private Oponente oponente;
    [Header("Posición del panel")]
    [Tooltip("Dónde queda el panel (posición y giro). Vacío = a un lado del jugador, calculado al empezar")]
    [SerializeField] private Transform puntoFijo;
    [Tooltip("Sin punto fijo: a qué lado del jugador queda (el diagnóstico de DebugLocomotion usa el otro lado)")]
    [SerializeField] private bool aLaDerecha = false;
    [Tooltip("Sin punto fijo: x = metros al lado, y = altura sobre el piso, z = metros al frente (respecto a cómo empieza el rig)")]
    [SerializeField] private Vector3 desplazamiento = new Vector3(1.3f, 1.5f, 1.6f);
    [Tooltip("Tamaño del panel: 0.0025 = 1 m de ancho")]
    [SerializeField] private float escalaPanel = 0.0025f;
    [Tooltip("Mostrar el panel al empezar; el botón Y del mando izquierdo lo muestra y lo oculta")]
    [SerializeField] private bool visibleAlInicio = true;

    private GameObject panel;
    private RectTransform rellenoJugador;
    private RectTransform rellenoOponente;
    private TextMeshProUGUI textoJugador;
    private TextMeshProUGUI textoOponente;

    private void Awake()
    {
        var rig = GetComponentInParent<OVRCameraRig>();
        if (rig == null)
        {
            Debug.LogWarning($"{name}: DebugSalud necesita estar en el OVRCameraRig.", this);
            enabled = false;
            return;
        }
        if (jugador == null) jugador = GetComponent<SaludJugador>();
        if (oponente == null) oponente = FindFirstObjectByType<Oponente>();

        CrearPanel(rig.transform);
        panel.SetActive(visibleAlInicio);
    }

    private void OnEnable()
    {
        if (jugador != null)
        {
            jugador.AlCambiarSalud += AlCambiarSaludJugador;
            jugador.AlQuedarseSinSalud += AlQuedarseSinSalud;
        }
        if (oponente != null) oponente.AlCambiarSalud += AlCambiarSaludOponente;
    }

    private void OnDisable()
    {
        if (jugador != null)
        {
            jugador.AlCambiarSalud -= AlCambiarSaludJugador;
            jugador.AlQuedarseSinSalud -= AlQuedarseSinSalud;
        }
        if (oponente != null) oponente.AlCambiarSalud -= AlCambiarSaludOponente;
    }

    private void OnDestroy()
    {
        // El panel no es hijo del rig, así que hay que destruirlo aparte
        if (panel != null) Destroy(panel);
    }

    private void Start()
    {
        if (jugador != null) AlCambiarSaludJugador(jugador.Salud, jugador.SaludMaxima);
        if (oponente != null) AlCambiarSaludOponente(oponente.Salud, oponente.Perfil.saludMaxima);
    }

    private void Update()
    {
        // Botón Y del mando izquierdo
        if (OVRInput.GetDown(OVRInput.Button.Four, OVRInput.Controller.LTouch))
            panel.SetActive(!panel.activeSelf);
    }

    private void AlCambiarSaludJugador(float salud, float maxima)
    {
        Rellenar(rellenoJugador, salud, maxima);
        textoJugador.text = jugador != null && jugador.Derribado
            ? $"Tú  {salud:F0}/{maxima:F0}  <color=#FF5555>DERRIBADO</color>"
            : $"Tú  {salud:F0}/{maxima:F0}";
    }

    // El estado Derribado se fija justo después del cambio de salud: se vuelve a pintar para mostrarlo
    private void AlQuedarseSinSalud() => AlCambiarSaludJugador(jugador.Salud, jugador.SaludMaxima);

    private void AlCambiarSaludOponente(float salud, float maxima)
    {
        Rellenar(rellenoOponente, salud, maxima);
        textoOponente.text = $"Glass Joe  {salud:F0}/{maxima:F0}";
    }

    private static void Rellenar(RectTransform relleno, float salud, float maxima) =>
        relleno.anchorMax = new Vector2(maxima > 0f ? Mathf.Clamp01(salud / maxima) : 0f, 1f);

    // ----- Construcción del panel -----

    private void CrearPanel(Transform origen)
    {
        // Sin padre: se queda en su sitio de la escena aunque el jugador mueva la cabeza o se desplace
        panel = new GameObject("PanelDebugSalud", typeof(RectTransform));

        if (puntoFijo != null)
        {
            panel.transform.SetPositionAndRotation(puntoFijo.position, puntoFijo.rotation);
        }
        else
        {
            Vector3 adelante = Vector3.ProjectOnPlane(origen.forward, Vector3.up).normalized;
            Vector3 derecha = Vector3.Cross(Vector3.up, adelante);
            Vector3 posicion = origen.position
                + adelante * desplazamiento.z
                + derecha * (aLaDerecha ? desplazamiento.x : -desplazamiento.x)
                + Vector3.up * desplazamiento.y;

            // Mira hacia donde empieza el jugador (solo gira en vertical, queda derecho)
            Vector3 haciaPanel = posicion - origen.position;
            haciaPanel.y = 0f;
            panel.transform.SetPositionAndRotation(posicion, Quaternion.LookRotation(haciaPanel));
        }

        var canvas = panel.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 200;
        var rt = (RectTransform)panel.transform;
        rt.sizeDelta = new Vector2(400f, 170f);
        rt.localScale = Vector3.one * escalaPanel;

        Imagen(Rect(rt, "Fondo", Vector2.zero, Vector2.one), new Color(0.05f, 0.06f, 0.09f, 0.8f));

        // Solo se muestran las dos barras de salud; no se presentan estadísticas de combate.
        rellenoJugador = CrearBarra(rt, "Jugador", 0.53f, 0.92f, new Color(0.85f, 0.15f, 0.15f, 0.9f), out textoJugador);
        rellenoOponente = CrearBarra(rt, "Oponente", 0.08f, 0.47f, new Color(0.2f, 0.5f, 0.95f, 0.9f), out textoOponente);
    }

    private static RectTransform CrearBarra(RectTransform padre, string nombre, float yMin, float yMax, Color color, out TextMeshProUGUI texto)
    {
        RectTransform contenedor = Rect(padre, "Barra" + nombre, new Vector2(0.03f, yMin), new Vector2(0.97f, yMax));
        Imagen(contenedor, new Color(0.15f, 0.15f, 0.15f, 0.9f));
        RectTransform relleno = Rect(contenedor, "Relleno", Vector2.zero, Vector2.one);
        Imagen(relleno, color);
        texto = Texto(Rect(contenedor, "Texto", Vector2.zero, Vector2.one), 28f, TextAlignmentOptions.Center);
        return relleno;
    }

    private static RectTransform Rect(Transform padre, string nombre, Vector2 min, Vector2 max)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        var r = (RectTransform)go.transform;
        r.SetParent(padre, false);
        r.anchorMin = min;
        r.anchorMax = max;
        r.offsetMin = r.offsetMax = Vector2.zero;
        return r;
    }

    private static void Imagen(RectTransform r, Color color)
    {
        var imagen = r.gameObject.AddComponent<Image>();
        imagen.color = color;
        imagen.raycastTarget = false;
    }

    private static TextMeshProUGUI Texto(RectTransform r, float tamano, TextAlignmentOptions alineacion)
    {
        var t = r.gameObject.AddComponent<TextMeshProUGUI>();
        t.fontSize = tamano;
        t.alignment = alineacion;
        t.color = Color.white;
        t.raycastTarget = false;
        return t;
    }
}