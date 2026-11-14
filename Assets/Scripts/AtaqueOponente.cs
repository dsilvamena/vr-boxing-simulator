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
/// Juego de pies: en guardia no se queda clavado. Se desplaza de lado alrededor del jugador (velocidadLateral del perfil),
/// cambiando de sentido cada pocos segundos y sin salir de un arco a cada lado de la línea en la que empezó, para que el
/// jugador solo tenga que girar un poco y no dar la vuelta entera. Cada golpe que recibe lo echa unos centímetros hacia
/// atrás (retrocesoPorGolpe) y un muelle lo devuelve a su sitio en una fracción de segundo: encaja el golpe sin irse
/// alejando del jugador, que en un espacio real no puede perseguirlo. Los pasos los pone PasosProcedurales.
/// Burla: cuando tumba al jugador, tras un instante hace el gesto de burla (estado Burla del controlador, si existe)
/// una vez por caída, según probabilidadBurla del perfil; si el jugador se levanta antes, vuelve a la guardia.
/// </summary>
[RequireComponent(typeof(Oponente))]
public class AtaqueOponente : MonoBehaviour
{
    private static readonly int Idle = Animator.StringToHash("Idle");
    private static readonly int AtaqueJab = Animator.StringToHash("Ataque_Jab");
    private static readonly int AtaqueUppercut = Animator.StringToHash("Ataque_Uppercut");
    private static readonly int AtaqueGancho = Animator.StringToHash("Ataque_Gancho");
    private static readonly int AtaqueGancho2 = Animator.StringToHash("Ataque_Gancho_2");
    private static readonly int Burla = Animator.StringToHash("Burla");

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

    [Header("Juego de pies")]
    [Tooltip("Si está activo, en guardia se desplaza de lado alrededor del jugador y cada golpe lo empuja hacia atrás")]
    [SerializeField] private bool juegoDePies = true;
    [Tooltip("Grados a cada lado de la línea inicial jugador-rival que puede recorrer de lado")]
    [SerializeField, Range(0f, 180f)] private float arcoLateral = 35f;
    [Tooltip("Segundos que mantiene un sentido (o se queda quieto) antes de elegir otro")]
    [SerializeField] private Vector2 segundosLateral = new Vector2(1.2f, 3.2f);
    [Tooltip("Probabilidad de quedarse quieto en vez de desplazarse, cada vez que elige")]
    [SerializeField, Range(0f, 1f)] private float probabilidadQuieto = 0.3f;
    [Tooltip("Rigidez del muelle que lo devuelve a su sitio tras el empujón de un golpe (más alto = vuelve antes)")]
    [SerializeField, Min(1f)] private float rigidezEmpuje = 70f;

