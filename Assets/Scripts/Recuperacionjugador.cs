using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Nocaut del jugador: cuando SaludJugador se queda sin salud, el jugador cae y tiene segundosParaLevantarse (10 s)
/// para reponerse. Para levantarse debe pulsar rápido A / B / X / Y (pulsacionesNecesarias veces); cada pulsación
/// devuelve parte de la salud. Si llega a tiempo, se levanta y el oponente espera antes de volver a atacar.
/// Las oportunidades son limitadas y cada una devuelve menos salud: saludPorCaida = { 40, 30, 20 } significa
/// que la 1.ª caída te levanta con 40, la 2.ª con 30 y la 3.ª con 20; la caída siguiente ya no da cuenta y pierdes
/// al instante. Si se acaba el tiempo de una cuenta, también pierde por K.O.
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
    [Tooltip("Salud (en puntos) con la que te levantas en cada caída: el 1.er valor para la 1.ª caída, el 2.º para la 2.ª... " +
             "Si caes más veces que valores hay, pierdes al instante, sin cuenta. Quita o agrega valores para cambiar las oportunidades " +
             "(por ejemplo { 40, 30 } = pierdes en la 3.ª caída)")]
    [SerializeField] private float[] saludPorCaida = { 40f, 30f, 20f };

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
    /// <summary>Caídas en las que todavía hay cuenta; la siguiente es K.O. inmediato.</summary>
    public int Oportunidades => saludPorCaida != null ? saludPorCaida.Length : 0;

    private int pulsaciones;
    private float saludObjetivo; // salud con la que se levantará en la caída actual
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
        Derribos++;
        AlDerribarse?.Invoke();

        // Sin oportunidades: no hay cuenta, pierde en el acto
        if (Derribos > Oportunidades)
        {
            if (registrar) Debug.Log($"Jugador derribado (caída {Derribos}): sin más oportunidades", this);
            panel.SetActive(true);
            Perder("Sin más oportunidades");
            return;
        }

        Contando = true;
        SegundosRestantes = segundosParaLevantarse;
        pulsaciones = 0;
        Progreso = 0f;
        saludObjetivo = Mathf.Clamp(saludPorCaida[Derribos - 1], 0f, jugador.SaludMaxima);
        panel.SetActive(true);
        ActualizarPanel();
        if (registrar) Debug.Log($"Jugador derribado (caída {Derribos} de {Oportunidades + 1}): {segundosParaLevantarse:F0} s para levantarse, se levantará con {saludObjetivo:F0} de salud", this);
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
        jugador.EstablecerSalud(saludObjetivo * Progreso);
    }

    private void Levantar()
    {
        Contando = false;
        panel.SetActive(false);
        jugador.Levantarse(jugador.SaludMaxima > 0f ? saludObjetivo / jugador.SaludMaxima : 0f);
        if (registrar) Debug.Log($"Jugador se levantó con {SegundosRestantes:F1} s de sobra → salud {jugador.Salud:F0}/{jugador.SaludMaxima:F0} (caída {Derribos} de {Oportunidades + 1})", this);
        AlLevantarse?.Invoke();
    }

    private void Perder(string motivo = "Se acabó el tiempo")
    {
        Contando = false;
        texto.text = $"<size=140><b>K.O.</b></size>\n<size=30>{motivo}</size>";
        relleno.parent.gameObject.SetActive(false);
        if (registrar) Debug.Log($"Jugador pierde por K.O.: {motivo}", this);
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
        texto.text = $"<size=46>¡LEVÁNTATE!</size>\n<size=120><b><color={color}>{segundos}</color></b></size>\n" +
                     $"<size=26>Pulsa A / B / X / Y rápido</size>\n<size=24><color=#9AA0A6>Caída {Derribos} de {Oportunidades + 1}</color></size>";
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
        rt.sizeDelta = new Vector2(420f, 350f);
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

    /// <summary>Vuelve a contar las caídas desde cero (por ejemplo, al reiniciar el combate sin recargar la escena).</summary>
    public void ReiniciarCaidas()
    {
        Derribos = 0;
        Contando = false;
        if (panel != null) panel.SetActive(false);
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