using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Tools > Boxeo > Vestir arena: deja la arena de MarpaStudio con el aspecto de una velada de boxeo en las siete escenas
/// del juego (se puede repetir; no toca los assets de terceros, solo las escenas):
///
/// Iluminación. La arena venía con una luz direccional "de sol" tapada por el techo (todo quedaba en su sombra, plano) y
/// 24 focos con sombra. Ahora:
/// - Una sola luz por píxel y con sombra: la del ring, direccional y casi cenital, que solo alumbra lo que no está en la
///   capa "Arena" (el ring, los púgiles, el réferi, los guantes). El edificio pasa a esa capa, así que ni lo ilumina ni
///   entra en su mapa de sombras: el ring queda como un pozo de luz y las gradas en penumbra.
/// - Los focos del edificio pasan a luces por vértice sin sombra (relleno frío y tenue de las gradas).
/// - Cuatro luces por vértice en las esquinas del ring, cálidas del lado del jugador y azules detrás del rival, que le
///   rellenan la cara y le recortan la silueta, como en una retransmisión.
/// - Ambiente en tres tonos (el de abajo, cálido, hace de rebote de la lona), niebla oscura que hunde el fondo y los
///   reflejos tomados de la propia arena en vez del cielo por defecto.
/// Con la calidad Medium de Android (una luz por píxel) esto cuesta menos que lo que había: un mapa de sombras con unas
/// decenas de objetos en vez de más de mil.
///
/// Ring. Lona propia con el emblema del juego (Art/Arena), esquinas roja, azul y dos neutrales blancas, y el pilar de la
/// estructura de focos que estaba fuera de sitio, en su esquina.
/// </summary>
public static class ArenaBuilder
{
    public const string CapaArena = "Arena";
    private const string Carpeta = "Assets/_Project/Art/Arena/";

    // Piezas del edificio que forman el ring: las ilumina la luz del ring
    private static readonly string[] PiezasRing = { "RingFloor", "Rope", "Corner", "Hook", "RingPillarsBase", "Banner", "Stairs" };

    // ----- Valores de la iluminación (públicos para poder afinarlos desde el Editor antes de fijarlos) -----
    public static Vector3 GiroLuzRing = new Vector3(68f, 20f, 0f);
    public static Color ColorLuzRing = new Color(1f, 0.95f, 0.86f);
    public static float IntensidadLuzRing = 1.25f;
    public static float FuerzaSombra = 0.8f;

    public static Color ColorFocos = new Color(0.62f, 0.72f, 1f);
    public static float IntensidadFocos = 0.3f;

    public static Color AmbienteCielo = new Color(0.07f, 0.08f, 0.13f);
    public static Color AmbienteHorizonte = new Color(0.10f, 0.10f, 0.14f);
    public static Color AmbienteSuelo = new Color(0.20f, 0.17f, 0.14f);
    public static Color ColorNiebla = new Color(0.015f, 0.02f, 0.04f);
    public static float DensidadNiebla = 0.022f;

    public static float AlturaEsquinas = 2.3f;       // sobre la lona
    public static float DistanciaEsquinas = 3.6f;    // del centro, en X y en Z
    public static float AlcanceEsquinas = 9f;
    public static Color ColorCalida = new Color(1f, 0.82f, 0.62f);
    public static float IntensidadCalida = 0.9f;
    public static Color ColorFria = new Color(0.45f, 0.65f, 1f);
    public static float IntensidadFria = 1.5f;

