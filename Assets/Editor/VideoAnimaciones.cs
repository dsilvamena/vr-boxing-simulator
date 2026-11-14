using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Video para revisar las animaciones de los personajes, con un rótulo en cada toma. Se graba en dos partes, como
/// fotogramas JPG a 30 fps en Builds/Video/animaciones/ (luego se unen con ffmpeg, ver docs/Video_Demo.md):
/// 1. GrabarClips() (fuera de Play): en una escena neutra, cada estado del controlador de cada rival (su clip, resuelto
///    a través del override), las poses del menú y los clips del réferi, muestreados uno a uno. Con ApoyoEnSuelo se sube
///    la cadera como en el juego. Al acabar vuelve a abrir Main_Scene.
/// 2. GrabarEnJuego() (en Play, con Main_Scene abierta): los cinco rivales del menú vistos desde los ojos del jugador
///    (pose y mirada de CuerpoVivo) y la pelea de Bald Bull: su mirada, el réferi yendo al centro, desplazándose durante
///    el round y caminando a contar un knockdown (Arbitro, "Probar: contar al rival"). La salud del jugador se rellena
///    para que no lo tumben. Usa Time.captureFramerate, así que el tiempo del juego va a 30 fps aunque el Editor no.
/// Solo para el Editor; no está en ninguna escena.
/// </summary>
public static class VideoAnimaciones
{
    private const int Ancho = 1280, Alto = 720, Fps = 30;
    private static string Raiz => Path.GetFullPath("Builds/Video/animaciones");

    public static string Estado { get; private set; } = "sin empezar";
    public static bool Terminado { get; private set; }

    private static RenderTexture rt;
    private static Texture2D lectura;
    private static Camera camara;
    private static TextMeshPro rotulo;
    private static string carpeta;
    private static int numero;

    // ---------- Comunes ----------

    private static void Preparar(string subcarpeta)
    {
        carpeta = Path.Combine(Raiz, subcarpeta);
        if (Directory.Exists(carpeta)) Directory.Delete(carpeta, true);
        Directory.CreateDirectory(carpeta);
        numero = 0;
        Terminado = false;
        // Sin HideAndDontSave, abrir otra escena las libera
        if (rt == null) rt = new RenderTexture(Ancho, Alto, 24) { antiAliasing = 4, name = "VideoAnimaciones", hideFlags = HideFlags.HideAndDontSave };
        if (lectura == null) lectura = new Texture2D(Ancho, Alto, TextureFormat.RGB24, false) { hideFlags = HideFlags.HideAndDontSave };
    }

    private static void CrearCamara(Color fondo, bool cielo)
    {
        var go = new GameObject("CamaraVideoAnimaciones");
        camara = go.AddComponent<Camera>();
        camara.enabled = false;
        camara.targetTexture = rt;
        camara.clearFlags = cielo ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
        camara.backgroundColor = fondo;
        camara.nearClipPlane = 0.05f;
        camara.farClipPlane = 200f;
        camara.fieldOfView = 35f;

        // Rótulo: texto con una franja oscura detrás, colgado de la cámara (abajo a la izquierda)
        var franja = GameObject.CreatePrimitive(PrimitiveType.Quad);
        UnityEngine.Object.DestroyImmediate(franja.GetComponent<Collider>());
        franja.name = "Franja";
        franja.transform.SetParent(go.transform, false);
        var material = new Material(Shader.Find("Sprites/Default")) { color = new Color(0.03f, 0.03f, 0.05f, 0.82f) };
        franja.GetComponent<MeshRenderer>().sharedMaterial = material;

        var texto = new GameObject("Rotulo");
        texto.transform.SetParent(go.transform, false);
        rotulo = texto.AddComponent<TextMeshPro>();
        var fuente = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/Fonts/Bangers SDF.asset");
        if (fuente != null) rotulo.font = fuente;
        rotulo.alignment = TextAlignmentOptions.BottomLeft;
        rotulo.color = Color.white;
        rotulo.richText = true;
        rotulo.enableWordWrapping = false;
        ColocarRotulo(franja.transform);
    }

