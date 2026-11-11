using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Nocaut del jugador: cuando SaludJugador se queda sin salud, el jugador cae y tiene segundosParaLevantarse (10 s)
/// para reponerse. Para levantarse debe pulsar rápido A / B / X / Y (pulsacionesNecesarias veces); cada pulsación
/// devuelve parte de la salud. Si llega a tiempo, se levanta y el oponente espera antes de volver a atacar.
/// Las oportunidades son limitadas y cada una devuelve menos salud: saludPorCaida = { 40, 30, 20 } significa
/// que la 1.ª caída te levanta con 40, la 2.ª con 30 y la 3.ª con 20; la caída siguiente ya no da cuenta y pierdes
/// al instante. Si se acaba el tiempo de una cuenta, también pierde por K.O. Tras el cartel de la derrota (por K.O. o
/// por salud) carga escenaResultados (Scene_Results, RF-18), que ofrece reintentar el nivel.
/// Va en el OVRCameraRig, junto a SaludJugador. La cuenta no se muestra (sin HUD, RF-14): la dice el réferi (Arbitro) y el
/// avance se nota en FeedbackSalud (viñeta, latido, háptica). Solo los carteles finales se crean por código, pegados a la cámara.
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

    [Header("Al perder (por K.O. o por salud)")]
    [Tooltip("Escena de resultados que se carga al perder (RF-18; debe estar en Build Settings); vacío = cargar escenaAlPerder")]
    [SerializeField] private string escenaResultados = "Scene_Results";
    [Tooltip("Escena a cargar al perder si no hay escena de resultados (debe estar en Build Settings); vacías las dos = no cambia de escena")]
    [SerializeField] private string escenaAlPerder = "Main_Scene";
    [SerializeField] private float segundosAntesDeSalir = 3f;

    [Header("Cartel de derrota")]
    [Tooltip("Distancia del cartel a la cámara, en metros")]
    [SerializeField] private float distanciaPanel = 0.5f;
    [SerializeField] private bool registrar = true;

    /// <summary>Empieza la cuenta (el jugador cayó).</summary>
    public event Action AlDerribarse;
    /// <summary>Una pulsación más; lleva el avance de la recuperación, de 0 a 1.</summary>
    public event Action<float> AlPulsar;
    /// <summary>El jugador se repuso a tiempo.</summary>
    public event Action AlLevantarse;
    /// <summary>Se acabó el tiempo: pierde por K.O.</summary>
    public event Action AlPerderPorKO;

    public bool Contando { get; private set; }
    public float SegundosRestantes { get; private set; }
    /// <summary>Lo que dura una cuenta completa.</summary>
    public float SegundosCuenta => segundosParaLevantarse;
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
            Perder("Sin más oportunidades");
            return;
        }

        Contando = true;
        SegundosRestantes = segundosParaLevantarse;
        pulsaciones = 0;
        Progreso = 0f;
        saludObjetivo = Mathf.Clamp(saludPorCaida[Derribos - 1], 0f, jugador.SaludMaxima);
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
        AlPulsar?.Invoke(Progreso);
    }

    private void Levantar()
    {
        Contando = false;
        jugador.Levantarse(jugador.SaludMaxima > 0f ? saludObjetivo / jugador.SaludMaxima : 0f);
        if (registrar) Debug.Log($"Jugador se levantó con {SegundosRestantes:F1} s de sobra → salud {jugador.Salud:F0}/{jugador.SaludMaxima:F0} (caída {Derribos} de {Oportunidades + 1})", this);
        AlLevantarse?.Invoke();
    }

    private void Perder(string motivo = "Se acabó el tiempo")
    {
        Contando = false;
        MostrarDerrota($"<size=140><b>K.O.</b></size>\n<size=30>{motivo}</size>");
        if (registrar) Debug.Log($"Jugador pierde por K.O.: {motivo}", this);
        AlPerderPorKO?.Invoke();
        if (!string.IsNullOrEmpty(EscenaSalida)) StartCoroutine(SalirDeLaPelea());
    }

    /// <summary>Acabó el último round con menos salud que el rival: misma salida que un K.O., pero sin haber caído.</summary>
    public void PerderPorSalud()
    {
        MostrarDerrota("<size=76><b>DERROTA</b></size>\n<size=30>Perdiste por salud</size>");
        if (registrar) Debug.Log("Jugador pierde por salud", this);
        if (!string.IsNullOrEmpty(EscenaSalida)) StartCoroutine(SalirDeLaPelea());
    }

    private void MostrarDerrota(string mensaje)
    {
        texto.text = mensaje;
        panel.SetActive(true);
    }

    private IEnumerator SalirDeLaPelea()
    {
        yield return new WaitForSecondsRealtime(segundosAntesDeSalir);
        SceneManager.LoadScene(EscenaSalida);
    }

    private string EscenaSalida => !string.IsNullOrEmpty(escenaResultados) ? escenaResultados : escenaAlPerder;

    private void CrearPanel(Transform camara)
    {
        panel = new GameObject("CartelDerrota");
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
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        texto = goTexto.GetComponent<TextMeshProUGUI>();
        texto.alignment = TextAlignmentOptions.Center;
        texto.color = Color.white;
        texto.raycastTarget = false;

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