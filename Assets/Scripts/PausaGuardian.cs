using UnityEngine;

/// <summary>
/// RF-20: pausa el combate cuando el jugador sale del área segura del Guardián de Meta Quest y lo reanuda al volver.
/// También pausa cuando el sistema le quita el foco a la app (menú de Meta, visor quitado), que es cuando el Quest
/// muestra su propia capa encima. Usa PauseMenu, así que el jugador ve el menú de pausa con un aviso como título.
/// Solo reanuda las pausas que abrió él: si el jugador pausó con el botón Menu, la deja abierta.
///
/// El área es el Play Area de OVRBoundary. Con OpenXR esa geometría viene en el espacio Stage (centrada en el centro
/// del área que se dibujó en el visor), mientras que el rig usa el origen Floor Level (donde estaba el visor al arrancar
/// o al recentrar). Antes se comparaba sin convertir, y si el jugador no empezaba en el centro del área, o recentraba,
/// el juego decía "VUELVE AL ÁREA SEGURA" estando dentro. Ahora:
/// - Los puntos se pasan del espacio Stage al de tracking con la pose que da OVRPlugin (solo si vienen centrados en el
///   origen, que es la forma de la geometría en Stage; si ya vinieran en tracking, no se tocan).
/// - El área se vuelve a leer al recuperar el foco (después de redibujar el Guardián), al recentrar y cada pocos segundos.
/// - Hace falta estar fuera tiempoFuera seguidos para pausar, y volver dentro con margen para reanudar.
/// - Válvula de seguridad: si el jugador pulsa REANUDAR mientras el juego cree que está fuera, se deja de usar la
///   geometría el resto de la sesión (el Guardián del propio Quest sigue avisando, y la pausa por foco sigue activa).
///   Así un fallo de la geometría nunca deja al jugador atrapado en la pausa.
/// En el Editor no hay Guardián: se puede simular un área (simularAreaEnEditor) o usar los menús contextuales.
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

    private const float TiempoFuera = 0.25f;        // segundos seguidos fuera antes de pausar
    private const float MargenVuelta = 0.05f;       // metros extra hacia dentro para reanudar (histéresis)
    private const float SegundosRecarga = 2f;       // cada cuánto se vuelve a leer el área
    private const float CentradoStage = 0.05f;      // un área con el centro a menos de esto del origen está en Stage

    /// <summary>La geometría resultó no ser fiable (el jugador reanudó estando "fuera"): no se usa hasta cerrar la app.</summary>
    private static bool geometriaDescartada;

    private OVRCameraRig rig;
    private Vector3[] limite;           // en el espacio de tracking
    private bool sinFoco;
    private bool forzarFuera;
    private bool pausadoPorSistema;
    private bool pausadoPorArea;
    private float fueraDesde = -1f;
    private float proximaRecarga;

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
        if (OVRManager.display != null) OVRManager.display.RecenteredPose += CargarLimite;
    }

    private void OnDisable()
    {
        OVRManager.InputFocusLost -= PerderFoco;
        OVRManager.InputFocusAcquired -= RecuperarFoco;
        OVRManager.HMDUnmounted -= PerderFoco;
        OVRManager.HMDMounted -= RecuperarFoco;
        if (OVRManager.display != null) OVRManager.display.RecenteredPose -= CargarLimite;
    }

    private void Start()
    {
        rig = FindAnyObjectByType<OVRCameraRig>();
        CargarLimite();
    }

    private void PerderFoco() => sinFoco = true;

    private void RecuperarFoco()
    {
        sinFoco = false;
        CargarLimite(); // al volver del menú de Meta el área puede ser otra (se redibujó el Guardián)
    }

    private void CargarLimite()
    {
        proximaRecarga = Time.unscaledTime + SegundosRecarga;
        if (geometriaDescartada)
        {
            limite = null;
            return;
        }
        Vector3[] puntos = null;
        if (!Application.isEditor && OVRManager.boundary != null && OVRManager.boundary.GetConfigured())
            puntos = OVRManager.boundary.GetGeometry(OVRBoundary.BoundaryType.PlayArea);
        if (puntos != null && puntos.Length >= 3)
        {
            limite = AEspacioDeTracking(puntos);
            return;
        }
        limite = null;
        if (Application.isEditor && simularAreaEnEditor)
        {
            Vector2 m = areaSimulada * 0.5f;
            limite = new[] { new Vector3(-m.x, 0f, -m.y), new Vector3(-m.x, 0f, m.y), new Vector3(m.x, 0f, m.y), new Vector3(m.x, 0f, -m.y) };
        }
    }

    // Con OpenXR el Play Area es el rectángulo del espacio Stage, centrado en su origen. El rig vive en Floor Level:
    // se pasa con la pose del origen Stage en el espacio de tracking actual (la misma conversión que usa la SDK de Meta).
    private static Vector3[] AEspacioDeTracking(Vector3[] puntos)
    {
        Vector3 centro = Vector3.zero;
        foreach (Vector3 p in puntos) centro += p;
        centro /= puntos.Length;
        if (new Vector2(centro.x, centro.z).magnitude > CentradoStage) return puntos; // ya está en tracking

        OVRPose stageALocal = OVRPlugin.GetTrackingTransformRelativePose(OVRPlugin.TrackingOrigin.Stage).ToOVRPose();
        var resultado = new Vector3[puntos.Length];
        for (int i = 0; i < puntos.Length; i++)
            resultado[i] = stageALocal.position + stageALocal.orientation * puntos[i];
        return resultado;
    }

    private void Update()
    {
        if (Time.unscaledTime >= proximaRecarga && !sinFoco) CargarLimite();

        // El jugador cerró a mano la pausa del área: o volvió, o la geometría está mal. Si sigue "fuera", se descarta.
        if (pausadoPorSistema && !pausa.IsOpen)
        {
            pausadoPorSistema = false;
            if (pausadoPorArea && !forzarFuera && FueraDelArea(0f))
            {
                geometriaDescartada = true;
                limite = null;
                Debug.LogWarning($"{name}: el jugador reanudó estando fuera del área según el juego; se deja de usar la " +
                                 "geometría del Guardián en esta sesión (el Guardián del sistema sigue activo).", this);
            }
            pausadoPorArea = false;
            fueraDesde = -1f;
        }

        bool fuera = forzarFuera || FueraDelArea(pausadoPorArea ? MargenVuelta : 0f);
        if (fuera && fueraDesde < 0f) fueraDesde = Time.unscaledTime;
        if (!fuera) fueraDesde = -1f;
        bool fueraDelArea = forzarFuera || (fuera && Time.unscaledTime - fueraDesde >= TiempoFuera);
        bool detener = sinFoco || fueraDelArea;

        if (detener && !pausadoPorSistema && !pausa.IsOpen)
        {
            pausadoPorSistema = true;
            pausadoPorArea = fueraDelArea;
            pausa.Show(fueraDelArea ? aviso : avisoSinFoco);
            Debug.Log($"{name}: combate pausado ({(fueraDelArea ? "fuera del área segura" : "sin foco")}).", this);
        }
        else if (!detener && pausadoPorSistema && !fuera)
        {
            pausadoPorSistema = false;
            pausadoPorArea = false;
            if (pausa.IsOpen) pausa.Hide();
            Debug.Log($"{name}: combate reanudado.", this);
        }
    }

    // margenExtra > 0 pide estar más adentro (para reanudar), y así no parpadea en el borde
    private bool FueraDelArea(float margenExtra)
    {
        if (limite == null || rig == null) return false;
        Vector3 cabeza = rig.trackingSpace.InverseTransformPoint(rig.centerEyeAnchor.position);
        var p = new Vector2(cabeza.x, cabeza.z);
        return !DentroDelPoligono(p) || DistanciaAlBorde(p) < margen + margenExtra;
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