    // El rótulo ocupa la franja inferior de la imagen sea cual sea el campo de visión
    private static void ColocarRotulo(Transform franja = null)
    {
        if (franja == null) franja = camara.transform.Find("Franja");
        float d = 1f;
        float alto = 2f * d * Mathf.Tan(camara.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float ancho = alto * Ancho / Alto;
        franja.localPosition = new Vector3(0f, -alto * 0.5f + alto * 0.075f, d + 0.001f);
        franja.localScale = new Vector3(ancho, alto * 0.15f, 1f);
        var rect = rotulo.rectTransform;
        rect.sizeDelta = new Vector2(ancho * 0.94f, alto * 0.13f);
        rect.localPosition = new Vector3(0f, -alto * 0.5f + alto * 0.075f, d);
        rotulo.fontSize = alto * 0.48f;
    }

    private static void Rotular(string titulo, string detalle)
    {
        rotulo.text = $"<size=120%>{titulo}</size>   <color=#FFD54A>{detalle}</color>";
        rotulo.ForceMeshUpdate();
    }

    private static void Fotografiar(int copias = 1)
    {
        camara.Render();
        var previa = RenderTexture.active;
        RenderTexture.active = rt;
        lectura.ReadPixels(new Rect(0, 0, Ancho, Alto), 0, 0);
        lectura.Apply(false);
        RenderTexture.active = previa;
        byte[] jpg = lectura.EncodeToJPG(90);
        for (int i = 0; i < copias; i++) File.WriteAllBytes(Path.Combine(carpeta, $"f_{++numero:D6}.jpg"), jpg);
    }

    // ---------- Parte 1: clips ----------

    private class Toma
    {
        public string titulo, detalle;
        public GameObject personaje;
        public AnimationClip clip;
        public int fotogramas;
        public bool bucle;
        public Vector3 direccionCamara;   // desde el personaje, en su espacio
    }

    private static readonly string[] Orden =
    {
        "Idle", "Jab", "Uppercut_Face", "Uppercut_Stomach", "Cabeza_Derecha", "Cabeza_Izquierda", "Cuerpo_Derecha",
        "Cuerpo_Izquierda", "Ataque_Jab", "Ataque_Uppercut", "Ataque_Gancho", "Ataque_Gancho_2", "Burla", "Victoria",
        "Knockdown", "Defeat",
    };

    private const string Personajes = "Assets/_Project/Art/Characters/";
    private static readonly Vector3 TresCuartos = new Vector3(0.5f, 0.12f, 1f);
    // Los rivales están en guardia de perfil (la cadera mira a su derecha de hacia donde miran): la cámara va a su izquierda
    private static readonly Vector3 TresCuartosRival = new Vector3(-0.45f, 0.12f, 1f);

    [MenuItem("Tools/Boxeo/Video de animaciones/1. Grabar clips")]
    public static void GrabarClips()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("Fuera de Play."); return; }
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) { Debug.LogError("Guarda o descarta los cambios de las escenas abiertas antes."); return; }

        Preparar("1_clips");
        frentes.Clear();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var luz = new GameObject("Luz").AddComponent<Light>();
        luz.type = LightType.Directional;
        luz.intensity = 1.15f;
        luz.color = new Color(1f, 0.96f, 0.9f);
        luz.shadows = LightShadows.Soft;
        luz.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.55f, 0.58f, 0.66f);
        RenderSettings.ambientEquatorColor = new Color(0.4f, 0.4f, 0.42f);
        RenderSettings.ambientGroundColor = new Color(0.25f, 0.22f, 0.2f);
        var suelo = GameObject.CreatePrimitive(PrimitiveType.Plane);
        suelo.transform.localScale = Vector3.one * 30f;
        suelo.GetComponent<MeshRenderer>().sharedMaterial = new Material(Shader.Find("Standard")) { color = new Color(0.62f, 0.63f, 0.66f) };
        CrearCamara(new Color(0.12f, 0.13f, 0.16f), false);

        var tomas = new List<Toma>();
        string gj = Personajes + "GlassJoe/";
        AnadirRival(tomas, "Glass Joe", gj + "GlassJoe.fbx", gj + "Animations/GlassJoe.controller", 1f, null);
        AnadirRival(tomas, "Bald Bull", Personajes + "BaldBull/BaldBull.prefab", null, 1.15f, "Menu_BaldBull");
        AnadirRival(tomas, "Soda Popinski", Personajes + "SodaPopinski/SodaPopinski.prefab", null, 0.8f, null);
        AnadirRival(tomas, "Mr. Sandman", Personajes + "MrSandman/MrSandman.prefab", null, 1f, "Menu_MrSandman");
        AnadirRival(tomas, "Iron Mike Tyson", Personajes + "MikeTyson/MikeTyson.prefab", null, 1f, "Menu_MikeTyson");
        AnadirReferi(tomas);

        int toma = 0, fotograma = 0;
        GameObject visible = null;
        EditorApplication.CallbackFunction paso = null;
        paso = () =>
        {
            try
            {
                for (int n = 0; n < 6; n++)
                {
                    if (toma >= tomas.Count)
                    {
                        EditorApplication.update -= paso;
                        Estado = $"clips terminados: {numero} fotogramas ({numero / (float)Fps:F0} s) en {carpeta}";
                        EditorSceneManager.OpenScene("Assets/Scenes/Main_Scene.unity", OpenSceneMode.Single);
                        Terminado = true;
                        Debug.Log("VideoAnimaciones: " + Estado);
                        return;
                    }
                    Toma t = tomas[toma];
                    if (fotograma == 0)
                    {
                        if (visible != t.personaje)
                        {
                            foreach (var o in tomas.Select(x => x.personaje).Distinct()) o.SetActive(o == t.personaje);
                            visible = t.personaje;
                        }
                        Encuadrar(t);
                        Rotular(t.titulo, t.detalle);
                    }
                    float tiempo = fotograma / (float)Fps;
                    float largo = Mathf.Max(t.clip.length, 1f / Fps);
                    Posar(t.personaje, t.clip, t.bucle ? Mathf.Repeat(tiempo, largo) : Mathf.Min(tiempo, largo));
                    Fotografiar();
                    if (++fotograma >= t.fotogramas)
                    {
                        fotograma = 0;
                        toma++;
                    }
                    Estado = $"clips: toma {toma + 1}/{tomas.Count}, {numero} fotogramas";
                }
            }
            catch (Exception e)
            {
                EditorApplication.update -= paso;
                Estado = "error: " + e;
                Terminado = true;
                Debug.LogException(e);
            }
        };
        EditorApplication.update += paso;
        Estado = $"clips: {tomas.Count} tomas";
    }

    private static GameObject Instanciar(string ruta, string controlador, float escala)
    {
        var origen = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
        var go = UnityEngine.Object.Instantiate(origen);
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        go.transform.localScale = Vector3.one * escala;
        var animator = go.GetComponentInChildren<Animator>();
        if (controlador != null) animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controlador);
        animator.applyRootMotion = false;
        // Solo se ven: fuera los colliders y los scripts (no corren fuera de Play, pero así no molestan)
        foreach (var c in go.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(c);
        go.SetActive(false);
        return go;
    }

    private static void AnadirRival(List<Toma> tomas, string nombre, string ruta, string controlador, float escala, string poseMenu)
    {
        GameObject go = Instanciar(ruta, controlador, escala);
        var animator = go.GetComponentInChildren<Animator>();
        var sobre = animator.runtimeAnimatorController as AnimatorOverrideController;
        var basico = (sobre != null ? sobre.runtimeAnimatorController : animator.runtimeAnimatorController) as AnimatorController;
        var estados = basico.layers[0].stateMachine.states.Select(s => s.state)
            .Where(s => s.motion is AnimationClip)
            .OrderBy(s => Array.IndexOf(Orden, s.name) < 0 ? 99 : Array.IndexOf(Orden, s.name)).ToList();
        foreach (AnimatorState estado in estados)
        {
            var clip = (AnimationClip)estado.motion;
            AnimationClip propio = sobre != null && sobre[clip] != null ? sobre[clip] : clip;
            bool bucle = propio.isLooping;
            float segundos = bucle ? Mathf.Clamp(propio.length * 2f, 2.5f, 4f) : Mathf.Min(propio.length, 6f) + 0.6f;
            tomas.Add(new Toma
            {
                titulo = nombre, detalle = $"{estado.name}  ·  {propio.name}  ·  {propio.length:F2} s",
                personaje = go, clip = propio, bucle = bucle, fotogramas = Mathf.CeilToInt(segundos * Fps),
                direccionCamara = TresCuartosRival,
            });
        }
        if (poseMenu != null)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Personajes + "Menu/" + poseMenu + ".anim");
            if (clip != null)
                tomas.Add(new Toma
                {
                    titulo = nombre, detalle = $"Pose del menú  ·  {clip.name}  ·  {clip.length:F2} s",
                    personaje = go, clip = clip, bucle = true, fotogramas = Mathf.CeilToInt(Mathf.Max(4f, clip.length * 1.5f) * Fps),
                    direccionCamara = new Vector3(0.15f, 0.1f, 1f),
                });
        }
    }

    private static void AnadirReferi(List<Toma> tomas)
    {
        string carpetaReferi = Personajes + "Referee/";
        GameObject go = Instanciar(carpetaReferi + "Referee.fbx", null, 1f);
        AnimationClip Clip(string ruta, string nombre) =>
            AssetDatabase.LoadAllAssetsAtPath(carpetaReferi + ruta).OfType<AnimationClip>().First(c => c.name == nombre);
        var dePie = Clip("Animations/Referee_DePie.anim", "Referee_DePie");
        var vigilar = Clip("Referee.fbx", "Pose_Referee");
        var caminar = Clip("Animations/Referee_Caminar.anim", "Referee_Caminar");
        var lateral = Clip("Animations/Referee_Lateral.anim", "Referee_Lateral");
        void Anadir(string detalle, AnimationClip clip, float segundos, Vector3 direccion) => tomas.Add(new Toma
        {
            titulo = "Réferi", detalle = $"{detalle}  ·  {clip.name}", personaje = go, clip = clip, bucle = true,
            fotogramas = Mathf.CeilToInt(segundos * Fps), direccionCamara = direccion,
        });
        Anadir("De pie (Agachado = 0)", dePie, 4f, TresCuartos);
        Anadir("Vigilando (Agachado = 1)", vigilar, 2f, TresCuartos);
        Anadir("Caminar, de lado", caminar, caminar.length * 3f, new Vector3(1f, 0.08f, 0.05f));
        Anadir("Caminar, de frente", caminar, caminar.length * 3f, TresCuartos);
        Anadir("Paso lateral a su derecha", lateral, lateral.length * 4f, new Vector3(0.1f, 0.1f, 1f));
    }

    private static void Encuadrar(Toma t)
    {
        Posar(t.personaje, t.clip, 0f);
        var animator = t.personaje.GetComponentInChildren<Animator>();
        float alto = Mathf.Max(1.2f, animator.GetBoneTransform(HumanBodyBones.Head).position.y + 0.25f);
        Vector3 objetivo = new Vector3(0f, alto * 0.5f, 0f);
        camara.fieldOfView = 35f;
        float distancia = alto * 0.68f / Mathf.Tan(camara.fieldOfView * 0.5f * Mathf.Deg2Rad);
        Vector3 direccion = Quaternion.LookRotation(Frente(t.personaje), Vector3.up) * t.direccionCamara.normalized;
        camara.transform.position = objetivo + direccion * distancia;
        camara.transform.LookAt(objetivo);
        ColocarRotulo();
    }

    // Hacia dónde mira cada personaje, sacado de sus hombros en la primera toma (los modelos no miran todos a +Z)
    private static readonly Dictionary<GameObject, Vector3> frentes = new Dictionary<GameObject, Vector3>();

    private static Vector3 Frente(GameObject personaje)
    {
        if (frentes.TryGetValue(personaje, out Vector3 frente)) return frente;
        var animator = personaje.GetComponentInChildren<Animator>();
        Vector3 derecha = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg).position
                          - animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg).position;
        derecha.y = 0f;
        frente = Vector3.Cross(derecha.normalized, Vector3.up);
        frentes[personaje] = frente;
        return frente;
    }

    private static void Posar(GameObject personaje, AnimationClip clip, float tiempo)
    {
        var animator = personaje.GetComponentInChildren<Animator>();
        Transform raiz = animator.transform;
        Vector3 posicion = raiz.localPosition;
        Quaternion giro = raiz.localRotation;
        clip.SampleAnimation(animator.gameObject, tiempo);
        // Sin root motion en el juego: el objeto del Animator no se mueve
        raiz.localPosition = posicion;
        raiz.localRotation = giro;
        var apoyo = animator.GetComponent<ApoyoEnSuelo>();
        if (apoyo != null)
        {
            float hundido = apoyo.MedirHundimiento();
            if (hundido > 0f) animator.GetBoneTransform(HumanBodyBones.Hips).position += Vector3.up * hundido;
        }
    }

    // ---------- Parte 2: en juego ----------

    private class Plano
    {
        public float desde, hasta;
        public string titulo, detalle;
        public Func<(Vector3 posicion, Vector3 mirar, float campo)> camara;
        public Action alEmpezar;
    }

    [MenuItem("Tools/Boxeo/Video de animaciones/2. Grabar en juego (en Play)")]
    public static void GrabarEnJuego()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError("Entra en Play con Main_Scene antes."); return; }
        Preparar("2_juego");
        int nivelSuperado = PlayerPrefs.GetInt("nivelSuperado", 0);
        Time.captureFramerate = Fps;

        var planos = new List<Plano>();
        float t0 = 0f;
        void Anadir(float segundos, string titulo, string detalle, Func<(Vector3, Vector3, float)> encuadre, Action alEmpezar = null)
        {
            planos.Add(new Plano { desde = t0, hasta = t0 + segundos, titulo = titulo, detalle = detalle, camara = encuadre, alEmpezar = alEmpezar });
            t0 += segundos;
        }
        Vector3 Ojos() => Camera.main != null ? Camera.main.transform.position : Vector3.up * 1.7f;
        Vector3 Cabeza(GameObject go) => go.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Head).position;

        // Menú: los cinco desde los ojos del jugador (te miran a ti)
        string[] menu = { "GlassJoe", "BaldBull", "SodaPopinski", "MrSandman", "MikeTyson" };
        string[] nombres = { "Glass Joe", "Bald Bull", "Soda Popinski", "Mr. Sandman", "Iron Mike Tyson" };
        Anadir(4f, "Menú", "Los cinco rivales desde los ojos del jugador", () =>
        {
            Vector3 centro = menu.Select(n => GameObject.Find(n)).Where(g => g != null).Aggregate(Vector3.zero, (a, g) => a + g.transform.position) / menu.Length;
            return (Ojos() + Vector3.back * 0.3f, centro + Vector3.up * 1.1f, 62f);
        });
        for (int i = 0; i < menu.Length; i++)
        {
            string n = menu[i];
            Anadir(3.2f, "Menú · " + nombres[i], "Pose y mirada al jugador (CuerpoVivo)", () =>
            {
                var go = GameObject.Find(n);
                return (Ojos(), go != null ? Cabeza(go) - Vector3.up * 0.35f : Vector3.forward, 24f);
            });
        }
        float finMenu = t0;

        // Pelea de Bald Bull
        Vector3 LadoRing()
        {
            var arbitro = UnityEngine.Object.FindAnyObjectByType<CuerpoArbitro>();
            var rival = UnityEngine.Object.FindAnyObjectByType<Oponente>();
            Vector3 centro = (rival.transform.position + new Vector3(Ojos().x, rival.transform.position.y, Ojos().z)) * 0.5f;
            // Al otro lado del ring que el sitio inicial del réferi y en alto, para verle de frente por encima de los púgiles
            float lado = arbitro.transform.parent != null && arbitro.transform.parent.position.x < centro.x ? 1f : -1f;
            return new Vector3(centro.x + lado * 4.4f, rival.transform.position.y + 2.5f, centro.z - 0.6f);
        }
        Vector3 Referi() => UnityEngine.Object.FindAnyObjectByType<CuerpoArbitro>().transform.position + Vector3.up * 0.95f;
        Vector3 Rival() => Cabeza(UnityEngine.Object.FindAnyObjectByType<Oponente>().gameObject);
        Anadir(6f, "Réferi", "Preparación: camina de frente hasta el centro y se vuelve", () => (LadoRing(), Referi(), 40f));
        // De lado, a la altura de la cabeza: se ve que la cara apunta a los ojos del jugador (desde los ojos lo tapan los guantes)
        Anadir(4.5f, "Bald Bull", "Mirada al jugador en la pelea (de perfil: la cara va hacia los ojos del jugador)", () =>
        {
            Vector3 hacia = Vector3.ProjectOnPlane(Ojos() - Rival(), Vector3.up).normalized;
            Vector3 lado = Vector3.Cross(Vector3.up, hacia);
            Vector3 medio = Vector3.Lerp(Rival(), Ojos(), 0.4f);
            // Del lado contrario al réferi, que si no se mete delante
            if (Vector3.Dot(lado, Referi() - medio) > 0f) lado = -lado;
            return (medio + lado * 2.1f + Vector3.up * 0.1f, medio - Vector3.up * 0.15f, 42f);
        });
        Anadir(9f, "Réferi", "Round: se desplaza de lado (paso lateral) sin dejar de mirar la pelea", () => (LadoRing(), Referi(), 40f));
        Anadir(11f, "Réferi", "Knockdown: camina hasta el rival y cuenta", () => (LadoRing(), Referi(), 40f), () =>
        {
            var arbitro = UnityEngine.Object.FindAnyObjectByType<Arbitro>();
            typeof(Arbitro).GetMethod("ProbarContarRival", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(arbitro, null);
        });
        Anadir(5f, "Réferi", "Tras la cuenta: vuelve a su sitio del round", () => (LadoRing(), Referi(), 40f));
        float fin = t0;

        float reloj = 0f;          // segundos de video grabados
        int ultimoFrame = -1;
        int planoActual = -1;
        bool cargando = false;
        float inicioPelea = -1f;
        EditorApplication.CallbackFunction paso = null;
        paso = () =>
        {
            try
            {
                if (!EditorApplication.isPlaying) { EditorApplication.update -= paso; Estado = "Play terminó antes de acabar"; Terminado = true; return; }
                if (Time.frameCount == ultimoFrame) return;
                int saltados = ultimoFrame < 0 ? 1 : Time.frameCount - ultimoFrame;
                ultimoFrame = Time.frameCount;

                // Cambio a la pelea al acabar el menú
                if (reloj >= finMenu && inicioPelea < 0f)
                {
                    if (!cargando)
                    {
                        cargando = true;
                        SceneManager.LoadScene("Scene_Ring_Level2");
                        return;
                    }
                    if (UnityEngine.Object.FindAnyObjectByType<CuerpoArbitro>() == null) return;
                    inicioPelea = Time.time;
                    camara = null;
                }
                // Que no lo tumben a mitad de la grabación
                var salud = UnityEngine.Object.FindAnyObjectByType<SaludJugador>();
                if (salud != null && salud.SaludNormalizada < 0.6f && !salud.SinSalud) salud.EstablecerSalud(salud.SaludMaxima);

                if (camara == null) CrearCamara(Color.black, true);
                int indice = planos.FindIndex(p => reloj >= p.desde && reloj < p.hasta);
                if (indice < 0)
                {
                    EditorApplication.update -= paso;
                    Time.captureFramerate = 0;
                    PlayerPrefs.SetInt("nivelSuperado", nivelSuperado);
                    Estado = $"juego terminado: {numero} fotogramas ({numero / (float)Fps:F0} s) en {carpeta}";
                    Terminado = true;
                    Debug.Log("VideoAnimaciones: " + Estado);
                    return;
                }
                Plano plano = planos[indice];
                if (indice != planoActual)
                {
                    planoActual = indice;
                    plano.alEmpezar?.Invoke();
                    Rotular(plano.titulo, plano.detalle);
                }
                var (posicion, mirar, campo) = plano.camara();
                camara.fieldOfView = campo;
                camara.transform.position = posicion;
                camara.transform.rotation = Quaternion.LookRotation(mirar - posicion, Vector3.up);
                ColocarRotulo();
                // Si el Editor se saltó fotogramas del juego, se repite la imagen para no perder el ritmo
                int copias = Mathf.Clamp(saltados, 1, 4);
                Fotografiar(copias);
                reloj += copias / (float)Fps;
                Estado = $"juego: plano {indice + 1}/{planos.Count}, {reloj:F1}/{fin:F0} s";
            }
            catch (Exception e)
            {
                EditorApplication.update -= paso;
                Time.captureFramerate = 0;
                Estado = "error: " + e;
                Terminado = true;
                Debug.LogException(e);
            }
        };
        EditorApplication.update += paso;
    }
}
