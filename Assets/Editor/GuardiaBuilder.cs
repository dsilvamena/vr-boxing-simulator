using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Menús para las guardias grabadas (GrabacionGuardia):
/// - Importar guardias grabadas: convierte en assets los JSON de GrabadoraGuardia (persistentDataPath/Guardias en el Editor,
///   o una carpeta con los que se sacaron del Quest con adb pull).
/// - Crear guardia provisional: una subida sintetizada en arco que termina en la guardia fija de DefensaOponente. Sirve
///   mientras no haya una grabación real con el visor; no sale de ninguna persona.
/// </summary>
public static class GuardiaBuilder
{
    private const string RutaProvisional = GrabadoraGuardia.CarpetaAssets + "/Guardia_Provisional.asset";

    [MenuItem("Tools/Boxeo/Importar guardias grabadas")]
    public static void Importar()
    {
        string carpeta = Path.Combine(Application.persistentDataPath, GrabadoraGuardia.CarpetaJson);
        if (!Directory.Exists(carpeta) || Directory.GetFiles(carpeta, "*.json").Length == 0)
            carpeta = EditorUtility.OpenFolderPanel("Carpeta con las guardias grabadas (.json)", "", "");
        if (string.IsNullOrEmpty(carpeta)) return;
        Directory.CreateDirectory(GrabadoraGuardia.CarpetaAssets);
        int nuevas = 0;
        foreach (string archivo in Directory.GetFiles(carpeta, "*.json"))
        {
            string nombre = Path.GetFileNameWithoutExtension(archivo);
            string ruta = $"{GrabadoraGuardia.CarpetaAssets}/{nombre}.asset";
            if (AssetDatabase.LoadAssetAtPath<GrabacionGuardia>(ruta) != null) continue;
            var grabacion = ScriptableObject.CreateInstance<GrabacionGuardia>();
            JsonUtility.FromJsonOverwrite(File.ReadAllText(archivo), grabacion);
            if (!grabacion.Valida)
            {
                Debug.LogWarning($"{archivo}: la grabación no tiene una subida válida, no se importa.");
                continue;
            }
            AssetDatabase.CreateAsset(grabacion, ruta);
            nuevas++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"Guardias importadas: {nuevas} nuevas desde {carpeta} a {GrabadoraGuardia.CarpetaAssets}.");
    }

    [MenuItem("Tools/Boxeo/Crear guardia provisional")]
    public static GrabacionGuardia CrearProvisional()
    {
        var existente = AssetDatabase.LoadAssetAtPath<GrabacionGuardia>(RutaProvisional);
        if (existente != null) return existente;

        var grabacion = ScriptableObject.CreateInstance<GrabacionGuardia>();
        grabacion.origen = "Provisional, sintetizada sin visor: arco desde el pecho hasta la guardia fija de DefensaOponente " +
                           "(0,1; -0,05; 0,22). Reemplazar por una grabación real (GrabadoraGuardia).";
        grabacion.alturaCabeza = 1.45f;
        const int muestras = 33;
        const float duracion = 0.32f;
        for (int i = 0; i < muestras; i++)
        {
            float k = i / (muestras - 1f);
            float e = Mathf.SmoothStep(0f, 1f, k);
            // Sube desde el pecho, se cierra hacia la cara y se adelanta un poco a mitad de camino, como al cubrirse
            var derecha = new Vector3(
                Mathf.Lerp(0.16f, 0.1f, e),
                Mathf.Lerp(-0.4f, -0.05f, e),
                Mathf.Lerp(0.18f, 0.22f, e) + 0.06f * Mathf.Sin(Mathf.PI * e));
            grabacion.muestras.Add(new GrabacionGuardia.Muestra
            {
                t = k * duracion,
                derecha = derecha,
                izquierda = new Vector3(-derecha.x, derecha.y, derecha.z),
                rotacionIzquierda = Quaternion.identity,
                rotacionDerecha = Quaternion.identity,
            });
        }
        Directory.CreateDirectory(GrabadoraGuardia.CarpetaAssets);
        AssetDatabase.CreateAsset(grabacion, RutaProvisional);
        AssetDatabase.SaveAssets();
        Debug.Log($"Creada {RutaProvisional}");
        return grabacion;
    }
}
