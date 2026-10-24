using UnityEngine;

/// <summary>
/// Ataca al jugador cada cierto tiempo (jabs y, cada uppercutCada ataques, un uppercut) y gira para mirarlo.
/// Cada golpe recibido reinicia la espera (esperaTrasGolpe), así que si el jugador lo presiona no le da tiempo a atacar.
/// Si lo golpean mientras ataca, ReaccionAnimada cambia de estado y el ataque se corta. Caído no ataca.
/// Por ahora es solo visual: el puño no golpea al jugador.
/// </summary>
[RequireComponent(typeof(Oponente))]
public class AtaqueOponente : MonoBehaviour
{
    private static readonly int Idle = Animator.StringToHash("Idle");
    private static readonly int AtaqueJab = Animator.StringToHash("Ataque_Jab");
    private static readonly int AtaqueUppercut = Animator.StringToHash("Ataque_Uppercut");

    [SerializeField] private Animator animator;
    [Tooltip("A quién mira y ataca; vacío = cámara principal (CenterEyeAnchor)")]
    [SerializeField] private Transform objetivo;
    [Tooltip("Rapidez con la que gira hacia el jugador")]
    [SerializeField] private float velocidadGiro = 8f;
    [Tooltip("Segundos de mezcla al empezar un ataque")]
    [SerializeField] private float mezclaAtaque = 0.1f;

    private Oponente oponente;
    private float proximoAtaque;
    private int ataques;

    /// <summary>Está lanzando un golpe (o empezando a lanzarlo).</summary>
    public bool Atacando
    {
        get
        {
            int actual = animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
            int siguiente = animator.GetNextAnimatorStateInfo(0).shortNameHash;
            return EsAtaque(actual) || EsAtaque(siguiente);
        }
    }

    private void Awake()
    {
        oponente = GetComponent<Oponente>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    private void OnEnable()
    {
        oponente.AlRecibirGolpe += AlRecibirGolpe;
        oponente.AlLevantarse += AlLevantarse;
        Esperar(oponente.Perfil.intervaloAtaque);
    }

    private void OnDisable()
    {
        oponente.AlRecibirGolpe -= AlRecibirGolpe;
        oponente.AlLevantarse -= AlLevantarse;
    }

    private void Update()
    {
        if (oponente.Caido) return;
        MirarObjetivo();

        if (Time.time < proximoAtaque) return;
        // Solo desde la guardia: si aún está reaccionando a un golpe, espera a que vuelva a Idle
        if (animator.IsInTransition(0) || animator.GetCurrentAnimatorStateInfo(0).shortNameHash != Idle) return;

        ataques++;
        int uppercutCada = oponente.Perfil.uppercutCada;
        bool uppercut = uppercutCada > 0 && ataques % uppercutCada == 0;
        animator.CrossFadeInFixedTime(uppercut ? AtaqueUppercut : AtaqueJab, mezclaAtaque, 0, 0f);
        Esperar(oponente.Perfil.intervaloAtaque);
    }

    // La reacción ya corta el ataque en el Animator; aquí solo se reinicia la espera
    private void AlRecibirGolpe(Golpe golpe) => Esperar(oponente.Perfil.esperaTrasGolpe);

    private void AlLevantarse() => proximoAtaque = Time.time + oponente.Perfil.esperaTrasLevantarse;

    private void Esperar(Vector2 rango) => proximoAtaque = Time.time + Random.Range(rango.x, rango.y);

    private void MirarObjetivo()
    {
        if (objetivo == null && Camera.main != null) objetivo = Camera.main.transform;
        if (objetivo == null) return;

        Vector3 direccion = objetivo.position - transform.position;
        direccion.y = 0f;
        if (direccion.sqrMagnitude < 0.001f) return;

        Quaternion mirar = Quaternion.LookRotation(direccion);
        transform.rotation = Quaternion.Slerp(transform.rotation, mirar, velocidadGiro * Time.deltaTime);
    }

    private static bool EsAtaque(int estado) => estado == AtaqueJab || estado == AtaqueUppercut;
}
