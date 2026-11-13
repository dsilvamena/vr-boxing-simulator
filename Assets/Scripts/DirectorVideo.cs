#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// SOLO PARA EL EDITOR: graba el video de demostración (respaldo de la demo del entregable 6). Juega solo una partida
/// completa desde el menú: los cinco niveles de principio a fin y la hoja de resultados, mostrando las funciones del
/// juego, y la graba desde los ojos del jugador con subtítulos que las explican.
///
/// El jugador es simulado: la cabeza y las manos se colocan cada fotograma (Application.onBeforeRender, después de
/// OVRCameraRig), y los golpes son movimientos reales del guante contra las zonas de golpe, que pasan por la física y
/// por Puno igual que en el visor. Para que cada combate no dure minutos, antes de cada golpe se baja la salud del rival
/// hasta cerca de su siguiente caída (la caída siempre la provoca un golpe visible). Las acciones de prueba que usa
/// (pausa, Guardián, fin del round, pulsaciones de la cuenta) son los mismos menús contextuales de las pruebas.
///
/// Graba en tiempo real (el Editor tiene que estar al frente): hasta 30 fotogramas por segundo con su marca de tiempo,
/// leídos de la GPU sin esperar (AsyncGPUReadback) y codificados en JPG en otro hilo; el audio es la mezcla final del
/// AudioListener (GrabadorAudioVideo). AudioRenderer, que permitiría tiempo fijo, no da muestras en este Editor.
/// Escribe en la carpeta indicada los fotogramas (f_000001.jpg...), lista.txt (duración de cada uno, para ffmpeg),
/// audio.wav y subtitulos.srt; el MP4 se arma con ffmpeg (docs/Video_Demo.md).
/// Uso, en Play desde Main_Scene: DirectorVideo.Empezar("/ruta/carpeta"). No está en ninguna escena ni entra al APK.
/// </summary>
public class DirectorVideo : MonoBehaviour
{
    private const int FpsVideo = 30;
    private const int Ancho = 1280;
    private const int Alto = 720;

    public static DirectorVideo Instancia { get; private set; }
    public bool Terminado { get; private set; }
    public string Paso { get; private set; } = "";
    public int FotogramasVideo => fotogramaVideo;

    private string carpeta;
    private int fotograma, fotogramaVideo;
    private Camera camara;
    private RenderTexture rt, rtResuelto;
    private double t0, proximaCaptura;
    private readonly List<double> marcas = new List<double>();
    private int pendientes;
    private TextMeshProUGUI textoSubtitulo, textoTitulo;
    private GameObject cajaSubtitulo;
    private readonly List<(int fotograma, string texto)> subtitulos = new List<(int, string)>();

    // Escena actual
    private OVRCameraRig rig;
    private Oponente op;
    private ControlRondas rondas;
    private SaludJugador jugador;
    private RecuperacionJugador recuperacion;
    private AtaqueOponente ataque;
    private Collider zonaCabeza, zonaTorso;

    // Jugador simulado
    private enum Pose { Guardia, Abajo, Apuntar }
    private Pose poseIzq = Pose.Abajo, poseDer = Pose.Abajo;
    private Vector3? mirarA, apuntarA;
    private Vector3 esquive, esquiveObjetivo;
    private float yaw, pitch;
    private bool orientado;
    private Vector3 manoIzq, manoDer;
    private bool manosListas;
    private Vector3? golpeIzq, golpeDer;
    private Quaternion giroCabeza = Quaternion.identity, giroYaw = Quaternion.identity;
    private Vector3 cabeza;
    private bool pulsarEnCuenta = true;     // si el jugador cae sin estar previsto, se levanta
    private bool protegerSalud = true;      // si la salud baja mucho sin estar previsto, se repone
    private int ataquesResueltos;
    private ResultadoAtaque ultimoAtaque;
    private float proximaPulsacion;
    private UnityEngine.Events.UnityAction alRender;

    public static DirectorVideo Empezar(string carpeta)
    {
        if (Instancia != null) return Instancia;
        var go = new GameObject("DirectorVideo");
        DontDestroyOnLoad(go);
        var d = go.AddComponent<DirectorVideo>();
        d.carpeta = carpeta;
        return d;
    }

    private void Awake() => Instancia = this;

    private void Start()
    {
        Directory.CreateDirectory(carpeta);
        foreach (string f in Directory.GetFiles(carpeta, "f_*.jpg")) File.Delete(f);
        CrearCamara();
        CrearSubtitulos();
        GrabadorAudioVideo.Abrir(Path.Combine(carpeta, "audio.wav"));
        t0 = Time.realtimeSinceStartupAsDouble;
        SceneManager.sceneLoaded += AlCargarEscena;
        alRender = ColocarJugador;
        Application.onBeforeRender += alRender;
        PrepararEscena();
        StartCoroutine(Capturar());
        StartCoroutine(Guion());
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= AlCargarEscena;
        if (alRender != null) Application.onBeforeRender -= alRender;
        if (!Terminado) Cerrar();
        if (Instancia == this) Instancia = null;
    }

    // ------------------------------------------------------------------ captura

