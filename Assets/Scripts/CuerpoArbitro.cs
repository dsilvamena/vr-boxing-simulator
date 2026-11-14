using UnityEngine;

/// <summary>
/// Cuerpo visible del réferi (RF-12): se coloca según lo que pasa en el ring y marca la cuenta con el brazo.
/// - Durante el round acompaña la pelea a un lado de la línea entre el jugador y el rival, sin cruzarse en ella.
/// - En la cuenta del rival se pone junto a él, del lado del jugador para que este lo vea, y lo mira.
/// - En la cuenta del jugador se pone delante de él, a un lado del rival (como en el boceto del estado de knockout),
///   lo mira y muestra el cartel con los botones que hay que pulsar.
/// - Antes de cada campana (la preparación y el final del descanso) se pone en el centro, entre los dos, y al sonar da
///   la orden de pelear con un tajo del brazo ("¡Box!"). El resto del descanso espera en su sitio inicial.
/// - Si el combate acaba por nocaut lo para agitando los dos brazos por encima de la cabeza, y en cualquier final
///   señala al ganador y se queda donde está.
/// Cada número que dice el Arbitro baja el brazo de golpe y lo vuelve a subir, como un réferi de verdad: el brazo va
/// con un muelle amortiguado, el antebrazo con retraso y el torso cabecea un poco. En la cuenta del rival el brazo
/// sube cuando empieza la cuenta, con el rival ya tendido.
/// Se desplaza acelerando y frenando. Si el controlador tiene "AndarX"/"AndarZ" (los clips de caminar y paso lateral de
/// Tools > Boxeo > Crear caminar del réferi) le manda su velocidad local y el blend tree pone las piernas; los trayectos
/// largos fuera del round los hace de frente y al llegar se vuelve. En el round se desplaza de lado sin dejar de mirar la
/// pelea. Los giros en el sitio los pisa PasosProcedurales, en el objeto del Animator.
/// El modelo (hijo Modelo, Referee.fbx) es Humanoid: con un Animator humanoide y brazoCuenta vacío, el gesto se hace
/// girando el brazo derecho del esqueleto después de la animación, así que vale para cualquier rig humanoide. Con
/// brazoCuenta asignado se gira ese transform en su eje X local (modelos sin esqueleto). El Animator recibe, si los tiene,
/// el float "Agachado" (0 de pie, 1 la postura de vigilar), el trigger "Contar" en cada número y el bool "Contando".
/// La voz es hija de este objeto, así que se mueve con él.
/// Va en el hijo Cuerpo del Arbitro de cada escena de pelea. Usa tiempo escalado, así que se queda quieto en la pausa.
/// </summary>
public class CuerpoArbitro : MonoBehaviour
{
    private enum Modo { Neutral, Centro, Pelea, CuentaRival, CuentaJugador, Final }

    [Tooltip("Vacío = el Arbitro del padre")]
    [SerializeField] private Arbitro arbitro;
    [Tooltip("Vacío = el Oponente de la escena")]
    [SerializeField] private Oponente oponente;
    [Tooltip("Vacío = el SaludJugador de la escena")]
    [SerializeField] private SaludJugador jugador;
    [Tooltip("Vacío = el ControlRondas de la escena; sin él acompaña la pelea todo el tiempo")]
    [SerializeField] private ControlRondas rondas;
    [Tooltip("Vacío = el LimiteRing de la escena; sin él no se limita a las cuerdas")]
    [SerializeField] private LimiteRing limite;

    [Header("Modelo")]
    [Tooltip("Hombro del brazo que marca la cuenta: gira en su eje X local. Vacío = el brazo derecho del Animator si es Humanoid; si no, sin gesto")]
    [SerializeField] private Transform brazoCuenta;
    [Tooltip("Animator del modelo: recibe \"Agachado\", \"Contar\" y \"Contando\" si los tiene. Vacío = el de los hijos")]
    [SerializeField] private Animator animador;
    [Tooltip("Cartel con los botones del button mashing; solo se ve en la cuenta del jugador. Vacío = sin cartel")]
    [SerializeField] private GameObject cartelPulsar;