    private Oponente oponente;
    private SaludJugador jugador;
    private int sentidoLateral;
    private float cambioLateralEn;
    private Vector3 rumboInicial;   // del jugador al rival cuando empezó, en el plano del suelo
    private bool hayRumboInicial;
    private Vector3 empuje;         // cuánto lo tiene desplazado ahora el empujón de los golpes; vuelve a cero solo
    private Vector3 velocidadEmpuje;
    private float burlaEn = -1f;    // cuándo empieza la burla de esta caída del jugador; -1 = aún no se decidió
    private bool burlaResuelta;     // ya se burló (o decidió no hacerlo) en esta caída
    private bool tieneBurla;
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
        tieneBurla = animator != null && animator.HasState(0, Burla);
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
        AplicarEmpuje();

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
            Burlarse();
            return;
        }
        TerminarBurla();

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
        if (enIdle && (esquive == null || !esquive.EnMovimiento)) MoverPies();
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

        // El esfuerzo del golpe se oye antes de que llegue: también avisa al jugador
        oponente.Sonar(p.sonidosAtaque, transform.position + Vector3.up * p.alturaAtaque, 0.8f);

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

    // El jugador está en la lona: espera a que termine su propio golpe y se burla una vez
    private void Burlarse()
    {
        if (!tieneBurla || burlaResuelta) return;
        if (burlaEn < 0f)
        {
            if (Random.value >= oponente.Perfil.probabilidadBurla) { burlaResuelta = true; return; }
            burlaEn = Time.time + 0.9f;
        }
        if (Time.time < burlaEn || animator.IsInTransition(0) || animator.GetCurrentAnimatorStateInfo(0).shortNameHash != Idle) return;
        animator.CrossFadeInFixedTime(Burla, 0.3f, 0, 0f);
        burlaResuelta = true;
    }

    // El jugador se levantó: si seguía con el gesto, vuelve a la guardia
    private void TerminarBurla()
    {
        if (!burlaResuelta && burlaEn < 0f) return;
        burlaResuelta = false;
        burlaEn = -1f;
        if (tieneBurla && animator.GetCurrentAnimatorStateInfo(0).shortNameHash == Burla && !animator.IsInTransition(0))
            animator.CrossFadeInFixedTime(Idle, 0.3f, 0, 0f);
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
        Empujar(golpe, 1f);
    }

    // Paró el golpe: aprovecha que el jugador quedó expuesto
    private void AlBloquear(Golpe golpe)
    {
        Esperar(oponente.Perfil.contraataqueTrasBloqueo);
        Empujar(golpe, 0.5f);
    }

    // El golpe lo desplaza hacia donde iba el puño. Con amortiguación crítica el muelle llega a v0 / (e·ω) y vuelve sin
    // pasarse hacia el jugador, así que esta velocidad inicial lo lleva justo a "metros"
    private void Empujar(Golpe golpe, float escala)
    {
        if (!juegoDePies) return;
        Vector3 direccion = golpe.velocidad;
        direccion.y = 0f;
        if (direccion.sqrMagnitude < 0.0001f) return;
        float metros = oponente.Perfil.retrocesoPorGolpe * Mathf.Lerp(0.3f, 1f, Mathf.Clamp01(golpe.intensidad)) * escala;
        velocidadEmpuje += direccion.normalized * (metros * 2.718f * Mathf.Sqrt(rigidezEmpuje));
    }

    // Se aplica como diferencia de un fotograma a otro (igual que el esquive): se suma a la caminata y no deja desvío
    private void AplicarEmpuje()
    {
        if (empuje.sqrMagnitude < 1e-8f && velocidadEmpuje.sqrMagnitude < 1e-6f)
        {
            if (empuje != Vector3.zero) transform.position -= empuje;
            empuje = velocidadEmpuje = Vector3.zero;
            return;
        }
        float dt = Mathf.Min(Time.deltaTime, 0.033f);
        float amortiguacion = 2f * Mathf.Sqrt(rigidezEmpuje);
        velocidadEmpuje += (-rigidezEmpuje * empuje - amortiguacion * velocidadEmpuje) * dt;
        Vector3 nuevo = empuje + velocidadEmpuje * dt;
        transform.position = EnElRing(transform.position + nuevo - empuje);
        empuje = nuevo;
    }

    private Vector3 EnElRing(Vector3 punto)
    {
        if (ring == null) ring = FindAnyObjectByType<LimiteRing>();
        return ring != null ? ring.DentroDelRing(punto, margenRing) : punto;
    }

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

    // En guardia: si el jugador se aleja más de distanciaCombate camina hacia él, y mientras tanto se desplaza de lado
    // a su alrededor. Todo sin salir del ring.
    private void MoverPies()
    {
        PerfilOponente p = oponente.Perfil;
        if (objetivo == null) return;

        Vector3 desdeJugador = transform.position - objetivo.position;
        desdeJugador.y = 0f;
        float distancia = desdeJugador.magnitude;
        if (distancia < 0.01f) return;
        Vector3 radial = desdeJugador / distancia;
        if (!hayRumboInicial)
        {
            rumboInicial = radial;
            hayRumboInicial = true;
        }

        Vector3 movimiento = Vector3.zero;
        float sobra = distancia - p.distanciaCombate;
        if (p.velocidadDesplazamiento > 0f && sobra > 0.05f)
            movimiento -= radial * Mathf.Min(sobra, p.velocidadDesplazamiento * Time.deltaTime);

        // De lado solo cuando ya está a distancia de pelea: si viene de lejos, primero llega
        if (juegoDePies && p.velocidadLateral > 0f && sobra < 0.3f)
        {
            if (Time.time >= cambioLateralEn)
            {
                sentidoLateral = Random.value < probabilidadQuieto ? 0 : (Random.value < 0.5f ? -1 : 1);
                cambioLateralEn = Time.time + Random.Range(segundosLateral.x, segundosLateral.y);
            }
            // Al llegar al borde del arco da la vuelta
            float angulo = Vector3.SignedAngle(rumboInicial, radial, Vector3.up);
            if (sentidoLateral != 0 && Mathf.Abs(angulo) > arcoLateral && Mathf.Sign(angulo) == sentidoLateral)
                sentidoLateral = -sentidoLateral;
            movimiento += Vector3.Cross(Vector3.up, radial) * (sentidoLateral * p.velocidadLateral * Time.deltaTime);
        }
        if (movimiento.sqrMagnitude < 1e-10f) return;

        Vector3 destino = transform.position + movimiento;
        Vector3 limitado = EnElRing(destino);
        // Las cuerdas no lo dejan seguir por ahí: prueba hacia el otro lado
        if ((limitado - destino).sqrMagnitude > 1e-8f && sentidoLateral != 0) sentidoLateral = -sentidoLateral;
        transform.position = limitado;
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