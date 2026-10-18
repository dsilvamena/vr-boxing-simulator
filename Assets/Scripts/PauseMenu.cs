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

    public bool IsOpen { get; private set; }

    void Start()
    {
        if (startOpen) Show();
        else Hide();
    }

    void Update()
    {
        if (OVRInput.GetDown(pauseButton)) Toggle();
    }

    // La UI sigue funcionando con timeScale en 0 porque el EventSystem usa tiempo sin escalar
    public void Show()
    {
        IsOpen = true;
        Time.timeScale = 0f;
        menuCanvas.SetActive(true);
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

    // Por si se sale de la escena por otro camino estando en pausa
    void OnDestroy()
    {
        Time.timeScale = 1f;
    }
}