    [Header("Posiciones")]
    [Tooltip("Distancia a la línea de la pelea durante el round, en metros")]
    [SerializeField, Min(0.5f)] private float distanciaLateralPelea = 2f;
    [Tooltip("Cuenta del jugador: cuánto se adelanta hacia el rival desde la cabeza del jugador")]
    [SerializeField] private float adelanteCuentaJugador = 1.05f;
    [Tooltip("Cuenta del jugador: cuánto se aparta hacia un lado para no meterse en el rival")]
    [SerializeField] private float ladoCuentaJugador = 0.4f;
    [Tooltip("Cuenta del rival: cuánto se acerca al jugador desde el rival")]
    [SerializeField] private float adelanteCuentaRival = 0.7f;
    [Tooltip("Cuenta del rival: cuánto se aparta hacia un lado")]
    [SerializeField] private float ladoCuentaRival = 0.7f;
    [Tooltip("Distancia mínima al centro del rival y a la cabeza del jugador")]
    [SerializeField, Min(0f)] private float distanciaMinima = 0.6f;
    [Tooltip("Distancia a las cuerdas")]
    [SerializeField, Min(0f)] private float margenCuerdas = 0.35f;
    [Tooltip("Durante el round no se mueve hasta que su sitio se aleja esta distancia (evita que tiemble)")]
    [SerializeField, Min(0f)] private float zonaMuertaPelea = 0.35f;

    [Header("Movimiento")]
    [Tooltip("m/s al ir a contar o volver a su sitio")]
    [SerializeField, Min(0.1f)] private float velocidad = 1.6f;
    [Tooltip("m/s acompañando la pelea")]
    [SerializeField, Min(0.1f)] private float velocidadPelea = 0.8f;
    [Tooltip("Rapidez del giro")]
    [SerializeField, Min(0.1f)] private float velocidadGiro = 6f;
    [Tooltip("Aceleración máxima al arrancar y al frenar (m/s²). Con poca, el cuerpo no sale disparado antes de que el " +
             "clip de caminar entre y los pies no patinan")]
    [SerializeField, Min(0.1f)] private float aceleracion = 2.5f;
    [Tooltip("Fuera del round, si su sitio está más lejos que esto camina de frente hacia él (y al llegar se vuelve); más cerca, se desplaza sin dejar de mirar")]
    [SerializeField, Min(0f)] private float distanciaCaminarDeFrente = 0.9f;

    [Header("Caminar (Animator con \"AndarX\", \"AndarZ\" y \"RitmoPaso\")")]
    [Tooltip("Velocidad (m/s) para la que está hecho el ciclo de caminar hacia delante (Referee_Caminar)")]
    [SerializeField, Min(0.1f)] private float velocidadClipCaminar = 1.2f;
    [Tooltip("Velocidad (m/s) para la que está hecho el paso lateral (Referee_Lateral)")]
    [SerializeField, Min(0.1f)] private float velocidadClipLateral = 0.49f;
    [Tooltip("Segundos de suavizado de los parámetros de caminar")]
    [SerializeField, Min(0f)] private float suavizadoAndar = 0.12f;

    [Header("Gesto de la cuenta")]
    [Tooltip("Ángulo del hombro con el brazo arriba, esperando el número (negativo = hacia delante y arriba)")]
    [SerializeField] private float anguloArriba = -150f;
    [Tooltip("Ángulo del hombro al marcar el número, con el brazo hacia delante")]
    [SerializeField] private float anguloAbajo = -75f;
    [Tooltip("Segundos que el brazo se queda abajo tras decir el número antes de volver a subir")]
    [SerializeField, Min(0.01f)] private float segundosAbajo = 0.2f;
    [Tooltip("Segundos que tarda en volver a subirlo")]
    [SerializeField, Min(0.01f)] private float segundosSubir = 0.45f;

    [Header("Señas")]
    [Tooltip("Distancia a la línea de la pelea cuando espera la campana en el centro, en metros")]
    [SerializeField, Min(0.3f)] private float distanciaLateralCentro = 1.05f;
    [Tooltip("Segundos antes de la campana en los que deja su sitio del descanso y va al centro")]
    [SerializeField, Min(0f)] private float segundosAntesDeCampana = 4f;
    [Tooltip("Segundos que agita los brazos para parar el combate")]
    [SerializeField, Min(0.1f)] private float segundosAgitando = 2.6f;

