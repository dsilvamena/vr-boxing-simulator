using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Menú Tools/Boxeo para convertir un personaje de la escena (un .glb sin esqueleto) en un Oponente golpeable.
/// Las hitboxes se calculan por alturas sobre los vértices del modelo: son un punto de partida, ajústalas a ojo.
/// </summary>
public static class OponenteBuilder
{
    private const string CarpetaPerfiles = "Assets/_Project/Data/Oponentes";

    [MenuItem("Tools/Boxeo/Configurar oponente seleccionado")]
    public static void Configurar()
    {
        var raiz = Selection.activeGameObject;
        if (raiz == null || raiz.transform.childCount == 0)
        {
            Debug.LogWarning("Selecciona el objeto raíz del personaje (el que tiene el modelo como hijo).");
            return;
        }
        Transform cuerpo = raiz.transform.GetChild(0);
        Undo.RegisterFullObjectHierarchyUndo(raiz, "Configurar oponente");

        // Kinematic: no lo mueven las físicas, solo la reacción
        var rb = Obtener<Rigidbody>(raiz);
        rb.isKinematic = true;
        rb.useGravity = false;

        foreach (var col in raiz.GetComponentsInChildren<Collider>())
            if (!col.isTrigger && col.GetComponent<ZonaGolpe>() == null) col.enabled = false;

        var audio = Obtener<AudioSource>(raiz);
        audio.playOnAwake = false;
        audio.spatialBlend = 1f;

        var oponente = Obtener<Oponente>(raiz);
        var so = new SerializedObject(oponente);
        so.FindProperty("perfil").objectReferenceValue = PerfilPara(raiz.name);
        so.FindProperty("audioSource").objectReferenceValue = audio;
        so.ApplyModifiedProperties();

        var reaccion = Obtener<ReaccionProcedural>(raiz);
        so = new SerializedObject(reaccion);
        so.FindProperty("cuerpo").objectReferenceValue = cuerpo;
        so.ApplyModifiedProperties();

        CrearHitboxes(cuerpo);
        Selection.activeGameObject = raiz;
    }

    // Sin "??": en el Editor GetComponent devuelve un null falso que no lo dispara
    private static T Obtener<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : Undo.AddComponent<T>(go);
    }

    private static PerfilOponente PerfilPara(string nombre)
    {
        string ruta = $"{CarpetaPerfiles}/{nombre}.asset";
        var perfil = AssetDatabase.LoadAssetAtPath<PerfilOponente>(ruta);
        if (perfil != null) return perfil;

        Directory.CreateDirectory(CarpetaPerfiles);
        perfil = ScriptableObject.CreateInstance<PerfilOponente>();
        AssetDatabase.CreateAsset(perfil, ruta);
        AssetDatabase.SaveAssets();
        return perfil;
    }

    private static void CrearHitboxes(Transform cuerpo)
    {
        for (int i = cuerpo.childCount - 1; i >= 0; i--)
            if (cuerpo.GetChild(i).GetComponent<ZonaGolpe>() != null) Undo.DestroyObjectImmediate(cuerpo.GetChild(i).gameObject);

        var puntos = new List<Vector3>();
        foreach (var mf in cuerpo.GetComponentsInChildren<MeshFilter>())
            if (mf.sharedMesh != null)
                foreach (var v in mf.sharedMesh.vertices) puntos.Add(mf.transform.TransformPoint(v));
        if (puntos.Count == 0) { Debug.LogWarning("El modelo no tiene mallas."); return; }

        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (var p in puntos) { minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y); }
        float alto = maxY - minY;

        // Cabeza: el 20% superior. Torso: del 42% al 78% de la altura, sin los brazos (se estrecha la caja)
        Franja(puntos, minY + alto * 0.8f, maxY, out Vector3 centroCabeza, out Vector3 tamCabeza);
        Franja(puntos, minY + alto * 0.42f, minY + alto * 0.78f, out Vector3 centroTorso, out Vector3 tamTorso);

        Hitbox<SphereCollider>("Hitbox_Cabeza", cuerpo, centroCabeza, TipoZona.Cabeza, 1.5f)
            .radius = (tamCabeza.x + tamCabeza.y + tamCabeza.z) / 6f;
        Hitbox<BoxCollider>("Hitbox_Torso", cuerpo, centroTorso, TipoZona.Torso, 1f)
            .size = new Vector3(tamTorso.x * 0.5f, tamTorso.y, tamTorso.z * 0.6f);
    }

    /// <summary>Centroide de los vértices entre dos alturas y el tamaño de su caja.</summary>
    private static void Franja(List<Vector3> puntos, float y0, float y1, out Vector3 centro, out Vector3 tamano)
    {
        Vector3 suma = Vector3.zero;
        int n = 0;
        var caja = new Bounds();
        foreach (var p in puntos)
        {
            if (p.y < y0 || p.y > y1) continue;
            if (n == 0) caja = new Bounds(p, Vector3.zero); else caja.Encapsulate(p);
            suma += p;
            n++;
        }
        centro = n > 0 ? suma / n : caja.center;
        centro.y = (y0 + y1) * 0.5f;
        tamano = caja.size;
    }

    private static T Hitbox<T>(string nombre, Transform padre, Vector3 posicion, TipoZona tipo, float multiplicador) where T : Collider
    {
        var go = new GameObject(nombre);
        Undo.RegisterCreatedObjectUndo(go, "Crear hitbox");
        go.transform.SetParent(padre, false);
        // Alineada con el mundo y a escala 1, para que los tamaños del collider estén en metros
        go.transform.SetPositionAndRotation(posicion, Quaternion.identity);
        go.transform.localScale = Vector3.one / padre.lossyScale.x;
        var col = go.AddComponent<T>();
        col.isTrigger = true;
        go.AddComponent<ZonaGolpe>().Configurar(tipo, multiplicador);
        return col;
    }
}
