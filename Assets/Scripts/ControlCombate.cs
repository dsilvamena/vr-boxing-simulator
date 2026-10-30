using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Cierre del combate: cuando el Oponente cae por última vez (Derrotado), muestra la victoria frente al jugador
/// y, pasados segundosAntesDeAvanzar, carga siguienteEscena (el siguiente nivel o, tras el último, el menú).
/// Guarda en PlayerPrefs ("nivelSuperado") el número de nivel más alto que se ha ganado.
/// Va en un objeto propio de cada escena de pelea. El panel se crea por código, pegado a la cámara.
/// La espera usa tiempo escalado, así que se detiene con el menú de pausa.
/// </summary>
public class ControlCombate : MonoBehaviour
{
    [Tooltip("Vacío = el Oponente de la escena")]
    [SerializeField] private Oponente oponente;
    [Tooltip("Número de este nivel (1, 2, 3...)")]
    [SerializeField, Min(1)] private int nivel = 1;
    [Tooltip("Escena a cargar al ganar (debe estar en Build Settings)")]
    [SerializeField] private string siguienteEscena = "Scene_Ring_Level2";
    [Tooltip("Texto bajo ¡VICTORIA!; {0} = nombre del rival")]
    [SerializeField] private string mensaje = "Derrotaste a {0}\nSiguiente: Nivel 2";
    [Tooltip("Nombre del rival que se muestra; vacío = el nombre de su perfil")]
    [SerializeField] private string nombreRival = "";
    [SerializeField] private float segundosAntesDeAvanzar = 6f;

    [Header("Panel")]
    [Tooltip("Distancia del panel a la cámara, en metros")]
    [SerializeField] private float distanciaPanel = 1.2f;

    public bool Ganado { get; private set; }

    private GameObject panel;
    private TextMeshProUGUI texto;

    private void Awake()
    {
        if (oponente == null) oponente = FindAnyObjectByType<Oponente>();
        if (oponente == null)
        {
            Debug.LogWarning($"{name}: ControlCombate no encontró un Oponente en la escena.", this);
            enabled = false;
            return;
        }
        if (string.IsNullOrEmpty(nombreRival)) nombreRival = oponente.Nombre;
        if (Camera.main != null) CrearPanel(Camera.main.transform);
    }

    private void OnEnable()
    {
        if (oponente != null) oponente.AlCaer += AlCaer;
    }

    private void OnDisable()
    {
        if (oponente != null) oponente.AlCaer -= AlCaer;
    }

    private void AlCaer(Golpe golpe)
    {
        if (!oponente.Derrotado || Ganado) return;
        Ganado = true;
        if (nivel > PlayerPrefs.GetInt("nivelSuperado", 0)) PlayerPrefs.SetInt("nivelSuperado", nivel);
        Debug.Log($"Victoria en el nivel {nivel} contra {nombreRival} → {siguienteEscena}", this);
        StartCoroutine(Avanzar());
    }

    private IEnumerator Avanzar()
    {
        // Deja ver la caída antes de mostrar el cartel
        yield return new WaitForSeconds(1.5f);
        if (panel != null)
        {
            texto.text = $"<size=100><b><color=#FFD23F>¡VICTORIA!</color></b></size>\n<size=34>{string.Format(mensaje, nombreRival)}</size>";
            panel.SetActive(true);
        }
        yield return new WaitForSeconds(segundosAntesDeAvanzar);
        if (!string.IsNullOrEmpty(siguienteEscena)) SceneManager.LoadScene(siguienteEscena);
    }

    private void CrearPanel(Transform camara)
    {
        panel = new GameObject("PanelVictoria");
        panel.transform.SetParent(camara, false);
        panel.transform.localPosition = new Vector3(0f, 0.05f, distanciaPanel);
        panel.transform.localRotation = Quaternion.identity;

        var canvas = panel.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 120; // por encima de la viñeta de FeedbackSalud y de la cuenta del jugador
        var rt = (RectTransform)panel.transform;
        rt.sizeDelta = new Vector2(800f, 320f);
        rt.localScale = Vector3.one * 0.0015f; // 1,2 m de ancho

        var goTexto = new GameObject("Texto", typeof(RectTransform), typeof(TextMeshProUGUI));
        goTexto.transform.SetParent(rt, false);
        var trt = (RectTransform)goTexto.transform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        texto = goTexto.GetComponent<TextMeshProUGUI>();
        texto.alignment = TextAlignmentOptions.Center;
        texto.color = Color.white;
        texto.outlineWidth = 0.15f;
        texto.outlineColor = Color.black;
        texto.raycastTarget = false;

        panel.SetActive(false);
    }

    // Para probar sin visor: en Play, clic derecho en el título del componente.
    // Golpea al rival con crosses (respetando su invulnerabilidad) y lo levanta tras cada knockdown hasta derrotarlo.
    [ContextMenu("Probar: ganar el combate")]
    private void ProbarVictoria() => StartCoroutine(GolpearHastaGanar());

    private IEnumerator GolpearHastaGanar()
    {
        while (!oponente.Derrotado)
        {
            if (oponente.Caido) oponente.Levantarse();
            oponente.RecibirGolpe(new Golpe { tipo = TipoGolpe.Cross, velocidad = Vector3.forward * 10f, punto = oponente.transform.position });
            yield return new WaitForSeconds(oponente.Perfil.invulnerabilidad + 0.05f);
        }
    }
}