    [Header("Postura (Animator con \"Agachado\")")]
    [Tooltip("Durante el round: 0 = de pie, 1 = la postura de vigilar entera, muy agachado")]
    [SerializeField, Range(0f, 1f)] private float agachadoPelea = 0.55f;
    [Tooltip("Contando al rival se inclina hacia él")]
    [SerializeField, Range(0f, 1f)] private float agachadoCuentaRival = 0.3f;
    [Tooltip("Rapidez con la que cambia de postura")]
    [SerializeField, Min(0.1f)] private float velocidadPostura = 2f;

    private static readonly int Contar = Animator.StringToHash("Contar");
    private static readonly int Contando = Animator.StringToHash("Contando");
    private static readonly int Agachado = Animator.StringToHash("Agachado");
    private static readonly int AndarX = Animator.StringToHash("AndarX");
    private static readonly int AndarZ = Animator.StringToHash("AndarZ");
    private static readonly int RitmoPaso = Animator.StringToHash("RitmoPaso");

    private Modo modo = Modo.Neutral;
    private Vector3 posicionInicial;
    private Quaternion rotacionInicial;
    private Vector3 destino;
    private float lado = 1f;          // lado elegido al empezar cada modo, para no saltar de uno a otro
    private bool siguiendoPelea;
    private float desdeNumero = 999f;  // segundos desde el último número; grande = sin gesto pendiente
    private float anguloBrazo;        // hombro: 0 = colgando, negativo = hacia delante y arriba
    private float velocidadBrazo;
    private float anguloAntebrazo;    // el antebrazo sigue al brazo con retraso (latigazo) y nunca se hiperextiende
    private float velocidadAntebrazo;
    private Vector3 velocidadMovimiento;
    private Transform pecho, cabezaHueso;
    private Vector3 escalaCartel;

    // Esqueleto humanoide: el gesto se aplica sobre la pose animada, con un peso que sube al empezar la cuenta
    private Transform hombro, codo, mano;
    private float pesoBrazo;
    private float agachado;
    private bool tieneContar, tieneContando, tieneAgachado, tieneAndar;
    private bool deFrente;            // camina hacia su sitio mirando hacia donde va

    // Señas: "¡Box!" reutiliza el brazo de la cuenta; señalar y agitar son poses de brazo que entran y salen por peso
    private Transform hombroIzquierdo, codoIzquierdo, manoIzquierda;
    private float tiempoBox = 99f;         // segundos desde la orden; grande = sin seña pendiente
    private bool tajoDado;
    private float senalarHasta = -1f;
    private Vector3 puntoSenalado;
    private bool senalaConIzquierdo;
    private float pesoSenalar;
    private float agitarHasta = -1f;
    private float pesoAgitar;
    private float faseAgitar;
    private bool combateTerminado;
    private bool ganoElJugador;
    private float senalarGanadorEn = -1f;

    public bool EnPosicion => PlanoXZ(destino - transform.position).magnitude < 0.05f;

