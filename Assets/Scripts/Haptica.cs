using System;
using UnityEngine;

/// <summary>
/// Punto único por el que pasa toda la vibración del combate (RF-15). Llama a OVRInput.SetControllerVibration y avisa con
/// AlVibrar, así las pruebas sin visor pueden comprobar qué patrón se pidió y cuándo, y el registro de la sesión puede
/// contarlos. Amplitud y frecuencia van de 0 a 1; 0 de amplitud apaga el control.
/// </summary>
public static class Haptica
{
    /// <summary>Se pidió una vibración: control, frecuencia, amplitud y tiempo real (Time.realtimeSinceStartup).</summary>
    public static event Action<OVRInput.Controller, float, float, float> AlVibrar;

    public static void Fijar(float frecuencia, float amplitud, OVRInput.Controller control)
    {
        OVRInput.SetControllerVibration(frecuencia, amplitud, control);
        AlVibrar?.Invoke(control, frecuencia, amplitud, Time.realtimeSinceStartup);
    }

    public static void Apagar(OVRInput.Controller control) => Fijar(0f, 0f, control);
}
