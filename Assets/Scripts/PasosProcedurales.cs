using UnityEngine;

/// <summary>
/// Pasos procedurales para los personajes humanoides que se desplazan por el ring sin clip de caminar (los rivales al
/// acercarse, girar o esquivar, y el réferi). Sin esto el cuerpo se desliza sobre la lona con los pies quietos.
///
/// Mientras el cuerpo se desplaza o gira, cada pie se queda clavado donde pisó (IK de pies). Cuando el cuerpo se aleja
/// de él más de distanciaPaso, o gira más de giroPaso, el pie da un paso: se levanta en arco y aterriza en su sitio de
/// la postura, un poco adelantado según la velocidad. Los pies se turnan; si el cuerpo se mueve de golpe (un esquive)
/// pueden ir los dos a la vez. Vale en cualquier dirección: adelante, atrás, de lado y girando.
/// Con el cuerpo quieto, manda la animación: antes de soltar los pies da un último paso para dejarlos en la postura, y
/// después la IK se apaga con una mezcla corta. La postura (dónde van los pies respecto a la raíz) se toma de la propia
/// animación mientras la IK está apagada, así que cada personaje conserva la suya.
///
/// Solo actúa en los estados de la capa base listados en estados (vacío = siempre): en los ataques, las reacciones y las
/// caídas manda el clip. Va en el objeto del Animator, que necesita IK Pass en la capa base. Lo añade
/// Tools > Boxeo > Poner pasos procedurales.
/// Nota: dentro de OnAnimatorIK, Animator.GetIKPosition devuelve el objetivo del fotograma anterior y no la pose animada,
/// por eso la postura se lee de los huesos en LateUpdate y no de ahí.
/// Con ApoyoEnSuelo (Mr. Sandman, Tyson) la postura se guarda sin la subida de cadera que ese componente aplica después
/// de la IK: así los pies se clavan a la altura del clip y ApoyoEnSuelo los sube igual que cuando la IK está apagada. Si
/// se guardaran ya subidos, ApoyoEnSuelo no vería suelas hundidas, no subiría la cadera y el personaje daría los pasos
/// agachado (22 cm en Sandman).
/// El objetivo de IK de un pie no es el hueso: Unity lo define con su propia orientación y algo desplazado, y cuánto
/// depende del esqueleto (en los de Mixamo casi coinciden; en el de Mr. Sandman, pasarle el giro del hueso volteaba el
/// pie). Por eso, con la IK apagada y el cuerpo quieto, se guarda el desfase entre el objetivo y el hueso, y al clavar el
/// pie se le suma: así, pedirle a la IK la pose del clip devuelve exactamente la pose del clip.
/// </summary>
[RequireComponent(typeof(Animator))]
public class PasosProcedurales : MonoBehaviour
{
    [Tooltip("Estados de la capa base en los que los pies pisan y dan pasos; vacío = en todos")]
    [SerializeField] private string[] estados = { "Idle" };
    [Tooltip("Metros que el cuerpo se aleja de un pie antes de que ese pie dé un paso")]
    [SerializeField, Min(0.02f)] private float distanciaPaso = 0.12f;
    [Tooltip("Grados que el cuerpo gira respecto a un pie antes de que ese pie dé un paso")]
    [SerializeField, Min(1f)] private float giroPaso = 30f;
    [Tooltip("Altura del pie a mitad de un paso completo")]
    [SerializeField, Min(0f)] private float alturaPaso = 0.07f;
    [Tooltip("Duración de un paso andando despacio; con más velocidad se acorta hasta duracionMinima")]
    [SerializeField, Min(0.05f)] private float duracionPaso = 0.3f;
    [SerializeField, Min(0.05f)] private float duracionMinima = 0.12f;
    [Tooltip("Fracción del avance durante el paso que el pie se adelanta al aterrizar")]
    [SerializeField, Range(0f, 1.5f)] private float anticipacion = 0.6f;
    [Tooltip("Lo más que el pie puede aterrizar lejos de su sitio de la postura")]
    [SerializeField, Min(0.05f)] private float alcanceMaximo = 0.35f;
    [Tooltip("Por debajo de esta velocidad (m/s) y de giroQuieto (°/s) el cuerpo se considera quieto")]
    [SerializeField, Min(0f)] private float velocidadQuieto = 0.12f;
    [SerializeField, Min(0f)] private float giroQuieto = 25f;
    [Tooltip("Segundos de mezcla al encender o apagar la IK de pies")]
    [SerializeField, Min(0.01f)] private float mezcla = 0.2f;

    private class Pie
    {
        public AvatarIKGoal objetivo;
        public HumanBodyBones hueso;
        public Transform transformHueso;
        public Vector3 posturaLocal;        // respecto a la raíz, leída de la animación con la IK apagada
        public Quaternion giroPosturaLocal;
        public bool hayPostura;
        public Vector3 plantado;
        public Quaternion giro;
        public bool enPaso;
        public float t, duracion, altura;
        public Vector3 desde;
        public Quaternion giroDesde;
        public Vector3 actual;
        public Quaternion giroActual;
        // Del hueso al objetivo de IK, en el espacio del hueso (constante para cada esqueleto)
        public Vector3 desfasePosicion;
        public Quaternion desfaseGiro = Quaternion.identity;
    }

