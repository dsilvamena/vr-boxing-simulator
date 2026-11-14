using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Tools > Boxeo > Poner reglamento: monta en las cinco escenas de pelea lo que necesita el reglamento de boxeo del
/// pulido final (se puede repetir):
/// - asigna al Arbitro su voz (VocesArbitroBuilder: números y frases de Audio/Arbitro) y a ControlRondas la claqueta de
///   los diez segundos (Audio/Efectos, de Tools/sintetizar_audio.py);
/// - coloca el marcador luminoso (MarcadorRing): un panel con el round, el reloj y los nombres en cada lado de la
///   estructura de focos, mirando al ring;
/// - fija en cada perfil en qué número de la cuenta se levanta el rival tras una caída (cuentaKnockdown): ninguno en el
///   10, que sería nocaut. Los más duros se levantan antes; la cuenta sigue igualmente hasta 8 (Arbitro).
/// </summary>
public static class ReglamentoBuilder
{
    private const string Audio = "Assets/_Project/Audio/Efectos/";
    private const string Perfiles = "Assets/_Project/Data/Oponentes/";
    private const string RutaFuente = "Assets/_Project/Fonts/Bangers SDF.asset";
    private const string RutaPanel = "Assets/_Project/Art/Arena/Marcador_Panel.mat";

    // Perfil y número de la cuenta en el que ya está de pie
    private static readonly (string perfil, int dePieEn)[] Cuentas =
    {
        ("GlassJoe", 9), ("BaldBull", 8), ("SodaPopinski", 8), ("MrSandman", 7), ("MikeTyson", 6),
    };

    [MenuItem("Tools/Boxeo/Poner reglamento")]
    public static void Poner()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("No en Play."); return; }
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) { Debug.LogError("Guarda o descarta los cambios de las escenas abiertas antes."); return; }

        foreach ((string nombre, int dePieEn) in Cuentas)
        {
            var perfil = AssetDatabase.LoadAssetAtPath<PerfilOponente>($"{Perfiles}{nombre}.asset");
            if (perfil == null) { Debug.LogWarning($"Falta el perfil {nombre}."); continue; }
            perfil.cuentaKnockdown = dePieEn;
            EditorUtility.SetDirty(perfil);
        }

        SceneSetup[] abiertas = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes.Where(s => s.enabled))
            {
                Scene escena = EditorSceneManager.OpenScene(s.path, OpenSceneMode.Single);
                var arbitro = Object.FindAnyObjectByType<Arbitro>();
                var rondas = Object.FindAnyObjectByType<ControlRondas>();
                if (arbitro == null || rondas == null) continue;

                VocesArbitroBuilder.Asignar(arbitro);

                var so = new SerializedObject(rondas);
                so.FindProperty("avisoDiezSegundos").objectReferenceValue = Clip("Aviso_Diez_Segundos");
                so.ApplyModifiedPropertiesWithoutUndo();

                PonerMarcador(escena, rondas);
                EditorSceneManager.MarkSceneDirty(escena);
                EditorSceneManager.SaveScene(escena);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Reglamento puesto en las escenas de pelea.");
        }
        finally
        {
            if (abiertas.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(abiertas);
        }
    }

    private static AudioClip Clip(string nombre)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Audio}{nombre}.wav");
        if (clip == null) Debug.LogWarning($"Falta {nombre}.wav: genera los sonidos con Tools/sintetizar_audio.py.");
        return clip;
    }

    // Cuatro paneles colgados de los travesaños de la estructura de focos, uno por lado, inclinados hacia el ring
    private static void PonerMarcador(Scene escena, ControlRondas rondas)
    {
        GameObject edificio = escena.GetRootGameObjects().FirstOrDefault(g => g.name == "Building");
        if (edificio == null) return;
        // Los travesaños son los Truss tumbados: más largos que altos
        Bounds[] travesanos = edificio.transform.Cast<Transform>()
            .Where(t => t.name.StartsWith("Truss") && t.GetComponent<Renderer>() != null)
            .Select(t => t.GetComponent<Renderer>().bounds).Where(b => b.size.y < 2f).ToArray();
        if (travesanos.Length == 0) return;

        GameObject viejo = escena.GetRootGameObjects().FirstOrDefault(g => g.name == "MarcadorRing");
        if (viejo != null) Object.DestroyImmediate(viejo);
        var raiz = new GameObject("MarcadorRing");
        SceneManager.MoveGameObjectToScene(raiz, escena);
        int capa = LayerMask.NameToLayer(ArenaBuilder.CapaArena);
        var fuente = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RutaFuente);
        var textos = new System.Collections.Generic.List<TextMeshPro>();

        foreach (Bounds b in travesanos)
        {
            Vector3 centro = b.center;
            // Mira hacia el centro del ring y cuelga por debajo del travesaño, un poco hacia dentro
            Vector3 haciaRing = new Vector3(-centro.x, 0f, -centro.z).normalized;
            var panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(panel.GetComponent<Collider>());
            panel.name = "Panel";
            panel.transform.SetParent(raiz.transform, false);
            panel.transform.position = centro + Vector3.down * (b.extents.y + 0.62f) + haciaRing * 0.2f;
            // El quad se ve desde su -Z: se orienta de espaldas al ring y se inclina hacia abajo para leerlo desde la lona
            panel.transform.rotation = Quaternion.LookRotation(-haciaRing) * Quaternion.Euler(-22f, 0f, 0f);
            panel.transform.localScale = new Vector3(4.6f, 1.15f, 1f);
            if (capa >= 0) panel.layer = capa;
            var render = panel.GetComponent<MeshRenderer>();
            render.sharedMaterial = MaterialPanel();
            render.shadowCastingMode = ShadowCastingMode.Off;
            render.receiveShadows = false;

            var go = new GameObject("Texto", typeof(RectTransform), typeof(TextMeshPro));
            go.transform.SetParent(raiz.transform, false);
            go.transform.SetPositionAndRotation(panel.transform.position - panel.transform.forward * 0.02f, panel.transform.rotation);
            if (capa >= 0) go.layer = capa;
            ((RectTransform)go.transform).sizeDelta = new Vector2(4.4f, 1.05f);
            var texto = go.GetComponent<TextMeshPro>();
            if (fuente != null) texto.font = fuente;
            texto.fontSize = 5.4f;
            texto.alignment = TextAlignmentOptions.Center;
            texto.textWrappingMode = TextWrappingModes.NoWrap;
            texto.lineSpacing = -18f;
            texto.color = new Color(1f, 0.78f, 0.25f);
            texto.text = "ROUND 1\n<size=52%><color=#FFFFFF>TÚ  VS  RIVAL</color></size>";
            textos.Add(texto);
        }

        var marcador = raiz.AddComponent<MarcadorRing>();
        var so = new SerializedObject(marcador);
        SerializedProperty lista = so.FindProperty("textos");
        lista.arraySize = textos.Count;
        for (int i = 0; i < textos.Count; i++) lista.GetArrayElementAtIndex(i).objectReferenceValue = textos[i];
        so.FindProperty("rondas").objectReferenceValue = rondas;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Fondo del panel: negro sin luces (un panel LED apagado alrededor del texto)
    private static Material MaterialPanel()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(RutaPanel);
        if (material != null) return material;
        material = new Material(Shader.Find("Unlit/Color")) { color = new Color(0.02f, 0.02f, 0.03f) };
        AssetDatabase.CreateAsset(material, RutaPanel);
        return material;
    }
}