    private void CrearCamara()
    {
        rt = new RenderTexture(Ancho, Alto, 24) { antiAliasing = 4 };
        rtResuelto = new RenderTexture(Ancho, Alto, 0);
        var go = new GameObject("CamaraVideo");
        go.transform.SetParent(transform, false);
        camara = go.AddComponent<Camera>();
        camara.enabled = false;
        camara.targetTexture = rt;
        camara.fieldOfView = 64f;   // ~98° horizontales: dentro de la viñeta (cubre ±56°), como el visor
        camara.nearClipPlane = 0.03f;
        camara.farClipPlane = 1000f;
        camara.stereoTargetEye = StereoTargetEyeMask.None;
        camara.allowHDR = false;
    }

    private void CrearSubtitulos()
    {
        var go = new GameObject("Subtitulos", typeof(Canvas), typeof(CanvasScaler));
        go.transform.SetParent(transform, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camara;
        canvas.planeDistance = 0.05f;
        canvas.sortingOrder = 1000;
        var escala = go.GetComponent<CanvasScaler>();
        escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(Ancho, Alto);

        cajaSubtitulo = new GameObject("Caja", typeof(RectTransform), typeof(Image));
        cajaSubtitulo.transform.SetParent(go.transform, false);
        var caja = (RectTransform)cajaSubtitulo.transform;
        caja.anchorMin = new Vector2(0.08f, 0.04f);
        caja.anchorMax = new Vector2(0.92f, 0.16f);
        caja.offsetMin = caja.offsetMax = Vector2.zero;
        cajaSubtitulo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.62f);
        textoSubtitulo = CrearTexto(caja, 30, TextAlignmentOptions.Center);

        var titulo = new GameObject("Titulo", typeof(RectTransform));
        titulo.transform.SetParent(go.transform, false);
        var rtTitulo = (RectTransform)titulo.transform;
        rtTitulo.anchorMin = new Vector2(0.02f, 0.88f);
        rtTitulo.anchorMax = new Vector2(0.7f, 0.98f);
        rtTitulo.offsetMin = rtTitulo.offsetMax = Vector2.zero;
        textoTitulo = CrearTexto(rtTitulo, 26, TextAlignmentOptions.TopLeft);
        textoTitulo.color = new Color(1f, 0.83f, 0.3f);
        cajaSubtitulo.SetActive(false);
    }