    private Animator animator;
    private ApoyoEnSuelo apoyo;
    private readonly Pie izquierdo = new Pie { objetivo = AvatarIKGoal.LeftFoot, hueso = HumanBodyBones.LeftFoot };
    private readonly Pie derecho = new Pie { objetivo = AvatarIKGoal.RightFoot, hueso = HumanBodyBones.RightFoot };
    private Pie[] pies;
    private int[] hashes;
    private float peso;
    private bool activo;
    private float quietoDesde;
    private Vector3 posicionAnterior;
    private float rumboAnterior;
    private Vector3 velocidad;
    private float velocidadGiro;
    private bool hayAnterior;

    /// <summary>Hay algún pie en el aire.</summary>
    public bool DandoPaso => izquierdo.enPaso || derecho.enPaso;
    public int Pasos { get; private set; }
    /// <summary>La IK de pies está mandando (0 = la animación).</summary>
    public float Peso => peso;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        apoyo = GetComponent<ApoyoEnSuelo>();
        pies = new[] { izquierdo, derecho };
        hashes = new int[estados.Length];
        for (int i = 0; i < estados.Length; i++) hashes[i] = Animator.StringToHash(estados[i]);
        if (!animator.isHuman) { enabled = false; return; }
        foreach (Pie pie in pies) pie.transformHueso = animator.GetBoneTransform(pie.hueso);
        if (izquierdo.transformHueso == null || derecho.transformHueso == null) enabled = false;
    }

    private void OnDisable()
    {
        peso = 0f;
        activo = false;
        hayAnterior = false;
        foreach (Pie pie in pies) pie.enPaso = false;
    }

    private bool EstadoConPasos()
    {
        if (hashes.Length == 0) return true;
        if (animator.IsInTransition(0)) return false;
        int actual = animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
        foreach (int h in hashes)
            if (actual == h) return true;
        return false;
    }

    private Vector3 SitioPostura(Pie pie) => transform.TransformPoint(pie.posturaLocal);
    private Quaternion GiroPostura(Pie pie) => transform.rotation * pie.giroPosturaLocal;

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        // Velocidad y giro del cuerpo en el plano del suelo (los mueven los scripts, no la animación)
        Vector3 posicion = transform.position;
        float rumbo = transform.eulerAngles.y;
        if (hayAnterior)
        {
            Vector3 medida = (posicion - posicionAnterior) / dt;
            medida.y = 0f;
            float suavizado = 1f - Mathf.Exp(-12f * dt);
            velocidad = Vector3.Lerp(velocidad, medida, suavizado);
            velocidadGiro = Mathf.Lerp(velocidadGiro, Mathf.Abs(Mathf.DeltaAngle(rumboAnterior, rumbo)) / dt, suavizado);
        }
        posicionAnterior = posicion;
        rumboAnterior = rumbo;
        hayAnterior = true;

        bool permitido = EstadoConPasos() && izquierdo.hayPostura && derecho.hayPostura;
        bool moviendose = velocidad.magnitude > velocidadQuieto || velocidadGiro > giroQuieto;
        if (!permitido)
        {
            activo = false;
        }
        else if (moviendose)
        {
            if (!activo && peso <= 0f)
            {
                // Se clavan donde la animación los tiene ahora mismo: sin salto
                foreach (Pie pie in pies)
                {
                    pie.plantado = pie.actual = pie.transformHueso.position;
                    pie.giro = pie.giroActual = pie.transformHueso.rotation;
                    pie.enPaso = false;
                }
            }
            activo = true;
            quietoDesde = Time.time;
        }
        else if (activo && !DandoPaso && Time.time - quietoDesde > 0.25f)
        {
            // Quieto: un último paso si algún pie quedó lejos de su sitio, y después se suelta
            bool asentados = true;
            foreach (Pie pie in pies)
                if (Distancia(SitioPostura(pie), pie.plantado) > 0.04f || Quaternion.Angle(GiroPostura(pie), pie.giro) > 10f)
                    asentados = false;
            if (asentados) activo = false;
        }
        peso = Mathf.MoveTowards(peso, activo ? 1f : 0f, dt / mezcla);
    }

    private void OnAnimatorIK(int capa)
    {
        if (capa != 0) return;
        if (peso <= 0f)
        {
            foreach (Pie pie in pies)
            {
                animator.SetIKPositionWeight(pie.objetivo, 0f);
                animator.SetIKRotationWeight(pie.objetivo, 0f);
            }
            MedirDesfase();
            return;
        }

        float dt = Time.deltaTime;
        bool quieto = Time.time - quietoDesde > 0.25f;
        Mover(izquierdo, derecho, dt, quieto);
        Mover(derecho, izquierdo, dt, quieto);

        float w = Mathf.SmoothStep(0f, 1f, peso);
        foreach (Pie pie in pies)
        {
            animator.SetIKPositionWeight(pie.objetivo, w);
            animator.SetIKRotationWeight(pie.objetivo, w);
            animator.SetIKPosition(pie.objetivo, pie.actual + pie.giroActual * pie.desfasePosicion);
            animator.SetIKRotation(pie.objetivo, pie.giroActual * pie.desfaseGiro);
        }
    }

    // Con la IK apagada, el objetivo que devuelve el Animator aquí es el de la pose que dejó el fotograma anterior (ya con
    // la subida de ApoyoEnSuelo), igual que los huesos: su diferencia es el desfase propio del esqueleto. Se mide con el
    // cuerpo quieto por si en algún caso el objetivo llegara un fotograma adelantado.
    private void MedirDesfase()
    {
        if (velocidad.sqrMagnitude > 0.0004f || velocidadGiro > 2f || !EstadoConPasos()) return;
        foreach (Pie pie in pies)
        {
            Quaternion inverso = Quaternion.Inverse(pie.transformHueso.rotation);
            pie.desfaseGiro = inverso * animator.GetIKRotation(pie.objetivo);
            pie.desfasePosicion = inverso * (animator.GetIKPosition(pie.objetivo) - pie.transformHueso.position);
        }
    }

    private void Mover(Pie pie, Pie otro, float dt, bool quieto)
    {
        Vector3 sitio = SitioPostura(pie);
        Quaternion giroSitio = GiroPostura(pie);

        if (!pie.enPaso)
        {
            float error = Distancia(sitio, pie.plantado);
            float errorGiro = Quaternion.Angle(giroSitio, pie.giro);
            // Moviéndose, pasos al pasar el umbral; quieto, uno más pequeño para dejar la postura limpia
            float umbral = quieto ? 0.04f : distanciaPaso;
            bool necesita = error > umbral || errorGiro > (quieto ? 10f : giroPaso);
            float errorOtro = Distancia(SitioPostura(otro), otro.plantado);
            // Se turnan, primero el más atrasado; si el cuerpo se fue lejos de golpe, los dos a la vez
            bool puede = (!otro.enPaso && (errorOtro <= error * 1.2f || errorOtro < umbral)) || error > distanciaPaso * 2.5f;
            if (necesita && puede)
            {
                pie.enPaso = true;
                pie.t = 0f;
                pie.duracion = Mathf.Lerp(duracionPaso, duracionMinima, Mathf.InverseLerp(0.4f, 3f, velocidad.magnitude));
                pie.altura = alturaPaso * Mathf.Clamp01(Mathf.Max(error / (distanciaPaso * 2f), errorGiro / (giroPaso * 2f)) + 0.35f);
                pie.desde = pie.plantado;
                pie.giroDesde = pie.giro;
                Pasos++;
            }
        }

        if (pie.enPaso)
        {
            pie.t += dt / pie.duracion;
            float s = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(pie.t));
            // El destino se recalcula cada fotograma: sigue al cuerpo si cambia de dirección
            Vector3 adelanto = quieto ? Vector3.zero : Vector3.ClampMagnitude(velocidad * (pie.duracion * anticipacion), alcanceMaximo);
            Vector3 hasta = sitio + adelanto;
            float arco = pie.altura * Mathf.Sin(Mathf.Clamp01(pie.t) * Mathf.PI);
            // La altura base es siempre la de la postura: el arco nunca se acumula de un paso a otro
            Vector3 plano = Vector3.Lerp(pie.desde, hasta, s);
            pie.actual = new Vector3(plano.x, sitio.y + arco, plano.z);
            pie.giroActual = Quaternion.Slerp(pie.giroDesde, giroSitio, s);
            if (pie.t >= 1f)
            {
                pie.enPaso = false;
                pie.plantado = hasta;
                pie.actual = new Vector3(hasta.x, sitio.y, hasta.z);
                pie.giro = pie.giroActual = giroSitio;
            }
        }
        else
        {
            // Clavado en el plano; la altura es la de la postura (el pie siempre en la lona)
            pie.actual = new Vector3(pie.plantado.x, sitio.y, pie.plantado.z);
            pie.giroActual = pie.giro;
        }
    }

    private static float Distancia(Vector3 a, Vector3 b)
    {
        Vector3 d = a - b;
        d.y = 0f;
        return d.magnitude;
    }

    // Con la IK apagada los huesos son la animación pura: de ahí sale la postura de los pies respecto a la raíz
    private void LateUpdate()
    {
        if (peso > 0f || !EstadoConPasos()) return;
        // ApoyoEnSuelo ya corrió este fotograma (orden -50) y subió la cadera: se le quita para guardar la altura del clip
        Vector3 subida = apoyo != null && apoyo.isActiveAndEnabled ? Vector3.up * apoyo.UltimaCorreccion : Vector3.zero;
        foreach (Pie pie in pies)
        {
            pie.posturaLocal = transform.InverseTransformPoint(pie.transformHueso.position - subida);
            pie.giroPosturaLocal = Quaternion.Inverse(transform.rotation) * pie.transformHueso.rotation;
            pie.hayPostura = true;
        }
    }
}
