using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    public GameObject optionsMenu;
    public GameObject mainMenu;
    [Tooltip("Panel para elegir nivel (solo en el menú principal; vacío en el ring)")]
    public GameObject levelsMenu;

    [Tooltip("Sliders de 0 a 1; se inicializan con lo guardado en PlayerPrefs")]
    public Slider volumeSlider;
    public Slider hapticsSlider;

    // Escenas de cada nivel, en orden (deben estar en Build Settings)
    public static readonly string[] Levels = { "Scene_Ring_Level1", "Scene_Ring_Level2", "Scene_Ring_Level3" };

    // Awake y no Start: en el ring PauseMenu desactiva el Canvas en su Start y este Start ya no correría
    void Awake()
    {
        AudioListener.volume = PlayerPrefs.GetFloat("volumen", 1f);
        // Sin notificar, para no volver a guardar el mismo valor
        if (volumeSlider) volumeSlider.SetValueWithoutNotify(AudioListener.volume);
        if (hapticsSlider) hapticsSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat("haptica", 1f));
    }

    public void OpenOptionsPanel() => Show(optionsMenu);

    public void OpenMainMenuPanel() => Show(mainMenu);

    public void OpenLevelsPanel() => Show(levelsMenu);

    void Show(GameObject panel)
    {
        mainMenu.SetActive(panel == mainMenu);
        optionsMenu.SetActive(panel == optionsMenu);
        if (levelsMenu) levelsMenu.SetActive(panel == levelsMenu);
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    public void PlayGame() => PlayLevel(1);

    /// <summary>Carga el nivel indicado, empezando en 1.</summary>
    public void PlayLevel(int level)
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(Levels[Mathf.Clamp(level, 1, Levels.Length) - 1]);
    }

    public void SetVolume(float v)
    {
        AudioListener.volume = v;
        PlayerPrefs.SetFloat("volumen", v);
    }

    public void SetHaptics(float v)
    {
        PlayerPrefs.SetFloat("haptica", v);
        // Un pulso corto para sentir la intensidad elegida
        OVRInput.SetControllerVibration(1f, v, OVRInput.Controller.RTouch);
        CancelInvoke(nameof(StopVibration));
        Invoke(nameof(StopVibration), 0.1f);
    }

    void StopVibration() => OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.RTouch);
}
