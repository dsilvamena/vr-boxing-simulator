using System.Collections;
using UnityEngine;

/// <summary>
/// Puño de una mano: Rigidbody kinematic que sigue al controlador y le avisa a los Oponente
/// cuando entra en una de sus ZonaGolpe.
/// La velocidad se mide relativa al TrackingSpace, así moverse o girar con el joystick no cuenta como golpe.
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class Puno : MonoBehaviour
{
    [Tooltip("Velocidad mínima (m/s) para que cuente como golpe")]
    [SerializeField] private float velocidadMinima = 0.5f;
    [Tooltip("Suavizado de la velocidad medida (0-1, más alto = más reacción)")]
    [SerializeField, Range(0.05f, 1f)] private float suavizado = 0.5f;
    [Tooltip("Segundos tras conectar un golpe en los que este puño no conecta otro")]
    [SerializeField] private float enfriamiento = 0.25f;

    [Header("Vibración")]
    [Tooltip("Rapidez del puño (m/s) con la que la vibración llega al máximo")]
    [SerializeField] private float rapidezVibracionMaxima = 2.5f;
    [Tooltip("Intensidad mínima de la vibración, para que un golpe suave también se note")]
    [SerializeField, Range(0f, 1f)] private float vibracionMinima = 0.4f;

    // Patrones de vibración por tipo de golpe: x = amplitud (0 = pausa), y = segundos
    private static readonly Vector2[] PatronJab = { new(0.7f, 0.05f) };                                  // seco y corto
    private static readonly Vector2[] PatronCross = { new(1f, 0.14f) };                                   // largo y fuerte
    private static readonly Vector2[] PatronUppercut = { new(0.5f, 0.05f), new(0f, 0.04f), new(1f, 0.1f) }; // doble, sube
    private static readonly Vector2[] PatronLateral = { new(0.35f, 0.06f) };                              // suave

    private Transform espacio;
    private Transform cabeza;
    private Vector3 posicionAnterior;
    private Vector3 velocidadLocal;
    private float listoEn;
    private Coroutine vibracion;

    public Mano Mano { get; private set; }
    /// <summary>Velocidad de la mano relativa al jugador, expresada en mundo.</summary>
    public Vector3 Velocidad => espacio != null ? espacio.TransformDirection(velocidadLocal) : velocidadLocal;

    private void Awake()
    {
        var rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        var rig = GetComponentInParent<OVRCameraRig>();
        if (rig != null)
        {
            espacio = rig.trackingSpace;
            cabeza = rig.centerEyeAnchor;
            Mano = transform.IsChildOf(rig.leftHandAnchor) ? Mano.Izquierda : Mano.Derecha;
        }
    }

    private void OnEnable() => posicionAnterior = PosicionLocal();

    private void OnDisable() => DetenerVibracion();

    private Vector3 PosicionLocal() => espacio != null ? espacio.InverseTransformPoint(transform.position) : transform.position;

    private void FixedUpdate()
    {
        Vector3 posicion = PosicionLocal();
        Vector3 medida = (posicion - posicionAnterior) / Time.fixedDeltaTime;
        velocidadLocal = Vector3.Lerp(velocidadLocal, medida, suavizado);
        posicionAnterior = posicion;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (Time.time < listoEn) return;
        var zona = other.GetComponent<ZonaGolpe>();
        if (zona == null || zona.Oponente == null) return;

        // Solo cuenta la velocidad que va hacia la zona: retirar la mano o rozarla no es un golpe
        Vector3 velocidad = Velocidad;
        Vector3 haciaZona = (other.bounds.center - transform.position).normalized;
        if (Vector3.Dot(velocidad, haciaZona) < velocidadMinima) return;

        // Sin rig (pruebas) se toma como frente el del propio puño
        Vector3 frente = cabeza != null ? cabeza.forward : transform.forward;
        var golpe = new Golpe
        {
            mano = Mano,
            tipo = Golpe.Clasificar(Mano, velocidad, frente),
            zona = zona,
            punto = other.ClosestPoint(transform.position),
            velocidad = velocidad,
        };
        if (!zona.Oponente.RecibirGolpe(golpe)) return;

        listoEn = Time.time + enfriamiento;
        float intensidad = Mathf.Lerp(vibracionMinima, 1f, velocidad.magnitude / rapidezVibracionMaxima);
        Vibrar(Patron(golpe.tipo), intensidad);
    }

    private static Vector2[] Patron(TipoGolpe tipo) => tipo switch
    {
        TipoGolpe.Jab => PatronJab,
        TipoGolpe.Cross => PatronCross,
        TipoGolpe.Uppercut => PatronUppercut,
        _ => PatronLateral,
    };

    private void Vibrar(Vector2[] patron, float intensidad)
    {
        float escala = intensidad * PlayerPrefs.GetFloat("haptica", 1f);
        if (escala <= 0.01f) return;
        DetenerVibracion();
        vibracion = StartCoroutine(Vibracion(patron, escala));
    }

    private IEnumerator Vibracion(Vector2[] patron, float escala)
    {
        foreach (Vector2 pulso in patron)
        {
            float amplitud = pulso.x * escala;
            OVRInput.SetControllerVibration(amplitud > 0f ? 1f : 0f, amplitud, Controlador());
            // Realtime para que se corte aunque el juego se pause justo después
            yield return new WaitForSecondsRealtime(pulso.y);
        }
        OVRInput.SetControllerVibration(0f, 0f, Controlador());
        vibracion = null;
    }

    private void DetenerVibracion()
    {
        if (vibracion == null) return;
        StopCoroutine(vibracion);
        vibracion = null;
        OVRInput.SetControllerVibration(0f, 0f, Controlador());
    }

    private OVRInput.Controller Controlador() => Mano == Mano.Izquierda ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;
}
