using System;
using System.Collections;
using System.Globalization;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Graba con el visor cómo sube la guardia una persona real, para que los rivales bloqueen con ese movimiento
/// (GrabacionGuardia, DefensaOponente). Va en el OVRCameraRig de Main_Scene: ahí no hay rival que moleste.
/// Uso: con las manos abajo, pulsar a la vez los dos sticks (o, en el Editor, el menú contextual). Tres tics de cuenta atrás,
/// se graban segundosGrabacion en los que hay que subir la guardia y dejarla quieta, y un pulso doble marca el final.
/// Se recorta a la subida y se guarda como JSON en persistentDataPath/Guardias; en el Editor (Play con Quest Link) también
/// como asset en Assets/_Project/Data/Guardias, listo para arrastrarlo al campo guardiaGrabada de un PerfilOponente.
/// Los JSON grabados en el Quest se importan con Tools > Boxeo > Importar guardias grabadas.
/// </summary>
public class GrabadoraGuardia : MonoBehaviour
{
    public const string CarpetaJson = "Guardias";
    public const string CarpetaAssets = "Assets/_Project/Data/Guardias";

    [Tooltip("Segundos de cuenta atrás antes de grabar")]
    [SerializeField, Min(0)] private int segundosCuentaAtras = 3;
    [Tooltip("Segundos que dura la grabación (después se recorta a la subida)")]
    [SerializeField, Min(0.5f)] private float segundosGrabacion = 2f;
    [Tooltip("Muestras por segundo")]
    [SerializeField, Range(20, 120)] private int frecuencia = 60;
    [Tooltip("Nombre que se pone a lo grabado (quien graba)")]
    [SerializeField] private string autor = "";
    [Header("Origen (vacío = el OVRCameraRig)")]
    [Tooltip("Cabeza; vacío = centerEyeAnchor")]
    [SerializeField] private Transform cabeza;
    [Tooltip("Mano izquierda; vacío = leftHandAnchor")]
    [SerializeField] private Transform manoIzquierda;
    [Tooltip("Mano derecha; vacío = rightHandAnchor")]
    [SerializeField] private Transform manoDerecha;
    [Tooltip("Piso, para la altura de la cabeza; vacío = trackingSpace (Floor Level)")]
    [SerializeField] private Transform piso;

    public bool Grabando { get; private set; }
    /// <summary>Última grabación terminada, ya recortada.</summary>
    public GrabacionGuardia Ultima { get; private set; }
    /// <summary>Ruta del último JSON guardado.</summary>
    public string UltimaRuta { get; private set; }

    private OVRCameraRig rig;

    private void Awake()
    {
        rig = GetComponentInParent<OVRCameraRig>();
        if (rig == null && (cabeza == null || manoIzquierda == null || manoDerecha == null))
        {
            Debug.LogWarning($"{name}: GrabadoraGuardia necesita estar en el OVRCameraRig o tener asignadas la cabeza y las manos.", this);
            enabled = false;
        }
    }

    private void Update()
    {
        if (Grabando) return;
        bool izquierdo = OVRInput.Get(OVRInput.Button.PrimaryThumbstick, OVRInput.Controller.Touch);
        bool derecho = OVRInput.Get(OVRInput.Button.SecondaryThumbstick, OVRInput.Controller.Touch);
        if (izquierdo && derecho) Grabar();
    }

    /// <summary>Empieza la cuenta atrás y la grabación.</summary>
    public void Grabar()
    {
        if (!Grabando && enabled) StartCoroutine(Grabacion());
    }

