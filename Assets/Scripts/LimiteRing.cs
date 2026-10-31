using UnityEngine;

/// <summary>
/// Mantiene al jugador dentro del ring y fuera del cuerpo del oponente. Va en el OVRCameraRig.
/// - Ring: si la cabeza llega al borde del área jugable, empuja el rig de vuelta, como una pared invisible.
/// - Oponente: si la cabeza se acerca a menos de distanciaMinima de su centro, empuja el rig hacia afuera
///   (se resbala alrededor de él en vez de atravesarlo).
/// Un collider por sí solo no basta: DebugLocomotion mueve el rig cambiando su transform, y eso atraviesa
/// cualquier collider. Por eso se limita la posición de la cabeza (también cuenta si camina en su espacio real).
/// El área del ring se define con el collider del piso o del ring (campo Zona) o, si está vacío, con Centro y Mitad.
/// Con el rig seleccionado se ve la caja amarilla del ring y el círculo naranja del oponente en la escena.
/// </summary>
public class LimiteRing : MonoBehaviour
{
    [Tooltip("Collider del piso o del ring (MeshCollider o BoxCollider); su caja envolvente en X/Z marca el límite. Vacío = usa Centro y Mitad")]
    [SerializeField] private Collider zona;
    [Tooltip("Sin Zona: centro del ring en X/Z, en coordenadas de mundo")]
    [SerializeField] private Vector2 centro = Vector2.zero;
    [Tooltip("Sin Zona: mitad del ancho y del largo del área jugable, en metros (el ring mide unos 5,8 m entre esquinas)")]
    [SerializeField] private Vector2 mitad = new Vector2(2.6f, 2.6f);
    [Tooltip("Distancia de seguridad al borde, para que la cabeza no atraviese las cuerdas")]
    [SerializeField, Min(0f)] private float margen = 0.15f;

    [Header("Oponente")]
    [Tooltip("A quién no se puede atravesar; vacío = busca al oponente de la escena")]
    [SerializeField] private Oponente oponente;
    [Tooltip("Distancia mínima en metros entre tu cabeza y el centro del oponente (su cuerpo mide unos 0,3 m de radio)")]
    [SerializeField, Min(0f)] private float distanciaMinima = 0.6f;

    private OVRCameraRig rig;

    /// <summary>Área jugable en X/Z (x, y del Rect = x, z del mundo), ya con el margen. El oponente también la respeta.</summary>
    public Rect AreaRing => Area();

    private void Awake()
    {
        rig = GetComponentInParent<OVRCameraRig>();
        if (rig == null)
        {
            Debug.LogWarning($"{name}: LimiteRing necesita estar en el OVRCameraRig.", this);
            enabled = false;
            return;
        }
        if (oponente == null) oponente = FindAnyObjectByType<Oponente>();
    }

    // Después de DebugLocomotion (Update): corrige el movimiento de este mismo fotograma
    private void LateUpdate()
    {
        SepararDelOponente();

        Rect area = Area();
        Vector3 cabeza = rig.centerEyeAnchor.position;
        float x = Mathf.Clamp(cabeza.x, area.xMin, area.xMax);
        float z = Mathf.Clamp(cabeza.z, area.yMin, area.yMax);
        if (Mathf.Approximately(x, cabeza.x) && Mathf.Approximately(z, cabeza.z)) return;

        // Se mueve el rig (no la cabeza): solo en el plano del piso, sin tocar la altura
        rig.transform.position += new Vector3(x - cabeza.x, 0f, z - cabeza.z);
    }

    // Primero se separa del oponente y después se aplica el ring, así que las cuerdas siempre mandan
    private void SepararDelOponente()
    {
        if (oponente == null || distanciaMinima <= 0f) return;

        Vector3 cabeza = rig.centerEyeAnchor.position;
        Vector3 centroOponente = oponente.transform.position;
        Vector2 desde = new Vector2(cabeza.x - centroOponente.x, cabeza.z - centroOponente.z);
        float distancia = desde.magnitude;
        if (distancia >= distanciaMinima) return;

        // Si queda justo encima (caso raro), se le empuja hacia atrás respecto a hacia dónde mira el rig
        Vector2 direccion = distancia > 0.001f
            ? desde / distancia
            : new Vector2(-rig.transform.forward.x, -rig.transform.forward.z).normalized;

        Vector2 destino = new Vector2(centroOponente.x, centroOponente.z) + direccion * distanciaMinima;
        rig.transform.position += new Vector3(destino.x - cabeza.x, 0f, destino.y - cabeza.z);
    }

    private Rect Area()
    {
        Vector2 min;
        Vector2 max;
        if (zona != null)
        {
            Bounds b = zona.bounds;
            min = new Vector2(b.min.x, b.min.z);
            max = new Vector2(b.max.x, b.max.z);
        }
        else
        {
            min = centro - mitad;
            max = centro + mitad;
        }

        min += Vector2.one * margen;
        max -= Vector2.one * margen;
        // Con un margen enorme el área se reduce a un punto en vez de invertirse
        if (max.x < min.x) min.x = max.x = (min.x + max.x) * 0.5f;
        if (max.y < min.y) min.y = max.y = (min.y + max.y) * 0.5f;
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private void OnDrawGizmosSelected()
    {
        Rect area = Area();
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(
            new Vector3(area.center.x, transform.position.y + 1f, area.center.y),
            new Vector3(area.width, 2f, area.height));

        Oponente o = oponente != null ? oponente : FindAnyObjectByType<Oponente>();
        if (o == null || distanciaMinima <= 0f) return;
        // Círculo aplanado a la altura del pecho: el espacio que el jugador no puede ocupar
        Gizmos.color = new Color(1f, 0.55f, 0.1f);
        Gizmos.matrix = Matrix4x4.TRS(
            new Vector3(o.transform.position.x, transform.position.y + 1f, o.transform.position.z),
            Quaternion.identity,
            new Vector3(1f, 0.02f, 1f));
        Gizmos.DrawWireSphere(Vector3.zero, distanciaMinima);
        Gizmos.matrix = Matrix4x4.identity;
    }
}