using UnityEngine;

/// <summary>
/// Ataca al jugador cada cierto tiempo (jabs y, cada uppercutCada ataques, un uppercut) y gira para mirarlo.
/// Cada golpe recibido reinicia la espera (esperaTrasGolpe), así que si el jugador lo presiona no le da tiempo a atacar.
/// Si lo golpean mientras ataca, ReaccionAnimada cambia de estado y el ataque se corta. Caído no ataca.
/// Al empezar el ataque fija el punto donde está la cabeza del jugador; al pasar retardoImpacto, si el ataque
/// sigue en curso, le avisa a SaludJugador, que decide si el jugador esquivó, bloqueó o recibió el golpe.
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
    private SaludJugador jugador;
    private float proximoAtaque;
    private int ataques;
    private float impactoEn = -1f;
    private bool impactoEsUppercut;
    private Vector3 puntoApuntado;

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
        if (oponente.Caido)
        {
            impactoEn = -1f;
            return;
        }
        MirarObjetivo();

        // Si el jugador está en el piso (cuenta de 10 s), no ataca; al levantarse espera esperaTrasLevantarse
        if (jugador == null) jugador = FindAnyObjectByType<SaludJugador>();
        if (jugador != null && jugador.Derribado)
        {
            impactoEn = -1f;
            proximoAtaque = Mathf.Max(proximoAtaque, Time.time + oponente.Perfil.esperaTrasLevantarse);
            return;
        }

        ResolverImpacto();

        if (Time.time < proximoAtaque) return;
        // Solo desde la guardia: si aún está reaccionando a un golpe, espera a que vuelva a Idle
        if (animator.IsInTransition(0) || animator.GetCurrentAnimatorStateInfo(0).shortNameHash != Idle) return;

        ataques++;
        int uppercutCada = oponente.Perfil.uppercutCada;
        bool uppercut = uppercutCada > 0 && ataques % uppercutCada == 0;
        animator.CrossFadeInFixedTime(uppercut ? AtaqueUppercut : AtaqueJab, mezclaAtaque, 0, 0f);

        // Apunta a donde está la cabeza del jugador ahora; si se mueve antes del impacto, esquiva
        puntoApuntado = jugador != null
            ? jugador.PosicionCabeza
            : transform.position + Vector3.up * oponente.Perfil.alturaAtaque + transform.forward * oponente.Perfil.alcanceAtaque;

        impactoEsUppercut = uppercut;
        impactoEn = Time.time + (uppercut ? oponente.Perfil.retardoImpactoUppercut : oponente.Perfil.retardoImpactoJab);
        Esperar(oponente.Perfil.intervaloAtaque);
    }

    private void ResolverImpacto()
    {
        if (impactoEn < 0f || Time.time < impactoEn) return;
        impactoEn = -1f;
        if (!Atacando) return; // lo golpearon y el ataque se cortó
        if (jugador == null) return;

        PerfilOponente p = oponente.Perfil;
        Vector3 origen = transform.position + Vector3.up * p.alturaAtaque;
        Vector3 haciaPunto = puntoApuntado - origen;
        // Si el punto apuntado queda más lejos que el alcance, el golpe se queda corto
        float alcance = Mathf.Min(haciaPunto.magnitude, p.alcanceAtaque);

        jugador.RecibirAtaque(
            origen,
            haciaPunto.normalized,
            alcance,
            p.radioImpacto,
            impactoEsUppercut ? p.danoAtaqueUppercut : p.danoAtaqueJab);
    }

    // La reacción ya corta el ataque en el Animator; aquí se cancela el impacto y se reinicia la espera
    private void AlRecibirGolpe(Golpe golpe)
    {
        impactoEn = -1f;
        Esperar(oponente.Perfil.esperaTrasGolpe);
    }

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

    // Con el oponente seleccionado se ve el alcance máximo del golpe; el ataque real apunta a la cabeza del jugador
    private void OnDrawGizmosSelected()
    {
        Oponente o = GetComponent<Oponente>();
        if (o == null || o.Perfil == null) return;
        PerfilOponente p = o.Perfil;
        Vector3 a = transform.position + Vector3.up * p.alturaAtaque;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(a, p.alcanceAtaque);
    }
}