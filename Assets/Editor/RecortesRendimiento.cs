using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Los primeros recortes de la lista ordenada de docs/Entrega5.md (bloque J), para aplicarlos y deshacerlos con un clic
/// el día del visor (Tools > Boxeo > Rendimiento). No se aplican solos: se deciden con los FPS del Quest, de uno en uno,
/// compilando y midiendo el nivel peor después de cada uno (docs/Protocolo_Rendimiento.md, "Si no se cumple").
///
/// 1. Quitar la sombra a todas las luces que no son direccionales, en las escenas de Build Settings.
/// 2. Mipmap Limit = 1 en la calidad Medium (la de Android): texturas a la mitad de lado.
/// 3. Foveación fija al iniciar: el componente FoveacionFija en el objeto de OVRManager de cada escena.
///
/// Cada recorte apunta el valor anterior de lo que cambia en ProjectSettings/RecortesRendimiento.json, y Deshacer lo
/// devuelve a ese valor exacto y guarda; cuando no queda ningún recorte aplicado, el archivo se borra, así que aplicar y
/// deshacer deja el repositorio igual. Los recortes que tocan escenas las abren, cambian y guardan de una en una, y al
/// final vuelven a dejar abiertas las que había: antes hay que guardar o descartar los cambios de las escenas abiertas.
/// </summary>
public static class RecortesRendimiento
{
    private const string Archivo = "ProjectSettings/RecortesRendimiento.json";
    private const string Menu = "Tools/Boxeo/Rendimiento/";
    private const string Calidad = "Medium"; // nivel 2, el de Android
    private const int MipmapRecortado = 1;

    [Serializable]
    private class Sombra
    {
        public string escena;
        public string luz;        // GlobalObjectId
        public string objeto;     // ruta en la jerarquía, solo para leer el archivo
        public int sombras;       // LightShadows anterior
        public bool revertir;     // la luz es de un prefab y no tenía override: se revierte en vez de escribir el valor
    }

    [Serializable]
    private class Registro
    {
        public List<Sombra> sombras = new List<Sombra>();
        public bool mipmap;
        public int mipmapAnterior;
        public List<string> foveacion = new List<string>(); // escenas donde se añadió FoveacionFija
        public bool Vacio => sombras.Count == 0 && !mipmap && foveacion.Count == 0;
    }

    // ---------------------------------------------------------------- recorte 1: sombras

    [MenuItem(Menu + "Recorte 1: quitar sombras (salvo la direccional)", false, 1)]
    private static void AplicarSombras()
    {
        var reg = Leer();
        int luces = 0, activas = 0;
        bool ok = false;
        try { ok = PorEscena(escena =>
        {
            bool cambio = false;
            foreach (Light luz in Componentes<Light>(escena))
            {
                if (luz.type == LightType.Directional || luz.shadows == LightShadows.None) continue;
                var prop = new SerializedObject(luz).FindProperty("m_Shadows.m_Type");
                reg.sombras.Add(new Sombra
                {
                    escena = escena.path,
                    luz = GlobalObjectId.GetGlobalObjectIdSlow(luz).ToString(),
                    objeto = RutaJerarquia(luz.transform),
                    sombras = (int)luz.shadows,
                    revertir = PrefabUtility.IsPartOfPrefabInstance(luz) && !prop.prefabOverride,
                });
                luz.shadows = LightShadows.None;
                EditorUtility.SetDirty(luz);
                luces++;
                if (luz.isActiveAndEnabled) activas++;
                cambio = true;
            }
            return cambio;
        }); }
        finally { Escribir(reg); } // aunque falle a mitad, queda apuntado lo que ya se guardó
        if (ok) Debug.Log($"Recorte 1 aplicado: sin sombra en {luces} luces ({activas} activas) de {reg.sombras.Select(s => s.escena).Distinct().Count()} escenas. Deshacer: {Menu}Deshacer recorte 1");
    }

    [MenuItem(Menu + "Recorte 1: quitar sombras (salvo la direccional)", true)]
    private static bool PuedeAplicarSombras() => !EditorApplication.isPlayingOrWillChangePlaymode && Leer().sombras.Count == 0;

