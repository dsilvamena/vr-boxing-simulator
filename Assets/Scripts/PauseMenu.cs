using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// Controla el menú de pausa dentro de una pelea. Va en un objeto aparte del
// Canvas, porque al desactivar el Canvas también se desactivarían sus componentes.
public class PauseMenu : MonoBehaviour
{
    public GameObject menuCanvas;
    public GameObject laserPointer;
    public bool startOpen = false;

    // Botón Menu del control izquierdo (el estándar de pausa en Quest)
    public OVRInput.Button pauseButton = OVRInput.Button.Start;
    public string mainMenuScene = "Main_Scene";
    [Tooltip("Título del panel principal; vacío = MainMenu/Titulo dentro del Canvas")]
    public TMP_Text title;

    [Header("Posición al abrir")]
    [Tooltip("El menú aparece frente a la cabeza del jugador, esté donde esté en el ring")]
    public bool followPlayer = true;
    [Tooltip("Metros entre la cabeza y el Canvas")]
    public float distance = 1.5f;
    [Tooltip("Altura del centro del Canvas respecto a los ojos; los botones están por encima del centro")]
    public float heightOffset = -0.45f;
    [Tooltip("Si el rival o el réferi están delante más cerca que distance, el menú se pone delante de ellos, como mucho a esta distancia, y se encoge en proporción (mismo tamaño aparente)")]
    public float minDistance = 0.7f;

    public bool IsOpen { get; private set; }

    string defaultTitle;
    Vector3 originalScale;

    void Start()
    {
        originalScale = menuCanvas.transform.localScale;
        DrawOnTop();
        if (!title)
        {
            var t = menuCanvas.transform.Find("MainMenu/Titulo");
            if (t) title = t.GetComponent<TMP_Text>();
        }
        if (title) defaultTitle = title.text;

        if (startOpen) Show();
        else Hide();
    }

    void Update()
    {
        if (OVRInput.GetDown(pauseButton)) Toggle();
    }

    // La UI sigue funcionando con timeScale en 0 porque el EventSystem usa tiempo sin escalar
    public void Show() => Show(null);

    /// <summary>Abre la pausa con otro título (por ejemplo, el aviso del Guardián); null = el de siempre.</summary>
    public void Show(string customTitle)
    {
        if (title) title.text = string.IsNullOrEmpty(customTitle) ? defaultTitle : customTitle;
        IsOpen = true;
        Time.timeScale = 0f;
        PlaceInFrontOfPlayer();
        menuCanvas.SetActive(true);
        // Siempre abre en el panel principal, aunque se haya cerrado desde Opciones
        var menu = menuCanvas.GetComponent<MainMenu>();
        if (menu) menu.OpenMainMenuPanel();
        if (laserPointer) laserPointer.SetActive(true);
    }

    public void Hide()
    {
        IsOpen = false;
        Time.timeScale = 1f;
        menuCanvas.SetActive(false);
        if (laserPointer) laserPointer.SetActive(false);
    }

    public void Toggle()
    {
        if (IsOpen) Hide();
        else Show();
    }

    public void BackToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuScene);
    }

    void PlaceInFrontOfPlayer()
    {
        Camera cam = Camera.main;
        if (!followPlayer || cam == null) return;

        // Solo el giro horizontal de la cabeza, para que el menú quede vertical
        Vector3 forward = cam.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f) forward = cam.transform.up;
        forward.y = 0f;
        forward.Normalize();

        // Con el rival delante (suele estar a 1 m) el menú a 1,5 m quedaba detrás de él y no se veía
        float d = Mathf.Clamp(NearestCharacterAhead(cam.transform.position, forward) - 0.35f, minDistance, distance);
        float k = d / distance;
        menuCanvas.transform.localScale = originalScale * k;
        Vector3 position = cam.transform.position + forward * d + Vector3.up * (heightOffset * k);
        menuCanvas.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward));
    }

    // El menú se dibuja por encima de todo: aunque se acerque, los guantes del rival en guardia (a ~0,6 m del jugador)
    // lo tapaban. Se copian los materiales de sus gráficos con ZTest Always (unity_GUIZTestMode, el que usan los shaders de UI)
    void DrawOnTop()
    {
        // SetFloat: la propiedad no está declarada en el shader y SetInt no la guarda
        float always = (float)UnityEngine.Rendering.CompareFunction.Always;
        foreach (var g in menuCanvas.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
        {
            if (g is TMP_Text t)
            {
                t.fontMaterial.SetFloat("unity_GUIZTestMode", always); // fontMaterial ya es una copia propia
                continue;
            }
            var m = new Material(g.materialForRendering);
            m.SetFloat("unity_GUIZTestMode", always);
            g.material = m;
        }
    }

    // Distancia horizontal al rival o al réferi más cercano que esté delante (en un cono de 50°)
    static float NearestCharacterAhead(Vector3 head, Vector3 forward)
    {
        float nearest = float.MaxValue;
        foreach (var o in FindObjectsByType<Oponente>()) Consider(o.transform.position);
        foreach (var r in FindObjectsByType<CuerpoArbitro>()) Consider(r.transform.position);
        return nearest;

        void Consider(Vector3 p)
        {
            Vector3 to = p - head;
            to.y = 0f;
            float along = Vector3.Dot(to, forward);
            if (along > 0f && Vector3.Angle(to, forward) < 50f) nearest = Mathf.Min(nearest, along);
        }
    }

    // Por si se sale de la escena por otro camino estando en pausa
    void OnDestroy()
    {
        Time.timeScale = 1f;
    }
}
