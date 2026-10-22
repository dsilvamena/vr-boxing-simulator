using UnityEngine;

/// <summary>
/// Puño de una mano: Rigidbody kinematic que sigue al controlador y empuja
/// los Rigidbody que golpea según la velocidad de la mano.
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class Puno : MonoBehaviour
{
    [Tooltip("Velocidad mínima (m/s) para que cuente como golpe")]
    [SerializeField] private float velocidadMinima = 0.8f;
    [Tooltip("Multiplicador de la fuerza aplicada al golpear")]
    [SerializeField] private float fuerza = 1.5f;
    [Tooltip("Suavizado de la velocidad medida (0-1, más alto = más reacción)")]
    [SerializeField, Range(0.05f, 1f)] private float suavizado = 0.5f;

    private Vector3 posicionAnterior;
    private Vector3 velocidad;

    public Vector3 Velocidad => velocidad;

    private void Awake()
    {
        var rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }

    private void OnEnable() => posicionAnterior = transform.position;

    private void FixedUpdate()
    {
        Vector3 medida = (transform.position - posicionAnterior) / Time.fixedDeltaTime;
        velocidad = Vector3.Lerp(velocidad, medida, suavizado);
        posicionAnterior = transform.position;
    }

    private void OnCollisionEnter(Collision collision)
    {
        Rigidbody objetivo = collision.rigidbody;
        if (objetivo == null || objetivo.isKinematic) return;
        if (velocidad.magnitude < velocidadMinima) return;

        Vector3 punto = collision.contactCount > 0 ? collision.GetContact(0).point : objetivo.worldCenterOfMass;
        objetivo.AddForceAtPosition(velocidad * fuerza, punto, ForceMode.Impulse);
    }
}
