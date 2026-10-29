using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Nocaut del jugador: cuando SaludJugador se queda sin salud, el jugador cae y tiene segundosParaLevantarse (10 s)
/// para reponerse. Para levantarse debe pulsar rápido A / B / X / Y (pulsacionesNecesarias veces); cada pulsación
/// devuelve parte de la salud. Si llega a tiempo, se levanta con saludAlLevantarse de su salud máxima y el oponente
/// espera antes de volver a atacar. Si se acaba el tiempo, pierde por K.O.
/// Va en el OVRCameraRig, junto a SaludJugador. El panel con la cuenta se crea por código, pegado a la cámara.
/// La cuenta usa Time.deltaTime, así que se detiene con el menú de pausa (timeScale = 0).
/// </summary>
public class RecuperacionJugador : MonoBehaviour
{
    [SerializeField] private SaludJugador jugador;

    [Header("Cuenta")]
    [Tooltip("Segundos que tiene el jugador en el piso para reponerse")]
    [SerializeField] private float segundosParaLevantarse = 10f;
    [Tooltip("Pulsaciones de A / B / X / Y necesarias para levantarse")]
    [SerializeField, Min(1)] private int pulsacionesNecesarias = 12;
    [Tooltip("Fracción de la salud máxima con la que se levanta (0-1)")]
    [SerializeField, Range(0f, 1f)] private float saludAlLevantarse = 0.4f;

    [Header("Al perder por K.O.")]
    [Tooltip("Escena a cargar al perder (debe estar en Build Settings); vacío = no cambia de escena")]
    [SerializeField] private string escenaAlPerder = "Main_Scene";
    [SerializeField] private float segundosAntesDeSalir = 3f;

    [Header("Panel")]
    [Tooltip("Distancia del panel a la cámara, en metros")]
    [SerializeField] private float distanciaPanel = 0.5f;
    [SerializeField] private bool registrar = true;

    /// <summary>Empieza la cuenta (el jugador cayó).</summary>
    public event Action AlDerribarse;
    /// <summary>El jugador se repuso a tiempo.</summary>
    public event Action AlLevantarse;
    /// <summary>Se acabó el tiempo: pierde por K.O.</summary>
    public event Action AlPerderPorKO;

    public bool Contando { get; private set; }
    public float SegundosRestantes { get; private set; }
    /// <summary>Avance de la recuperación, de 0 a 1.</summary>
    public float Progreso { get; private set; }
    /// <summary>Veces que el jugador ha caído en el combate.</summary>
    public int Derribos { get; private set; }

    private int pulsaciones;
    private GameObject panel;
    private TextMeshProUGUI texto;
    private RectTransform relleno;

    private void Awake()
    {
        if (jugador == null) jugador = GetComponent<SaludJugador>();
        var rig = GetComponentInParent<OVRCameraRig>();
        if (jugador == null || rig == null)
        {
            Debug.LogWarning($"{name}: RecuperacionJugador necesita SaludJugador y el OVRCameraRig.", this);
            enabled = false;
            return;
        }
        CrearPanel(rig.centerEyeAnchor);
    }

    private void OnEnable()
    {
        if (jugador != null) jugador.AlQuedarseSinSalud += Empezar;
    }

    private void OnDisable()
    {
        if (jugador != null) jugador.AlQuedarseSinSalud -= Empezar;
    }

    private void Update()
    {
        if (!Contando || Time.timeScale == 0f) return;

        SegundosRestantes -= Time.deltaTime;

        if (HayPulsacion()) Pulsar();

        if (Progreso >= 1f) Levantar();
        else if (SegundosRestantes <= 0f) Perder();
        else ActualizarPanel();
    }

    private void Empezar()
    {
        if (Contando) return;
        Contando = true;
        Derribos++;
        SegundosRestantes = segundosParaLevantarse;
        pulsaciones = 0;
        Progreso = 0f;
        panel.SetActive(true);
        ActualizarPanel();
        if (registrar) Debug.Log($"Jugador derribado (caída {Derribos}): {segundosParaLevantarse:F0} s para levantarse", this);
        AlDerribarse?.Invoke();
    }

