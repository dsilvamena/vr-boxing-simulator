using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Tools > Boxeo > Poner pulido de combate: monta en las escenas de Build Settings lo que hace que la pelea se vea y se
/// sienta más real, sin cambiar las reglas del combate:
/// - CuerpoVivo en el Animator de cada rival (mirada, sacudida por golpe, inclinación al esquivar, cansancio) y de los
///   boxeadores humanoides de adorno (solo mirada y respiración).
/// - EfectosGolpe junto a cada Oponente (salpicadura, destello de bloqueo, cámara lenta en las caídas), con su material.
/// - El silbido del guante en cada Puno y el resoplido de ataque, el desplazamiento lateral y el retroceso por golpe en
///   los perfiles de los rivales (más ligero y lento Glass Joe, más pesado y rápido de pies Tyson).
/// - El estado Burla en el controlador base (GlassJoe.controller) con el clip Burla de Soda Popinski, que heredan los
///   override controllers: el rival se burla cuando tumba al jugador. Glass Joe no lo hace (probabilidadBurla 0).
/// Crea el material y la textura de las partículas si faltan. No cambia nada que ya esté puesto: se puede repetir.
/// </summary>
public static class PulidoBuilder
{
    private const string CarpetaEfectos = "Assets/_Project/Art/Efectos";
    private const string RutaTextura = CarpetaEfectos + "/Particula_Suave.png";
    private const string RutaMaterial = CarpetaEfectos + "/Particula_Suave.mat";
    private const string CarpetaAudio = "Assets/_Project/Audio/Efectos";
    private const string CarpetaPerfiles = "Assets/_Project/Data/Oponentes";

    private const string RutaControlador = "Assets/_Project/Art/Characters/GlassJoe/Animations/GlassJoe.controller";
    private const string RutaBurla = "Assets/_Project/Art/Characters/SodaPopinski/Animations/SodaPopinski@Burla.fbx";

    // Perfil → (velocidadLateral m/s, retrocesoPorGolpe m, probabilidadBurla)
    private static readonly (string perfil, float lateral, float retroceso, float burla)[] Pies =
    {
        ("GlassJoe", 0.12f, 0.14f, 0f),
        ("BaldBull", 0.16f, 0.08f, 1f),
        ("SodaPopinski", 0.20f, 0.07f, 1f),
        ("MrSandman", 0.26f, 0.07f, 1f),
        ("MikeTyson", 0.36f, 0.05f, 1f),
    };

