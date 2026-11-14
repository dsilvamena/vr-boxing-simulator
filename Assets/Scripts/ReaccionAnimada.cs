using UnityEngine;

/// <summary>
/// Reacción a los golpes con un Animator (personajes con esqueleto). Elige el estado según la zona, el tipo de golpe y
/// hacia dónde iba el puño, y lo reinicia aunque ya se esté reproduciendo, para que cada golpe se note:
/// - a la cabeza: un uppercut le levanta el mentón (Uppercut_Face), un golpe de lado le gira la cara hacia donde va el
///   guante (Cabeza_Derecha / Cabeza_Izquierda) y uno de frente se la echa atrás (Jab);
/// - al cuerpo: uno de lado lo dobla sobre el costado golpeado (Cuerpo_Derecha / Cuerpo_Izquierda) y los demás por el
///   estómago (Uppercut_Stomach).
/// Los estados de lado los crea Tools > Boxeo > Crear reacciones a los golpes; si el controlador no los tiene se usan
/// los de frente, como antes.
/// Los estados de reacción y Knockdown vuelven solos a Idle por transición en el Animator Controller.
/// El clip de Knockdown cae, se queda en el piso y se levanta en pocos segundos: con réferi se sostiene en el piso
/// (momentoPiso) hasta que termina la cuenta y entonces sigue desde ahí, así que no se ve de pie mientras sigue Caido.
/// </summary>
[RequireComponent(typeof(Oponente))]
public class ReaccionAnimada : MonoBehaviour
{
    private static readonly int Idle = Animator.StringToHash("Idle");
    private static readonly int Jab = Animator.StringToHash("Jab");
    private static readonly int UppercutCara = Animator.StringToHash("Uppercut_Face");
    private static readonly int UppercutEstomago = Animator.StringToHash("Uppercut_Stomach");
    private static readonly int Knockdown = Animator.StringToHash("Knockdown");
    private static readonly int Defeat = Animator.StringToHash("Defeat");
    private static readonly int CabezaDerecha = Animator.StringToHash("Cabeza_Derecha");
    private static readonly int CabezaIzquierda = Animator.StringToHash("Cabeza_Izquierda");
    private static readonly int CuerpoDerecha = Animator.StringToHash("Cuerpo_Derecha");
    private static readonly int CuerpoIzquierda = Animator.StringToHash("Cuerpo_Izquierda");

    /// <summary>El estado es una de las reacciones a un golpe (no la caída).</summary>
    public static bool EsReaccion(int estado) =>
        estado == Jab || estado == UppercutCara || estado == UppercutEstomago
        || estado == CabezaDerecha || estado == CabezaIzquierda || estado == CuerpoDerecha || estado == CuerpoIzquierda;

    [SerializeField] private Animator animator;
    [Tooltip("Parte de la velocidad del puño que tiene que ir de lado (0 a 1) para que la reacción sea la de un golpe lateral")]
    [SerializeField, Range(0f, 1f)] private float umbralLateral = 0.5f;
    [Tooltip("Segundos de mezcla al empezar una reacción")]
    [SerializeField] private float mezclaGolpe = 0.08f;
    [Tooltip("Segundos de mezcla al caer")]
    [SerializeField] private float mezclaCaida = 0.15f;
    [Tooltip("Momento del clip de Knockdown (0 a 1) en el que ya está en el piso; ahí espera la cuenta del réferi")]
    [SerializeField, Range(0f, 1f)] private float momentoPiso = 0.44f;

    private Oponente oponente;
    private bool tieneLaterales;

    /// <summary>La animación de caída ya lo dejó tendido (Knockdown pasado de momentoPiso, o Defeat avanzado). El réferi
    /// espera a esto para empezar a contar.</summary>
    public bool EnElPiso
    {
        get
        {
            if (animator == null || !oponente.Caido) return false;
            AnimatorStateInfo estado = animator.GetCurrentAnimatorStateInfo(0);
            if (animator.IsInTransition(0)) return false;
            if (estado.shortNameHash == Knockdown) return estado.normalizedTime >= momentoPiso - 0.02f;
            return estado.shortNameHash == Defeat && estado.normalizedTime >= 0.6f;
        }
    }

    private void Awake()
    {
        oponente = GetComponent<Oponente>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        tieneLaterales = animator != null && animator.HasState(0, CabezaDerecha) && animator.HasState(0, CabezaIzquierda)
                         && animator.HasState(0, CuerpoDerecha) && animator.HasState(0, CuerpoIzquierda);
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

    private void Update()
    {
        if (!oponente.Caido || oponente.Derrotado || !oponente.LevantaArbitro) return;
        AnimatorStateInfo estado = animator.GetCurrentAnimatorStateInfo(0);
        // Se fija el tiempo y no animator.speed, que AtaqueOponente devuelve a 1 cuando no está atacando
        if (estado.shortNameHash == Knockdown && estado.normalizedTime >= momentoPiso)
            animator.Play(Knockdown, 0, momentoPiso);
    }

    private void Reaccionar(Golpe golpe)
    {
        bool cabeza = golpe.zona == null || golpe.zona.Zona == TipoZona.Cabeza;
        int estado;
        float lado = LadoDelGolpe(golpe);
        if (golpe.tipo == TipoGolpe.Uppercut) estado = cabeza ? UppercutCara : UppercutEstomago;
        else if (lado > 0f) estado = cabeza ? CabezaDerecha : CuerpoDerecha;
        else if (lado < 0f) estado = cabeza ? CabezaIzquierda : CuerpoIzquierda;
        // De frente (jab, cross): a la cabeza la echa hacia atrás, al cuerpo lo dobla por el estómago
        else estado = cabeza ? Jab : UppercutEstomago;
        animator.CrossFadeInFixedTime(estado, mezclaGolpe, 0, 0f);
    }

    // +1 si el puño lo empuja hacia su derecha, -1 hacia su izquierda, 0 si venía de frente (o no hay clips de lado)
    private float LadoDelGolpe(Golpe golpe)
    {
        if (!tieneLaterales) return 0f;
        Vector3 local = transform.InverseTransformDirection(golpe.velocidad);
        local.y = 0f;
        if (local.sqrMagnitude < 0.0001f) return 0f;
        float lateral = local.x / local.magnitude;
        return Mathf.Abs(lateral) >= umbralLateral ? Mathf.Sign(lateral) : 0f;
    }

    private void Caer(Golpe golpe) =>
        animator.CrossFadeInFixedTime(oponente.Derrotado ? Defeat : Knockdown, mezclaCaida, 0, 0f);

    // Si ya llegó al piso, el resto del clip lo levanta y su transición lo devuelve a Idle;
    // esto cubre levantarse antes de eso (Reiniciar, el descanso justo tras la caída)
    private void Levantarse()
    {
        AnimatorStateInfo estado = animator.GetCurrentAnimatorStateInfo(0);
        if (estado.shortNameHash == Knockdown && estado.normalizedTime >= momentoPiso) return;
        if (estado.shortNameHash != Idle)
            animator.CrossFadeInFixedTime(Idle, 0.4f, 0);
    }
}
