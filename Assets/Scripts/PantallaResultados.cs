using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Hoja de resultados de Scene_Results (RF-18) y, tras el último nivel, el cinturón de campeón (RF-19).
/// La escena se carga al terminar cada combate: ControlCombate tras el cartel de ¡VICTORIA! y RecuperacionJugador tras
/// el de K.O. o DERROTA. No recibe nada de ellos: lee el combate de RegistroSesion.Instancia.CombateActual, que vive en
/// DontDestroyOnLoad y ya está cerrado al cambiar de escena (nivel, escena, rival, resultado, round, precisión, ataques
/// bloqueados y tiempo).
/// Muestra las tres estadísticas del requisito (precisión de golpes, golpes bloqueados y tiempo total) y dos botones:
/// el principal (SIGUIENTE NIVEL tras ganar los niveles 1 a 4, REINTENTAR tras perder, REPETIR tras ganar el último) y
/// MENÚ. Los botones se usan con el puntero láser (OVRInputModule), como el menú principal.
/// </summary>
public class PantallaResultados : MonoBehaviour
{
    [Header("Textos")]
    [SerializeField] private TMP_Text titulo;
    [SerializeField] private TMP_Text subtitulo;
    [SerializeField] private TMP_Text valorPrecision;
    [SerializeField] private TMP_Text detallePrecision;
    [SerializeField] private TMP_Text valorBloqueos;
    [SerializeField] private TMP_Text detalleBloqueos;
    [SerializeField] private TMP_Text valorTiempo;
    [SerializeField] private TMP_Text detalleTiempo;
    [Tooltip("Línea con los golpes que paró el rival y los derribos")]
    [SerializeField] private TMP_Text detalle;

    [Header("Botones")]
    [SerializeField] private Button botonPrincipal;
    [SerializeField] private TMP_Text textoBotonPrincipal;
    [SerializeField] private Button botonMenu;
    [SerializeField] private string escenaMenu = "Main_Scene";

    [Header("Colores")]
    [SerializeField] private Color colorVictoria = new Color(1f, 0.82f, 0.25f);
    [SerializeField] private Color colorDerrota = new Color(0.9f, 0.16f, 0.12f);

    [Header("Campeón (al ganar el último nivel)")]
    [Tooltip("Raíz del cinturón, desactivada en la escena")]
    [SerializeField] private GameObject cinturon;
    [Tooltip("Lo que se aparta para dejar sitio al cinturón (el Canvas)")]
    [SerializeField] private Transform panel;
    [Tooltip("Desplazamiento del panel, en metros y en los ejes del propio panel")]
    [SerializeField] private Vector3 desplazamientoPanelCampeon = new Vector3(0.45f, 0f, 0f);
    [Tooltip("Giro del panel hacia el jugador, en grados")]
    [SerializeField] private float giroPanelCampeon = -15f;
    [Tooltip("Escala del panel junto al cinturón, para que los dos quepan en la vista")]
    [SerializeField] private float escalaPanelCampeon = 0.8f;
    [TextArea(2, 4)]
    [Tooltip("Cierre del arco narrativo (desenlace); {0} = nombre del rival")]
    [SerializeField] private string textoCampeon = "Derrotaste a {0}, el campeón invicto.\nEl cinturón de campeón regional es tuyo.";

    [Header("Sonido")]
    [SerializeField] private AudioSource fuente;
    [Tooltip("Suena al abrir los resultados de una victoria")]
    [SerializeField] private AudioClip ovacion;

    /// <summary>Escena que carga el botón principal; vacío si no hay.</summary>
    public string DestinoPrincipal { get; private set; }
    public bool Ganado { get; private set; }
    public bool Campeon { get; private set; }
    public DatosCombate Datos { get; private set; }

    private Vector3 posicionPanel;
    private Quaternion rotacionPanel;
    private Vector3 escalaPanel;
    private bool panelGuardado;
    private Vector2 posicionBotonMenu;
    private bool botonMenuGuardado;

    private void Start()
    {
        Time.timeScale = 1f;
        Mostrar(RegistroSesion.Instancia != null ? RegistroSesion.Instancia.CombateActual : null);
    }

