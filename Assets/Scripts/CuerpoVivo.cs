using UnityEngine;

/// <summary>
/// Capa procedural que se suma a los clips de un personaje humanoide para que se vea vivo y acuse los golpes:
/// - Mirada: la cabeza (y un poco el torso) sigue los ojos del jugador con la IK de mirada del Animator.
/// - Impacto: cada golpe que entra sacude la cabeza o el torso en la dirección real del puño (un gancho le gira la cara,
///   un uppercut le levanta el mentón, uno al cuerpo lo dobla), con un muelle amortiguado y proporcional a la intensidad.
///   Los clips de reacción siguen sonando por debajo; esto les da dirección y peso. Un golpe bloqueado solo lo empuja un poco.
/// - Esquive: mientras EsquiveOponente lo aparta, el torso se inclina hacia donde va (un "slip"), en vez de deslizarse tieso.
/// - Cansancio: al perder salud se encorva, baja la cabeza y jadea (respiración más amplia y rápida).
/// Todo son giros añadidos a los huesos en LateUpdate, después de la animación y la IK, así que no cambia ningún clip ni
/// la lógica del combate. Las hitboxes cuelgan de los huesos y los siguen. En el piso (Knockdown, Defeat) se apaga.
/// Va en el objeto del Animator (necesita IK Pass en la capa base para la mirada). Sin Oponente (los boxeadores de
/// adorno del menú) solo mira y respira. Lo añade Tools > Boxeo > Poner pulido de combate.
/// </summary>
[RequireComponent(typeof(Animator))]
public class CuerpoVivo : MonoBehaviour
{
    private static readonly int Idle = Animator.StringToHash("Idle");
    private static readonly int Knockdown = Animator.StringToHash("Knockdown");
    private static readonly int Defeat = Animator.StringToHash("Defeat");
    private static readonly int ReaccionJab = Animator.StringToHash("Jab");
    private static readonly int ReaccionCara = Animator.StringToHash("Uppercut_Face");
    private static readonly int ReaccionCuerpo = Animator.StringToHash("Uppercut_Stomach");

    [Header("Mirada")]
    [Tooltip("A quién mira; vacío = cámara principal (los ojos del jugador)")]
    [SerializeField] private Transform objetivo;
    [Tooltip("Cuánto sigue la cabeza al jugador (0 = nada, 1 = del todo)")]
    [SerializeField, Range(0f, 1f)] private float pesoMirada = 0.75f;
    [Tooltip("Parte de la mirada que acompaña el torso")]
    [SerializeField, Range(0f, 1f)] private float pesoTorsoMirada = 0.12f;
    [Tooltip("Segundos que tarda la mirada en seguir al jugador")]
    [SerializeField, Min(0.01f)] private float suavizadoMirada = 0.12f;

    [Header("Impacto")]
    [Tooltip("Grados que gira la cabeza con un golpe completo a la cabeza")]
    [SerializeField, Min(0f)] private float sacudidaCabeza = 36f;
    [Tooltip("Grados que se dobla el torso con un golpe completo al cuerpo")]
    [SerializeField, Min(0f)] private float sacudidaTorso = 16f;
    [Tooltip("Parte de la sacudida que queda cuando el golpe fue bloqueado")]
    [SerializeField, Range(0f, 1f)] private float fraccionBloqueo = 0.3f;
    [Tooltip("Rigidez y amortiguación del muelle que devuelve cabeza y torso a su sitio")]
    [SerializeField, Min(1f)] private float rigidez = 110f;
    [SerializeField, Min(0f)] private float amortiguacion = 11f;

    [Header("Esquive")]
    [Tooltip("Grados que inclina el torso al apartarse del todo")]
    [SerializeField, Min(0f)] private float inclinacionEsquive = 24f;

    [Header("Cansancio")]
    [Tooltip("Grados que se encorva con la salud a cero")]
    [SerializeField, Min(0f)] private float encorvado = 8f;
    [Tooltip("Grados que baja la cabeza con la salud a cero")]
    [SerializeField, Min(0f)] private float cabezaBaja = 7f;
    [Tooltip("Amplitud de la respiración en grados: x = descansado, y = sin salud")]
    [SerializeField] private Vector2 respiracion = new Vector2(0.5f, 2.6f);
    [Tooltip("Respiraciones por segundo: x = descansado, y = sin salud")]
    [SerializeField] private Vector2 ritmoRespiracion = new Vector2(0.28f, 0.95f);

    private Animator animator;
    private Oponente oponente;
    private EsquiveOponente esquive;
    private Transform raiz;
    private Transform columna, pecho, cuello, cabeza;

    // Muelles en el espacio de la raíz: vector = eje * grados
    private Vector3 anguloCabeza, velocidadCabeza;
    private Vector3 anguloTorso, velocidadTorso;
    private float presencia = 1f;      // 0 en el piso: no se añade nada
    private float mirada;
    private Vector3 puntoMirada;
    private bool hayPuntoMirada;
    private float cansancio;
    private float faseRespiracion;
    private Vector3 inclinacion;       // desplazamiento del esquive suavizado, en el espacio de la raíz

