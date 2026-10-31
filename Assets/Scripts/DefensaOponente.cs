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
        float escala = transform.lossyScale.y;
        Vector3 frente = transform.forward;
        Vector3 derecha = transform.right;
        Vector3 centro = cabeza.position + (frente * posicionGuantes.z + Vector3.up * posicionGuantes.y) * escala;
        animator.SetIKPosition(AvatarIKGoal.LeftHand, centro - derecha * posicionGuantes.x * escala);
        animator.SetIKPosition(AvatarIKGoal.RightHand, centro + derecha * posicionGuantes.x * escala);
    }

    [ContextMenu("Probar: subir la guardia")]
    private void ProbarGuardia() => guardiaHasta = Time.time + oponente.Perfil.segundosGuardia * 3f;
}
