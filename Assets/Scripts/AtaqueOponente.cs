using UnityEngine;

/// <summary>
/// Ataca al jugador cada cierto tiempo (jabs y, cada uppercutCada ataques, un uppercut) y gira para mirarlo.
/// Cada golpe recibido reinicia la espera (esperaTrasGolpe), así que si el jugador lo presiona no le da tiempo a atacar.
/// Si lo golpean mientras ataca, ReaccionAnimada cambia de estado y el ataque se corta. Caído no ataca.
/// Al empezar el ataque fija el punto donde está la cabeza del jugador; al pasar retardoImpacto, si el ataque
/// sigue en curso, le avisa a SaludJugador, que decide si el jugador esquivó, bloqueó o recibió el golpe.
/// IA reactiva (perfil): ataca a velocidadAtaque, se acerca si el jugador se aleja (sin salir del ring de LimiteRing),
/// adelanta el ataque si el jugador baja la guardia (castigoGuardiaBaja) y contraataca pronto tras bloquear.
/// Choque con la guardia: si el jugador tiene las manos arriba y el golpe iba a llegar, el guante se frena contra sus
/// manos justo antes del impacto y, si el golpe resulta bloqueado, se queda un instante y se retira, en vez de seguir
/// de largo atravesándolas. No cambia el daño ni quién bloquea: eso lo sigue decidiendo SaludJugador.
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

    [Header("Choque con la guardia")]
    [Tooltip("Si está activo, el guante se frena contra tus manos cuando bloqueas, en vez de atravesarlas")]
    [SerializeField] private bool chocarConGuardia = true;
    [Tooltip("Segundos antes del impacto en los que, con tu guardia arriba y el golpe en camino, el guante se frena")]
    [SerializeField, Min(0f)] private float anticipoGuardia = 0.08f;
    [Tooltip("Segundos que el guante se queda frenado contra tu guardia tras un bloqueo")]
    [SerializeField, Min(0f)] private float pausaGuardia = 0.08f;
    [Tooltip("Segundos que tarda en retirar el brazo y volver a la guardia tras chocar")]
    [SerializeField, Min(0.02f)] private float retornoGuardia = 0.1f;

    private Oponente oponente;
    private SaludJugador jugador;
    private float proximoAtaque;
    private int ataques;
    private float impactoEn = -1f;
    private bool impactoEsUppercut;
    private Vector3 puntoApuntado;
    private LimiteRing ring;
    private bool frenado;     // el Animator está detenido con el guante contra la guardia
    private bool retirando;   // el golpe fue bloqueado: tras pausaGuardia se retira el brazo
    private float retirarEn;
    private bool pausado;
    private Vector3? destinoPausa;

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
        frenado = retirando = false;
        animator.speed = 1f;
    }

    private void Update()
    {
        // velocidadAtaque solo acelera el ataque: en cuanto termina (o lo cortan) el Animator vuelve a su ritmo
        if (!Mathf.Approximately(animator.speed, 1f) && !Atacando) animator.speed = 1f;

        if (jugador == null) jugador = FindAnyObjectByType<SaludJugador>();
        GestionarFreno();

        if (oponente.Caido)
        {
            impactoEn = -1f;
            return;
        }
        MirarObjetivo();

        if (pausado)
        {
            CaminarAlDestino();
            return;
        }

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

    /// <summary>
    /// Deja de atacar y de buscar al jugador (descanso entre rounds, fin del combate) y corta el golpe que estuviera
    /// lanzando. Sigue mirando al jugador y, si se indica un destino, camina hasta él.
    /// </summary>
    public void Pausar(Vector3? destino = null)
    {
        pausado = true;
        destinoPausa = destino;
        impactoEn = -1f;
        frenado = retirando = false;
        animator.speed = 1f;
        // Si acaba de caer, no le pisa la animación de caída
        if (Atacando && !oponente.Caido) animator.CrossFadeInFixedTime(Idle, retornoGuardia, 0, 0f);
    }

    /// <summary>Vuelve a pelear; el primer ataque llega tras la espera normal.</summary>
    public void Reanudar()
    {
        pausado = false;
        destinoPausa = null;
        Esperar(oponente.Perfil.intervaloAtaque);
    }

    private void CaminarAlDestino()
    {
        if (destinoPausa == null) return;
        transform.position = Vector3.MoveTowards(transform.position, destinoPausa.Value, oponente.Perfil.velocidadDesplazamiento * Time.deltaTime);
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

        ResultadoAtaque resultado = jugador.RecibirAtaque(
            origen,
            haciaPunto.normalized,
            alcance,
            p.radioImpacto,
            impactoEsUppercut ? p.danoAtaqueUppercut : p.danoAtaqueJab);

        if (!chocarConGuardia) return;
        if (resultado == ResultadoAtaque.Bloqueado)
        {
            // El guante choca con la guardia: se queda quieto un instante y después se retira
            frenado = true;
            retirando = true;
            retirarEn = Time.time + pausaGuardia;
            animator.speed = 0f;
        }
        else if (frenado)
        {
            Soltar(true); // iba frenado pero el golpe conectó o falló: sigue su animación
        }
    }

    // Frena el guante contra la guardia antes del impacto y lo retira después de un bloqueo
    private void GestionarFreno()
    {
        if (!chocarConGuardia) return;

        if (!frenado)
        {
            bool cercaDelImpacto = anticipoGuardia > 0f && impactoEn >= 0f && Time.time >= impactoEn - anticipoGuardia;
            if (cercaDelImpacto && !oponente.Caido && jugador != null && !jugador.Derribado
                && jugador.EnGuardia && Atacando && ElGolpeLlegaria())
            {
                frenado = true;
                retirando = false;
                animator.speed = 0f;
            }
            return;
        }

        // Ya frenado: se suelta si lo golpean/caen, si el ataque terminó o si el impacto se canceló sin choque
        if (oponente.Caido || !Atacando) { Soltar(false); return; }
        if (retirando)
        {
            if (Time.time >= retirarEn) RetirarGuante();
        }
        else if (impactoEn < 0f)
        {
            Soltar(true);
        }
    }

    // Misma geometría que SaludJugador.RecibirAtaque: ¿el golpe, tal como va, alcanzaría la cabeza?
    private bool ElGolpeLlegaria()
    {
        PerfilOponente p = oponente.Perfil;
        Vector3 origen = transform.position + Vector3.up * p.alturaAtaque;
        Vector3 haciaPunto = puntoApuntado - origen;
        float alcance = Mathf.Min(haciaPunto.magnitude, p.alcanceAtaque);
        Vector3 fin = origen + haciaPunto.normalized * alcance;

        Vector3 recorrido = fin - origen;
        float t = recorrido.sqrMagnitude > 0.0001f
            ? Mathf.Clamp01(Vector3.Dot(jugador.PosicionCabeza - origen, recorrido) / recorrido.sqrMagnitude)
            : 0f;
        Vector3 masCercano = origen + recorrido * t;
        return Vector3.Distance(jugador.PosicionCabeza, masCercano) <= p.radioImpacto;
    }

    private void Soltar(bool reanudarAtaque)
    {
        frenado = retirando = false;
        animator.speed = reanudarAtaque ? oponente.Perfil.velocidadAtaque : 1f;
    }

    private void RetirarGuante()
    {
        frenado = retirando = false;
        animator.speed = 1f;
        animator.CrossFadeInFixedTime(Idle, retornoGuardia, 0, 0f);
    }

    // La reacción ya corta el ataque en el Animator; aquí se cancela el impacto y se reinicia la espera
    private void AlRecibirGolpe(Golpe golpe)
    {
        impactoEn = -1f;
        if (frenado) Soltar(false);
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