    private static TextMeshProUGUI CrearTexto(RectTransform padre, float tamano, TextAlignmentOptions alineacion)
    {
        var go = new GameObject("Texto", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(padre, false);
        var r = (RectTransform)go.transform;
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(16f, 6f);
        r.offsetMax = new Vector2(-16f, -6f);
        var t = go.GetComponent<TextMeshProUGUI>();
        t.fontSize = tamano;
        t.alignment = alineacion;
        t.color = Color.white;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.outlineWidth = 0.15f;
        t.outlineColor = Color.black;
        return t;
    }

    private IEnumerator Capturar()
    {
        var fin = new WaitForEndOfFrame();
        while (!Terminado)
        {
            yield return fin;
            if (Terminado) break;
            fotograma++;
            double t = Time.realtimeSinceStartupAsDouble - t0;
            if (t < proximaCaptura) continue;
            // Si el Editor va lento se saltan huecos: cada fotograma lleva su marca y ffmpeg respeta las duraciones
            proximaCaptura = Math.Max(proximaCaptura + 1.0 / FpsVideo, t);
            GrabarImagen(t);
        }
    }

    private void GrabarImagen(double t)
    {
        Transform ojo = rig != null ? rig.centerEyeAnchor : Camera.main != null ? Camera.main.transform : null;
        if (ojo == null) return;
        Camera principal = ojo.GetComponent<Camera>();
        if (principal != null)
        {
            camara.clearFlags = principal.clearFlags;
            camara.backgroundColor = principal.backgroundColor;
            camara.cullingMask = principal.cullingMask;
        }
        camara.transform.SetPositionAndRotation(ojo.position, ojo.rotation);
        camara.Render();
        Graphics.Blit(rt, rtResuelto); // resuelve el MSAA antes de leer
        fotogramaVideo++;
        marcas.Add(t);
        string ruta = Path.Combine(carpeta, $"f_{fotogramaVideo:D6}.jpg");
        Interlocked.Increment(ref pendientes);
        AsyncGPUReadback.Request(rtResuelto, 0, TextureFormat.RGBA32, pedido =>
        {
            if (pedido.hasError)
            {
                Interlocked.Decrement(ref pendientes);
                return;
            }
            byte[] datos = pedido.GetData<byte>().ToArray();
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    byte[] jpg = ImageConversion.EncodeArrayToJPG(datos, GraphicsFormat.R8G8B8A8_SRGB, Ancho, Alto, 0, 88);
                    File.WriteAllBytes(ruta, jpg);
                }
                finally { Interlocked.Decrement(ref pendientes); }
            });
        });
    }

    private void Subtitulo(string texto)
    {
        textoSubtitulo.text = texto;
        cajaSubtitulo.SetActive(!string.IsNullOrEmpty(texto));
        double t = Time.realtimeSinceStartupAsDouble - t0;
        subtitulos.Add(((int)(t * 1000), texto));
        Paso = texto;
        Debug.Log($"Video [{t:F1} s]: {texto}");
    }

    private void Titulo(string texto) => textoTitulo.text = texto;

    private void Cerrar()
    {
        Terminado = true;
        GrabadorAudioVideo.Cerrar();
        // Lista para el demuxer concat de ffmpeg: cada fotograma dura hasta el siguiente
        var lista = new StringBuilder();
        for (int i = 0; i < marcas.Count; i++)
        {
            double duracion = i + 1 < marcas.Count ? marcas[i + 1] - marcas[i] : 1.0 / FpsVideo;
            lista.AppendLine($"file 'f_{i + 1:D6}.jpg'").AppendLine(FormattableString.Invariant($"duration {duracion:F4}"));
        }
        if (marcas.Count > 0) lista.AppendLine($"file 'f_{marcas.Count:D6}.jpg'");
        File.WriteAllText(Path.Combine(carpeta, "lista.txt"), lista.ToString());
        var srt = new StringBuilder();
        int n = 0;
        for (int i = 0; i < subtitulos.Count; i++)
        {
            if (string.IsNullOrEmpty(subtitulos[i].texto)) continue;
            int desde = subtitulos[i].fotograma;
            int hasta = i + 1 < subtitulos.Count ? subtitulos[i + 1].fotograma : (int)((Time.realtimeSinceStartupAsDouble - t0) * 1000);
            if (hasta <= desde) continue;
            srt.AppendLine((++n).ToString()).AppendLine($"{Tiempo(desde)} --> {Tiempo(hasta)}").AppendLine(subtitulos[i].texto).AppendLine();
        }
        File.WriteAllText(Path.Combine(carpeta, "subtitulos.srt"), srt.ToString());
        Debug.Log($"Video: {fotogramaVideo} fotogramas en {Time.realtimeSinceStartupAsDouble - t0:F0} s y {GrabadorAudioVideo.Segundos:F0} s de audio, en {carpeta}");
    }

    private static string Tiempo(int ms)
    {
        var t = TimeSpan.FromMilliseconds(ms);
        return $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00},{t.Milliseconds:000}";
    }

    // ------------------------------------------------------------------ escena y jugador simulado

    private void AlCargarEscena(Scene escena, LoadSceneMode modo) => PrepararEscena();

    private void PrepararEscena()
    {
        rig = FindAnyObjectByType<OVRCameraRig>();
        op = FindAnyObjectByType<Oponente>();
        rondas = FindAnyObjectByType<ControlRondas>();
        jugador = FindAnyObjectByType<SaludJugador>();
        recuperacion = FindAnyObjectByType<RecuperacionJugador>();
        ataque = FindAnyObjectByType<AtaqueOponente>();
        zonaCabeza = zonaTorso = null;
        if (op != null)
            foreach (var z in op.GetComponentsInChildren<ZonaGolpe>())
            {
                if (z.Zona == TipoZona.Cabeza) zonaCabeza = z.GetComponent<Collider>();
                else zonaTorso = z.GetComponent<Collider>();
            }
        if (rig != null) rig.enabled = false;
        var oyente = FindAnyObjectByType<AudioListener>();
        if (oyente != null && oyente.GetComponent<GrabadorAudioVideo>() == null) oyente.gameObject.AddComponent<GrabadorAudioVideo>();
        foreach (var l in FindObjectsByType<LimiteRing>()) l.enabled = false;
        foreach (var g in FindObjectsByType<PausaGuardian>())
        {
            Campo(g, "sinFoco", false);
            g.enabled = false;
        }
        if (jugador != null) jugador.AlResolverAtaque += AlResolverAtaque;
        if (op != null)
        {
            op.AlRecibirGolpe += g => Subtitulo($"{NombreGolpe(g.tipo)} {(g.zona != null && g.zona.Zona == TipoZona.Torso ? "al cuerpo" : "a la cabeza")}: conecta");
            op.AlBloquear += g => Subtitulo("El rival bloquea el golpe con la guardia");
            op.AlEsquivar += g => Subtitulo("El rival esquiva el golpe (vibración suave de roce)");
        }
        orientado = false;
        manosListas = false;
        esquive = esquiveObjetivo = Vector3.zero;
        golpeIzq = golpeDer = null;
        mirarA = apuntarA = null;
    }

    private void AlResolverAtaque(ResultadoAtaque r)
    {
        ataquesResueltos++;
        ultimoAtaque = r;
    }

    private static string NombreGolpe(TipoGolpe t) => t switch
    {
        TipoGolpe.Jab => "Jab",
        TipoGolpe.Cross => "Cross",
        TipoGolpe.Uppercut => "Uppercut",
        _ => "Gancho",
    };

    private void ColocarJugador()
    {
        if (rig == null || Terminado) return;
        float dt = Mathf.Clamp(Time.unscaledDeltaTime, 1f / 120f, 0.1f);
        Transform ts = rig.trackingSpace;
        Vector3 baseCabeza = ts.TransformPoint(new Vector3(0f, 1.65f, 0f));
        Vector3 objetivo = mirarA ?? (op != null ? op.transform.position + Vector3.up * 1.6f : baseCabeza + ts.forward * 2f);
        Vector3 d = objetivo - baseCabeza;
        float yawObjetivo = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        float pitchObjetivo = Mathf.Clamp(-Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg, -30f, 35f);
        if (!orientado)
        {
            yaw = yawObjetivo;
            pitch = pitchObjetivo;
            orientado = true;
        }
        float k = 1f - Mathf.Exp(-5f * dt);
        yaw = Mathf.LerpAngle(yaw, yawObjetivo, k);
        pitch = Mathf.Lerp(pitch, pitchObjetivo, k);
        giroYaw = Quaternion.Euler(0f, yaw, 0f);

        float t = Time.time;
        Vector3 balanceo = new Vector3(Mathf.Sin(t * 1.3f) * 0.012f, Mathf.Sin(t * 2.1f) * 0.008f, 0f);
        esquive = Vector3.Lerp(esquive, esquiveObjetivo, 1f - Mathf.Exp(-16f * dt));
        cabeza = baseCabeza + giroYaw * (balanceo + esquive);
        giroCabeza = giroYaw * Quaternion.Euler(pitch, 0f, Mathf.Sin(t * 0.9f) * 1.2f - esquive.x * 25f);
        rig.centerEyeAnchor.SetPositionAndRotation(cabeza, giroCabeza);

        Vector3 objIzq = golpeIzq ?? PosicionPose(poseIzq, false);
        Vector3 objDer = golpeDer ?? PosicionPose(poseDer, true);
        if (!manosListas)
        {
            manoIzq = objIzq;
            manoDer = objDer;
            manosListas = true;
        }
        float km = 1f - Mathf.Exp(-14f * dt);
        manoIzq = golpeIzq ?? Vector3.Lerp(manoIzq, objIzq, km);
        manoDer = golpeDer ?? Vector3.Lerp(manoDer, objDer, km);
        Quaternion giroMano = giroYaw * Quaternion.Euler(10f, 0f, 0f);
        rig.leftHandAnchor.SetPositionAndRotation(manoIzq, giroMano);
        Quaternion giroDer = poseDer == Pose.Apuntar && apuntarA.HasValue && golpeDer == null
            ? Quaternion.LookRotation(apuntarA.Value - manoDer) : giroMano;
        rig.rightHandAnchor.SetPositionAndRotation(manoDer, giroDer);
    }

    private Vector3 PosicionPose(Pose pose, bool derecha)
    {
        float lado = derecha ? 1f : -1f;
        Vector3 local = pose switch
        {
            // Más baja y abierta que una guardia cerrada para que se vea el ring; sigue dentro de lo que bloquea (< 0,4 m)
            Pose.Guardia => new Vector3(0.16f * lado, -0.21f, 0.27f),
            Pose.Apuntar => new Vector3(0.18f * lado, -0.28f, 0.3f),
            _ => new Vector3(0.22f * lado, -0.5f, 0.22f),
        };
        return cabeza + giroYaw * local;
    }

    private void Update()
    {
        if (Terminado || recuperacion == null || jugador == null) return;
        // Red de seguridad: el guion decide cuándo cae el jugador; si cae sin estar previsto, se levanta pulsando
        if (pulsarEnCuenta && recuperacion.Contando && Time.time >= proximaPulsacion)
        {
            proximaPulsacion = Time.time + 0.12f;
            Llamar(recuperacion, "ProbarPulsacion");
        }
        if (protegerSalud && !jugador.Derribado && jugador.Salud > 0f && jugador.SaludNormalizada < 0.35f)
            jugador.EstablecerSalud(jugador.SaludMaxima * 0.6f);
    }

    // ------------------------------------------------------------------ acciones

    private static object Campo(object o, string nombre, object valor = null)
    {
        FieldInfo f = o.GetType().GetField(nombre, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
        if (f == null) return null;
        if (valor != null) f.SetValue(o, valor);
        return f.GetValue(o);
    }

    private static void Llamar(object o, string metodo)
    {
        MethodInfo m = o.GetType().GetMethod(metodo, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
        if (m != null) m.Invoke(o, null);
        else Debug.LogWarning($"Video: {o.GetType().Name} no tiene {metodo}");
    }

    // En tiempo real: la pausa del juego (timeScale 0) no la detiene
    private static WaitForSecondsRealtime Esperar(float s) => new WaitForSecondsRealtime(s);

    private IEnumerator Hasta(Func<bool> condicion, float maximo, string que)
    {
        float fin = Time.time + maximo;
        while (!condicion())
        {
            if (Time.time > fin)
            {
                Debug.LogWarning($"Video: se agotó la espera de «{que}» ({maximo:F0} s)");
                yield break;
            }
            yield return null;
        }
    }

    private IEnumerator EsperarEscena(string nombre) =>
        Hasta(() => SceneManager.GetActiveScene().name == nombre && (nombre == "Main_Scene" || nombre == "Scene_Results" || op != null), 60f, "escena " + nombre);

    private Button Boton(string nombre)
    {
        foreach (var b in FindObjectsByType<Button>())
            if (b.name == nombre && b.gameObject.activeInHierarchy) return b;
        return null;
    }

    private IEnumerator Pulsar(string nombre)
    {
        Button b = Boton(nombre);
        if (b == null)
        {
            Debug.LogWarning($"Video: no hay botón {nombre}");
            yield break;
        }
        apuntarA = b.transform.position;
        poseDer = Pose.Apuntar;
        yield return Esperar(1.2f);
        b.onClick.Invoke();
        yield return Esperar(0.4f);
    }

    private Vector3 CentroCanvas()
    {
        foreach (var c in FindObjectsByType<Canvas>())
            if (c.renderMode == RenderMode.WorldSpace && c.gameObject.activeInHierarchy && c.GetComponentInChildren<Button>() != null)
                return c.transform.position;
        return cabeza + giroYaw * Vector3.forward * 2f;
    }

    private IEnumerator EmpezarPelea(string titulo, string presentacion)
    {
        yield return Esperar(0.5f);
        var pausa = FindAnyObjectByType<PauseMenu>();
        if (pausa != null && pausa.IsOpen) pausa.Hide();
        Titulo(titulo);
        Subtitulo(presentacion);
        poseIzq = poseDer = Pose.Guardia;
        mirarA = null;
        apuntarA = null;
        yield return Hasta(() => rondas == null || rondas.Estado == EstadoRondas.Round, 15f, "campana");
        Subtitulo("Suena la campana: empieza el round. El rival se acerca dando pasos");
        yield return Hasta(() => op == null || PlanoXZ(op.transform.position - cabeza).magnitude < 1.4f, 15f, "rival cerca");
    }

    private static Vector3 PlanoXZ(Vector3 v) => new Vector3(v.x, 0f, v.z);

    private IEnumerator EsperarAtaque(float maximo = 15f)
    {
        int antes = ataquesResueltos;
        yield return Hasta(() => ataquesResueltos > antes, maximo, "ataque del rival");
    }

    private IEnumerator Bloquear(string texto)
    {
        poseIzq = poseDer = Pose.Guardia;
        Subtitulo(texto);
        yield return EsperarAtaque();
        if (ultimoAtaque == ResultadoAtaque.Bloqueado) Subtitulo("Bloqueado: los guantes delante de la cara reducen el daño (vibración doble)");
        yield return Esperar(0.8f);
    }

    private IEnumerator Esquivar()
    {
        poseIzq = poseDer = Pose.Abajo;
        Subtitulo("Esquive: apartar la cabeza de la línea del golpe");
        yield return Hasta(() => ataque == null || ataque.Atacando, 15f, "inicio del ataque");
        esquiveObjetivo = new Vector3(0.42f, -0.1f, -0.05f);
        yield return EsperarAtaque(4f);
        if (ultimoAtaque == ResultadoAtaque.Esquivado) Subtitulo("Esquivado: el golpe pasa sin hacer daño");
        yield return Esperar(0.6f);
        esquiveObjetivo = Vector3.zero;
        poseIzq = poseDer = Pose.Guardia;
        yield return Esperar(0.6f);
    }

    private IEnumerator RecibirGolpes(int cuantos, string texto)
    {
        poseIzq = poseDer = Pose.Abajo;
        Subtitulo(texto);
        for (int i = 0; i < cuantos; i++)
        {
            yield return EsperarAtaque();
            if (ultimoAtaque == ResultadoAtaque.Conectado)
                Subtitulo("Golpe recibido: la vista se oscurece por los bordes y, con poca salud, se oye el latido");
        }
        poseIzq = poseDer = Pose.Guardia;
    }

    private enum Tipo { Jab, Cross, Uppercut, Gancho }

    // Un golpe del guante contra la zona, con trayectoria de mínimo jerk (como un brazo de verdad)
    private IEnumerator Golpear(bool derecha, Tipo tipo, bool torso = false)
    {
        Collider zona = torso && zonaTorso != null ? zonaTorso : zonaCabeza;
        if (zona == null) yield break;
        Vector3 lateral = giroYaw * Vector3.right * (derecha ? 1f : -1f);
        Vector3 adelante = giroYaw * Vector3.forward;
        // El golpe sale de la guardia alta, a la altura de la barbilla, como en el boxeo
        Vector3 desde = derecha ? manoDer : manoIzq;
        Vector3 alta = cabeza + giroYaw * new Vector3((derecha ? 1f : -1f) * 0.11f, -0.11f, 0.24f);
        yield return Mover(derecha, desde, alta, (desde + alta) * 0.5f, 2.5f);
        Vector3 inicio = alta;
        // Gancho: termina en el costado de la cabeza viniendo de fuera; uppercut: sube casi vertical hasta la barbilla
        Vector3 Fin() => tipo == Tipo.Gancho ? zona.bounds.center - lateral * 0.25f
                       : tipo == Tipo.Uppercut ? zona.bounds.center - adelante * 0.05f + Vector3.up * 0.05f
                       : zona.bounds.center + adelante * 0.12f;
        if (tipo == Tipo.Uppercut)
        {
            // Primero baja la mano por delante del pecho del rival, sin tocarlo, y después sube hacia la barbilla
            Vector3 abajo = zona.bounds.center - adelante * 0.32f - Vector3.up * 0.38f;
            yield return Mover(derecha, inicio, abajo, (inicio + abajo) * 0.5f - adelante * 0.1f, 2.4f);
            inicio = abajo;
        }
        float rapidez = tipo == Tipo.Jab ? 4.5f : tipo == Tipo.Cross ? 5.5f : 4.5f;
        float largo = Vector3.Distance(inicio, Fin());
        float duracion = Mathf.Max(0.12f, 1.875f * largo / rapidez);
        float t0 = Time.time;
        while (Time.time - t0 < duracion)
        {
            float s = (Time.time - t0) / duracion;
            float m = s * s * s * (10f - 15f * s + 6f * s * s);
            Vector3 fin = Fin();
            Vector3 c = tipo == Tipo.Gancho ? zona.bounds.center + lateral * 1.0f - adelante * 0.35f
                       : tipo == Tipo.Uppercut ? (inicio + fin) * 0.5f - adelante * 0.03f : (inicio + fin) * 0.5f;
            Vector3 p = Bezier(inicio, c, fin, m);
            if (derecha) golpeDer = p; else golpeIzq = p;
            yield return null;
        }
        Vector3 llegada = derecha ? golpeDer ?? Fin() : golpeIzq ?? Fin();
        yield return Mover(derecha, llegada, PosicionPose(Pose.Guardia, derecha), llegada - adelante * 0.2f, 2.5f);
        if (derecha) golpeDer = null; else golpeIzq = null;
    }

    private IEnumerator Mover(bool derecha, Vector3 a, Vector3 b, Vector3 c, float rapidez)
    {
        float duracion = Mathf.Max(0.1f, 1.875f * Vector3.Distance(a, b) / rapidez);
        float t0 = Time.time;
        while (Time.time - t0 < duracion)
        {
            float s = (Time.time - t0) / duracion;
            float m = s * s * s * (10f - 15f * s + 6f * s * s);
            Vector3 p = Bezier(a, c, b, m);
            if (derecha) golpeDer = p; else golpeIzq = p;
            yield return null;
        }
    }

    private static Vector3 Bezier(Vector3 a, Vector3 c, Vector3 b, float t) =>
        (1 - t) * (1 - t) * a + 2 * (1 - t) * t * c + t * t * b;

    private bool PuedeRecibir => op != null && !op.Caido && !op.Derrotado && !op.Protegido && !op.Invulnerable;

    // Golpea en series variadas hasta que el rival cae una vez más. Acerca su salud a la siguiente caída para que
    // basten pocos golpes (la caída la da siempre un golpe que se ve)
    private IEnumerator GolpearHastaCaida(string texto, int golpesSinAcelerar = 0)
    {
        if (op == null) yield break;
        int caidas = op.Caidas;
        Subtitulo(texto);
        var serie = new (bool, Tipo, bool)[]
        {
            (false, Tipo.Jab, false), (true, Tipo.Cross, false), (false, Tipo.Gancho, false), (true, Tipo.Uppercut, false),
            (true, Tipo.Cross, true), (false, Tipo.Jab, false), (true, Tipo.Gancho, false), (false, Tipo.Uppercut, false),
        };
        float fin = Time.time + 60f;
        int i = 0;
        while (op != null && op.Caidas == caidas && Time.time < fin)
        {
            yield return Hasta(() => op == null || PuedeRecibir || op.Caidas != caidas, 20f, "rival golpeable");
            if (op == null || op.Caidas != caidas) break;
            if (i >= golpesSinAcelerar)
            {
                float umbral = op.Perfil.saludMaxima * (1f - (op.Caidas + 1f) / op.Perfil.caidasParaDerrota);
                float margen = Mathf.Max(4f, op.Perfil.saludMaxima * 0.05f);
                if (op.Salud - umbral > margen) op.EstablecerSalud(umbral + margen);
            }
            var (derecha, tipo, torso) = serie[i++ % serie.Length];
            yield return Golpear(derecha, tipo, torso);
            yield return Esperar(0.25f);
        }
        if (op != null && op.Caidas == caidas)
        {
            Debug.LogWarning("Video: el rival no cayó a golpes; se le tumba");
            op.Derribar();
        }
    }

    private IEnumerator CuentaDelRival()
    {
        yield return Esperar(0.8f);
        if (op == null || op.Derrotado) yield break;
        Subtitulo("Knockdown: el réferi va hacia el rival y cuenta en voz alta cuando ya está en la lona");
        // El jugador baja los guantes y mira al rival en la lona (sin moverse: detrás suele haber cuerdas)
        Vector3 rival = op.transform.position;
        poseIzq = poseDer = Pose.Abajo;
        mirarA = rival + Vector3.up * 0.4f;
        yield return Hasta(() => op == null || !op.Caido, 25f, "fin de la cuenta del rival");
        Subtitulo("El rival se levanta al terminar la cuenta");
        mirarA = null;
        poseIzq = poseDer = Pose.Guardia;
        yield return Esperar(0.8f);
        esquiveObjetivo = Vector3.zero;
        yield return Esperar(0.8f);
    }

    private IEnumerator Resultados(string boton, string texto, float segundos = 5f)
    {
        yield return EsperarEscena("Scene_Results");
        yield return Esperar(0.6f);
        mirarA = CentroCanvas();
        poseIzq = Pose.Abajo;
        poseDer = Pose.Abajo;
        Titulo("Resultados");
        Subtitulo(texto);
        yield return Esperar(segundos);
        yield return Pulsar(boton);
    }

    private IEnumerator CaidaDelJugador(bool levantarse)
    {
        protegerSalud = false;
        pulsarEnCuenta = false;
        jugador.EstablecerSalud(jugador.SaludMaxima * 0.15f);
        yield return RecibirGolpes(1, "Con poca salud la vista parpadea y el latido se acelera");
        yield return Hasta(() => jugador.Derribado, 15f, "caída del jugador");
        Subtitulo("¡Al suelo! El réferi se pone delante y cuenta; el cartel indica qué hacer");
        var referi = FindAnyObjectByType<CuerpoArbitro>();
        yield return Esperar(1f);
        if (referi != null) mirarA = referi.transform.position + Vector3.up * 1.7f;
        yield return Esperar(2f);
        if (levantarse)
        {
            Subtitulo("Button mashing: pulsar A, B, X o Y rápido para levantarse (cada pulsación vibra más)");
            pulsarEnCuenta = true;
            yield return Hasta(() => !recuperacion.Contando, 15f, "levantarse");
            Subtitulo("De pie: la vista se aclara y el combate sigue");
            mirarA = null;
            poseIzq = poseDer = Pose.Guardia;
            protegerSalud = true;
            yield return Esperar(2f);
        }
        else
        {
            Subtitulo("Sin pulsar, la cuenta llega a 10: derrota por K.O.");
            mirarA = null;
            yield return Hasta(() => SceneManager.GetActiveScene().name == "Scene_Results", 30f, "derrota");
            pulsarEnCuenta = true;
            protegerSalud = true;
        }
    }

    // ------------------------------------------------------------------ guion

    private IEnumerator Guion()
    {
        int nivelAntes = PlayerPrefs.GetInt("nivelSuperado", 0);
        yield return Esperar(0.5f);

        // Menú
        Titulo("Simulador de Boxeo RV");
        Subtitulo("Menú principal dentro de la arena: se elige con el láser del mando derecho y el gatillo");
        mirarA = CentroCanvas();
        poseDer = Pose.Apuntar;
        apuntarA = mirarA;
        yield return Esperar(3f);
        yield return Pulsar("ButtonOptions");
        Subtitulo("Opciones: volumen y vibración, que se guardan");
        yield return Esperar(3f);
        yield return Pulsar("ButtonAtras");
        yield return Pulsar("ButtonPlayer");
        Subtitulo("Cinco niveles: cada uno se desbloquea al ganar el anterior");
        yield return Esperar(3f);
        yield return Pulsar("ButtonNivel1");

        // Nivel 1: defensa, los cuatro tipos de golpe y las cuentas
        yield return EsperarEscena("Scene_Ring_Level1");
        yield return EmpezarPelea("Nivel 1: Glass Joe", "Nivel 1: Glass Joe. Combate de 3 rounds de 90 s, sin HUD");
        yield return Bloquear("Guardia arriba ante el ataque del rival");
        yield return Esquivar();
        yield return GolpearHastaCaida("Golpes reales con los mandos: jab, cross, gancho y uppercut, cada uno con su vibración", 4);
        yield return CuentaDelRival();
        yield return GolpearHastaCaida("Cada golpe que conecta resta salud al rival según su tipo y rapidez");
        yield return CuentaDelRival();
        yield return GolpearHastaCaida("Tercera caída: K.O.");
        Subtitulo("¡Victoria por K.O.!");
        yield return Resultados("BotonPrincipal", "Resultados de cada combate: precisión, golpes bloqueados y tiempo total");

        // Nivel 2: barra de vida de prueba, daño, derrota y reintento
        yield return EsperarEscena("Scene_Ring_Level2");
        yield return EmpezarPelea("Nivel 2: Bald Bull", "Nivel 2: Bald Bull, ataques directos de fuerza");
        var barra = FindAnyObjectByType<DebugSalud>();
        GameObject panel = barra != null ? Campo(barra, "panel") as GameObject : null;
        if (panel != null)
        {
            panel.SetActive(true);
            Subtitulo("Barra de vida de prueba (botón Y): oculta por defecto, el juego no usa HUD");
            mirarA = panel.transform.position;
            yield return Esperar(3f);
            mirarA = null;
        }
        yield return RecibirGolpes(2, "Sin guardia, los golpes del rival entran");
        if (panel != null) panel.SetActive(false);
        yield return CaidaDelJugador(levantarse: false);
        yield return Resultados("BotonPrincipal", "Derrota: la hoja ofrece REINTENTAR el mismo nivel");
        yield return EsperarEscena("Scene_Ring_Level2");
        yield return EmpezarPelea("Nivel 2: Bald Bull (reintento)", "Reintento contra Bald Bull");
        yield return Bloquear("Esta vez, guardia arriba");
        yield return GolpearHastaCaida("Contraataque");
        yield return CuentaDelRival();
        yield return GolpearHastaCaida("Segunda caída");
        yield return CuentaDelRival();
        yield return GolpearHastaCaida("K.O.");
        yield return Resultados("BotonPrincipal", "Victoria: SIGUIENTE NIVEL");

        // Nivel 3: combos, pausa, Guardián y victoria por salud
        yield return EsperarEscena("Scene_Ring_Level3");
        yield return EmpezarPelea("Nivel 3: Soda Popinski", "Nivel 3: Soda Popinski, combinaciones rápidas de ganchos");
        yield return Bloquear("Combo de tres ganchos contra la guardia");
        yield return Hasta(() => ataque == null || !ataque.Atacando, 6f, "fin del combo");
        var pausa = FindAnyObjectByType<PauseMenu>();
        if (pausa != null)
        {
            Subtitulo("Botón Menú: pausa el combate");
            pausa.Show();
            mirarA = CentroCanvas();
            yield return Esperar(3f);
            if (Boton("ButtonReanudar") != null) yield return Pulsar("ButtonReanudar"); else pausa.Hide();
            mirarA = null;
            poseIzq = poseDer = Pose.Guardia;
            var guardian = FindAnyObjectByType<PausaGuardian>(FindObjectsInactive.Include);
            if (guardian != null)
            {
                Campo(guardian, "sinFoco", false);
                guardian.enabled = true;
                esquiveObjetivo = new Vector3(-0.5f, 0f, 0f);
                Llamar(guardian, "ProbarSalir");
                Subtitulo("Fuera del área segura del Guardián: el juego se pausa y avisa");
                yield return Esperar(0.3f);
                mirarA = CentroCanvas();
                yield return Esperar(3.5f);
                esquiveObjetivo = Vector3.zero;
                Llamar(guardian, "ProbarVolver");
                mirarA = null;
                Subtitulo("De vuelta en el área segura, el combate sigue solo");
                yield return Esperar(2f);
                guardian.enabled = false;
            }
        }
        yield return GolpearHastaCaida("Contraataque entre combos");
        yield return CuentaDelRival();
        if (rondas != null)
        {
            Subtitulo("Si nadie cae por K.O., al final del tercer round gana quien tenga más salud");
            yield return Esperar(1.5f);
            Llamar(rondas, "ProbarGanarPorSalud");
        }
        yield return Resultados("BotonPrincipal", "Victoria por salud al terminar el último round");

        // Nivel 4: esquive del rival, cambio de round y levantarse
        yield return EsperarEscena("Scene_Ring_Level4");
        yield return EmpezarPelea("Nivel 4: Mr. Sandman", "Nivel 4: Mr. Sandman, alta velocidad y esquive activo");
        yield return GolpearHastaCaida("Mr. Sandman esquiva y bloquea; cuando bloquea queda abierto");
        yield return CuentaDelRival();
        if (rondas != null)
        {
            Llamar(rondas, "ProbarAcabarRound");
            Subtitulo("Fin del round: tres campanadas");
            yield return Hasta(() => rondas.Estado == EstadoRondas.Descanso, 6f, "descanso");
            Subtitulo("Descanso de 20 s: el rival vuelve a su esquina y el jugador recupera salud");
            yield return Hasta(() => rondas.Estado == EstadoRondas.Round, 30f, "round 2");
            Subtitulo("Round 2: una campanada");
            yield return Hasta(() => op == null || PlanoXZ(op.transform.position - cabeza).magnitude < 1.4f, 15f, "rival cerca");
        }
        yield return CaidaDelJugador(levantarse: true);
        yield return GolpearHastaCaida("Segunda caída de Mr. Sandman");
        yield return CuentaDelRival();
        yield return GolpearHastaCaida("K.O.");
        yield return Resultados("BotonPrincipal", "Siguiente: el campeón");

        // Nivel 5: Tyson y el cinturón
        yield return EsperarEscena("Scene_Ring_Level5");
        yield return EmpezarPelea("Nivel 5: Iron Mike Tyson", "Nivel 5: Iron Mike Tyson, el campeón invicto");
        yield return Bloquear("Combo de cuatro golpes de Tyson contra la guardia");
        yield return Hasta(() => ataque == null || !ataque.Atacando, 6f, "fin del combo");
        yield return GolpearHastaCaida("Hay que pegar entre sus combos");
        yield return CuentaDelRival();
        yield return GolpearHastaCaida("Segunda caída");
        yield return CuentaDelRival();
        yield return GolpearHastaCaida("K.O. al campeón");
        Subtitulo("¡Eres el nuevo campeón!");
        yield return EsperarEscena("Scene_Results");
        yield return Esperar(1.5f);
        var cinturon = FindAnyObjectByType<CinturonCampeon>();
        if (cinturon != null)
        {
            mirarA = cinturon.transform.position;
            Subtitulo("El cinturón de campeón regional (RF-19) y el texto de cierre de la historia");
            yield return Esperar(5f);
        }
        yield return Resultados("BotonMenu", "Fin del recorrido: vuelta al menú", 4f);
        yield return EsperarEscena("Main_Scene");
        mirarA = CentroCanvas();
        Subtitulo("Simulador de Boxeo RV: cinco rivales, sin HUD, háptica y audio 3D");
        yield return Esperar(4f);

        PlayerPrefs.SetInt("nivelSuperado", nivelAntes);
        PlayerPrefs.Save();
        yield return Hasta(() => Volatile.Read(ref pendientes) == 0, 10f, "fotogramas pendientes");
        Cerrar();
    }
}
#endif