    /// <summary>Rellena la hoja con un combate; null = no hay ninguno (escena abierta a mano).</summary>
    public void Mostrar(DatosCombate c)
    {
        Datos = c;
        bool hayCombate = c != null && c.resultado != "Abandonado";
        Ganado = hayCombate && c.resultado.StartsWith("Victoria");
        int niveles = MainMenu.Levels.Length;
        Campeon = Ganado && c.nivel >= niveles;

        if (!hayCombate)
        {
            Escribir(titulo, "RESULTADOS", colorVictoria);
            Escribir(subtitulo, "Todavía no has terminado ningún combate");
            foreach (var t in new[] { valorPrecision, valorBloqueos, valorTiempo }) Escribir(t, "—");
            foreach (var t in new[] { detallePrecision, detalleBloqueos, detalleTiempo, detalle }) Escribir(t, "");
            DestinoPrincipal = null;
        }
        else
        {
            Escribir(titulo, Campeon ? "¡CAMPEÓN!" : Ganado ? "¡VICTORIA!" : "DERROTA", Ganado ? colorVictoria : colorDerrota);
            string forma = FormaDelResultado(c);
            Escribir(subtitulo, Campeon
                ? string.Format(textoCampeon, c.rival)
                : $"Nivel {c.nivel}: {c.rival}\n<size=80%>{forma}</size>");

            if (c.golpesLanzados > 0)
            {
                Escribir(valorPrecision, Porcentaje(c.precision));
                Escribir(detallePrecision, $"{c.golpesConectados} de {Contar(c.golpesLanzados, "golpe", "golpes")}");
            }
            else
            {
                Escribir(valorPrecision, "—");
                Escribir(detallePrecision, "No lanzaste golpes");
            }

            Escribir(valorBloqueos, c.ataquesBloqueados.ToString(CultureInfo.InvariantCulture));
            Escribir(detalleBloqueos, c.ataquesRecibidos > 0
                ? $"de {Contar(c.ataquesRecibidos, "ataque", "ataques")} ({Porcentaje(c.porcentajeBloqueo)})"
                : "No recibiste ataques");

            int segundos = Mathf.RoundToInt(c.segundosCombate);
            Escribir(valorTiempo, $"{segundos / 60}:{segundos % 60:00}");
            Escribir(detalleTiempo, Contar(c.roundAlcanzado, "round", "rounds"));

            Escribir(detalle, $"El rival bloqueó {c.golpesBloqueadosPorRival} y esquivó {c.golpesEsquivadosPorRival} de tus golpes" +
                              $"   ·   Derribos: {c.derribosAlRival} al rival, {c.caidasJugador} tuyos");

            string escenaCombate = !string.IsNullOrEmpty(c.escena) ? c.escena
                : c.nivel >= 1 && c.nivel <= niveles ? MainMenu.Levels[c.nivel - 1] : null;
            if (Ganado && !Campeon && c.nivel >= 1)
            {
                DestinoPrincipal = MainMenu.Levels[c.nivel];
                Escribir(textoBotonPrincipal, "SIGUIENTE NIVEL");
            }
            else
            {
                DestinoPrincipal = escenaCombate;
                Escribir(textoBotonPrincipal, Ganado ? "REPETIR" : "REINTENTAR");
            }
        }

        bool hayPrincipal = !string.IsNullOrEmpty(DestinoPrincipal);
        if (botonPrincipal) botonPrincipal.gameObject.SetActive(hayPrincipal);
        CentrarMenu(!hayPrincipal);
        ColocarPanel();
        if (cinturon) cinturon.SetActive(Campeon);
        if (fuente && ovacion && Ganado) fuente.PlayOneShot(ovacion);
        Debug.Log(hayCombate
            ? $"Resultados: {c.resultado} en el nivel {c.nivel} contra {c.rival} (precisión {c.precision:P0}, bloqueos {c.ataquesBloqueados}/{c.ataquesRecibidos}, {c.segundosCombate:F0} s) → botón principal: {DestinoPrincipal}{(Campeon ? ", cinturón" : "")}"
            : "Resultados: no hay ningún combate terminado", this);
    }