    private IEnumerator Grabacion()
    {
        Grabando = true;
        for (int i = 0; i < segundosCuentaAtras; i++)
        {
            Pulso(0.4f, 0.05f);
            yield return new WaitForSecondsRealtime(1f);
        }
        Pulso(1f, 0.1f);

        var grabacion = ScriptableObject.CreateInstance<GrabacionGuardia>();
        float inicio = Time.realtimeSinceStartup;
        float paso = 1f / frecuencia;
        float siguiente = 0f;
        float alturaCabeza = AlturaCabeza();
        while (true)
        {
            float t = Time.realtimeSinceStartup - inicio;
            if (t > segundosGrabacion) break;
            if (t >= siguiente)
            {
                grabacion.muestras.Add(Muestrear(t));
                siguiente += paso;
                alturaCabeza = Mathf.Max(alturaCabeza, AlturaCabeza());
            }
            yield return null;
        }

        int total = grabacion.muestras.Count;
        grabacion.Recortar();
        grabacion.alturaCabeza = alturaCabeza;
        string fecha = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        grabacion.name = $"Guardia_{fecha}";
        grabacion.origen = $"Grabada con el visor el {DateTime.Now:yyyy-MM-dd HH:mm}{(string.IsNullOrEmpty(autor) ? "" : $" por {autor}")}";
        Ultima = grabacion;
        Guardar(grabacion);
        Debug.Log($"Guardia grabada: {grabacion.muestras.Count} de {total} muestras tras recortar, {grabacion.Duracion:F2} s de subida, " +
                  $"cabeza a {alturaCabeza:F2} m → {UltimaRuta}", this);
        Pulso(1f, 0.08f);
        yield return new WaitForSecondsRealtime(0.12f);
        Pulso(1f, 0.08f);
        Grabando = false;
    }

    private Transform Cabeza => cabeza != null ? cabeza : rig.centerEyeAnchor;
    private Transform Izquierda => manoIzquierda != null ? manoIzquierda : rig.leftHandAnchor;
    private Transform Derecha => manoDerecha != null ? manoDerecha : rig.rightHandAnchor;
    private Transform Piso => piso != null ? piso : rig != null ? rig.trackingSpace : null;

    // Marco de la cabeza sin inclinación: el que usa el rival para reproducirla
    private GrabacionGuardia.Muestra Muestrear(float t)
    {
        Transform c = Cabeza;
        Vector3 frente = Vector3.ProjectOnPlane(c.forward, Vector3.up);
        if (frente.sqrMagnitude < 0.0001f) frente = Vector3.ProjectOnPlane(c.up, Vector3.up);
        Quaternion inversa = Quaternion.Inverse(Quaternion.LookRotation(frente.normalized, Vector3.up));
        return new GrabacionGuardia.Muestra
        {
            t = t,
            izquierda = inversa * (Izquierda.position - c.position),
            derecha = inversa * (Derecha.position - c.position),
            rotacionIzquierda = inversa * Izquierda.rotation,
            rotacionDerecha = inversa * Derecha.rotation,
        };
    }

    private float AlturaCabeza() => Cabeza.position.y - (Piso != null ? Piso.position.y : 0f);

    private void Guardar(GrabacionGuardia grabacion)
    {
        try
        {
            string carpeta = Path.Combine(Application.persistentDataPath, CarpetaJson);
            Directory.CreateDirectory(carpeta);
            UltimaRuta = Path.Combine(carpeta, grabacion.name + ".json");
            File.WriteAllText(UltimaRuta, JsonUtility.ToJson(grabacion, true));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"{name}: no se pudo guardar la grabación: {e.Message}", this);
        }
#if UNITY_EDITOR
        // Copia como asset: la instancia en memoria sigue siendo la del juego
        Directory.CreateDirectory(CarpetaAssets);
        var copia = Instantiate(grabacion);
        string ruta = AssetDatabase.GenerateUniqueAssetPath($"{CarpetaAssets}/{grabacion.name}.asset");
        AssetDatabase.CreateAsset(copia, ruta);
        AssetDatabase.SaveAssets();
        UltimaRuta = ruta;
#endif
    }

    private void Pulso(float amplitud, float segundos)
    {
        float escala = PlayerPrefs.GetFloat("haptica", 1f);
        Haptica.Fijar(1f, amplitud * escala, OVRInput.Controller.LTouch);
        Haptica.Fijar(1f, amplitud * escala, OVRInput.Controller.RTouch);
        StartCoroutine(Apagar(segundos));
    }

    private static IEnumerator Apagar(float segundos)
    {
        yield return new WaitForSecondsRealtime(segundos);
        Haptica.Apagar(OVRInput.Controller.LTouch);
        Haptica.Apagar(OVRInput.Controller.RTouch);
    }

    [ContextMenu("Probar: grabar guardia")]
    private void ProbarGrabar() => Grabar();
}
