using UnityEngine;

/// <summary>
/// Reacción a los golpes sin animaciones, para modelos sin esqueleto: inclina y empuja el cuerpo
/// (pivotando en los pies) según dónde y en qué dirección entra el golpe, y lo devuelve con un resorte.
/// En KO lo tumba hacia atrás hasta que se levanta.
/// </summary>
[RequireComponent(typeof(Oponente))]
public class ReaccionProcedural : MonoBehaviour
{
    [Tooltip("Hijo que se inclina (el modelo). Su origen hace de pivote, así que debe estar en los pies")]
    [SerializeField] private Transform cuerpo;
    [Tooltip("Inclinación al caer en KO, en grados")]
    [SerializeField] private float anguloKO = 80f;

    private Oponente oponente;
    private Vector3 posicionBase;
    private Quaternion rotacionBase;

    // Inclinación en mundo como eje * ángulo (radianes), y retroceso en metros
    private Vector3 angulo, velocidadAngular;
    private Vector3 retroceso, velocidadRetroceso;
    private Vector3 objetivoAngulo;

    private void Awake()
    {
        oponente = GetComponent<Oponente>();
        if (cuerpo == null && transform.childCount > 0) cuerpo = transform.GetChild(0);
        posicionBase = cuerpo.localPosition;
        rotacionBase = cuerpo.localRotation;
    }

    private void OnEnable()
    {
        oponente.AlRecibirGolpe += Reaccionar;
        oponente.AlCaer += Caer;
        oponente.AlLevantarse += Levantarse;
    }

    private void OnDisable()
    {
        oponente.AlRecibirGolpe -= Reaccionar;
        oponente.AlCaer -= Caer;
        oponente.AlLevantarse -= Levantarse;
    }

    private void Reaccionar(Golpe golpe)
    {
        PerfilOponente p = oponente.Perfil;
        float mult = golpe.zona != null ? golpe.zona.Multiplicador : 1f;

        // Dirección del golpe escalada por la intensidad (no por la velocidad cruda), así un golpe moderado ya se nota entero
        Vector3 empuje = golpe.velocidad.normalized * golpe.intensidad;

        // Como un cuerpo rígido clavado en los pies: un golpe alto inclina más, uno lateral lo gira
        Vector3 brazo = golpe.punto - cuerpo.position;
        velocidadAngular += Vector3.Cross(brazo, empuje) * (2f * p.fuerzaReaccion * mult);

        Vector3 horizontal = Vector3.ProjectOnPlane(empuje, Vector3.up);
        velocidadRetroceso += horizontal * (0.75f * p.fuerzaReaccion * mult);
    }

    private void Caer(Golpe golpe)
    {
        Vector3 horizontal = Vector3.ProjectOnPlane(golpe.velocidad, Vector3.up);
        if (horizontal.sqrMagnitude < 0.0001f) horizontal = -cuerpo.forward;
        objetivoAngulo = Vector3.Cross(Vector3.up, horizontal.normalized) * (anguloKO * Mathf.Deg2Rad);
    }

    private void Levantarse() => objetivoAngulo = Vector3.zero;

    private void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        if (dt <= 0f) return;

        PerfilOponente p = oponente.Perfil;
        bool caido = oponente.Caido;
        // Al caer, resorte más blando y más amortiguado para que no rebote
        float k = caido ? p.rigidez * 0.3f : p.rigidez;
        float c = caido ? p.amortiguacion * 2f : p.amortiguacion;

        velocidadAngular += (-k * (angulo - objetivoAngulo) - c * velocidadAngular) * dt;
        // Pasado el máximo deja de abrirse, pero sin cortar de golpe (al levantarse del KO viene de más lejos)
        if (!caido && angulo.magnitude > p.anguloMaximo * Mathf.Deg2Rad && Vector3.Dot(velocidadAngular, angulo) > 0f)
            velocidadAngular = Vector3.ProjectOnPlane(velocidadAngular, angulo.normalized);
        angulo += velocidadAngular * dt;

        velocidadRetroceso += (-p.rigidez * retroceso - p.amortiguacion * velocidadRetroceso) * dt;
        retroceso = Vector3.ClampMagnitude(retroceso + velocidadRetroceso * dt, p.retrocesoMaximo);

        Aplicar();
    }

    private void Aplicar()
    {
        float grados = angulo.magnitude * Mathf.Rad2Deg;
        Quaternion inclinacion = grados > 0.001f ? Quaternion.AngleAxis(grados, angulo.normalized) : Quaternion.identity;

        Transform padre = cuerpo.parent;
        if (padre == null)
        {
            cuerpo.SetLocalPositionAndRotation(posicionBase + retroceso, inclinacion * rotacionBase);
            return;
        }
        // La inclinación y el retroceso están en mundo; se pasan al espacio del padre (que puede estar escalado y rotado)
        cuerpo.SetLocalPositionAndRotation(
            posicionBase + padre.InverseTransformVector(retroceso),
            Quaternion.Inverse(padre.rotation) * inclinacion * padre.rotation * rotacionBase);
    }
}
