using UnityEngine;

/// <summary>
/// Animación del cinturón de campeón (RF-19) en Scene_Results: al activarse sube desde abajo creciendo (despliegue),
/// luego gira sin parar sobre el eje vertical y el oro brilla con un pulso de emisión. La luz opcional pulsa a la vez.
/// Va en la raíz del cinturón; PantallaResultados lo activa solo al ganar el último nivel.
/// El brillo usa un MaterialPropertyBlock sobre _EmissionColor, así que el material del oro debe tener activada la
/// emisión (palabra clave _EMISSION) con color negro: así la variante del shader entra en la build y en reposo no brilla.
/// Usa tiempo sin escalar, así que también se mueve si timeScale es 0.
/// </summary>
public class CinturonCampeon : MonoBehaviour
{
    [Header("Despliegue")]
    [Tooltip("Segundos que tarda en subir y crecer hasta su tamaño")]
    [SerializeField, Min(0.01f)] private float segundosDespliegue = 1.6f;
    [Tooltip("Metros por debajo de su posición desde los que sube")]
    [SerializeField] private float alturaInicial = 0.6f;

    [Header("Giro")]
    [SerializeField] private float gradosPorSegundo = 35f;

    [Header("Brillo")]
    [Tooltip("Renderer del cinturón; vacío = el primero de los hijos")]
    [SerializeField] private Renderer cuerpo;
    [Tooltip("Índice del material del oro en el Renderer")]
    [SerializeField, Min(0)] private int indiceOro = 1;
    [ColorUsage(false, true)]
    [SerializeField] private Color colorBrillo = new Color(1f, 0.75f, 0.3f);
    [Tooltip("Intensidad de la emisión: mínima y máxima del pulso")]
    [SerializeField] private Vector2 intensidad = new Vector2(0.2f, 0.75f);
    [Tooltip("Pulsos por segundo")]
    [SerializeField] private float frecuencia = 0.7f;
    [Tooltip("Destello extra al terminar el despliegue")]
    [SerializeField] private float destello = 1.5f;
    [Tooltip("Luz que pulsa con el brillo (opcional)")]
    [SerializeField] private Light luz;

    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    private Vector3 posicionFinal;
    private Vector3 escalaFinal;
    private float inicio;
    private float intensidadLuz;
    private MaterialPropertyBlock bloque;
    private bool preparado;

    /// <summary>Terminó de subir y crecer.</summary>
    public bool Desplegado => Time.unscaledTime - inicio >= segundosDespliegue;

    private void Preparar()
    {
        if (preparado) return;
        preparado = true;
        posicionFinal = transform.localPosition;
        escalaFinal = transform.localScale;
        if (cuerpo == null) cuerpo = GetComponentInChildren<Renderer>();
        if (luz != null) intensidadLuz = luz.intensity;
        bloque = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        Preparar();
        inicio = Time.unscaledTime;
        Actualizar();
    }

    private void OnDisable()
    {
        if (!preparado) return;
        transform.localPosition = posicionFinal;
        transform.localScale = escalaFinal;
    }

    private void Update() => Actualizar();

    private void Actualizar()
    {
        float t = Time.unscaledTime - inicio;
        float avance = Mathf.Clamp01(t / segundosDespliegue);
        float subida = SalidaSuave(avance);
        transform.localPosition = posicionFinal + Vector3.down * (alturaInicial * (1f - subida));
        transform.localScale = escalaFinal * Mathf.Lerp(0.2f, 1f, SalidaConRebote(avance));
        // Gira más rápido mientras sube y se asienta en la velocidad normal
        float velocidad = gradosPorSegundo * (1f + 3f * (1f - subida));
        transform.Rotate(Vector3.up, velocidad * Time.unscaledDeltaTime, Space.World);

        float pulso = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * frecuencia * t);
        float brillo = Mathf.Lerp(intensidad.x, intensidad.y, pulso);
        // Destello al llegar arriba, que se apaga en medio segundo
        float trasDespliegue = t - segundosDespliegue;
        if (trasDespliegue >= 0f && trasDespliegue < 0.5f) brillo += destello * (1f - trasDespliegue / 0.5f);

        if (cuerpo != null)
        {
            cuerpo.GetPropertyBlock(bloque, indiceOro);
            bloque.SetColor(EmissionColor, colorBrillo * brillo);
            cuerpo.SetPropertyBlock(bloque, indiceOro);
        }
        if (luz != null) luz.intensity = intensidadLuz * (0.6f + 0.4f * pulso) * subida;
    }

    private static float SalidaSuave(float x) => 1f - (1f - x) * (1f - x) * (1f - x);

    // Se pasa un poco del tamaño final y vuelve
    private static float SalidaConRebote(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }

    [ContextMenu("Probar: desplegar de nuevo")]
    private void ProbarDespliegue()
    {
        Preparar();
        inicio = Time.unscaledTime;
    }
}
