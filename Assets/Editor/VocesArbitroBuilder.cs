using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Tools > Boxeo > Poner voces del réferi: asigna al Arbitro de cada escena de pelea los diez números y todas las frases
/// de Audio/Arbitro (generadas con Voice Studio por Tools/voces_arbitro.py). Cada lista toma los WAV cuyo nombre empieza
/// por su prefijo (Voz_Box_*, Voz_Seguir_*...), así que una frase nueva solo necesita su WAV con el prefijo correcto
/// y volver a ejecutar esto. Se puede repetir.
/// </summary>
public static class VocesArbitroBuilder
{
    public const string Carpeta = "Assets/_Project/Audio/Arbitro";

    private static readonly (string campo, string prefijo)[] Frases =
    {
        ("frasesBox", "Voz_Box_"),
        ("frasesSeguir", "Voz_Seguir_"),
        ("frasesEsquina", "Voz_Esquina_"),
        ("frasesNocaut", "Voz_Nocaut_"),
        ("frasesInstrucciones", "Voz_Instrucciones_"),
        ("frasesTiempo", "Voz_Tiempo_"),
        ("frasesDecision", "Voz_Decision_"),
    };

    [MenuItem("Tools/Boxeo/Poner voces del réferi")]
    public static void Poner()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("No en Play."); return; }
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) { Debug.LogError("Guarda o descarta los cambios de las escenas abiertas antes."); return; }

        SceneSetup[] abiertas = EditorSceneManager.GetSceneManagerSetup();
        int escenas = 0;
        try
        {
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes.Where(s => s.enabled))
            {
                Scene escena = EditorSceneManager.OpenScene(s.path, OpenSceneMode.Single);
                var arbitro = Object.FindAnyObjectByType<Arbitro>();
                if (arbitro == null) continue;
                Asignar(arbitro);
                EditorSceneManager.MarkSceneDirty(escena);
                EditorSceneManager.SaveScene(escena);
                escenas++;
            }
            Debug.Log($"Voces del réferi puestas en {escenas} escenas.");
        }
        finally
        {
            if (abiertas.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(abiertas);
        }
    }

    /// <summary>Rellena los números y las listas de frases del Arbitro (sin guardar la escena).</summary>
    public static void Asignar(Arbitro arbitro)
    {
        AudioClip[] clips = AssetDatabase.FindAssets("t:AudioClip", new[] { Carpeta })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => System.IO.Path.GetDirectoryName(p).Replace('\\', '/') == Carpeta)
            .OrderBy(p => p, System.StringComparer.Ordinal)
            .Select(AssetDatabase.LoadAssetAtPath<AudioClip>)
            .ToArray();

        var so = new SerializedObject(arbitro);
        Llenar(so.FindProperty("numeros"), Enumerable.Range(1, 10)
            .Select(n => clips.FirstOrDefault(c => c.name == $"Voz_Cuenta_{n:00}")).ToArray(), "numeros");
        foreach ((string campo, string prefijo) in Frases)
            Llenar(so.FindProperty(campo), clips.Where(c => c.name.StartsWith(prefijo)).ToArray(), campo);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Llenar(SerializedProperty lista, AudioClip[] clips, string campo)
    {
        if (clips.Length == 0 || clips.Any(c => c == null))
            Debug.LogWarning($"Arbitro.{campo}: faltan clips en {Carpeta} (genéralos con Tools/voces_arbitro.py).");
        lista.arraySize = clips.Length;
        for (int i = 0; i < clips.Length; i++) lista.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
    }
}
