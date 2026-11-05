using UnityEngine;

/// <summary>
/// Reacción a los golpes con un Animator (personajes con esqueleto). Elige el estado según el tipo de golpe
/// y la zona, y lo reinicia aunque ya se esté reproduciendo, para que cada golpe se note.
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

    [SerializeField] private Animator animator;
    [Tooltip("Segundos de mezcla al empezar una reacción")]
    [SerializeField] private float mezclaGolpe = 0.08f;
    [Tooltip("Segundos de mezcla al caer")]
    [SerializeField] private float mezclaCaida = 0.15f;
    [Tooltip("Momento del clip de Knockdown (0 a 1) en el que ya está en el piso; ahí espera la cuenta del réferi")]
    [SerializeField, Range(0f, 1f)] private float momentoPiso = 0.44f;

    private Oponente oponente;

    private void Awake()
    {
        oponente = GetComponent<Oponente>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
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
        int estado = golpe.tipo switch
        {
            TipoGolpe.Uppercut => cabeza ? UppercutCara : UppercutEstomago,
            // Jab, cross y ganchos: a la cabeza la echa hacia atrás, al cuerpo lo dobla
            _ => cabeza ? Jab : UppercutEstomago,
        };
        animator.CrossFadeInFixedTime(estado, mezclaGolpe, 0, 0f);
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
