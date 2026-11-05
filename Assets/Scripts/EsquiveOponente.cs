using UnityEngine;

/// <summary>
/// Esquive activo del oponente (RF-07). El Oponente le pregunta por cada golpe del jugador que le llega a una ZonaGolpe,
/// antes que a DefensaOponente. Se decide al contacto y no anticipando con la mano: solo entonces se sabe que hay un
/// golpe de verdad, así que un amago no lo hace saltar.
/// Si esquiva, el golpe no daña, no cuenta como golpe seguido para DefensaOponente y dispara Oponente.AlEsquivar.
/// AtaqueOponente responde con la ventana de contraataque del perfil.
/// No esquiva mientras ataca, cae, se incorpora o está Protegido, ni con la guardia arriba o recién bajada (la abertura
/// es el premio del jugador), ni durante el enfriamiento. Mientras se aparta (segundosEsquive) tampoco le llega la otra
/// mano; al volver (segundosRegresoEsquive) ya se le puede pegar.
/// Sin animación de esquive, por ahora se mueve el transform: se aparta al lado contrario del guante o hacia atrás y
/// vuelve a su sitio. El desplazamiento se suma como diferencia a cada fotograma, así que no pisa la caminata de
/// AtaqueOponente ni la vuelta a la posición inicial del descanso, y al terminar no deja ningún desvío. Las hitboxes
/// cuelgan de los huesos y se mueven con él. Nunca sale del ring (LimiteRing.AreaRing).
/// </summary>
[RequireComponent(typeof(Oponente))]
public class EsquiveOponente : MonoBehaviour
{
    [Tooltip("De cada esquive, la parte que va hacia atrás; el resto va al lado contrario del guante")]
    [SerializeField, Range(0f, 1f)] private float fraccionAtras = 0.3f;
    [Tooltip("Distancia que deja entre su centro y las cuerdas al esquivar, en metros")]
    [SerializeField, Min(0f)] private float margenRing = 0.4f;
    [Tooltip("Fracción de distanciaEsquive que debe poder recorrer una dirección; si no, prueba otra")]
    [SerializeField, Range(0f, 1f)] private float recorridoMinimo = 0.6f;

    private Oponente oponente;
    private AtaqueOponente ataque;
    private DefensaOponente defensa;
    private LimiteRing ring;
    private float listoEn;
    private float tiempo;
    private Vector3 desplazamiento;
    private Vector3 aplicado;
    private bool forzar;

    /// <summary>Se está apartando o volviendo a su sitio.</summary>
    public bool EnMovimiento { get; private set; }
    /// <summary>Se está apartando: los golpes que lleguen tampoco lo alcanzan.</summary>
    public bool Apartandose => EnMovimiento && tiempo < oponente.Perfil.segundosEsquive;

    private bool Atacando => ataque != null && ataque.enabled && ataque.Atacando;

    private void Awake()
    {
        oponente = GetComponent<Oponente>();
        ataque = GetComponent<AtaqueOponente>();
        defensa = GetComponent<DefensaOponente>();
    }

    // Si lo desactivan a medio esquive, no se queda desplazado
    private void OnDisable()
    {
        if (!EnMovimiento) return;
        transform.position -= aplicado;
        aplicado = Vector3.zero;
        EnMovimiento = false;
    }

    /// <summary>Decide si esquiva este golpe. Lo llama Oponente.RecibirGolpe una vez por golpe que cuenta.</summary>
    public bool Esquiva(Golpe golpe)
    {
        if (Apartandose && !Atacando) return true;
        if (MotivoParaNoEsquivar() != null) return false;

        PerfilOponente p = oponente.Perfil;
        if (!forzar && Random.value >= p.probabilidadEsquive) return false;

        listoEn = Time.time + p.enfriamientoEsquive;
        tiempo = 0f;
        desplazamiento = ElegirDesplazamiento(golpe, p.distanciaEsquive);
        aplicado = Vector3.zero;
        EnMovimiento = true;
        return true;
    }

