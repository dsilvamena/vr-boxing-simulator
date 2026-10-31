using UnityEngine;

/// <summary>
/// Ataca al jugador cada cierto tiempo (jabs y, cada uppercutCada ataques, un uppercut) y gira para mirarlo.
/// Cada golpe recibido reinicia la espera (esperaTrasGolpe), así que si el jugador lo presiona no le da tiempo a atacar.
/// Si lo golpean mientras ataca, ReaccionAnimada cambia de estado y el ataque se corta. Caído no ataca.
/// Al empezar el ataque fija el punto donde está la cabeza del jugador; al pasar retardoImpacto, si el ataque
/// sigue en curso, le avisa a SaludJugador, que decide si el jugador esquivó, bloqueó o recibió el golpe.
/// IA reactiva (perfil): ataca a velocidadAtaque, se acerca si el jugador se aleja (sin salir del ring de LimiteRing),
/// adelanta el ataque si el jugador baja la guardia (castigoGuardiaBaja) y contraataca pronto tras bloquear.
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
    [Tooltip("Distancia que deja entre su centro y las cuerdas al acercarse, en metros")]
    [SerializeField] private float margenRing = 0.4f;

    private Oponente oponente;
    private SaludJugador jugador;
    private float proximoAtaque;
    private int ataques;
    private float impactoEn = -1f;
    private bool impactoEsUppercut;
    private Vector3 puntoApuntado;
    private LimiteRing ring;

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
        oponente.AlBloquear += AlBloquear;
        oponente.AlLevantarse += AlLevantarse;
        Esperar(oponente.Perfil.intervaloAtaque);
    }

    private void OnDisable()
    {
        oponente.AlRecibirGolpe -= AlRecibirGolpe;
        oponente.AlBloquear -= AlBloquear;
        oponente.AlLevantarse -= AlLevantarse;
        animator.speed = 1f;
    }

    private void Update()
    {
        // velocidadAtaque solo acelera el ataque: en cuanto termina (o lo cortan) el Animator vuelve a su ritmo
        if (!Mathf.Approximately(animator.speed, 1f) && !Atacando) animator.speed = 1f;

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

        PerfilOponente p = oponente.Perfil;
        // Guardia baja = abertura: la espera hasta el próximo ataque corre más rápido
        if (jugador != null && !jugador.EnGuardia) proximoAtaque -= Time.deltaTime * p.castigoGuardiaBaja;

        // Solo desde la guardia: si aún está reaccionando a un golpe, espera a que vuelva a Idle
        bool enIdle = !animator.IsInTransition(0) && animator.GetCurrentAnimatorStateInfo(0).shortNameHash == Idle;
        if (enIdle) Acercarse();
        if (Time.time < proximoAtaque || !enIdle) return;

        ataques++;
        int uppercutCada = oponente.Perfil.uppercutCada;
        bool uppercut = uppercutCada > 0 && ataques % uppercutCada == 0;
        animator.CrossFadeInFixedTime(uppercut ? AtaqueUppercut : AtaqueJab, mezclaAtaque, 0, 0f);
        animator.speed = p.velocidadAtaque;

        // Apunta a donde está la cabeza del jugador ahora; si se mueve antes del impacto, esquiva
        puntoApuntado = jugador != null
            ? jugador.PosicionCabeza
            : transform.position + Vector3.up * oponente.Perfil.alturaAtaque + transform.forward * oponente.Perfil.alcanceAtaque;

        impactoEsUppercut = uppercut;
        float retardo = uppercut ? p.retardoImpactoUppercut : p.retardoImpactoJab;
        impactoEn = Time.time + retardo / Mathf.Max(0.1f, p.velocidadAtaque);
        Esperar(p.intervaloAtaque);
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

    // Paró el golpe: aprovecha que el jugador quedó expuesto
    private void AlBloquear(Golpe golpe) => Esperar(oponente.Perfil.contraataqueTrasBloqueo);

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

    // Si el jugador se aleja más de distanciaCombate, camina hacia él sin salir del ring
    private void Acercarse()
    {
        PerfilOponente p = oponente.Perfil;
        if (p.velocidadDesplazamiento <= 0f || objetivo == null) return;

        Vector3 haciaJugador = objetivo.position - transform.position;
        haciaJugador.y = 0f;
        float sobra = haciaJugador.magnitude - p.distanciaCombate;
        if (sobra <= 0.05f) return;

        Vector3 destino = transform.position + haciaJugador.normalized * Mathf.Min(sobra, p.velocidadDesplazamiento * Time.deltaTime);
        if (ring == null) ring = FindAnyObjectByType<LimiteRing>();
        if (ring != null)
        {
            Rect area = ring.AreaRing;
            destino.x = Mathf.Clamp(destino.x, area.xMin + margenRing, area.xMax - margenRing);
            destino.z = Mathf.Clamp(destino.z, area.yMin + margenRing, area.yMax - margenRing);
        }
        transform.position = destino;
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