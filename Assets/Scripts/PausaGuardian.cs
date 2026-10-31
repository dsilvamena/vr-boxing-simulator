using UnityEngine;

/// <summary>
/// RF-20: pausa el combate cuando el jugador sale del área segura del Guardián de Meta Quest y lo reanuda al volver.
/// También pausa cuando el sistema le quita el foco a la app (menú de Meta, visor quitado), que es cuando el Quest
/// muestra su propia capa encima. Usa PauseMenu, así que el jugador ve el menú de pausa con un aviso como título.
/// Solo reanuda las pausas que abrió él: si el jugador pausó con el botón Menu, la deja abierta.
/// El área es el Play Area que devuelve OVRBoundary (en el espacio de tracking). En el Editor no hay Guardián:
/// se puede simular un área (simularAreaEnEditor) o usar los menús contextuales del componente.
/// Va junto al PauseMenu de cada escena de pelea.
/// </summary>
public class PausaGuardian : MonoBehaviour
{
    [Tooltip("Vacío = el PauseMenu de este objeto")]
    [SerializeField] private PauseMenu pausa;
    [Tooltip("Metros antes del borde en los que ya pausa (el Guardián aparece un poco antes del límite)")]
    [SerializeField, Min(0f)] private float margen = 0.1f;
    [Tooltip("Título del menú de pausa mientras está fuera del área")]
    [SerializeField] private string aviso = "VUELVE AL ÁREA SEGURA";
    [Tooltip("Título del menú de pausa cuando el sistema le quitó el foco")]
    [SerializeField] private string avisoSinFoco = "PAUSA";

    [Header("Solo en el Editor")]
    [Tooltip("Sin Guardián (Editor, Meta XR Simulator): usa un área rectangular centrada en el origen del tracking")]
    [SerializeField] private bool simularAreaEnEditor = false;
    [Tooltip("Ancho y largo del área simulada, en metros (el informe pide al menos 1,5 × 1,5)")]
    [SerializeField] private Vector2 areaSimulada = new Vector2(2f, 2f);

    private OVRCameraRig rig;
    private Vector3[] limite;
    private bool sinFoco;
    private bool forzarFuera;
    private bool pausadoPorSistema;

    private void Awake()
    {
        if (pausa == null) pausa = GetComponent<PauseMenu>();
        if (pausa == null) { Debug.LogWarning($"{name}: PausaGuardian necesita un PauseMenu.", this); enabled = false; }
    }

    private void OnEnable()
    {
        OVRManager.InputFocusLost += PerderFoco;
        OVRManager.InputFocusAcquired += RecuperarFoco;
        OVRManager.HMDUnmounted += PerderFoco;
        OVRManager.HMDMounted += RecuperarFoco;
    }

    private void OnDisable()
    {
        OVRManager.InputFocusLost -= PerderFoco;
        OVRManager.InputFocusAcquired -= RecuperarFoco;
        OVRManager.HMDUnmounted -= PerderFoco;
        OVRManager.HMDMounted -= RecuperarFoco;
    }

    private void Start()
    {
        rig = FindAnyObjectByType<OVRCameraRig>();
        CargarLimite();
    }

    private void PerderFoco() => sinFoco = true;

    private void RecuperarFoco() => sinFoco = false;

    private void CargarLimite()
    {
        if (OVRManager.boundary != null && OVRManager.boundary.GetConfigured())
        {
            limite = OVRManager.boundary.GetGeometry(OVRBoundary.BoundaryType.PlayArea);
            if (limite != null && limite.Length >= 3)
            {
                Debug.Log($"{name}: área del Guardián con {limite.Length} vértices.", this);
                return;
            }
        }
        limite = null;
        if (Application.isEditor && simularAreaEnEditor)
        {
            Vector2 m = areaSimulada * 0.5f;
            limite = new[] { new Vector3(-m.x, 0f, -m.y), new Vector3(-m.x, 0f, m.y), new Vector3(m.x, 0f, m.y), new Vector3(m.x, 0f, -m.y) };
        }
    }

    private void Update()
    {
        bool fueraDelArea = forzarFuera || FueraDelArea();
        bool detener = sinFoco || fueraDelArea;

        if (detener && !pausadoPorSistema && !pausa.IsOpen)
        {
            pausadoPorSistema = true;
            pausa.Show(fueraDelArea ? aviso : avisoSinFoco);
            Debug.Log($"{name}: combate pausado ({(fueraDelArea ? "fuera del área segura" : "sin foco")}).", this);
        }
        else if (!detener && pausadoPorSistema)
        {
            pausadoPorSistema = false;
            if (pausa.IsOpen) pausa.Hide();
            Debug.Log($"{name}: combate reanudado.", this);
        }
        // Si el jugador cerró la pausa a mano estando fuera, vuelve a abrirse en el siguiente fotograma
        else if (pausadoPorSistema && !pausa.IsOpen)
        {
            pausadoPorSistema = false;
        }
    }

    private bool FueraDelArea()
    {
        if (limite == null || rig == null) return false;
        // La geometría del Guardián está en el espacio de tracking, igual que la pose local de la cabeza
        Vector3 cabeza = rig.trackingSpace.InverseTransformPoint(rig.centerEyeAnchor.position);
        var p = new Vector2(cabeza.x, cabeza.z);
        return !DentroDelPoligono(p) || DistanciaAlBorde(p) < margen;
    }

    private bool DentroDelPoligono(Vector2 p)
    {
        bool dentro = false;
        for (int i = 0, j = limite.Length - 1; i < limite.Length; j = i++)
        {
            Vector2 a = new Vector2(limite[i].x, limite[i].z);
            Vector2 b = new Vector2(limite[j].x, limite[j].z);
            if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) dentro = !dentro;
        }
        return dentro;
    }

    private float DistanciaAlBorde(Vector2 p)
    {
        float minimo = float.MaxValue;
        for (int i = 0, j = limite.Length - 1; i < limite.Length; j = i++)
        {
            Vector2 a = new Vector2(limite[i].x, limite[i].z);
            Vector2 b = new Vector2(limite[j].x, limite[j].z);
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 0.0001f));
            minimo = Mathf.Min(minimo, Vector2.Distance(p, a + ab * t));
        }
        return minimo;
    }

    // Para probar sin visor: en Play, clic derecho en el título del componente
    [ContextMenu("Probar: salir del área segura")]
    private void ProbarSalir() => forzarFuera = true;

    [ContextMenu("Probar: volver al área segura")]
    private void ProbarVolver() => forzarFuera = false;
}
