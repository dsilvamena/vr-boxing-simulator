using UnityEngine;

/// <summary>
/// Hitbox (collider trigger) de una parte del cuerpo de un Oponente.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ZonaGolpe : MonoBehaviour
{
    [SerializeField] private TipoZona zona = TipoZona.Torso;
    [Tooltip("Multiplica el daño y la reacción de los golpes en esta zona")]
    [SerializeField] private float multiplicador = 1f;

    public TipoZona Zona => zona;
    public float Multiplicador => multiplicador;
    public Oponente Oponente { get; private set; }

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
        Oponente = GetComponentInParent<Oponente>();
        if (Oponente == null) Debug.LogWarning($"{name}: ZonaGolpe sin Oponente en los padres.", this);
    }

    public void Configurar(TipoZona tipo, float mult)
    {
        zona = tipo;
        multiplicador = mult;
    }
}