    private void Awake()
    {
        if (arbitro == null) arbitro = GetComponentInParent<Arbitro>();
        if (oponente == null) oponente = FindAnyObjectByType<Oponente>();
        if (jugador == null) jugador = FindAnyObjectByType<SaludJugador>();
        if (rondas == null) rondas = FindAnyObjectByType<ControlRondas>();
        if (limite == null) limite = FindAnyObjectByType<LimiteRing>();
        if (arbitro == null || oponente == null || jugador == null)
        {
            Debug.LogWarning($"{name}: CuerpoArbitro necesita un Arbitro en el padre, un Oponente y un SaludJugador en la escena.", this);
            enabled = false;
            return;
        }
        if (animador == null) animador = GetComponentInChildren<Animator>();
        if (animador != null)
        {
            // Solo se mandan los parámetros que existen: SetBool/SetTrigger de uno inexistente avisa en cada llamada
            foreach (var parametro in animador.parameters)
            {
                if (parametro.nameHash == Contar) tieneContar = true;
                else if (parametro.nameHash == Contando) tieneContando = true;
                else if (parametro.nameHash == Agachado) tieneAgachado = true;
                else if (parametro.nameHash == AndarZ) tieneAndar = true;
            }
            if (brazoCuenta == null && animador.isHuman)
            {
                hombro = animador.GetBoneTransform(HumanBodyBones.RightUpperArm);
                codo = animador.GetBoneTransform(HumanBodyBones.RightLowerArm);
                mano = animador.GetBoneTransform(HumanBodyBones.RightHand);
                if (codo == null || mano == null) hombro = null;
                hombroIzquierdo = animador.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                codoIzquierdo = animador.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                manoIzquierda = animador.GetBoneTransform(HumanBodyBones.LeftHand);
                if (codoIzquierdo == null || manoIzquierda == null) hombroIzquierdo = null;
                pecho = animador.GetBoneTransform(HumanBodyBones.UpperChest) ?? animador.GetBoneTransform(HumanBodyBones.Chest);
                cabezaHueso = animador.GetBoneTransform(HumanBodyBones.Head);
            }
        }
        posicionInicial = transform.position;
        rotacionInicial = transform.rotation;
        destino = posicionInicial;
        if (cartelPulsar != null)
        {
            escalaCartel = cartelPulsar.transform.localScale;
            cartelPulsar.SetActive(false);
        }
    }

    private void OnEnable()
    {
        if (arbitro != null)
        {
            arbitro.AlDecirNumero += AlDecirNumero;
            arbitro.AlOrdenar += AlOrdenar;
        }
        if (rondas != null) rondas.AlTerminarCombate += AlTerminarCombate;
    }

    private void OnDisable()
    {
        if (arbitro != null)
        {
            arbitro.AlDecirNumero -= AlDecirNumero;
            arbitro.AlOrdenar -= AlOrdenar;
        }
        if (rondas != null) rondas.AlTerminarCombate -= AlTerminarCombate;
    }

    private void AlOrdenar(OrdenArbitro orden, Vector3 punto)
    {
        switch (orden)
        {
            case OrdenArbitro.Box:
                tiempoBox = 0f;
                tajoDado = false;
                break;
            case OrdenArbitro.Fin:
                agitarHasta = Time.time + segundosAgitando;
                break;
        }
    }

    // Se acabó: tras parar el combate (si fue por nocaut) señala al ganador
    private void AlTerminarCombate(ResultadoCombate resultado)
    {
        combateTerminado = true;
        ganoElJugador = resultado == ResultadoCombate.VictoriaPorKO || resultado == ResultadoCombate.VictoriaPorSalud;
        bool porNocaut = resultado == ResultadoCombate.VictoriaPorKO || resultado == ResultadoCombate.DerrotaPorKO;
        senalarGanadorEn = Time.time + (porNocaut ? segundosAgitando + 0.3f : 1.2f);
    }

    private void Update()
    {
        if (Time.timeScale == 0f) return;
        CambiarModo(ModoActual());

        // Al ganador lo señala hasta que cambia la escena, siguiéndolo si se mueve
        if (combateTerminado && senalarGanadorEn >= 0f && Time.time >= senalarGanadorEn && Time.time >= agitarHasta)
        {
            puntoSenalado = ganoElJugador ? jugador.PosicionCabeza : oponente.transform.position + Vector3.up * 1.35f;
            senalarHasta = Time.time + 1f;
            senalaConIzquierdo = false;
        }

        destino = Destino();
        float rapidez = modo == Modo.Pelea ? velocidadPelea : velocidad;
        if (modo == Modo.Pelea)
        {
            // Durante el round solo se mueve cuando su sitio queda lejos, y entonces llega hasta él
            float distancia = PlanoXZ(destino - transform.position).magnitude;
            if (distancia > zonaMuertaPelea) siguiendoPelea = true;
            else if (distancia < 0.05f && velocidadMovimiento.sqrMagnitude < 0.0025f) siguiendoPelea = false;
        }
        Desplazar(rapidez, modo != Modo.Pelea || siguiendoPelea);

        // Un trayecto largo fuera del round lo hace de frente; al acercarse a su sitio se vuelve hacia lo que mira
        float restante = PlanoXZ(destino - transform.position).magnitude;
        if (modo == Modo.Pelea || modo == Modo.Final) deFrente = false;
        else if (restante > distanciaCaminarDeFrente) deFrente = true;
        else if (restante < distanciaCaminarDeFrente * 0.5f) deFrente = false;

        Girar();
        Andar();
        Gesticular();
    }

