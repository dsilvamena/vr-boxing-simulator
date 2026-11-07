using UnityEngine;

/// <summary>
/// Bloqueo del oponente (RF-07). El Oponente le pregunta por cada golpe del jugador si lo para:
/// la probabilidad base es la del perfil y sube con cada golpe seguido (el rival "lee" la combinación),
/// hasta bloqueoMaximo. Tras un bloqueo mantiene la guardia segundosGuardia, y en ese tiempo para todo; seguir
/// pegando no la alarga. Al bajarla queda abierto aberturaTrasGuardia segundos, sin poder bloquear: es el premio
/// por no rendirse ante la guardia. Mientras ataca tampoco bloquea: contragolpear durante su ataque siempre entra.
/// La guardia se ve con IK de manos (Animator Humanoid): sube los guantes frente a la cara sin necesitar un clip.
/// OnAnimatorIK solo llega al objeto del Animator, por eso se le añade un RelevoIK que llama a AplicarIK.
/// La capa base del Animator Controller necesita IK Pass activado.
/// Si el perfil tiene guardiaGrabada (GrabadoraGuardia), los guantes siguen esa trayectoria grabada con el visor al subir y,
/// al revés, al bajar; si no, van en línea recta a posicionGuantes.
/// </summary>
[RequireComponent(typeof(Oponente))]
public class DefensaOponente : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [Tooltip("Segundos que tarda en subir la guardia")]
    [SerializeField] private float tiempoSubir = 0.06f;
    [Tooltip("Segundos que tarda en bajar la guardia")]
    [SerializeField] private float tiempoBajar = 0.3f;
    [Tooltip("Dónde quedan los guantes respecto a la cabeza, en metros: x hacia cada lado, y arriba, z adelante")]
    [SerializeField] private Vector3 posicionGuantes = new Vector3(0.1f, -0.05f, 0.22f);
    [Tooltip("Segundos tras bajar la guardia en los que no puede volver a bloquear")]
    [SerializeField] private float aberturaTrasGuardia = 0.6f;

    private Oponente oponente;
    private AtaqueOponente ataque;
    private float guardiaHasta = -10f;
    private float ultimoGolpe = -10f;
    private int seguidos;
    private float peso;

    /// <summary>Tiene la guardia arriba: el próximo golpe se bloquea.</summary>
    public bool EnGuardia => Time.time < guardiaHasta && !Atacando;
    /// <summary>Tiene la guardia arriba o acaba de bajarla. EsquiveOponente no esquiva entonces: la abertura es el premio del jugador.</summary>
    public bool GuardiaOcupada => Time.time < guardiaHasta + aberturaTrasGuardia;
    /// <summary>Probabilidad de bloquear el próximo golpe si llega ahora.</summary>
    public float ProbabilidadActual
    {
        get
        {
            PerfilOponente p = oponente.Perfil;
            int n = Time.time - ultimoGolpe <= p.ventanaGolpesSeguidos ? seguidos + 1 : 0;
            return Mathf.Max(p.probabilidadBloqueo, Mathf.Min(p.bloqueoMaximo, p.probabilidadBloqueo + n * p.bloqueoPorGolpeSeguido));
        }
    }

    private bool Atacando => ataque != null && ataque.enabled && ataque.Atacando;

    private void Awake()
    {
        oponente = GetComponent<Oponente>();
        ataque = GetComponent<AtaqueOponente>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator == null || !animator.isHuman)
        {
            Debug.LogWarning($"{name}: DefensaOponente sin Animator Humanoid; bloquea pero no se ve la guardia.", this);
            return;
        }
        var relevo = animator.GetComponent<RelevoIK>();
        if (relevo == null) relevo = animator.gameObject.AddComponent<RelevoIK>();
        relevo.Destino = this;
    }

    /// <summary>Decide si para este golpe. Lo llama Oponente.RecibirGolpe una vez por golpe que cuenta.</summary>
    public bool Bloquea(Golpe golpe)
    {
        float probabilidad = ProbabilidadActual;
        PerfilOponente p = oponente.Perfil;
        seguidos = Time.time - ultimoGolpe <= p.ventanaGolpesSeguidos ? seguidos + 1 : 0;
        ultimoGolpe = Time.time;

        if (Atacando)
        {
            guardiaHasta = Mathf.Min(guardiaHasta, Time.time);
            return false;
        }
        if (EnGuardia) return true;
        if (Time.time < guardiaHasta + aberturaTrasGuardia) return false;
        if (Random.value >= probabilidad) return false;

        // Al bloquear vuelve a la probabilidad base: el jugador tiene que armar otra combinación
        guardiaHasta = Time.time + p.segundosGuardia;
        seguidos = 0;
        return true;
    }

    /// <summary>Baja la guardia y olvida los golpes seguidos (descanso entre rounds).</summary>
    public void Reiniciar()
    {
        guardiaHasta = -10f;
        ultimoGolpe = -10f;
        seguidos = 0;
    }

    private void Update()
    {
        bool arriba = EnGuardia && !oponente.Caido;
        float duracion = arriba ? tiempoSubir : tiempoBajar;
        peso = Mathf.MoveTowards(peso, arriba ? 1f : 0f, Time.deltaTime / Mathf.Max(0.01f, duracion));
    }

    public void AplicarIK(int capa)
    {
        if (capa != 0) return;
        float w = Mathf.SmoothStep(0f, 1f, peso);
        animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, w);
        animator.SetIKPositionWeight(AvatarIKGoal.RightHand, w);
        if (w <= 0.001f) return;

        Transform cabeza = animator.GetBoneTransform(HumanBodyBones.Head);
        if (cabeza == null) return;
        // El rival siempre mira al jugador (AtaqueOponente lo gira), así que su frente es hacia el jugador
        Vector3 frente = transform.forward;
        Vector3 derecha = transform.right;

        GrabacionGuardia grabada = oponente.Perfil.guardiaGrabada;
        if (grabada != null && grabada.Valida)
        {
            AplicarGrabacion(grabada, cabeza, frente, derecha);
            return;
        }

        float escala = transform.lossyScale.y;
        Vector3 centro = cabeza.position + (frente * posicionGuantes.z + Vector3.up * posicionGuantes.y) * escala;
        animator.SetIKPosition(AvatarIKGoal.LeftHand, centro - derecha * posicionGuantes.x * escala);
        animator.SetIKPosition(AvatarIKGoal.RightHand, centro + derecha * posicionGuantes.x * escala);
    }

    // La subida grabada, escalada a la altura del rival. peso es el avance: al subir va hacia delante y al bajar hacia atrás.
    // La grabación empieza con las manos de quien grabó (abajo, en el pecho), y el rival ya tiene las manos arriba en la
    // animación de Idle: la trayectoria arranca en la pose animada y esa diferencia se va borrando a lo largo de la subida,
    // así se conserva la forma del movimiento grabado sin saltos ni bajar las manos de más.
    private void AplicarGrabacion(GrabacionGuardia grabada, Transform cabeza, Vector3 frente, Vector3 derecha)
    {
        float alturaRival = cabeza.position.y - transform.position.y;
        float escala = grabada.alturaCabeza > 0.1f ? alturaRival / grabada.alturaCabeza : transform.lossyScale.y;
        grabada.Evaluar(0f, out Vector3 izquierda0, out Vector3 derecha0);
        grabada.Evaluar(peso, out Vector3 izquierda, out Vector3 derechaLocal);
        Vector3 Mundo(Vector3 local) => cabeza.position + Marco(local, frente, derecha) * escala;
        Colocar(AvatarIKGoal.LeftHand, Mundo(izquierda), Mundo(izquierda0));
        Colocar(AvatarIKGoal.RightHand, Mundo(derechaLocal), Mundo(derecha0));
    }

    private void Colocar(AvatarIKGoal mano, Vector3 grabado, Vector3 inicioGrabado)
    {
        // Con peso 0 en este fotograma, GetIKPosition devuelve dónde deja la mano la animación
        animator.SetIKPositionWeight(mano, 0f);
        Vector3 animada = animator.GetIKPosition(mano);
        animator.SetIKPositionWeight(mano, 1f);
        animator.SetIKPosition(mano, grabado + (animada - inicioGrabado) * (1f - peso));
    }

    private static Vector3 Marco(Vector3 local, Vector3 frente, Vector3 derecha) =>
        derecha * local.x + Vector3.up * local.y + frente * local.z;

    [ContextMenu("Probar: subir la guardia")]
    private void ProbarGuardia() => guardiaHasta = Time.time + oponente.Perfil.segundosGuardia * 3f;
}