    // A / X (Button.One) y B / Y (Button.Two) de cualquiera de los dos controles
    private static bool HayPulsacion() =>
        OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.Touch) ||
        OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.Touch);

    private void Pulsar()
    {
        pulsaciones++;
        Progreso = Mathf.Clamp01((float)pulsaciones / pulsacionesNecesarias);
        // La salud sube con la recuperación, así FeedbackSalud va aclarando la visión
        jugador.EstablecerSalud(jugador.SaludMaxima * saludAlLevantarse * Progreso);
    }

    private void Levantar()
    {
        Contando = false;
        panel.SetActive(false);
        jugador.Levantarse(saludAlLevantarse);
        if (registrar) Debug.Log($"Jugador se levantó con {SegundosRestantes:F1} s de sobra → salud {jugador.Salud:F0}/{jugador.SaludMaxima:F0}", this);
        AlLevantarse?.Invoke();
    }

    private void Perder()
    {
        Contando = false;
        texto.text = "<size=140><b>K.O.</b></size>";
        relleno.parent.gameObject.SetActive(false);
        if (registrar) Debug.Log("Jugador no se levantó: pierde por K.O.", this);
        AlPerderPorKO?.Invoke();
        if (!string.IsNullOrEmpty(escenaAlPerder)) StartCoroutine(SalirDeLaPelea());
    }

    private IEnumerator SalirDeLaPelea()
    {
        yield return new WaitForSecondsRealtime(segundosAntesDeSalir);
        SceneManager.LoadScene(escenaAlPerder);
    }

    private void ActualizarPanel()
    {
        int segundos = Mathf.CeilToInt(Mathf.Max(0f, SegundosRestantes));
        string color = segundos <= 3 ? "#FF4040" : "#FFFFFF";
        texto.text = $"<size=52>¡LEVÁNTATE!</size>\n<size=130><b><color={color}>{segundos}</color></b></size>\n<size=30>Pulsa A / B / X / Y rápido</size>";
        relleno.anchorMax = new Vector2(Progreso, 1f);
    }

    private void CrearPanel(Transform camara)
    {
        panel = new GameObject("PanelRecuperacion");
        panel.transform.SetParent(camara, false);
        panel.transform.localPosition = new Vector3(0f, 0f, distanciaPanel);
        panel.transform.localRotation = Quaternion.identity;

        var canvas = panel.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 110; // por encima de la viñeta de FeedbackSalud (100)
        var rt = (RectTransform)panel.transform;
        rt.sizeDelta = new Vector2(400f, 320f);
        rt.localScale = Vector3.one * 0.0015f; // 0,6 m de ancho

        // Texto
        var goTexto = new GameObject("Texto", typeof(RectTransform), typeof(TextMeshProUGUI));
        goTexto.transform.SetParent(rt, false);
        var trt = (RectTransform)goTexto.transform;
        trt.anchorMin = new Vector2(0f, 0.15f);
        trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        texto = goTexto.GetComponent<TextMeshProUGUI>();
        texto.alignment = TextAlignmentOptions.Center;
        texto.color = Color.white;
        texto.raycastTarget = false;

        // Barra de progreso: fondo y relleno que crece de izquierda a derecha
        var goFondo = new GameObject("Barra", typeof(RectTransform), typeof(Image));
        goFondo.transform.SetParent(rt, false);
        var frt = (RectTransform)goFondo.transform;
        frt.anchorMin = new Vector2(0.1f, 0.03f);
        frt.anchorMax = new Vector2(0.9f, 0.10f);
        frt.offsetMin = frt.offsetMax = Vector2.zero;
        var fondo = goFondo.GetComponent<Image>();
        fondo.color = new Color(0f, 0f, 0f, 0.6f);
        fondo.raycastTarget = false;

        var goRelleno = new GameObject("Relleno", typeof(RectTransform), typeof(Image));
        goRelleno.transform.SetParent(frt, false);
        relleno = (RectTransform)goRelleno.transform;
        relleno.anchorMin = Vector2.zero;
        relleno.anchorMax = new Vector2(0f, 1f);
        relleno.offsetMin = relleno.offsetMax = Vector2.zero;
        var imgRelleno = goRelleno.GetComponent<Image>();
        imgRelleno.color = new Color(0.3f, 0.9f, 0.4f, 1f);
        imgRelleno.raycastTarget = false;

        panel.SetActive(false);
    }

    // Para probar sin visor: en Play, clic derecho en el título del componente
    [ContextMenu("Probar: derribar al jugador")]
    private void ProbarDerribo()
    {
        jugador.Derribar();
    }

    [ContextMenu("Probar: pulsar una vez")]
    private void ProbarPulsacion()
    {
        if (Contando) Pulsar();
    }
}