    // Arranca y frena como una persona, con la aceleración limitada: antes era un SmoothDamp, que al arrancar pasaba de 0
    // a 0,8 m/s en 0,08 s; el cuerpo salía disparado antes de que el blend tree empezara a caminar y los pies patinaban.
    // Frena a tiempo para parar en el destino y, si deja de seguirlo (en el round, dentro de la zona muerta), frena igual
    // en vez de pararse en seco.
    private void Desplazar(float rapidez, bool seguir)
    {
        float dt = Time.deltaTime;
        Vector3 hacia = destino - transform.position;
        float distancia = hacia.magnitude;
        Vector3 deseada = Vector3.zero;
        if (seguir && distancia > 0.005f)
        {
            // La rapidez con la que aún se para justo en el destino frenando con algo menos de la aceleración máxima
            float frenada = Mathf.Sqrt(2f * aceleracion * 0.8f * distancia);
            deseada = hacia / distancia * Mathf.Min(rapidez, frenada);
        }
        velocidadMovimiento = Vector3.MoveTowards(velocidadMovimiento, deseada, aceleracion * dt);
        Vector3 paso = velocidadMovimiento * dt;
        if (seguir && distancia < 0.05f && paso.magnitude >= distancia)
        {
            transform.position = destino;
            velocidadMovimiento = Vector3.zero;
        }
        else transform.position += paso;
    }

    // El Animator recibe la velocidad en el espacio del réferi, en unidades de la velocidad de cada clip (1 = la del
    // paso), y "RitmoPaso" acelera el ciclo cuando va más deprisa que eso, para que los pies no patinen
    private void Andar()
    {
        if (!tieneAndar) return;
        Vector3 local = transform.InverseTransformDirection(PlanoXZ(velocidadMovimiento));
        var andar = new Vector2(local.x / velocidadClipLateral, local.z / velocidadClipCaminar);
        float ritmo = 1f;
        if (andar.magnitude > 1f)
        {
            ritmo = andar.magnitude;
            andar /= ritmo;
        }
        float dt = Time.deltaTime;
        animador.SetFloat(AndarX, andar.x, suavizadoAndar, dt);
        animador.SetFloat(AndarZ, andar.y, suavizadoAndar, dt);
        animador.SetFloat(RitmoPaso, ritmo, suavizadoAndar, dt);
    }

    private Modo ModoActual()
    {
        if (combateTerminado) return Modo.Final;
        if (arbitro.ContandoJugador) return Modo.CuentaJugador;
        if (arbitro.ContandoRival) return Modo.CuentaRival;
        if (rondas == null) return Modo.Pelea;
        switch (rondas.Estado)
        {
            case EstadoRondas.Round: return Modo.Pelea;
            case EstadoRondas.Preparacion: return Modo.Centro;
            case EstadoRondas.Descanso: return rondas.SegundosRestantes <= segundosAntesDeCampana ? Modo.Centro : Modo.Neutral;
            default: return Modo.Neutral;
        }
    }

    private void CambiarModo(Modo nuevo)
    {
        if (nuevo == modo) return;
        modo = nuevo;
        siguiendoPelea = true;
        desdeNumero = 999f;
        lado = ElegirLado(nuevo);
        bool contando = nuevo == Modo.CuentaJugador || nuevo == Modo.CuentaRival;
        if (tieneContando) animador.SetBool(Contando, contando);
        if (cartelPulsar != null) cartelPulsar.SetActive(nuevo == Modo.CuentaJugador);
    }