    // Sin botón principal, MENÚ queda solo en el centro
    private void CentrarMenu(bool centrar)
    {
        if (!botonMenu) return;
        var rt = (RectTransform)botonMenu.transform;
        if (!botonMenuGuardado)
        {
            posicionBotonMenu = rt.anchoredPosition;
            botonMenuGuardado = true;
        }
        rt.anchoredPosition = centrar ? new Vector2(0f, posicionBotonMenu.y) : posicionBotonMenu;
    }

    private void ColocarPanel()
    {
        if (!panel) return;
        if (!panelGuardado)
        {
            posicionPanel = panel.localPosition;
            rotacionPanel = panel.localRotation;
            escalaPanel = panel.localScale;
            panelGuardado = true;
        }
        if (Campeon)
        {
            panel.localPosition = posicionPanel + rotacionPanel * desplazamientoPanelCampeon;
            panel.localRotation = rotacionPanel * Quaternion.Euler(0f, giroPanelCampeon, 0f);
            panel.localScale = escalaPanel * escalaPanelCampeon;
        }
        else
        {
            panel.localPosition = posicionPanel;
            panel.localRotation = rotacionPanel;
            panel.localScale = escalaPanel;
        }
    }

    private static string FormaDelResultado(DatosCombate c)
    {
        switch (c.resultado)
        {
            case nameof(ResultadoCombate.VictoriaPorKO): return $"Ganaste por K.O. en el round {c.roundAlcanzado}";
            case nameof(ResultadoCombate.VictoriaPorSalud): return "Ganaste por salud al terminar el último round";
            case nameof(ResultadoCombate.DerrotaPorKO): return $"Perdiste por K.O. en el round {c.roundAlcanzado}";
            case nameof(ResultadoCombate.DerrotaPorSalud): return "Perdiste por salud al terminar el último round";
            default: return c.resultado;
        }
    }

    private static string Contar(int n, string singular, string plural) => $"{n} {(n == 1 ? singular : plural)}";

    private static string Porcentaje(float proporcion) => $"{Mathf.RoundToInt(proporcion * 100f)} %";

    private static void Escribir(TMP_Text texto, string valor, Color? color = null)
    {
        if (!texto) return;
        texto.text = valor;
        if (color.HasValue) texto.color = color.Value;
    }

    /// <summary>Botón principal: siguiente nivel, reintentar o repetir.</summary>
    public void Continuar()
    {
        if (!string.IsNullOrEmpty(DestinoPrincipal)) Cargar(DestinoPrincipal);
    }

    public void IrAlMenu() => Cargar(escenaMenu);

    private static void Cargar(string escena)
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(escena);
    }

    // Para ver la hoja sin jugar: en Play, clic derecho en el título del componente. Los datos son inventados
    [ContextMenu("Probar: victoria en el nivel 2")]
    private void ProbarVictoria() => Mostrar(Ejemplo(2, "Bald Bull", nameof(ResultadoCombate.VictoriaPorKO)));

    [ContextMenu("Probar: derrota en el nivel 3")]
    private void ProbarDerrota() => Mostrar(Ejemplo(3, "Soda Popinski", nameof(ResultadoCombate.DerrotaPorKO)));

    [ContextMenu("Probar: campeón (nivel 5)")]
    private void ProbarCampeon() => Mostrar(Ejemplo(5, "Iron Mike Tyson", nameof(ResultadoCombate.VictoriaPorKO)));

    private static DatosCombate Ejemplo(int nivel, string rival, string resultado) => new DatosCombate
    {
        nivel = nivel,
        escena = MainMenu.Levels[nivel - 1],
        rival = rival,
        resultado = resultado,
        roundAlcanzado = 2,
        segundosCombate = 154f,
        golpesLanzados = 48,
        golpesConectados = 29,
        golpesBloqueadosPorRival = 11,
        golpesEsquivadosPorRival = 4,
        precision = 29f / 48f,
        ataquesRecibidos = 31,
        ataquesBloqueados = 19,
        porcentajeBloqueo = 19f / 31f,
        derribosAlRival = 3,
        caidasJugador = 1,
    };
}