    /// <summary>Permite volver a esquivar en cuanto empiece el round (descanso). El movimiento en curso termina solo.</summary>
    public void Reiniciar() => listoEn = 0f;

    private string MotivoParaNoEsquivar()
    {
        if (EnMovimiento) return "todavía está en un esquive";
        if (Time.time < listoEn) return "está en enfriamiento";
        if (oponente.Caido) return "está caído";
        if (oponente.Protegido) return "está protegido (fuera de los rounds)";
        if (oponente.Invulnerable) return "se está incorporando o acaba de recibir un golpe";
        if (Atacando) return "está atacando";
        if (defensa != null && defensa.GuardiaOcupada) return "tiene la guardia arriba o recién bajada";
        return null;
    }

    private void Update()
    {
        if (!EnMovimiento) return;

        tiempo += Time.deltaTime;
        PerfilOponente p = oponente.Perfil;
        float salida = Mathf.Max(0.01f, p.segundosEsquive);
        float regreso = Mathf.Max(0.01f, p.segundosRegresoEsquive);

        // Sale deprisa desde el primer fotograma (el guante ya está encima) y vuelve con suavidad
        float fraccion = tiempo < salida
            ? Mathf.Sin(tiempo / salida * Mathf.PI * 0.5f)
            : 1f - Mathf.SmoothStep(0f, 1f, (tiempo - salida) / regreso);

        Vector3 nuevo = desplazamiento * fraccion;
        if (tiempo >= salida + regreso)
        {
            nuevo = Vector3.zero;
            EnMovimiento = false;
        }
        transform.position = Limitar(transform.position + nuevo - aplicado);
        aplicado = nuevo;
    }

    // Prefiere apartarse del guante o, a veces, hacia atrás. Si eso lo deja contra las cuerdas, prueba las otras direcciones
    private Vector3 ElegirDesplazamiento(Golpe golpe, float distancia)
    {
        Vector3 lado = Vector3.Dot(golpe.punto - transform.position, transform.right) > 0f ? -transform.right : transform.right;
        Vector3 atras = -transform.forward;
        Vector3[] direcciones = Random.value < fraccionAtras
            ? new[] { atras, lado, -lado }
            : new[] { lado, atras, -lado };

        Vector3 mejor = Vector3.zero;
        foreach (Vector3 direccion in direcciones)
        {
            Vector3 recorrido = Limitar(transform.position + direccion * distancia) - transform.position;
            if (recorrido.magnitude >= distancia * recorridoMinimo) return recorrido;
            if (recorrido.sqrMagnitude > mejor.sqrMagnitude) mejor = recorrido;
        }
        return mejor;
    }

    private Vector3 Limitar(Vector3 punto)
    {
        if (ring == null) ring = FindAnyObjectByType<LimiteRing>();
        return ring != null ? ring.DentroDelRing(punto, margenRing) : punto;
    }

    // Para probar sin visor: en Play, clic derecho en el título del componente. Sigue el camino de un golpe real
    // (Oponente.RecibirGolpe, AlEsquivar, contraataque) y solo ignora la probabilidad.
    [ContextMenu("Probar: esquivar")]
    private void ProbarEsquivar()
    {
        string motivo = MotivoParaNoEsquivar();
        if (motivo != null)
        {
            Debug.LogWarning($"{name}: no puede esquivar porque {motivo}.", this);
            return;
        }

        PerfilOponente p = oponente.Perfil;
        Vector3 ladoGuante = transform.right * (Random.value < 0.5f ? 0.3f : -0.3f);
        forzar = true;
        oponente.RecibirGolpe(new Golpe
        {
            tipo = TipoGolpe.Cross,
            velocidad = -transform.forward * p.rapidezGolpeCompleto,
            punto = transform.position + Vector3.up * p.alturaAtaque + ladoGuante,
        });
        forzar = false;
    }
}