    [MenuItem("Tools/Boxeo/Vestir arena")]
    public static void VestirTodas()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("No en Play."); return; }
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) { Debug.LogError("Guarda o descarta los cambios de las escenas abiertas antes."); return; }

        SceneSetup[] abiertas = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes.Where(s => s.enabled))
            {
                Scene escena = EditorSceneManager.OpenScene(s.path, OpenSceneMode.Single);
                if (Vestir(escena)) EditorSceneManager.SaveScene(escena);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Arena vestida en las escenas de la build.");
        }
        finally
        {
            if (abiertas.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(abiertas);
        }
    }

    /// <summary>Viste la escena (sin guardarla). Devuelve false si no es una escena con la arena.</summary>
    public static bool Vestir(Scene escena)
    {
        GameObject edificio = escena.GetRootGameObjects().FirstOrDefault(g => g.name == "Building");
        if (edificio == null) return false;
        int capa = CapaDeLaArena();

        SepararCapas(edificio, capa);
        Iluminar(escena, capa);
        ColocarPilar(edificio);
        VestirRing(escena, edificio);
        PublicoBuilder.Poner(escena, edificio, capa);
        EditorSceneManager.MarkSceneDirty(escena);
        return true;
    }

    // La capa "Arena": la primera capa de usuario libre si aún no existe
    private static int CapaDeLaArena()
    {
        int capa = LayerMask.NameToLayer(CapaArena);
        if (capa >= 0) return capa;
        var ajustes = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty capas = ajustes.FindProperty("layers");
        for (int i = 6; i < capas.arraySize; i++)
        {
            SerializedProperty p = capas.GetArrayElementAtIndex(i);
            if (!string.IsNullOrEmpty(p.stringValue)) continue;
            p.stringValue = CapaArena;
            ajustes.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            return i;
        }
        throw new System.InvalidOperationException("No queda ninguna capa libre para \"Arena\".");
    }

    private static bool EsDelRing(string nombre) => PiezasRing.Any(p => nombre == p || nombre.StartsWith(p + " "));

    private static void SepararCapas(GameObject edificio, int capa)
    {
        foreach (Transform hijo in edificio.transform)
        {
            int destino = EsDelRing(hijo.name) ? 0 : capa;
            foreach (Transform t in hijo.GetComponentsInChildren<Transform>(true))
                if (t.gameObject.layer != destino) t.gameObject.layer = destino;
        }
    }

    private static void Iluminar(Scene escena, int capa)
    {
        GameObject[] raices = escena.GetRootGameObjects();

        // La luz del ring: la direccional que ya había, recolocada
        GameObject direccional = raices.FirstOrDefault(g => g.name == "Directional Light" || g.name == "LuzRing");
        if (direccional != null && direccional.TryGetComponent(out Light luzRing))
        {
            direccional.name = "LuzRing";
            direccional.transform.rotation = Quaternion.Euler(GiroLuzRing);
            luzRing.type = LightType.Directional;
            luzRing.color = ColorLuzRing;
            luzRing.intensity = IntensidadLuzRing;
            luzRing.shadows = LightShadows.Soft;
            luzRing.shadowStrength = FuerzaSombra;
            luzRing.renderMode = LightRenderMode.ForcePixel;
            luzRing.lightmapBakeType = LightmapBakeType.Realtime;
            luzRing.cullingMask = ~(1 << capa);
            EditorUtility.SetDirty(luzRing);
        }

        // Los focos del edificio: relleno por vértice, sin sombra. La puntual del centro del ring sobra con la luz nueva
        GameObject luces = raices.FirstOrDefault(g => g.name == "Light");
        if (luces != null)
        {
            foreach (Light luz in luces.GetComponentsInChildren<Light>(true))
            {
                luz.shadows = LightShadows.None;
                luz.renderMode = LightRenderMode.ForceVertex;
                luz.lightmapBakeType = LightmapBakeType.Realtime;
                if (luz.name == "Area Light") luz.gameObject.SetActive(false);
                else if (luz.name.StartsWith("e"))
                {
                    luz.color = ColorFocos;
                    luz.intensity = IntensidadFocos;
                }
                else luz.intensity = Mathf.Min(luz.intensity, 2f);
                EditorUtility.SetDirty(luz);
            }
            // En Medium las reflection probes en tiempo real están apagadas: los reflejos salen del ambiente
            foreach (ReflectionProbe sonda in luces.GetComponentsInChildren<ReflectionProbe>(true))
                sonda.gameObject.SetActive(false);
        }

        // Las cuatro luces de las esquinas del ring
        GameObject lona = GameObject.Find("RingFloor");
        float alturaLona = lona != null ? lona.GetComponent<Renderer>().bounds.max.y : 0f;
        GameObject grupo = raices.FirstOrDefault(g => g.name == "LucesRing");
        if (grupo == null)
        {
            grupo = new GameObject("LucesRing");
            SceneManager.MoveGameObjectToScene(grupo, escena);
        }
        // El jugador empieza en -Z y el rival en +Z: cálidas delante del rival, frías a su espalda
        (string nombre, float x, float z, bool fria)[] esquinas =
        {
            ("Calida_Izquierda", -1f, -1f, false), ("Calida_Derecha", 1f, -1f, false),
            ("Fria_Izquierda", -1f, 1f, true), ("Fria_Derecha", 1f, 1f, true),
        };
        foreach ((string nombre, float x, float z, bool fria) in esquinas)
        {
            Transform t = grupo.transform.Find(nombre);
            if (t == null)
            {
                t = new GameObject(nombre, typeof(Light)).transform;
                t.SetParent(grupo.transform, false);
            }
            t.position = new Vector3(x * DistanciaEsquinas, alturaLona + AlturaEsquinas, z * DistanciaEsquinas);
            var luz = t.GetComponent<Light>();
            luz.type = LightType.Point;
            luz.range = AlcanceEsquinas;
            luz.color = fria ? ColorFria : ColorCalida;
            luz.intensity = fria ? IntensidadFria : IntensidadCalida;
            luz.shadows = LightShadows.None;
            luz.renderMode = LightRenderMode.ForceVertex;
            luz.lightmapBakeType = LightmapBakeType.Realtime;
            // Solo el ring y lo que hay en él: el edificio ya tiene sus focos
            luz.cullingMask = ~(1 << capa);
        }

        // Ambiente, niebla y reflejos: ajustes de la escena (hay que tenerla activa para escribirlos)
        Scene activa = SceneManager.GetActiveScene();
        SceneManager.SetActiveScene(escena);
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = AmbienteCielo;
        RenderSettings.ambientEquatorColor = AmbienteHorizonte;
        RenderSettings.ambientGroundColor = AmbienteSuelo;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = ColorNiebla;
        RenderSettings.fogDensity = DensidadNiebla;
        RenderSettings.reflectionIntensity = 1f;
        if (activa.IsValid() && activa != escena) SceneManager.SetActiveScene(activa);
    }

    // Uno de los cuatro pilares de la estructura de focos estaba en mitad de un lado, hundido en el suelo
    private static void ColocarPilar(GameObject edificio)
    {
        Transform[] pilares = edificio.transform.Cast<Transform>()
            .Where(t => t.name.StartsWith("Truss") && t.GetComponent<Renderer>() != null && t.GetComponent<Renderer>().bounds.size.y > 5f)
            .ToArray();
        if (pilares.Length != 4) return;
        // Los tres que están bien dan la altura y las dos coordenadas de las esquinas
        Bounds[] cajas = pilares.Select(p => p.GetComponent<Renderer>().bounds).ToArray();
        float altura = cajas.Select(b => b.center.y).OrderBy(y => y).ElementAt(2);
        float[] xs = { cajas.Min(b => b.center.x), cajas.Max(b => b.center.x) };
        float[] zs = { cajas.Min(b => b.center.z), cajas.Max(b => b.center.z) };
        for (int i = 0; i < 4; i++)
        {
            Vector3 c = cajas[i].center;
            bool enEsquina = xs.Any(x => Mathf.Abs(c.x - x) < 0.3f) && zs.Any(z => Mathf.Abs(c.z - z) < 0.3f) && Mathf.Abs(c.y - altura) < 0.3f;
            if (enEsquina) continue;
            // La esquina que no ocupa ninguno de los otros
            foreach (float x in xs)
                foreach (float z in zs)
                {
                    bool ocupada = cajas.Where((b, j) => j != i).Any(b => Mathf.Abs(b.center.x - x) < 0.3f && Mathf.Abs(b.center.z - z) < 0.3f);
                    if (ocupada) continue;
                    pilares[i].position += new Vector3(x - c.x, altura - c.y, z - c.z);
                }
        }
    }

    // ---------- Ring: lona, esquinas y cuerdas ----------

    private const string RutaFuente = "Assets/_Project/Fonts/Bangers SDF.asset";
    private const string MaterialesArena = "Assets/ThirdParty/MarpaStudio/Art/Materials/BoxingArena/";
    private static readonly Color Marino = new Color(0.05f, 0.08f, 0.2f);
    private static readonly Color Rojo = new Color(0.72f, 0.06f, 0.07f);
    private static readonly Color Azul = new Color(0.07f, 0.2f, 0.72f);
    private static readonly Color Blanco = new Color(0.86f, 0.86f, 0.84f);

    private static void VestirRing(Scene escena, GameObject edificio)
    {
        Transform piso = edificio.transform.Find("RingFloor");
        if (piso == null) return;
        Bounds caja = piso.GetComponent<Renderer>().bounds;

        GameObject grupo = escena.GetRootGameObjects().FirstOrDefault(g => g.name == "RingVestido");
        if (grupo == null)
        {
            grupo = new GameObject("RingVestido");
            SceneManager.MoveGameObjectToScene(grupo, escena);
        }

        // La lona: un plano justo encima del piso del ring, con su propia textura
        Transform lona = grupo.transform.Find("Lona");
        if (lona == null)
        {
            lona = GameObject.CreatePrimitive(PrimitiveType.Quad).transform;
            lona.name = "Lona";
            Object.DestroyImmediate(lona.GetComponent<Collider>());
            lona.SetParent(grupo.transform, false);
        }
        lona.SetPositionAndRotation(new Vector3(caja.center.x, caja.max.y + 0.004f, caja.center.z), Quaternion.Euler(90f, 0f, 0f));
        lona.localScale = new Vector3(caja.size.x - 0.04f, caja.size.z - 0.04f, 1f);
        var render = lona.GetComponent<MeshRenderer>();
        render.sharedMaterial = MaterialLona();
        render.shadowCastingMode = ShadowCastingMode.Off;
        render.receiveShadows = true;

        // Esquinas: roja la del rival (al fondo a la derecha), azul la del jugador (cerca a la izquierda) y las otras
        // dos blancas, las neutrales, que es adonde el réferi manda al que queda de pie tras un derribo
        foreach (Transform esquina in edificio.transform.Cast<Transform>().Where(t => t.name.StartsWith("Corner")))
        {
            Vector3 c = esquina.GetComponent<Renderer>().bounds.center - caja.center;
            Color color = c.x > 0f && c.z > 0f ? Rojo : c.x < 0f && c.z < 0f ? Azul : Blanco;
            string nombre = color == Rojo ? "Esquina_Roja" : color == Azul ? "Esquina_Azul" : "Esquina_Neutral";
            esquina.GetComponent<Renderer>().sharedMaterial = MaterialLiso(nombre, color, 0.35f);
        }

        // Las cuerdas y sus ganchos no proyectan sombra: son tan finos que el mapa de sombras los deja a trazos
        foreach (Renderer fino in edificio.transform.Cast<Transform>().Where(t => t.name.StartsWith("Rope") || t.name.StartsWith("Hook")).Select(t => t.GetComponent<Renderer>()))
            if (fino != null) fino.shadowCastingMode = ShadowCastingMode.Off;

        // Cuerdas: roja arriba, blanca en medio y azul abajo (tiñen la textura original)
        Transform[] cuerdas = edificio.transform.Cast<Transform>().Where(t => t.name.StartsWith("Rope"))
            .OrderByDescending(t => t.GetComponent<Renderer>().bounds.center.y).ToArray();
        for (int i = 0; i < cuerdas.Length; i++)
        {
            Color color = i == 0 ? Rojo : i == cuerdas.Length - 1 ? Azul : Blanco;
            string nombre = i == 0 ? "Cuerda_Roja" : i == cuerdas.Length - 1 ? "Cuerda_Azul" : "Cuerda_Blanca";
            cuerdas[i].GetComponent<Renderer>().sharedMaterial = MaterialTenido(nombre, "Rope.mat", color);
        }
    }

    private static Material MaterialLiso(string nombre, Color color, float brillo)
    {
        string ruta = Carpeta + nombre + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (material == null)
        {
            Directory.CreateDirectory(Carpeta);
            material = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(material, ruta);
        }
        material.color = color;
        material.SetFloat("_Glossiness", brillo);
        material.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(material);
        return material;
    }

    // Copia de un material de la arena con otro color (el original no se toca)
    private static Material MaterialTenido(string nombre, string original, Color color)
    {
        string ruta = Carpeta + nombre + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (material == null)
        {
            Directory.CreateDirectory(Carpeta);
            material = new Material(AssetDatabase.LoadAssetAtPath<Material>(MaterialesArena + original));
            AssetDatabase.CreateAsset(material, ruta);
        }
        material.color = color;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material MaterialLona()
    {
        string rutaTextura = Carpeta + "Lona_Ring.png";
        var textura = AssetDatabase.LoadAssetAtPath<Texture2D>(rutaTextura);
        if (textura == null) textura = CrearTexturaLona(rutaTextura);
        Material material = MaterialLiso("Lona_Ring", Color.white, 0.12f);
        material.mainTexture = textura;
        return material;
    }

    [MenuItem("Tools/Boxeo/Regenerar la lona del ring")]
    private static void RegenerarLona() => CrearTexturaLona(Carpeta + "Lona_Ring.png");

    /// <summary>
    /// Pinta la lona: tela clara con algo de grano y desgaste hacia el centro, faldón azul marino por fuera de las cuerdas
    /// y, en el centro, el emblema del juego. El texto sale del atlas SDF de la fuente del juego (Bangers), así que no
    /// hace falta ningún programa de dibujo. Arriba de la textura = el fondo del ring visto desde donde empieza el jugador.
    /// </summary>
    private static Texture2D CrearTexturaLona(string ruta)
    {
        const int n = 1024;
        const float lado = 6.96f;                  // metros que cubre la textura
        var px = new Color[n * n];
        var azar = new System.Random(7);
        Color tela = new Color(0.70f, 0.74f, 0.80f);

        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                // Posición en metros desde el centro
                float mx = ((x + 0.5f) / n - 0.5f) * lado, my = ((y + 0.5f) / n - 0.5f) * lado;
                float r = Mathf.Sqrt(mx * mx + my * my);
                float borde = Mathf.Max(Mathf.Abs(mx), Mathf.Abs(my));

                // Tela: grano fino + manchas suaves + desgaste hacia el centro
                float grano = ((float)azar.NextDouble() - 0.5f) * 0.035f;
                float mancha = (Mathf.PerlinNoise(mx * 0.9f + 11f, my * 0.9f + 5f) - 0.5f) * 0.07f
                               + (Mathf.PerlinNoise(mx * 3.1f + 3f, my * 3.1f + 17f) - 0.5f) * 0.035f;
                float desgaste = Mathf.SmoothStep(0.08f, 0f, r / 3f) * -1f;
                Color c = tela * (1f + grano + mancha + desgaste);

                // Faldón por fuera de las cuerdas (3,075 m del centro), con un filete rojo
                c = Color.Lerp(c, Marino * (1f + grano * 2f), Suave(borde, 3.16f, 0.012f));
                c = Color.Lerp(c, Rojo, Suave(borde, 3.10f, 0.01f) * (1f - Suave(borde, 3.16f, 0.01f)));

                // Emblema: aro rojo, filete blanco y disco azul marino
                c = Color.Lerp(c, Rojo, 1f - Suave(r, 1.32f, 0.008f));
                c = Color.Lerp(c, Blanco, 1f - Suave(r, 1.17f, 0.008f));
                c = Color.Lerp(c, Marino * (1f + mancha), 1f - Suave(r, 1.12f, 0.008f));
                c.a = 1f;
                px[y * n + x] = c;
            }

        var fuente = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RutaFuente);
        if (fuente != null)
        {
            float pxPorMetro = n / lado;
            Escribir(px, n, fuente, "SIMULADOR DE", new Vector2(n * 0.5f, n * 0.5f + 0.50f * pxPorMetro), 0.20f * pxPorMetro, Blanco);
            Escribir(px, n, fuente, "BOXEO RV", new Vector2(n * 0.5f, n * 0.5f - 0.20f * pxPorMetro), 0.52f * pxPorMetro, Color.white);
            Escribir(px, n, fuente, "UTEC", new Vector2(n * 0.5f, n * 0.5f - 0.68f * pxPorMetro), 0.20f * pxPorMetro, new Color(0.95f, 0.75f, 0.2f));
        }

        var textura = new Texture2D(n, n, TextureFormat.RGB24, false);
        textura.SetPixels(px);
        textura.Apply();
        Directory.CreateDirectory(Carpeta);
        File.WriteAllBytes(ruta, textura.EncodeToPNG());
        Object.DestroyImmediate(textura);
        AssetDatabase.ImportAsset(ruta);

        var importador = (TextureImporter)AssetImporter.GetAtPath(ruta);
        importador.maxTextureSize = 1024;
        importador.mipmapEnabled = true;
        // Se ve casi de canto: sin filtro anisótropo el emblema se emborrona a un par de metros
        importador.anisoLevel = 8;
        importador.wrapMode = TextureWrapMode.Clamp;
        importador.SetPlatformTextureSettings(new TextureImporterPlatformSettings
        {
            name = "Android", overridden = true, maxTextureSize = 1024, format = TextureImporterFormat.ASTC_6x6,
        });
        importador.SaveAndReimport();
        Debug.Log($"Lona generada en {ruta}");
        return AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
    }

    // 0 por debajo del umbral, 1 por encima, con una transición de "ancho" metros
    private static float Suave(float valor, float umbral, float ancho) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(umbral - ancho, umbral + ancho, valor));

    // Escribe un texto centrado en "centro" (píxeles; la línea base pasa por ahí) con mayúsculas de "altura" píxeles,
    // muestreando el atlas SDF de la fuente
    private static void Escribir(Color[] px, int n, TMP_FontAsset fuente, string texto, Vector2 centro, float altura, Color color)
    {
        Texture2D atlas = fuente.atlasTexture;
        Color32[] sdf = atlas.GetPixels32();
        float escala = altura / fuente.faceInfo.capLine;   // píxeles de salida por unidad de la fuente
        int relleno = fuente.atlasPadding;

        float ancho = 0f;
        foreach (char ch in texto)
            if (fuente.characterLookupTable.TryGetValue(ch, out TMP_Character car)) ancho += car.glyph.metrics.horizontalAdvance * escala;
        float pluma = centro.x - ancho * 0.5f;

        foreach (char ch in texto)
        {
            if (!fuente.characterLookupTable.TryGetValue(ch, out TMP_Character car)) continue;
            var glifo = car.glyph;
            var rect = glifo.glyphRect;
            if (rect.width > 0)
            {
                // El rectángulo del atlas con su relleno, que es donde está el degradado del borde
                float x0 = pluma + glifo.metrics.horizontalBearingX * escala - relleno * escala;
                float y1 = centro.y + glifo.metrics.horizontalBearingY * escala + relleno * escala;
                float w = (rect.width + 2 * relleno) * escala, h = (rect.height + 2 * relleno) * escala;
                // El SDF cambia de 0 a 1 en 2·relleno píxeles del atlas: medio píxel de salida de suavizado
                float suavizado = 0.5f / (2f * relleno * escala) * 1.2f;
                for (int y = Mathf.Max(0, Mathf.FloorToInt(y1 - h)); y < Mathf.Min(n, Mathf.CeilToInt(y1)); y++)
                    for (int x = Mathf.Max(0, Mathf.FloorToInt(x0)); x < Mathf.Min(n, Mathf.CeilToInt(x0 + w)); x++)
                    {
                        float u = rect.x - relleno + (x + 0.5f - x0) / escala;
                        float v = rect.y - relleno + (y + 0.5f - (y1 - h)) / escala;
                        float d = Bilineal(sdf, atlas.width, atlas.height, u, v);
                        float a = Suave(d, 0.5f, suavizado);
                        if (a <= 0f) continue;
                        Color fondo = px[y * n + x];
                        px[y * n + x] = Color.Lerp(fondo, color, a);
                    }
            }
            pluma += glifo.metrics.horizontalAdvance * escala;
        }
    }

    private static float Bilineal(Color32[] datos, int ancho, int alto, float u, float v)
    {
        u = Mathf.Clamp(u - 0.5f, 0f, ancho - 1.001f);
        v = Mathf.Clamp(v - 0.5f, 0f, alto - 1.001f);
        int x = (int)u, y = (int)v;
        float fx = u - x, fy = v - y;
        float a = datos[y * ancho + x].a, b = datos[y * ancho + x + 1].a;
        float c = datos[(y + 1) * ancho + x].a, d = datos[(y + 1) * ancho + x + 1].a;
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy) / 255f;
    }
}