    // El lado que lo deja más dentro del ring; si dan igual, el que tenga más cerca
    private float ElegirLado(Modo nuevo)
    {
        if (nuevo == Modo.Neutral) return lado;
        float mejor = lado;
        float mejorCosto = float.MaxValue;
        foreach (float candidato in new[] { 1f, -1f })
        {
            Vector3 punto = Calcular(nuevo, candidato);
            float costo = PlanoXZ(DentroDelRing(punto) - punto).magnitude * 10f + PlanoXZ(punto - transform.position).magnitude;
            if (costo < mejorCosto)
            {
                mejorCosto = costo;
                mejor = candidato;
            }
        }
        return mejor;
    }

    private Vector3 Destino()
    {
        if (modo == Modo.Neutral) return posicionInicial;
        // Al acabar el combate se queda donde lo pilló
        if (modo == Modo.Final) return transform.position;
        Vector3 punto = Apartar(DentroDelRing(Calcular(modo, lado)));
        punto.y = posicionInicial.y;
        return punto;
    }

    private Vector3 Calcular(Modo cual, float haciaLado)
    {
        Vector3 cabeza = PlanoXZ(jugador.PosicionCabeza);
        Vector3 rival = PlanoXZ(oponente.transform.position);
        Vector3 haciaRival = rival - cabeza;
        haciaRival = haciaRival.sqrMagnitude > 0.0001f ? haciaRival.normalized : Vector3.forward;
        Vector3 costado = Vector3.Cross(Vector3.up, haciaRival) * haciaLado;

        switch (cual)
        {
            case Modo.CuentaJugador:
                return cabeza + haciaRival * adelanteCuentaJugador + costado * ladoCuentaJugador;
            case Modo.CuentaRival:
                return rival - haciaRival * adelanteCuentaRival + costado * ladoCuentaRival;
            case Modo.Pelea:
                return (cabeza + rival) * 0.5f + costado * distanciaLateralPelea;
            case Modo.Centro:
                return (cabeza + rival) * 0.5f + costado * distanciaLateralCentro;
            default:
                return posicionInicial;
        }
    }

    private Vector3 DentroDelRing(Vector3 punto) => limite != null ? limite.DentroDelRing(punto, margenCuerdas) : punto;

    // Ni dentro del rival ni encima del jugador
    private Vector3 Apartar(Vector3 punto)
    {
        punto = Separar(punto, PlanoXZ(oponente.transform.position));
        return Separar(punto, PlanoXZ(jugador.PosicionCabeza));
    }

    private Vector3 Separar(Vector3 punto, Vector3 centro)
    {
        Vector3 desde = PlanoXZ(punto - centro);
        if (desde.magnitude >= distanciaMinima) return punto;
        Vector3 direccion = desde.sqrMagnitude > 0.0001f ? desde.normalized : Vector3.right;
        return centro + direccion * distanciaMinima;
    }

    private void Girar()
    {
        Vector3 mirar = modo switch
        {
            Modo.CuentaJugador => jugador.PosicionCabeza,
            Modo.CuentaRival => oponente.transform.position,
            Modo.Pelea => (jugador.PosicionCabeza + oponente.transform.position) * 0.5f,
            Modo.Centro => (jugador.PosicionCabeza + oponente.transform.position) * 0.5f,
            Modo.Final => (jugador.PosicionCabeza + oponente.transform.position) * 0.5f,
            _ => transform.position + rotacionInicial * Vector3.forward,
        };
        if (deFrente && PlanoXZ(destino - transform.position).sqrMagnitude > 0.01f) mirar = destino;
        Vector3 direccion = PlanoXZ(mirar - transform.position);
        if (direccion.sqrMagnitude < 0.0001f) return;
        Quaternion objetivo = Quaternion.LookRotation(direccion, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, objetivo, 1f - Mathf.Exp(-velocidadGiro * Time.deltaTime));
    }

    private void AlDecirNumero(int numero, bool alJugador)
    {
        desdeNumero = 0f;
        if (tieneContar) animador.SetTrigger(Contar);
    }

