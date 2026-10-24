using UnityEngine;

/// <summary>
/// Reacción a los golpes con un Animator (personajes con esqueleto). Elige el estado según el tipo de golpe
/// y la zona, y lo reinicia aunque ya se esté reproduciendo, para que cada golpe se note.
/// Los estados de reacción y Knockdown vuelven solos a Stance por transición en el Animator Controller.
/// </summary>
[RequireComponent(typeof(Oponente))]
public class ReaccionAnimada : MonoBehaviour
{
    private static readonly int Stance = Animator.StringToHash("Stance");
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

    // Normalmente el clip de Knockdown ya terminó y volvió a Stance; esto cubre levantarse antes (Reiniciar)
    private void Levantarse()
    {
        if (animator.GetCurrentAnimatorStateInfo(0).shortNameHash != Stance)
            animator.CrossFadeInFixedTime(Stance, 0.4f, 0);
    }
}
