using System;
using System.Diagnostics;
using UnityEngine;

/// <summary>
/// Punto único por el que pasa toda la vibración del combate (RF-15). Llama a OVRInput.SetControllerVibration y avisa con
/// AlVibrar, así las pruebas sin visor pueden comprobar qué patrón se pidió y cuándo, y el registro de la sesión puede
/// contarlos. Amplitud y frecuencia van de 0 a 1; 0 de amplitud apaga el control.
/// También mide la latencia háptica que controla el código (RNF-02): si quien vibra pasa el momento en que detectó el
/// impacto, al volver de OVRInput publica los milisegundos transcurridos en AlMedirLatencia.
/// </summary>
public static class Haptica
{
    /// <summary>Se pidió una vibración: control, frecuencia, amplitud y tiempo real (Time.realtimeSinceStartup).</summary>
    public static event Action<OVRInput.Controller, float, float, float> AlVibrar;

    /// <summary>
    /// Latencia de un impacto a su vibración, en milisegundos: desde que se detectó (Puno.OnTriggerEnter) hasta que volvió
    /// la llamada a OVRInput.SetControllerVibration. No incluye lo que pasa después en el runtime de OpenXR ni en el control.
    /// </summary>
    public static event Action<OVRInput.Controller, double> AlMedirLatencia;

    /// <summary>Marca de tiempo de alta resolución para pasar a Fijar como origen del impacto.</summary>
    public static long Ahora() => Stopwatch.GetTimestamp();

    public static void Fijar(float frecuencia, float amplitud, OVRInput.Controller control) => Fijar(frecuencia, amplitud, control, 0);

    /// <param name="origenImpacto">Haptica.Ahora() del momento en que se detectó el impacto que provoca esta vibración (0 = ninguno).</param>
    public static void Fijar(float frecuencia, float amplitud, OVRInput.Controller control, long origenImpacto)
    {
        OVRInput.SetControllerVibration(frecuencia, amplitud, control);
        if (origenImpacto != 0 && amplitud > 0f)
            AlMedirLatencia?.Invoke(control, (Stopwatch.GetTimestamp() - origenImpacto) * 1000.0 / Stopwatch.Frequency);
        AlVibrar?.Invoke(control, frecuencia, amplitud, Time.realtimeSinceStartup);
    }

    public static void Apagar(OVRInput.Controller control) => Fijar(0f, 0f, control);
}