    // Con cuenta: brazo arriba, y en cada número baja de golpe y vuelve a subir. Sin cuenta: brazo abajo.
    // El brazo no sigue una curva fija sino un muelle amortiguado: baja rápido, se pasa un poco y se asienta; sube más
    // despacio. El antebrazo sigue al brazo con su propio muelle, así que en la bajada se queda atrás y llega con un
    // pequeño latigazo, y arriba queda el codo algo doblado, como un brazo de verdad.
    private void Gesticular()
    {
        float dt = Time.deltaTime;
        desdeNumero += dt;
        bool contando = modo == Modo.CuentaJugador || modo == Modo.CuentaRival;
        // En la cuenta del rival el brazo sube cuando empieza la cuenta (el rival ya tendido), no mientras cae
        bool brazoActivo = contando && (modo == Modo.CuentaJugador || arbitro.NumeroActual > 0);
        // "¡Box!": levanta el brazo, da un tajo hacia delante entre los dos púgiles y lo baja
        tiempoBox += dt;
        if (tiempoBox < 0.32f + segundosAbajo + 0.1f)
        {
            brazoActivo = true;
            if (!tajoDado && tiempoBox >= 0.32f)
            {
                tajoDado = true;
                desdeNumero = 0f;
            }
        }
        pesoSenalar = Mathf.MoveTowards(pesoSenalar, Time.time < senalarHasta ? 1f : 0f, 4f * dt);
        pesoAgitar = Mathf.MoveTowards(pesoAgitar, Time.time < agitarHasta ? 1f : 0f, 4f * dt);
        faseAgitar += dt * Mathf.PI * 2f * 1.5f;
        bool bajando = brazoActivo && desdeNumero < segundosAbajo;
        float objetivo = !brazoActivo ? 0f : bajando ? anguloAbajo : anguloArriba;
        float frecuencia = bajando ? 4.5f : brazoActivo ? Mathf.Max(0.8f, 0.9f / segundosSubir) : 1.6f;
        Muelle(ref anguloBrazo, ref velocidadBrazo, objetivo, frecuencia, bajando ? 0.6f : 0.9f, dt);

        float abajo01 = Mathf.Clamp01(Mathf.InverseLerp(anguloArriba, anguloAbajo, anguloBrazo));
        float flexion = brazoActivo ? Mathf.Lerp(25f, 6f, abajo01) : 10f;
        Muelle(ref anguloAntebrazo, ref velocidadAntebrazo, anguloBrazo - flexion, 6f, 0.45f, dt);

        if (brazoCuenta != null) brazoCuenta.localRotation = Quaternion.Euler(anguloBrazo, 0f, 0f);
        // El esqueleto no tiene una pose de reposo con el brazo recto: el gesto entra y sale por peso, con suavidad
        pesoBrazo = Mathf.MoveTowards(pesoBrazo, brazoActivo ? 1f : 0f, 2.5f * dt);

        if (tieneAgachado)
        {
            // En la cuenta del rival se inclina sobre el caído
            float postura = modo == Modo.Pelea ? agachadoPelea
                : modo == Modo.CuentaRival ? agachadoCuentaRival : 0f;
            agachado = Mathf.MoveTowards(agachado, postura, velocidadPostura * Time.deltaTime);
            animador.SetFloat(Agachado, agachado);
        }

        // El cartel late con cada número para que llame la atención
        if (cartelPulsar != null && cartelPulsar.activeSelf)
        {
            float latido = 1f + 0.15f * Mathf.Exp(-desdeNumero * 6f);
            cartelPulsar.transform.localScale = escalaCartel * latido;
        }
    }

    private static void Muelle(ref float x, ref float v, float objetivo, float frecuencia, float amortiguacion, float dt)
    {
        if (dt <= 0f) return;
        float w = 2f * Mathf.PI * frecuencia;
        // Dos subpasos: estable aunque baje la tasa de fotogramas
        for (int i = 0; i < 2; i++)
        {
            float h = dt * 0.5f;
            v += (w * w * (objetivo - x) - 2f * amortiguacion * w * v) * h;
            x += v * h;
        }
    }

