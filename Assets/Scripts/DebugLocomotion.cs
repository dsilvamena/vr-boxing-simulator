using TMPro;
using UnityEngine;

/// <summary>
/// Solo para debug: mueve el OVRCameraRig con el joystick izquierdo, relativo a hacia dónde mira la cabeza,
/// y gira con el joystick derecho (giro por pasos, más cómodo sentado).
/// Útil para probar sentado. Desactívalo (o quita el componente) antes de publicar.
/// </summary>
public class DebugLocomotion : MonoBehaviour
{
    [SerializeField] private OVRCameraRig rig;
    [SerializeField] private float velocidad = 2f;
    [SerializeField, Range(0f, 0.5f)] private float zonaMuerta = 0.15f;

    [SerializeField] private float gradosPorGiro = 30f;
    [Tooltip("Cuánto hay que empujar el stick derecho para disparar el giro")]
    [SerializeField, Range(0.3f, 0.9f)] private float umbralGiro = 0.7f;

    [Tooltip("Muestra un texto pegado a la vista con lo que lee de los sticks")]
    [SerializeField] private bool mostrarDiagnostico = false;

    private bool giroListo = true;
    private int giros;
    private TextMeshPro textoDiagnostico;

    private void Reset() => rig = GetComponent<OVRCameraRig>();

    private void Start()
    {
        if (rig == null || !mostrarDiagnostico) return;

        var go = new GameObject("DiagnosticoLocomotion");
        go.transform.SetParent(rig.centerEyeAnchor, false);
        go.transform.localPosition = new Vector3(0f, -0.12f, 0.8f);
        textoDiagnostico = go.AddComponent<TextMeshPro>();
        textoDiagnostico.fontSize = 0.6f;
        textoDiagnostico.alignment = TextAlignmentOptions.Center;
        textoDiagnostico.color = Color.yellow;
        textoDiagnostico.textWrappingMode = TextWrappingModes.NoWrap;
    }

    private void Update()
    {
        if (rig == null) return;

        Girar();
        Mover();
        ActualizarDiagnostico();
    }

    private void ActualizarDiagnostico()
    {
        if (textoDiagnostico == null) return;

        Vector2 izq = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);
        Vector2 der = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick);
        textoDiagnostico.text =
            $"L {(OVRInput.IsControllerConnected(OVRInput.Controller.LTouch) ? "ok" : "NO")} {izq.x:F2},{izq.y:F2}\n" +
            $"R {(OVRInput.IsControllerConnected(OVRInput.Controller.RTouch) ? "ok" : "NO")} {der.x:F2},{der.y:F2}\n" +
            $"yaw rig {rig.transform.eulerAngles.y:F0}  giros {giros}  listo {giroListo}";
    }

    private void Girar()
    {
        // Sin máscara de control: con Link, Get(..., RTouch) devolvía siempre 0
        float x = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick).x;

        if (Mathf.Abs(x) < umbralGiro * 0.5f)
        {
            giroListo = true;
            return;
        }
        if (!giroListo || Mathf.Abs(x) < umbralGiro) return;

        giroListo = false;
        giros++;
        // Rota alrededor de la cabeza para que no te desplace al girar
        rig.transform.RotateAround(rig.centerEyeAnchor.position, Vector3.up, Mathf.Sign(x) * gradosPorGiro);
    }

    private void Mover()
    {
        Vector2 stick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);
        if (stick.magnitude < zonaMuerta) return;

        Transform cabeza = rig.centerEyeAnchor;
        Vector3 adelante = Vector3.ProjectOnPlane(cabeza.forward, Vector3.up).normalized;
        Vector3 derecha = Vector3.ProjectOnPlane(cabeza.right, Vector3.up).normalized;

        Vector3 direccion = adelante * stick.y + derecha * stick.x;
        rig.transform.position += direccion * (velocidad * Time.deltaTime);
    }
}
