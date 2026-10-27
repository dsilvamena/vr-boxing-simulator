using UnityEngine;

/// <summary>
/// Solo para pruebas sin visor: simula ataques sobre el jugador desde el menú del componente
/// (en Play, clic derecho en el título del componente). Va en el OVRCameraRig.
/// </summary>
public class PruebaAtaques : MonoBehaviour
{
    [SerializeField] private SaludJugador jugador;
    [SerializeField] private float dano = 20f;
    [SerializeField] private float radio = 0.3f;

    private OVRCameraRig rig;

    private void Awake()
    {
        rig = GetComponentInParent<OVRCameraRig>();
        if (jugador == null) jugador = GetComponent<SaludJugador>();
    }

    [ContextMenu("Atacar de frente (conecta)")]
    private void Frontal() => Lanzar(Vector3.zero);

    [ContextMenu("Atacar con la cabeza desplazada (esquive)")]
    private void Desplazado() => Lanzar(rig.centerEyeAnchor.right * 0.6f);

    [ContextMenu("Atacar con guardia (bloqueo)")]
    private void ConGuardia()
    {
        Transform cabeza = rig.centerEyeAnchor;
        Transform izq = rig.leftHandAnchor, der = rig.rightHandAnchor;
        Vector3 posIzq = izq.position, posDer = der.position;

        // Manos frente a la cara solo durante este ataque
        izq.position = cabeza.position + cabeza.forward * 0.25f - cabeza.right * 0.1f;
        der.position = cabeza.position + cabeza.forward * 0.25f + cabeza.right * 0.1f;
        Lanzar(Vector3.zero);

        izq.position = posIzq;
        der.position = posDer;
    }

    private void Lanzar(Vector3 desvio)
    {
        Transform cabeza = rig.centerEyeAnchor;
        Vector3 origen = cabeza.position + cabeza.forward * 1.2f + desvio;
        jugador.RecibirAtaque(origen, -cabeza.forward, 1.5f, radio, dano);
    }
}