    // Después del Animator: el brazo derecho apunta en la dirección del gesto (el mismo ángulo que el hombro de un modelo
    // sin esqueleto, medido desde el brazo colgando y girando hacia delante) y el antebrazo en la suya. El torso y la
    // cabeza acompañan cada número con un pequeño cabeceo
    private void LateUpdate()
    {
        // También en la pausa: el Animator sigue escribiendo la pose aunque no avance
        if (hombro == null) return;
        Senas();
        if (pesoBrazo <= 0.001f) return;
        float peso = Mathf.SmoothStep(0f, 1f, pesoBrazo);
        float abajo01 = Mathf.Clamp01(Mathf.InverseLerp(anguloArriba, anguloAbajo, anguloBrazo));
        Vector3 derecha = transform.right;
        if (pecho != null) pecho.rotation = Quaternion.AngleAxis(5f * abajo01 * peso, derecha) * pecho.rotation;
        if (cabezaHueso != null) cabezaHueso.rotation = Quaternion.AngleAxis(7f * abajo01 * peso, derecha) * cabezaHueso.rotation;

        // El codo solo se dobla hacia delante: el antebrazo nunca pasa al brazo (sin hiperextensión)
        float antebrazo = Mathf.Min(anguloAntebrazo, anguloBrazo);
        antebrazo = Mathf.Max(antebrazo, anguloBrazo - 70f);
        Vector3 direccionBrazo = transform.rotation * (Quaternion.Euler(anguloBrazo, 0f, 0f) * Vector3.down);
        Vector3 direccionAntebrazo = transform.rotation * (Quaternion.Euler(antebrazo, 0f, 0f) * Vector3.down);
        Apuntar(hombro, codo.position, direccionBrazo, peso);
        Apuntar(codo, mano.position, direccionAntebrazo, peso);
    }

    // Parar el combate: los dos brazos en alto, cruzándose y abriéndose por encima de la cabeza.
    // Señalar: un brazo extendido hacia el punto (el ganador)
    private void Senas()
    {
        if (pesoAgitar > 0.001f && hombroIzquierdo != null)
        {
            float peso = Mathf.SmoothStep(0f, 1f, pesoAgitar);
            float abiertos = (1f + Mathf.Cos(faseAgitar)) * 0.5f;   // 1 = en V, 0 = antebrazos cruzados
            for (int i = 0; i < 2; i++)
            {
                Vector3 lado = transform.right * (i == 0 ? -1f : 1f);
                Vector3 brazo = (Vector3.up + lado * (0.25f + 0.5f * abiertos) + transform.forward * 0.2f).normalized;
                Vector3 antebrazo = (Vector3.up + lado * (-0.9f + 1.5f * abiertos) + transform.forward * 0.25f).normalized;
                Transform h = i == 0 ? hombroIzquierdo : hombro, c = i == 0 ? codoIzquierdo : codo, m = i == 0 ? manoIzquierda : mano;
                Apuntar(h, c.position, brazo, peso);
                Apuntar(c, m.position, antebrazo, peso);
            }
        }

        if (pesoSenalar > 0.001f)
        {
            bool izquierdo = senalaConIzquierdo && hombroIzquierdo != null;
            Transform h = izquierdo ? hombroIzquierdo : hombro, c = izquierdo ? codoIzquierdo : codo, m = izquierdo ? manoIzquierda : mano;
            float peso = Mathf.SmoothStep(0f, 1f, pesoSenalar);
            Vector3 direccion = (puntoSenalado - h.position).normalized;
            Apuntar(h, c.position, direccion, peso);
            Apuntar(c, m.position, direccion, peso);
        }
    }

    private static void Apuntar(Transform hueso, Vector3 hijo, Vector3 direccion, float peso)
    {
        Vector3 actual = hijo - hueso.position;
        if (actual.sqrMagnitude < 1e-6f) return;
        Quaternion giro = Quaternion.FromToRotation(actual, direccion);
        hueso.rotation = Quaternion.Slerp(Quaternion.identity, giro, peso) * hueso.rotation;
    }

    private static Vector3 PlanoXZ(Vector3 v) => new Vector3(v.x, 0f, v.z);

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(Application.isPlaying ? destino : transform.position, 0.25f);
    }
}