    [MenuItem(Menu + "Deshacer recorte 1", false, 2)]
    private static void DeshacerSombras()
    {
        var reg = Leer();
        var pendientes = new List<Sombra>(reg.sombras);
        int restauradas = 0;
        bool ok = false;
        try { ok = PorEscena(escena =>
        {
            bool cambio = false;
            foreach (Sombra s in pendientes.Where(s => s.escena == escena.path).ToList())
            {
                Light luz = GlobalObjectId.TryParse(s.luz, out GlobalObjectId id)
                    ? GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as Light : null;
                if (luz == null)
                {
                    Debug.LogWarning($"Recorte 1: no se encontró la luz {s.objeto} en {s.escena}; se deja como está.");
                    continue;
                }
                if (s.revertir)
                    PrefabUtility.RevertPropertyOverride(new SerializedObject(luz).FindProperty("m_Shadows.m_Type"), InteractionMode.AutomatedAction);
                else
                    luz.shadows = (LightShadows)s.sombras;
                EditorUtility.SetDirty(luz);
                pendientes.Remove(s);
                restauradas++;
                cambio = true;
            }
            return cambio;
        }, reg.sombras.Select(s => s.escena).Distinct()); }
        finally
        {
            reg.sombras = pendientes;
            Escribir(reg);
        }
        if (!ok) return;
        Debug.Log($"Recorte 1 deshecho: {restauradas} luces con su sombra de antes{(pendientes.Count > 0 ? $"; {pendientes.Count} sin encontrar (siguen en {Archivo})" : "")}.");
    }

    [MenuItem(Menu + "Deshacer recorte 1", true)]
    private static bool PuedeDeshacerSombras() => !EditorApplication.isPlayingOrWillChangePlaymode && Leer().sombras.Count > 0;

    // ---------------------------------------------------------------- recorte 2: mipmaps

    [MenuItem(Menu + "Recorte 2: Mipmap Limit 1 en Medium", false, 21)]
    private static void AplicarMipmap()
    {
        var reg = Leer();
        if (!CambiarCalidad("globalTextureMipmapLimit", MipmapRecortado, out int anterior)) return;
        reg.mipmap = true;
        reg.mipmapAnterior = anterior;
        Escribir(reg);
        Debug.Log($"Recorte 2 aplicado: Mipmap Limit de {Calidad} {anterior} → {MipmapRecortado} (texturas a la mitad de lado en Android).");
    }

    [MenuItem(Menu + "Recorte 2: Mipmap Limit 1 en Medium", true)]
    private static bool PuedeAplicarMipmap() => !EditorApplication.isPlayingOrWillChangePlaymode && !Leer().mipmap;

    [MenuItem(Menu + "Deshacer recorte 2", false, 22)]
    private static void DeshacerMipmap()
    {
        var reg = Leer();
        if (!CambiarCalidad("globalTextureMipmapLimit", reg.mipmapAnterior, out _)) return;
        reg.mipmap = false;
        Escribir(reg);
        Debug.Log($"Recorte 2 deshecho: Mipmap Limit de {Calidad} = {reg.mipmapAnterior}.");
    }

    [MenuItem(Menu + "Deshacer recorte 2", true)]
    private static bool PuedeDeshacerMipmap() => !EditorApplication.isPlayingOrWillChangePlaymode && Leer().mipmap;

    // ---------------------------------------------------------------- recorte 3: foveación

    [MenuItem(Menu + "Recorte 3: foveación fija al iniciar", false, 41)]
    private static void AplicarFoveacion()
    {
        var reg = Leer();
        bool ok = false;
        try { ok = PorEscena(escena =>
        {
            OVRManager manager = Componentes<OVRManager>(escena).FirstOrDefault();
            if (manager == null)
            {
                Debug.LogWarning($"Recorte 3: {escena.path} no tiene OVRManager; se salta.");
                return false;
            }
            if (manager.GetComponent<FoveacionFija>() != null) return false;
            manager.gameObject.AddComponent<FoveacionFija>();
            reg.foveacion.Add(escena.path);
            return true;
        }); }
        finally { Escribir(reg); }
        if (ok) Debug.Log($"Recorte 3 aplicado: FoveacionFija en {reg.foveacion.Count} escenas (nivel High, fijo; se cambia en el componente).");
    }

    [MenuItem(Menu + "Recorte 3: foveación fija al iniciar", true)]
    private static bool PuedeAplicarFoveacion() => !EditorApplication.isPlayingOrWillChangePlaymode && Leer().foveacion.Count == 0;

    [MenuItem(Menu + "Deshacer recorte 3", false, 42)]
    private static void DeshacerFoveacion()
    {
        var reg = Leer();
        var pendientes = new List<string>(reg.foveacion);
        bool ok = false;
        try { ok = PorEscena(escena =>
        {
            pendientes.Remove(escena.path);
            var componentes = Componentes<FoveacionFija>(escena).ToList();
            foreach (FoveacionFija f in componentes) UnityEngine.Object.DestroyImmediate(f);
            return componentes.Count > 0;
        }, reg.foveacion); }
        finally
        {
            reg.foveacion = pendientes;
            Escribir(reg);
        }
        if (!ok) return;
        Debug.Log("Recorte 3 deshecho: FoveacionFija quitado de las escenas.");
    }

