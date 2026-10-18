using UnityEngine;

// Controla la visibilidad del menú dentro de una pelea. Va en un objeto aparte del
// Canvas, porque al desactivar el Canvas también se desactivarían sus componentes.
public class PauseMenu : MonoBehaviour
{
    public GameObject menuCanvas;
    public GameObject laserPointer;
    public bool startOpen = false;

    public bool IsOpen { get; private set; }

    void Start()
    {
        if (startOpen) Show();
        else Hide();
    }

    // Pendiente: llamar a Toggle() desde un botón del control y pausar el juego aquí
    public void Show()
    {
        IsOpen = true;
        menuCanvas.SetActive(true);
        if (laserPointer) laserPointer.SetActive(true);
    }

    public void Hide()
    {
        IsOpen = false;
        menuCanvas.SetActive(false);
        if (laserPointer) laserPointer.SetActive(false);
    }

    public void Toggle()
    {
        if (IsOpen) Hide();
        else Show();
    }
}
