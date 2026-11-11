using UnityEngine;

/// <summary>
/// Ataca al jugador cada cierto tiempo y gira para mirarlo. Si el perfil tiene combo, cada ataque encadena esos golpes;
/// si está vacío, cada ataque es un golpe (jabs y, cada uppercutCada ataques, un uppercut).
/// Cada golpe recibido reinicia la espera (esperaTrasGolpe), así que si el jugador lo presiona no le da tiempo a atacar.
/// Si lo golpean mientras ataca, ReaccionAnimada cambia de estado y el ataque se corta. Caído no ataca.
/// Al empezar cada golpe fija el punto donde está la cabeza del jugador; al pasar retardoImpacto, si el ataque
/// sigue en curso, le avisa a SaludJugador, que decide si el jugador esquivó, bloqueó o recibió el golpe.
/// Combo: el siguiente golpe arranca segundosEntreGolpesCombo después del impacto del anterior, sin pasar por Idle. Un
/// golpe conectado, una caída, el jugador en el piso, Pausar (fin del round) o desactivar el componente cancelan el
/// resto del combo y el impacto pendiente. Un golpe que el jugador bloquea no lo cancela.
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
    private static readonly int AtaqueGancho = Animator.StringToHash("Ataque_Gancho");
    private static readonly int AtaqueGancho2 = Animator.StringToHash("Ataque_Gancho_2");

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
    private TipoAtaque ataqueEnCurso;
    private int siguienteDelCombo = -1; // índice del próximo golpe del combo; -1 = no hay combo en curso
    private float siguienteGolpeEn;
    private EsquiveOponente esquive;
    private Vector3 puntoApuntado;
    private LimiteRing ring;
    private bool frenado;     // el Animator está detenido con el guante contra la guardia
    private bool retirando;   // el golpe fue bloqueado: tras pausaGuardia se retira el brazo
    private float retirarEn;
    private bool pausado;
    private Vector3? destinoPausa;

    /// <summary>Está lanzando un golpe (o empezando a lanzarlo), o en medio de un combo.</summary>
    public bool Atacando => AnimandoAtaque || ComboEnCurso;

    private bool ComboEnCurso => siguienteDelCombo >= 0;

    // Solo mira el Animator: un golpe cortado por una reacción deja de contar aquí
    private bool AnimandoAtaque
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
        esquive = GetComponent<EsquiveOponente>();
    }

    private void OnEnable()
    {
        oponente.AlRecibirGolpe += AlRecibirGolpe;
        oponente.AlBloquear += AlBloquear;
        oponente.AlEsquivar += AlEsquivar;
        oponente.AlCaer += AlCaer;
        oponente.AlLevantarse += AlLevantarse;
        Esperar(oponente.Perfil.intervaloAtaque);
    }

    private void OnDisable()
    {
        oponente.AlRecibirGolpe -= AlRecibirGolpe;
        oponente.AlBloquear -= AlBloquear;
        oponente.AlEsquivar -= AlEsquivar;
        oponente.AlCaer -= AlCaer;
        oponente.AlLevantarse -= AlLevantarse;
        CancelarGolpes();
        frenado = retirando = false;
        animator.speed = 1f;
    }

    private void Update()
    {
        // velocidadAtaque solo acelera el ataque: en cuanto termina (o lo cortan) el Animator vuelve a su ritmo
        if (!Mathf.Approximately(animator.speed, 1f) && !AnimandoAtaque) animator.speed = 1f;

        if (jugador == null) jugador = FindAnyObjectByType<SaludJugador>();
        GestionarFreno();

        if (oponente.Caido)
        {
            CancelarGolpes();
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
            CancelarGolpes();
            proximoAtaque = Mathf.Max(proximoAtaque, Time.time + oponente.Perfil.esperaTrasLevantarse);
            return;
        }

        ResolverImpacto();

        PerfilOponente p = oponente.Perfil;
        if (ComboEnCurso)
        {
            SeguirCombo(p);
            return;
        }

        // Guardia baja = abertura: la espera hasta el próximo ataque corre más rápido
        if (jugador != null && !jugador.EnGuardia) proximoAtaque -= Time.deltaTime * p.castigoGuardiaBaja;

        // Solo desde la guardia: si aún está reaccionando a un golpe, espera a que vuelva a Idle
        bool enIdle = !animator.IsInTransition(0) && animator.GetCurrentAnimatorStateInfo(0).shortNameHash == Idle;
        // Mientras se aparta o vuelve de un esquive no camina: ese movimiento se suma a este y se pelearían
        if (enIdle && (esquive == null || !esquive.EnMovimiento)) Acercarse();
        if (Time.time < proximoAtaque || !enIdle) return;
        // Con la espera corta de Tyson llegaba a lanzar combos enteros al aire mientras aún caminaba hacia el jugador:
        // fuera de alcance sigue acercándose, y el ataque sale en cuanto llega
        if (FueraDeAlcance(p)) return;

        ataques++;
        if (p.combo != null && p.combo.Length > 0)
        {
            siguienteDelCombo = 0;
            LanzarSiguienteDelCombo(p);
            return;
        }

        bool uppercut = p.uppercutCada > 0 && ataques % p.uppercutCada == 0;
        Lanzar(uppercut ? TipoAtaque.Uppercut : TipoAtaque.Jab, p);
        Esperar(p.intervaloAtaque);
    }

    // Misma geometría que el impacto (ResolverImpacto): el golpe sale de alturaAtaque y llega hasta alcanceAtaque
    private bool FueraDeAlcance(PerfilOponente p) =>
        jugador != null && Vector3.Distance(transform.position + Vector3.up * p.alturaAtaque, jugador.PosicionCabeza) > p.alcanceAtaque;

    private void Lanzar(TipoAtaque tipo, PerfilOponente p)
    {
        animator.CrossFadeInFixedTime(Estado(tipo), mezclaAtaque, 0, 0f);
        animator.speed = p.velocidadAtaque;

        // Apunta a donde está la cabeza del jugador ahora; si se mueve antes del impacto, esquiva
        puntoApuntado = jugador != null
            ? jugador.PosicionCabeza
            : transform.position + Vector3.up * p.alturaAtaque + transform.forward * p.alcanceAtaque;

        ataqueEnCurso = tipo;
        impactoEn = Time.time + p.RetardoImpacto(tipo) / Mathf.Max(0.1f, p.velocidadAtaque);
    }

    // Espera el impacto del golpe en curso y a que el guante se suelte de la guardia antes de lanzar el siguiente
    private void SeguirCombo(PerfilOponente p)
    {
        if (impactoEn >= 0f || frenado || Time.time < siguienteGolpeEn) return;
        LanzarSiguienteDelCombo(p);
    }

    // El último golpe ya no deja combo en curso: desde él corre la espera normal hasta el próximo ataque
    private void LanzarSiguienteDelCombo(PerfilOponente p)
    {
        Lanzar(p.combo[siguienteDelCombo], p);
        siguienteDelCombo++;
        if (siguienteDelCombo >= p.combo.Length)
        {
            siguienteDelCombo = -1;
            Esperar(p.intervaloAtaque);
        }
    }

    // Lo que tenía en camino ya no llega: ni el impacto pendiente ni el resto del combo
    private void CancelarGolpes()
    {
        impactoEn = -1f;
        siguienteDelCombo = -1;
    }

    /// <summary>
    /// Deja de atacar y de buscar al jugador (descanso entre rounds, fin del combate) y corta el golpe que estuviera
    /// lanzando. Sigue mirando al jugador y, si se indica un destino, camina hasta él.
    /// </summary>
    public void Pausar(Vector3? destino = null)
    {
        pausado = true;
        destinoPausa = destino;
        CancelarGolpes();
        frenado = retirando = false;
        animator.speed = 1f;
        // Si acaba de caer, no le pisa la animación de caída
        if (AnimandoAtaque && !oponente.Caido) animator.CrossFadeInFixedTime(Idle, retornoGuardia, 0, 0f);
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
        PerfilOponente p = oponente.Perfil;
        if (!AnimandoAtaque)
        {
            // El golpe se cortó sin que nadie lo cancelara: el combo tampoco sigue
            if (ComboEnCurso)
            {
                siguienteDelCombo = -1;
                Esperar(p.intervaloAtaque);
            }
            return;
        }
        siguienteGolpeEn = Time.time + p.segundosEntreGolpesCombo;
        if (jugador == null) return;

        Vector3 origen = transform.position + Vector3.up * p.alturaAtaque;
        Vector3 haciaPunto = puntoApuntado - origen;
        // Si el punto apuntado queda más lejos que el alcance, el golpe se queda corto
        float alcance = Mathf.Min(haciaPunto.magnitude, p.alcanceAtaque);

        ResultadoAtaque resultado = jugador.RecibirAtaque(
            origen,
            haciaPunto.normalized,
            alcance,
            p.radioImpacto,
            p.DanoAtaque(ataqueEnCurso));

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
                && jugador.EnGuardia && AnimandoAtaque && ElGolpeLlegaria())
            {
                frenado = true;
                retirando = false;
                animator.speed = 0f;
            }
            return;
        }

        // Ya frenado: se suelta si lo golpean/caen, si el ataque terminó o si el impacto se canceló sin choque
        if (oponente.Caido || !AnimandoAtaque) { Soltar(false); return; }
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

    // La reacción ya corta el ataque en el Animator; aquí se cancelan el impacto y el combo, y se reinicia la espera
    private void AlRecibirGolpe(Golpe golpe)
    {
        CancelarGolpes();
        if (frenado) Soltar(false);
        Esperar(oponente.Perfil.esperaTrasGolpe);
    }

    // Paró el golpe: aprovecha que el jugador quedó expuesto
    private void AlBloquear(Golpe golpe) => Esperar(oponente.Perfil.contraataqueTrasBloqueo);

    // Se apartó y el jugador quedó con el brazo estirado: contraataca enseguida
    private void AlEsquivar(Golpe golpe) => Esperar(oponente.Perfil.ventanaContraataque);

    // Una caída sin golpe (Derribar) no pasa por AlRecibirGolpe
    private void AlCaer(Golpe golpe) => CancelarGolpes();

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
        transform.position = ring != null ? ring.DentroDelRing(destino, margenRing) : destino;
    }

    private static bool EsAtaque(int estado) =>
        estado == AtaqueJab || estado == AtaqueUppercut || estado == AtaqueGancho || estado == AtaqueGancho2;

    private static int Estado(TipoAtaque tipo) => tipo switch
    {
        TipoAtaque.Jab => AtaqueJab,
        TipoAtaque.Uppercut => AtaqueUppercut,
        TipoAtaque.Gancho => AtaqueGancho,
        _ => AtaqueGancho2,
    };

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