using UnityEngine;

/// <summary>
/// Recorte 3 de rendimiento (docs/Entrega5.md, bloque J): fija la foveación del Quest al empezar la escena, para que la
/// periferia de la imagen se dibuje a menos resolución. No está en ninguna escena: lo pone en el objeto de OVRManager de
/// las siete escenas, y lo quita, Tools > Boxeo > Rendimiento > Recorte 3. Mientras no se aplique, el juego no cambia.
/// Usa la función Meta XR Foveation de OpenXR, que ya está activada. En el Editor (sin visor) no hace nada.
/// </summary>
public class FoveacionFija : MonoBehaviour
{
    [Tooltip("Cuánto se baja la resolución de la periferia")]
    [SerializeField] private OVRManager.FoveatedRenderingLevel nivel = OVRManager.FoveatedRenderingLevel.High;
    [Tooltip("Con dinámica, el sistema solo llega a 'nivel' cuando la GPU no da abasto; sin ella, el nivel es fijo")]
    [SerializeField] private bool dinamica;

    private void Start()
    {
        if (!OVRManager.fixedFoveatedRenderingSupported)
        {
            Debug.Log($"{name}: este dispositivo no admite foveación; no se cambia nada.", this);
            return;
        }
        OVRManager.foveatedRenderingLevel = nivel;
        OVRManager.useDynamicFoveatedRendering = dinamica;
        Debug.Log($"{name}: foveación {nivel}{(dinamica ? " dinámica" : "")}.", this);
    }
}
