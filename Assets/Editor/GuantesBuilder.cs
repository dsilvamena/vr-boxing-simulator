using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Menú Tools/Boxeo para poner los guantes en las manos en lugar de las esferas de los Puno.
/// El .glb trae los dos guantes en una sola malla con esqueleto (squash & stretch); aquí se separan por huesos
/// en dos mallas estáticas, con los nudillos hacia +Z, el centro del puño en el origen y tamaño real.
/// </summary>
public static class GuantesBuilder
{
    private const string RutaGlb = "Assets/_Project/Art/boxing-gloves/source/Boxing gloves.glb";
    private const string CarpetaMallas = "Assets/_Project/Art/boxing-gloves";
    /// <summary>Largo del guante, del puño al final del puño elástico, en metros.</summary>
    private const float Largo = 0.3f;
    /// <summary>Altura (0 = puño elástico, 1 = nudillos) que queda en el origen: el centro del puño.</summary>
    private const float CentroPuno = 0.7f;
    /// <summary>Radio del collider del Puno, en metros: cubre el puño del guante.</summary>
    private const float RadioPuno = 0.07f;

    [MenuItem("Tools/Boxeo/Poner guantes en las manos")]
    public static void PonerGuantes()
    {
        var glb = AssetDatabase.LoadAssetAtPath<GameObject>(RutaGlb);
        var smr = glb != null ? glb.GetComponentInChildren<SkinnedMeshRenderer>() : null;
        if (smr == null) { Debug.LogWarning($"No se pudo cargar la malla de {RutaGlb}."); return; }

        Mesh izquierdo = Hornear(smr, true, "Guante_Izquierdo");
        Mesh derecho = Hornear(smr, false, "Guante_Derecho");

        foreach (var puno in Object.FindObjectsByType<Puno>())
        {
            var rig = puno.GetComponentInParent<OVRCameraRig>();
            bool esIzquierdo = rig != null && puno.transform.IsChildOf(rig.leftHandAnchor);
            Vestir(puno, esIzquierdo, esIzquierdo ? izquierdo : derecho, smr.sharedMaterials);
        }
    }

    private static void Vestir(Puno puno, bool esIzquierdo, Mesh malla, Material[] materiales)
    {
        GameObject go = puno.gameObject;
        Undo.RegisterFullObjectHierarchyUndo(go, "Poner guantes");

        // Fuera la esfera visible; el collider y el Rigidbody se quedan
        var render = go.GetComponent<MeshRenderer>();
        if (render != null) Undo.DestroyObjectImmediate(render);
        var filtro = go.GetComponent<MeshFilter>();
        if (filtro != null) Undo.DestroyObjectImmediate(filtro);

        // Escala 1 en el Puno para que el guante y el collider estén en metros
        go.transform.localScale = Vector3.one;
        var esfera = go.GetComponent<SphereCollider>();
        if (esfera != null)
        {
            esfera.radius = RadioPuno;
            esfera.center = Vector3.zero;
        }

        Transform viejo = go.transform.Find("Guante");
        if (viejo != null) Undo.DestroyObjectImmediate(viejo.gameObject);

        var guante = new GameObject("Guante");
        Undo.RegisterCreatedObjectUndo(guante, "Crear guante");
        guante.transform.SetParent(go.transform, false);
        // La malla tiene la palma hacia abajo; con el mando en la mano el puño queda vertical
        // (pulgar arriba, palma hacia dentro), así que se rueda 90° hacia el centro. Ajustable a mano en "Guante".
        guante.transform.localRotation = Quaternion.Euler(0f, 0f, esIzquierdo ? 90f : -90f);
        guante.AddComponent<MeshFilter>().sharedMesh = malla;
        guante.AddComponent<MeshRenderer>().sharedMaterials = materiales;
    }

    /// <summary>Copia los triángulos del guante de un lado (según el hueso con más peso) a una malla nueva.</summary>
    private static Mesh Hornear(SkinnedMeshRenderer smr, bool izquierdo, string nombre)
    {
        Mesh fuente = smr.sharedMesh;
        Transform[] huesos = smr.bones;
        BoneWeight[] pesos = fuente.boneWeights;
        Vector3[] posiciones = fuente.vertices;
        Vector3[] normales = fuente.normals;
        Vector2[] uvs = fuente.uv;

        var delLado = new bool[posiciones.Length];
        for (int i = 0; i < posiciones.Length; i++)
        {
            string hueso = huesos[pesos[i].boneIndex0].name;
            bool esL = hueso.EndsWith(".L") || hueso.EndsWith(".l");
            bool esR = hueso.EndsWith(".R") || hueso.EndsWith(".r");
            // Si el hueso no tiene lado (ROOT), se decide por el lado del vértice (en Unity el .L queda en -X)
            delLado[i] = esL || esR ? esL == izquierdo : (posiciones[i].x < 0f) == izquierdo;
        }

        var nuevoIndice = new Dictionary<int, int>();
        var pos = new List<Vector3>();
        var nor = new List<Vector3>();
        var uv = new List<Vector2>();
        var submallas = new List<List<int>>();
        for (int s = 0; s < fuente.subMeshCount; s++)
        {
            int[] tris = fuente.GetTriangles(s);
            var nuevos = new List<int>();
            for (int t = 0; t < tris.Length; t += 3)
            {
                if (!delLado[tris[t]] || !delLado[tris[t + 1]] || !delLado[tris[t + 2]]) continue;
                for (int k = 0; k < 3; k++)
                {
                    int v = tris[t + k];
                    if (!nuevoIndice.TryGetValue(v, out int n))
                    {
                        n = pos.Count;
                        nuevoIndice[v] = n;
                        pos.Add(posiciones[v]);
                        nor.Add(normales[v]);
                        uv.Add(uvs[v]);
                    }
                    nuevos.Add(n);
                }
            }
            submallas.Add(nuevos);
        }

        // El guante apunta a +Y (puño arriba): se gira para que los nudillos miren a +Z y se escala al largo real
        var caja = new Bounds(pos[0], Vector3.zero);
        foreach (var p in pos) caja.Encapsulate(p);
        float escala = Largo / caja.size.y;
        var centro = new Vector3(caja.center.x, caja.min.y + caja.size.y * CentroPuno, caja.center.z);
        Quaternion giro = Quaternion.Euler(90f, 0f, 0f);
        for (int i = 0; i < pos.Count; i++)
        {
            pos[i] = giro * ((pos[i] - centro) * escala);
            nor[i] = giro * nor[i];
        }

        var malla = new Mesh { name = nombre };
        malla.SetVertices(pos);
        malla.SetNormals(nor);
        malla.SetUVs(0, uv);
        malla.subMeshCount = submallas.Count;
        for (int s = 0; s < submallas.Count; s++) malla.SetTriangles(submallas[s], s);
        malla.RecalculateTangents();
        malla.RecalculateBounds();

        string ruta = $"{CarpetaMallas}/{nombre}.asset";
        var existente = AssetDatabase.LoadAssetAtPath<Mesh>(ruta);
        if (existente != null)
        {
            EditorUtility.CopySerialized(malla, existente);
            malla = existente;
        }
        else AssetDatabase.CreateAsset(malla, ruta);
        AssetDatabase.SaveAssets();
        return malla;
    }
}