    [MenuItem(Menu + "Deshacer recorte 3", true)]
    private static bool PuedeDeshacerFoveacion() => !EditorApplication.isPlayingOrWillChangePlaymode && Leer().foveacion.Count > 0;

    // ---------------------------------------------------------------- estado

    [MenuItem(Menu + "Ver recortes aplicados", false, 61)]
    private static void Estado()
    {
        var reg = Leer();
        var calidad = ObjetoCalidad();
        Debug.Log("Recortes de rendimiento aplicados:\n" +
                  $"1. Sombras: {(reg.sombras.Count > 0 ? $"sí, {reg.sombras.Count} luces en {reg.sombras.Select(s => s.escena).Distinct().Count()} escenas" : "no")}\n" +
                  $"2. Mipmap Limit en {Calidad}: {(reg.mipmap ? $"sí (antes {reg.mipmapAnterior})" : "no")}; ahora {PropiedadCalidad(calidad, "globalTextureMipmapLimit")?.intValue}\n" +
                  $"3. Foveación: {(reg.foveacion.Count > 0 ? $"sí, en {reg.foveacion.Count} escenas" : "no")}\n" +
                  $"MSAA de {Calidad}: {PropiedadCalidad(calidad, "antiAliasing")?.intValue}×");
    }

    // ---------------------------------------------------------------- ayudas

    private static Registro Leer()
    {
        if (!File.Exists(Archivo)) return new Registro();
        try { return JsonUtility.FromJson<Registro>(File.ReadAllText(Archivo)) ?? new Registro(); }
        catch (Exception e)
        {
            Debug.LogError($"No se pudo leer {Archivo}: {e.Message}");
            return new Registro();
        }
    }

    private static void Escribir(Registro reg)
    {
        if (reg.Vacio)
        {
            if (File.Exists(Archivo)) File.Delete(Archivo);
            return;
        }
        File.WriteAllText(Archivo, JsonUtility.ToJson(reg, true));
    }

    /// <summary>Abre cada escena (todas las de Build Settings, o las indicadas), aplica el cambio y la guarda si cambió.</summary>
    private static bool PorEscena(Func<Scene, bool> cambiar, IEnumerable<string> soloEstas = null)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Los recortes no se aplican en Play.");
            return false;
        }
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            if (!SceneManager.GetSceneAt(i).isDirty) continue;
            Debug.LogError($"La escena {SceneManager.GetSceneAt(i).name} tiene cambios sin guardar: guárdala o descártala antes de aplicar o deshacer un recorte.");
            return false;
        }
        var rutas = soloEstas?.ToList() ?? EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToList();
        SceneSetup[] abiertas = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            foreach (string ruta in rutas)
            {
                Scene escena = EditorSceneManager.OpenScene(ruta, OpenSceneMode.Single);
                if (!cambiar(escena)) continue;
                EditorSceneManager.MarkSceneDirty(escena);
                EditorSceneManager.SaveScene(escena);
            }
        }
        finally
        {
            if (abiertas.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(abiertas);
        }
        return true;
    }

    private static IEnumerable<T> Componentes<T>(Scene escena) where T : Component =>
        escena.GetRootGameObjects().SelectMany(raiz => raiz.GetComponentsInChildren<T>(true));

    private static string RutaJerarquia(Transform t) => t.parent == null ? t.name : RutaJerarquia(t.parent) + "/" + t.name;

    private static SerializedObject ObjetoCalidad() =>
        new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);

    private static SerializedProperty PropiedadCalidad(SerializedObject calidad, string campo)
    {
        SerializedProperty niveles = calidad.FindProperty("m_QualitySettings");
        for (int i = 0; i < niveles.arraySize; i++)
        {
            SerializedProperty nivel = niveles.GetArrayElementAtIndex(i);
            if (nivel.FindPropertyRelative("name").stringValue == Calidad) return nivel.FindPropertyRelative(campo);
        }
        return null;
    }

    /// <summary>Cambia un campo del nivel de calidad Medium y guarda QualitySettings.asset.</summary>
    private static bool CambiarCalidad(string campo, int valor, out int anterior)
    {
        anterior = 0;
        SerializedObject calidad = ObjetoCalidad();
        SerializedProperty prop = PropiedadCalidad(calidad, campo);
        if (prop == null)
        {
            Debug.LogError($"No se encontró {campo} en la calidad {Calidad}.");
            return false;
        }
        anterior = prop.intValue;
        prop.intValue = valor;
        calidad.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        return true;
    }
}
