using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Público de las gradas (lo llama ArenaBuilder al vestir cada escena). La arena trae 1 012 asientos (planos de 1 m con
/// la butaca pintada) y estaba vacía mientras sonaba el murmullo de la multitud. Aquí se sienta gente en ellos con el
/// menor coste posible para el Quest 2:
/// - una sola malla con todas las siluetas (Art/Arena/Publico.asset; unos 3 000 triángulos, una draw call), compartida
///   por las siete escenas porque los asientos están en el mismo sitio en todas;
/// - una textura de 512 × 64 generada aquí con seis siluetas de busto y el destello de cámara (pocos KB en el APK);
/// - el shader Boxeo/Publico, sin luces: el tinte de cada espectador va en el color del vértice, más apagado cuanto más
///   lejos del ring. Las anima el propio shader (ver Publico.shader).
/// Las butacas de primera fila (mallas "Chair", a pocos metros del ring) se dejan vacías: de cerca una silueta plana
/// se nota. Para quitar el público basta con borrar o desactivar el objeto "Publico" de la escena.
/// </summary>
public static class PublicoBuilder
{
    private const string Carpeta = "Assets/_Project/Art/Arena/";
    private const string RutaMalla = Carpeta + "Publico.asset";
    private const string RutaTextura = Carpeta + "Publico.png";
    private const string RutaMaterial = Carpeta + "Publico.mat";
    private const int Celdas = 8;          // 6 siluetas + destello + 1 libre
    private const int Siluetas = 6;
    private const float Ocupacion = 0.82f;

    /// <summary>Pone el objeto "Publico" en la escena (y crea la malla, la textura y el material si faltan).</summary>
    public static void Poner(Scene escena, GameObject edificio, int capa)
    {
        var malla = AssetDatabase.LoadAssetAtPath<Mesh>(RutaMalla);
        if (malla == null) malla = CrearMalla(edificio);
        if (malla == null) return;

        GameObject publico = escena.GetRootGameObjects().FirstOrDefault(g => g.name == "Publico");
        if (publico == null)
        {
            publico = new GameObject("Publico", typeof(MeshFilter), typeof(MeshRenderer));
            SceneManager.MoveGameObjectToScene(publico, escena);
        }
        publico.layer = capa;
        publico.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        publico.GetComponent<MeshFilter>().sharedMesh = malla;
        var render = publico.GetComponent<MeshRenderer>();
        render.sharedMaterial = Material();
        render.shadowCastingMode = ShadowCastingMode.Off;
        render.receiveShadows = false;
        render.lightProbeUsage = LightProbeUsage.Off;
        render.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }

    [MenuItem("Tools/Boxeo/Regenerar el público")]
    private static void Regenerar()
    {
        GameObject edificio = GameObject.Find("Building");
        if (edificio == null) { Debug.LogError("Abre una escena con la arena."); return; }
        CrearTextura();
        CrearMalla(edificio);
        AssetDatabase.SaveAssets();
    }

    private static Material Material()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(RutaMaterial);
        if (material == null)
        {
            material = new Material(Shader.Find("Boxeo/Publico"));
            AssetDatabase.CreateAsset(material, RutaMaterial);
        }
        var textura = AssetDatabase.LoadAssetAtPath<Texture2D>(RutaTextura);
        if (textura == null) textura = CrearTextura();
        material.shader = Shader.Find("Boxeo/Publico");
        material.mainTexture = textura;
        EditorUtility.SetDirty(material);
        return material;
    }

    // Una silueta por asiento ocupado, mirando hacia donde mira el asiento, y un destello de cámara cada pocos
    private static Mesh CrearMalla(GameObject edificio)
    {
        Transform[] asientos = edificio.transform.Cast<Transform>()
            .Where(t => t.name.StartsWith("PlaneSeat") && t.gameObject.activeInHierarchy)
            .OrderBy(t => t.position.x).ThenBy(t => t.position.z).ThenBy(t => t.position.y)
            .ToArray();
        if (asientos.Length == 0) { Debug.LogWarning("La escena no tiene asientos (PlaneSeat)."); return null; }

        var azar = new System.Random(2026);
        float Azar() => (float)azar.NextDouble();
        // Ropa en tonos apagados; unos pocos claros para que no sea una masa uniforme
        Color[] ropa =
        {
            new Color(0.20f, 0.24f, 0.42f), new Color(0.42f, 0.16f, 0.16f), new Color(0.30f, 0.30f, 0.32f),
            new Color(0.18f, 0.32f, 0.26f), new Color(0.46f, 0.40f, 0.30f), new Color(0.62f, 0.62f, 0.66f),
            new Color(0.36f, 0.22f, 0.40f), new Color(0.55f, 0.35f, 0.18f),
        };

        var vertices = new List<Vector3>();
        var normales = new List<Vector3>();
        var uvs = new List<Vector2>();
        var colores = new List<Color>();
        var triangulos = new List<int>();

        void Cuadro(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3[] desplazamientos, int celda, Color color)
        {
            int i = vertices.Count;
            vertices.AddRange(new[] { a, b, c, d });
            normales.AddRange(desplazamientos ?? new[] { Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero });
            float u0 = (celda + 0.02f) / Celdas, u1 = (celda + 0.98f) / Celdas;
            uvs.AddRange(new[] { new Vector2(u0, 0f), new Vector2(u1, 0f), new Vector2(u1, 1f), new Vector2(u0, 1f) });
            colores.AddRange(new[] { color, color, color, color });
            triangulos.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
        }

        int gente = 0, destellos = 0;
        foreach (Transform asiento in asientos)
        {
            if (Azar() > Ocupacion) continue;
            Vector3 frente = asiento.forward;
            Vector3 lado = Vector3.Cross(Vector3.up, frente).normalized;
            float ancho = 0.62f + Azar() * 0.14f, alto = 0.78f + Azar() * 0.16f;
            Vector3 pie = asiento.position + frente * 0.16f + lado * ((Azar() - 0.5f) * 0.12f) + Vector3.down * 0.12f;

            // Cuanto más lejos del ring, más apagado: la luz está en el centro
            float distancia = new Vector2(pie.x, pie.z).magnitude;
            float brillo = Mathf.Lerp(0.62f, 0.2f, Mathf.InverseLerp(9f, 34f, distancia)) * (0.8f + Azar() * 0.4f);
            Color color = ropa[azar.Next(ropa.Length)] * brillo;
            color.a = Azar();   // fase de la animación

            Cuadro(pie - lado * ancho * 0.5f, pie + lado * ancho * 0.5f,
                pie + lado * ancho * 0.5f + Vector3.up * alto, pie - lado * ancho * 0.5f + Vector3.up * alto,
                null, azar.Next(Siluetas), color);
            gente++;

            // Uno de cada ocho lleva cámara: el destello sale a la altura de la cara
            if (Azar() < 0.125f)
            {
                Vector3 centro = pie + Vector3.up * (alto * 0.78f) + frente * 0.08f + lado * ((Azar() - 0.5f) * 0.2f);
                float r = 0.2f + Azar() * 0.12f;
                Vector3[] esquinas = { (-lado - Vector3.up) * r, (lado - Vector3.up) * r, (lado + Vector3.up) * r, (-lado + Vector3.up) * r };
                Cuadro(centro, centro, centro, centro, esquinas, Siluetas, new Color(1f, 1f, 1f, Azar()));
                destellos++;
            }
        }

        var malla = AssetDatabase.LoadAssetAtPath<Mesh>(RutaMalla);
        bool nueva = malla == null;
        if (nueva) malla = new Mesh();
        else malla.Clear();
        malla.name = "Publico";
        malla.indexFormat = vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        malla.SetVertices(vertices);
        malla.SetNormals(normales);
        malla.SetUVs(0, uvs);
        malla.SetColors(colores);
        malla.SetTriangles(triangulos, 0);
        malla.RecalculateBounds();
        // Los destellos se abren fuera de la caja calculada y las siluetas saltan: se le da margen
        Bounds caja = malla.bounds;
        caja.Expand(1.5f);
        malla.bounds = caja;
        malla.UploadMeshData(false);
        if (nueva)
        {
            Directory.CreateDirectory(Carpeta);
            AssetDatabase.CreateAsset(malla, RutaMalla);
        }
        else EditorUtility.SetDirty(malla);
        Debug.Log($"Público: {gente} espectadores y {destellos} cámaras en {asientos.Length} asientos ({triangulos.Count / 3} triángulos).");
        return malla;
    }

    // Seis bustos (cabeza, cuello y hombros; algunos con gorra o un brazo en alto) en gris, más claros por arriba como si
    // les diera la luz del techo, y la estrella del destello. El color lo pone el vértice
    private static Texture2D CrearTextura()
    {
        const int alto = 64, celda = 64;
        int ancho = celda * Celdas;
        var px = new Color[ancho * alto];

        for (int c = 0; c < Celdas; c++)
            for (int y = 0; y < alto; y++)
                for (int x = 0; x < celda; x++)
                {
                    // Coordenadas de la celda: x de -0,5 a 0,5; y de 0 (base) a 1
                    var p = new Vector2((x + 0.5f) / celda - 0.5f, (y + 0.5f) / alto);
                    float distancia, valor;
                    if (c < Siluetas) Busto(c, p, out distancia, out valor);
                    else if (c == Siluetas) Estrella(p, out distancia, out valor);
                    else { distancia = 1f; valor = 0f; }
                    // Medio píxel de suavizado en el borde (el shader recorta en alfa 0,5)
                    float alfa = Mathf.Clamp01(0.5f - distancia * celda);
                    px[y * ancho + c * celda + x] = new Color(valor, valor, valor, alfa);
                }

        var textura = new Texture2D(ancho, alto, TextureFormat.RGBA32, false);
        textura.SetPixels(px);
        textura.Apply();
        Directory.CreateDirectory(Carpeta);
        File.WriteAllBytes(RutaTextura, textura.EncodeToPNG());
        Object.DestroyImmediate(textura);
        AssetDatabase.ImportAsset(RutaTextura);

        var importador = (TextureImporter)AssetImporter.GetAtPath(RutaTextura);
        importador.alphaIsTransparency = true;
        importador.mipmapEnabled = true;
        // Sin esto el recorte se come las siluetas lejanas: los mipmaps promedian el alfa hacia abajo
        importador.mipMapsPreserveCoverage = true;
        importador.alphaTestReferenceValue = 0.5f;
        importador.wrapMode = TextureWrapMode.Clamp;
        importador.maxTextureSize = 512;
        importador.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(RutaTextura);
    }

    // Distancia con signo al contorno (negativa dentro) y tono de gris
    private static void Busto(int variante, Vector2 p, out float distancia, out float valor)
    {
        float hombros = variante % 2 == 0 ? 0.36f : 0.31f;
        float cabezaY = 0.72f;
        float radio = variante == 3 ? 0.165f : 0.15f;

        float torso = Mathf.Min(Elipse(p, new Vector2(0f, 0.3f), new Vector2(hombros, 0.25f)),
            Caja(p, new Vector2(0f, 0.14f), new Vector2(hombros, 0.16f)));
        float cuello = Caja(p, new Vector2(0f, 0.56f), new Vector2(0.055f, 0.08f));
        float cabeza = Elipse(p, new Vector2(0f, cabezaY), new Vector2(radio * 0.9f, radio));
        float cuerpo = Mathf.Min(torso, Mathf.Min(cuello, cabeza));

        // Variantes: 2 y 5 con un brazo en alto (lados contrarios), 4 con los dos; 1 con gorra
        if (variante == 2 || variante == 4) cuerpo = Mathf.Min(cuerpo, Capsula(p, new Vector2(0.3f, 0.42f), new Vector2(0.4f, 0.93f), 0.055f));
        if (variante == 5 || variante == 4) cuerpo = Mathf.Min(cuerpo, Capsula(p, new Vector2(-0.3f, 0.42f), new Vector2(-0.4f, 0.93f), 0.055f));
        if (variante == 1) cuerpo = Mathf.Min(cuerpo, Caja(p, new Vector2(0.09f, cabezaY + 0.06f), new Vector2(0.17f, 0.02f)));

        distancia = cuerpo;
        // La cabeza más clara que la ropa; todo algo más claro hacia arriba
        bool enCabeza = cabeza < torso && p.y > 0.55f;
        valor = (enCabeza ? 0.95f : 0.62f) * Mathf.Lerp(0.72f, 1.05f, Mathf.InverseLerp(0.05f, 0.9f, p.y));
    }

    // Estrella de cuatro puntas con el centro redondo
    private static void Estrella(Vector2 p, out float distancia, out float valor)
    {
        var q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y - 0.5f));
        float puntas = Mathf.Pow(q.x, 0.55f) + Mathf.Pow(q.y, 0.55f) - Mathf.Pow(0.46f, 0.55f);
        float centro = q.magnitude - 0.16f;
        distancia = Mathf.Min(puntas * 0.3f, centro);
        valor = 1f;
    }

    private static float Elipse(Vector2 p, Vector2 centro, Vector2 radios)
    {
        var q = new Vector2((p.x - centro.x) / radios.x, (p.y - centro.y) / radios.y);
        return (q.magnitude - 1f) * Mathf.Min(radios.x, radios.y);
    }

    private static float Caja(Vector2 p, Vector2 centro, Vector2 mitad)
    {
        var d = new Vector2(Mathf.Abs(p.x - centro.x) - mitad.x, Mathf.Abs(p.y - centro.y) - mitad.y);
        return Mathf.Max(d.x, d.y);
    }

    private static float Capsula(Vector2 p, Vector2 a, Vector2 b, float radio)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return (p - (a + ab * t)).magnitude - radio;
    }
}
