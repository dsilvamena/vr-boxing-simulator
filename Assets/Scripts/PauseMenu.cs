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

    public bool IsOpen { get; private set; }

    string defaultTitle;

    void Start()
    {
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

        Vector3 position = cam.transform.position + forward * distance + Vector3.up * heightOffset;
        menuCanvas.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward));
    }

    // Por si se sale de la escena por otro camino estando en pausa
    void OnDestroy()
    {
        Time.timeScale = 1f;
    }
}
