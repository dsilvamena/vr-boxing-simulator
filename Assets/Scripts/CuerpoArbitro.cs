using UnityEngine;

/// <summary>
/// Cuerpo visible del réferi (RF-12): se coloca según lo que pasa en el ring y marca la cuenta con el brazo.
/// - Durante el round acompaña la pelea a un lado de la línea entre el jugador y el rival, sin cruzarse en ella.
/// - En la cuenta del rival se pone junto a él, del lado del jugador para que este lo vea, y lo mira.
/// - En la cuenta del jugador se pone delante de él, a un lado del rival (como en el boceto del estado de knockout),
///   lo mira y muestra el cartel con los botones que hay que pulsar.
/// - Fuera de los rounds (preparación, descanso, final) vuelve a su sitio inicial.
/// Cada número que dice el Arbitro baja el brazo de golpe y lo vuelve a subir, como un réferi de verdad.
/// No hay animación de caminar: se desliza, igual que el rival al volver a su sitio en el descanso.
/// El modelo (hijo Modelo, Referee.fbx) es Humanoid: con un Animator humanoide y brazoCuenta vacío, el gesto se hace
/// girando el brazo derecho del esqueleto después de la animación, así que vale para cualquier rig humanoide. Con
/// brazoCuenta asignado se gira ese transform en su eje X local (modelos sin esqueleto). El Animator recibe, si los tiene,
/// el float "Agachado" (0 de pie, 1 la postura de vigilar), el trigger "Contar" en cada número y el bool "Contando".
/// La voz es hija de este objeto, así que se mueve con él.
/// Va en el hijo Cuerpo del Arbitro de cada escena de pelea. Usa tiempo escalado, así que se queda quieto en la pausa.
/// </summary>
public class CuerpoArbitro : MonoBehaviour
{
    private enum Modo { Neutral, Pelea, CuentaRival, CuentaJugador }

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

    [Header("Gesto de la cuenta")]
    [Tooltip("Ángulo del hombro con el brazo arriba, esperando el número (negativo = hacia delante y arriba)")]
    [SerializeField] private float anguloArriba = -150f;
    [Tooltip("Ángulo del hombro al marcar el número, con el brazo hacia delante")]
    [SerializeField] private float anguloAbajo = -75f;
    [Tooltip("Segundos que tarda en bajar el brazo al decir el número")]
    [SerializeField, Min(0.01f)] private float segundosBajar = 0.12f;
    [Tooltip("Segundos que tarda en volver a subirlo")]
    [SerializeField, Min(0.01f)] private float segundosSubir = 0.45f;

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

    private Modo modo = Modo.Neutral;
    private Vector3 posicionInicial;
    private Quaternion rotacionInicial;
    private Vector3 destino;
    private float lado = 1f;          // lado elegido al empezar cada modo, para no saltar de uno a otro
    private bool siguiendoPelea;
    private float desdeNumero = 999f;  // segundos desde el último número; grande = sin gesto pendiente
    private float anguloBrazo;
    private Vector3 escalaCartel;

    // Esqueleto humanoide: el gesto se aplica sobre la pose animada, con un peso que sube al empezar la cuenta
    private Transform hombro, codo, mano;
    private float pesoBrazo;
    private float agachado;
    private bool tieneContar, tieneContando, tieneAgachado;

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
            }
            if (brazoCuenta == null && animador.isHuman)
            {
                hombro = animador.GetBoneTransform(HumanBodyBones.RightUpperArm);
                codo = animador.GetBoneTransform(HumanBodyBones.RightLowerArm);
                mano = animador.GetBoneTransform(HumanBodyBones.RightHand);
                if (codo == null || mano == null) hombro = null;
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
        if (arbitro != null) arbitro.AlDecirNumero += AlDecirNumero;
    }

    private void OnDisable()
    {
        if (arbitro != null) arbitro.AlDecirNumero -= AlDecirNumero;
    }

    private void Update()
    {
        if (Time.timeScale == 0f) return;
        CambiarModo(ModoActual());

        destino = Destino();
        float rapidez = modo == Modo.Pelea ? velocidadPelea : velocidad;
        if (modo == Modo.Pelea)
        {
            // Durante el round solo se mueve cuando su sitio queda lejos, y entonces llega hasta él
            float distancia = PlanoXZ(destino - transform.position).magnitude;
            if (distancia > zonaMuertaPelea) siguiendoPelea = true;
            else if (distancia < 0.05f) siguiendoPelea = false;
        }
        if (modo != Modo.Pelea || siguiendoPelea)
            transform.position = Vector3.MoveTowards(transform.position, destino, rapidez * Time.deltaTime);

        Girar();
        Gesticular();
    }

    private Modo ModoActual()
    {
        if (arbitro.ContandoJugador) return Modo.CuentaJugador;
        if (arbitro.ContandoRival) return Modo.CuentaRival;
        if (rondas == null) return Modo.Pelea;
        return rondas.Estado == EstadoRondas.Round ? Modo.Pelea : Modo.Neutral;
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
            _ => transform.position + rotacionInicial * Vector3.forward,
        };
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

    // Con cuenta: brazo arriba, y en cada número baja de golpe y vuelve a subir. Sin cuenta: brazo abajo
    private void Gesticular()
    {
        desdeNumero += Time.deltaTime;
        bool contando = modo == Modo.CuentaJugador || modo == Modo.CuentaRival;
        float objetivo;
        if (!contando) objetivo = 0f;
        else if (desdeNumero < segundosBajar) objetivo = Mathf.Lerp(anguloArriba, anguloAbajo, desdeNumero / segundosBajar);
        else objetivo = Mathf.Lerp(anguloAbajo, anguloArriba, Mathf.SmoothStep(0f, 1f, (desdeNumero - segundosBajar) / segundosSubir));

        // Al empezar o terminar la cuenta el brazo sube o baja suave; dentro de la cuenta sigue el gesto tal cual
        bool enGesto = contando && desdeNumero < segundosBajar + segundosSubir;
        anguloBrazo = enGesto ? objetivo : Mathf.MoveTowards(anguloBrazo, objetivo, 400f * Time.deltaTime);
        if (brazoCuenta != null) brazoCuenta.localRotation = Quaternion.Euler(anguloBrazo, 0f, 0f);
        // El esqueleto no tiene una pose de reposo con el brazo recto: el gesto entra y sale por peso
        pesoBrazo = Mathf.MoveTowards(pesoBrazo, contando ? 1f : 0f, 3f * Time.deltaTime);

        if (tieneAgachado)
        {
            float postura = modo == Modo.Pelea ? agachadoPelea : modo == Modo.CuentaRival ? agachadoCuentaRival : 0f;
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

    // Después del Animator: el brazo derecho apunta en la dirección del gesto (el mismo ángulo que el hombro de un modelo
    // sin esqueleto, medido desde el brazo colgando y girando hacia delante), recto del hombro a la mano
    private void LateUpdate()
    {
        // También en la pausa: el Animator sigue escribiendo la pose aunque no avance
        if (hombro == null || pesoBrazo <= 0.001f) return;
        Vector3 direccion = transform.rotation * (Quaternion.Euler(anguloBrazo, 0f, 0f) * Vector3.down);
        Apuntar(hombro, codo.position, direccion, pesoBrazo);
        Apuntar(codo, mano.position, direccion, pesoBrazo);
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