    /// <summary>Grados que la cabeza está desviada ahora mismo por un golpe (para pruebas).</summary>
    public float SacudidaActual => anguloCabeza.magnitude;
    /// <summary>0 = descansado, 1 = sin salud (suavizado).</summary>
    public float Cansancio => cansancio;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        if (!animator.isHuman) { enabled = false; return; }
        oponente = GetComponentInParent<Oponente>();
        esquive = GetComponentInParent<EsquiveOponente>();
        raiz = oponente != null ? oponente.transform : transform;
        columna = animator.GetBoneTransform(HumanBodyBones.Spine);
        pecho = animator.GetBoneTransform(HumanBodyBones.UpperChest);
        if (pecho == null) pecho = animator.GetBoneTransform(HumanBodyBones.Chest);
        cuello = animator.GetBoneTransform(HumanBodyBones.Neck);
        cabeza = animator.GetBoneTransform(HumanBodyBones.Head);
        if (columna == null || cabeza == null) enabled = false;
        faseRespiracion = Random.value * Mathf.PI * 2f;
    }

    private void OnEnable()
    {
        if (oponente == null) return;
        oponente.AlRecibirGolpe += AlRecibirGolpe;
        oponente.AlBloquear += AlBloquear;
    }

    private void OnDisable()
    {
        if (oponente == null) return;
        oponente.AlRecibirGolpe -= AlRecibirGolpe;
        oponente.AlBloquear -= AlBloquear;
    }

    private void AlRecibirGolpe(Golpe golpe) => Sacudir(golpe, 1f);
    private void AlBloquear(Golpe golpe) => Sacudir(golpe, fraccionBloqueo);

    /// <summary>
    /// Le da el impulso de un golpe. El eje sale del par que hace el puño sobre el cuello (cabeza) o la cintura (torso):
    /// brazo de palanca × dirección del golpe, así que la cabeza gira hacia donde la empuja el guante.
    /// </summary>
    public void Sacudir(Golpe golpe, float escala)
    {
        if (!isActiveAndEnabled || golpe.velocidad.sqrMagnitude < 0.0001f) return;
        bool aLaCabeza = golpe.zona == null || golpe.zona.Zona == TipoZona.Cabeza;
        float intensidad = Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(golpe.intensidad)) * escala;
        Vector3 direccion = golpe.velocidad.normalized;

        if (aLaCabeza)
        {
            Transform pivote = cuello != null ? cuello : cabeza;
            Impulsar(ref velocidadCabeza, Par(pivote.position, golpe.punto, direccion), sacudidaCabeza * intensidad);
            // El cuerpo acompaña un poco el golpe a la cabeza
            Impulsar(ref velocidadTorso, Par(columna.position, golpe.punto, direccion), sacudidaTorso * 0.35f * intensidad);
        }
        else
        {
            Impulsar(ref velocidadTorso, Par(columna.position, golpe.punto, direccion), sacudidaTorso * intensidad);
            // Un golpe al cuerpo le hace bajar la cabeza: giro sobre su derecha
            velocidadCabeza += Vector3.right * (sacudidaCabeza * 0.25f * intensidad * Mathf.Sqrt(rigidez));
        }
    }

    // Eje del giro que provoca una fuerza en ese punto, en el espacio de la raíz
    private Vector3 Par(Vector3 pivote, Vector3 punto, Vector3 direccion)
    {
        Vector3 brazo = punto - pivote;
        // Un golpe que pasa justo por el pivote no giraría nada: se le da siempre algo de palanca hacia arriba
        if (brazo.sqrMagnitude < 0.0004f) brazo = Vector3.up * 0.1f;
        Vector3 par = Vector3.Cross(brazo.normalized + Vector3.up * 0.25f, direccion);
        if (par.sqrMagnitude < 0.0001f) par = Vector3.Cross(Vector3.up, direccion);
        return raiz.InverseTransformDirection(par.normalized);
    }

    // La velocidad inicial que hace que el muelle llegue a "grados" de desviación (sin contar la amortiguación)
    private void Impulsar(ref Vector3 velocidad, Vector3 eje, float grados) => velocidad += eje * (grados * Mathf.Sqrt(rigidez));

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // En el piso y mientras se incorpora mandan los clips
        AnimatorStateInfo estado = animator.GetCurrentAnimatorStateInfo(0);
        int hash = estado.shortNameHash;
        bool enElPiso = hash == Knockdown || hash == Defeat || (oponente != null && oponente.Caido);
        presencia = Mathf.MoveTowards(presencia, enElPiso ? 0f : 1f, dt / (enElPiso ? 0.2f : 0.6f));

        // En los clips de reacción la cabeza la mueve el golpe: la mirada cede
        bool reaccionando = hash == ReaccionJab || hash == ReaccionCara || hash == ReaccionCuerpo;
        float objetivoMirada = enElPiso ? 0f : reaccionando ? 0.3f : 1f;
        mirada = Mathf.MoveTowards(mirada, objetivoMirada, dt / 0.25f);

        Muelle(ref anguloCabeza, ref velocidadCabeza, sacudidaCabeza * 1.3f, dt);
        Muelle(ref anguloTorso, ref velocidadTorso, sacudidaTorso * 1.3f, dt);

        float falta = oponente != null ? 1f - oponente.SaludNormalizada : 0f;
        cansancio = Mathf.MoveTowards(cansancio, falta, dt * 0.25f);
        faseRespiracion += dt * Mathf.PI * 2f * Mathf.Lerp(ritmoRespiracion.x, ritmoRespiracion.y, cansancio);
        if (faseRespiracion > Mathf.PI * 2f) faseRespiracion -= Mathf.PI * 2f;

        Vector3 desplazado = esquive != null && esquive.EnMovimiento ? raiz.InverseTransformDirection(esquive.Desplazamiento) : Vector3.zero;
        inclinacion = Vector3.Lerp(inclinacion, desplazado, 1f - Mathf.Exp(-22f * dt));

        if (objetivo == null && Camera.main != null) objetivo = Camera.main.transform;
        if (objetivo != null)
        {
            puntoMirada = hayPuntoMirada
                ? Vector3.Lerp(puntoMirada, objetivo.position, 1f - Mathf.Exp(-dt / suavizadoMirada))
                : objetivo.position;
            hayPuntoMirada = true;
        }
    }

    // Muelle amortiguado por eje, con pasos cortos para que no se dispare si un fotograma viene largo
    private void Muelle(ref Vector3 angulo, ref Vector3 velocidad, float maximo, float dt)
    {
        int pasos = Mathf.Clamp(Mathf.CeilToInt(dt / 0.008f), 1, 8);
        float h = dt / pasos;
        for (int i = 0; i < pasos; i++)
        {
            velocidad += (-rigidez * angulo - amortiguacion * velocidad) * h;
            angulo += velocidad * h;
        }
        if (angulo.magnitude > maximo)
        {
            angulo = angulo.normalized * maximo;
            velocidad = Vector3.ProjectOnPlane(velocidad, angulo.normalized);
        }
    }

    private void OnAnimatorIK(int capa)
    {
        if (capa != 0) return;
        float w = hayPuntoMirada ? pesoMirada * mirada : 0f;
        animator.SetLookAtWeight(w, pesoTorsoMirada, 1f, 0f, 0.55f);
        if (w > 0f) animator.SetLookAtPosition(puntoMirada);
    }

    private void LateUpdate()
    {
        if (presencia <= 0.001f) return;
        float p = Mathf.SmoothStep(0f, 1f, presencia);

        // Torso: cansancio + respiración (sobre su derecha), esquive e impacto
        float cabeceo = encorvado * cansancio
                        + Mathf.Sin(faseRespiracion) * Mathf.Lerp(respiracion.x, respiracion.y, cansancio);
        Vector3 torso = Vector3.right * cabeceo + anguloTorso;
        float distancia = inclinacion.magnitude;
        if (distancia > 0.001f && oponente != null)
        {
            float recorrido = Mathf.Clamp01(distancia / Mathf.Max(0.05f, oponente.Perfil.distanciaEsquive));
            // Inclinarse hacia donde se va = girar sobre el eje horizontal perpendicular al desplazamiento
            torso += Vector3.Cross(Vector3.up, inclinacion / distancia) * (inclinacionEsquive * recorrido);
        }
        Girar(columna, torso * (pecho != null ? 0.5f : 1f) * p);
        if (pecho != null) Girar(pecho, torso * 0.5f * p);

        // Cabeza: impacto + cabeza baja por cansancio; el cuello se lleva una parte
        Vector3 giroCabeza = anguloCabeza + Vector3.right * (cabezaBaja * cansancio);
        if (cuello != null) Girar(cuello, giroCabeza * 0.4f * p);
        Girar(cabeza, giroCabeza * (cuello != null ? 0.6f : 1f) * p);
    }

    // Gira el hueso sobre un eje dado en el espacio de la raíz, sin depender de cómo estén orientados sus ejes propios
    private void Girar(Transform hueso, Vector3 ejeGrados)
    {
        float grados = ejeGrados.magnitude;
        if (grados < 0.01f) return;
        Vector3 eje = raiz.TransformDirection(ejeGrados / grados);
        hueso.rotation = Quaternion.AngleAxis(grados, eje) * hueso.rotation;
    }

    [ContextMenu("Probar: gancho a la cabeza")]
    private void ProbarGancho() => Sacudir(new Golpe
    {
        tipo = TipoGolpe.Lateral, intensidad = 1f, velocidad = raiz.right * 3f,
        punto = cabeza.position + raiz.forward * 0.1f - raiz.right * 0.08f,
    }, 1f);

    [ContextMenu("Probar: recto a la cabeza")]
    private void ProbarRecto() => Sacudir(new Golpe
    {
        tipo = TipoGolpe.Cross, intensidad = 1f, velocidad = -raiz.forward * 3f,
        punto = cabeza.position + raiz.forward * 0.1f,
    }, 1f);
}