    [MenuItem("Tools/Boxeo/Poner pulido de combate")]
    private static void Poner()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("No en Play."); return; }
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) { Debug.LogError("Guarda o descarta los cambios de las escenas abiertas antes."); return; }

        Material material = MaterialParticulas();
        AudioClip[] aire = Clips("Golpe_Aire_");
        AudioClip[] esfuerzo = Clips("Rival_Esfuerzo_");
        int perfiles = ConfigurarPerfiles(esfuerzo);
        PonerEstadoBurla();

        SceneSetup[] abiertas = EditorSceneManager.GetSceneManagerSetup();
        int cambios = 0;
        try
        {
            foreach (string ruta in EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path))
            {
                Scene escena = EditorSceneManager.OpenScene(ruta, OpenSceneMode.Single);
                int antes = cambios;
                foreach (GameObject raiz in escena.GetRootGameObjects())
                {
                    foreach (Animator animator in raiz.GetComponentsInChildren<Animator>(true))
                    {
                        // El réferi tiene su propio gesto (CuerpoArbitro) y no entra aquí
                        if (animator.avatar == null || !animator.avatar.isHuman) continue;
                        if (animator.GetComponentInParent<CuerpoArbitro>() != null) continue;
                        if (animator.GetComponent<CuerpoVivo>() != null) continue;
                        PasosBuilderIK(animator);
                        animator.gameObject.AddComponent<CuerpoVivo>();
                        cambios++;
                    }
                    foreach (Oponente oponente in raiz.GetComponentsInChildren<Oponente>(true))
                    {
                        var efectos = oponente.GetComponent<EfectosGolpe>();
                        if (efectos == null) { efectos = oponente.gameObject.AddComponent<EfectosGolpe>(); cambios++; }
                        cambios += Asignar(efectos, "material", material);
                    }
                    foreach (Puno puno in raiz.GetComponentsInChildren<Puno>(true))
                        cambios += AsignarClips(puno, "sonidosAire", aire);
                }
                if (cambios == antes) continue;
                EditorSceneManager.MarkSceneDirty(escena);
                EditorSceneManager.SaveScene(escena);
            }
        }
        finally
        {
            if (abiertas.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(abiertas);
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"Pulido de combate: {cambios} cambios en las escenas, {perfiles} perfiles actualizados.");
    }

    // La mirada usa la IK del Animator: hace falta IK Pass en la capa base
    private static void PasosBuilderIK(Animator animator)
    {
        RuntimeAnimatorController controlador = animator.runtimeAnimatorController;
        if (controlador is AnimatorOverrideController o) controlador = o.runtimeAnimatorController;
        if (controlador is not UnityEditor.Animations.AnimatorController c || c.layers.Length == 0 || c.layers[0].iKPass) return;
        UnityEditor.Animations.AnimatorControllerLayer[] capas = c.layers;
        capas[0].iKPass = true;
        c.layers = capas;
        EditorUtility.SetDirty(c);
    }

    private static int ConfigurarPerfiles(AudioClip[] esfuerzo)
    {
        int cambiados = 0;
        foreach ((string nombre, float lateral, float retroceso, float burla) in Pies)
        {
            var perfil = AssetDatabase.LoadAssetAtPath<PerfilOponente>($"{CarpetaPerfiles}/{nombre}.asset");
            if (perfil == null) { Debug.LogWarning($"Falta el perfil {nombre}."); continue; }
            bool cambio = false;
            if (!Mathf.Approximately(perfil.velocidadLateral, lateral)) { perfil.velocidadLateral = lateral; cambio = true; }
            if (!Mathf.Approximately(perfil.retrocesoPorGolpe, retroceso)) { perfil.retrocesoPorGolpe = retroceso; cambio = true; }
            if (!Mathf.Approximately(perfil.probabilidadBurla, burla)) { perfil.probabilidadBurla = burla; cambio = true; }
            if (esfuerzo.Length > 0 && (perfil.sonidosAtaque == null || !perfil.sonidosAtaque.SequenceEqual(esfuerzo)))
            {
                perfil.sonidosAtaque = esfuerzo;
                cambio = true;
            }
            if (!cambio) continue;
            EditorUtility.SetDirty(perfil);
            cambiados++;
        }
        return cambiados;
    }

    // Los override controllers sustituyen clips, no estados: el estado va en el controlador base y lo heredan todos
    private static void PonerEstadoBurla()
    {
        var controlador = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(RutaControlador);
        AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(RutaBurla).OfType<AnimationClip>().FirstOrDefault(c => c.name == "Burla");
        if (controlador == null || clip == null) { Debug.LogWarning("Falta GlassJoe.controller o el clip Burla."); return; }
        UnityEditor.Animations.AnimatorStateMachine maquina = controlador.layers[0].stateMachine;
        if (maquina.states.Any(s => s.state.name == "Burla")) return;

        UnityEditor.Animations.AnimatorState idle = maquina.states.First(s => s.state.name == "Idle").state;
        UnityEditor.Animations.AnimatorState estado = maquina.AddState("Burla");
        estado.motion = clip;
        UnityEditor.Animations.AnimatorStateTransition vuelta = estado.AddTransition(idle);
        vuelta.hasExitTime = true;
        vuelta.exitTime = 0.9f;
        vuelta.hasFixedDuration = true;
        vuelta.duration = 0.3f;
        EditorUtility.SetDirty(controlador);
        Debug.Log("Estado Burla añadido a GlassJoe.controller.");
    }

    private static AudioClip[] Clips(string prefijo) =>
        AssetDatabase.FindAssets($"{prefijo} t:AudioClip", new[] { CarpetaAudio })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(r => Path.GetFileName(r).StartsWith(prefijo))
            .OrderBy(r => r)
            .Select(AssetDatabase.LoadAssetAtPath<AudioClip>)
            .ToArray();

    private static int Asignar(Object destino, string campo, Object valor)
    {
        var so = new SerializedObject(destino);
        SerializedProperty p = so.FindProperty(campo);
        if (p.objectReferenceValue == valor) return 0;
        p.objectReferenceValue = valor;
        so.ApplyModifiedPropertiesWithoutUndo();
        return 1;
    }

    private static int AsignarClips(Object destino, string campo, AudioClip[] clips)
    {
        if (clips.Length == 0) return 0;
        var so = new SerializedObject(destino);
        SerializedProperty p = so.FindProperty(campo);
        bool igual = p.arraySize == clips.Length;
        for (int i = 0; igual && i < clips.Length; i++) igual = p.GetArrayElementAtIndex(i).objectReferenceValue == clips[i];
        if (igual) return 0;
        p.arraySize = clips.Length;
        for (int i = 0; i < clips.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        return 1;
    }

    /// <summary>Punto blanco de bordes suaves y su material de partículas (mezcla alfa, sin luz): se crean una vez.</summary>
    private static Material MaterialParticulas()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(RutaMaterial);
        if (material != null) return material;

        Directory.CreateDirectory(CarpetaEfectos);
        const int lado = 64;
        var textura = new Texture2D(lado, lado, TextureFormat.RGBA32, false);
        for (int y = 0; y < lado; y++)
        for (int x = 0; x < lado; x++)
        {
            float r = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), Vector2.one * (lado * 0.5f)) / (lado * 0.5f);
            float alfa = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.35f, 1f, r));
            textura.SetPixel(x, y, new Color(1f, 1f, 1f, alfa));
        }
        File.WriteAllBytes(RutaTextura, textura.EncodeToPNG());
        Object.DestroyImmediate(textura);
        AssetDatabase.ImportAsset(RutaTextura);
        var importador = (TextureImporter)AssetImporter.GetAtPath(RutaTextura);
        importador.alphaIsTransparency = true;
        importador.wrapMode = TextureWrapMode.Clamp;
        importador.mipmapEnabled = true;
        importador.SaveAndReimport();

        Shader shader = Shader.Find("Mobile/Particles/Alpha Blended");
        if (shader == null) shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
        material = new Material(shader) { mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(RutaTextura) };
        AssetDatabase.CreateAsset(material, RutaMaterial);
        return material;
    }
}
