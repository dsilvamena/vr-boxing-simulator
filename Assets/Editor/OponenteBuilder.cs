using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Menú Tools/Boxeo para convertir un personaje de la escena en un Oponente golpeable.
/// Con un Animator Humanoid usa ReaccionAnimada y cuelga las hitboxes de los huesos; sin esqueleto (.glb)
/// usa ReaccionProcedural y calcula las hitboxes por alturas. Son un punto de partida: ajústalas a ojo.
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

        // El Animator puede estar en la raíz (instancia del FBX, como Glass Joe) o en el modelo hijo
        var animator = raiz.GetComponentInChildren<Animator>();
        if (animator != null && animator.isHuman)
        {
            // Con esqueleto: reacciona por animación y las hitboxes van colgadas de los huesos
            var procedural = raiz.GetComponent<ReaccionProcedural>();
            if (procedural != null) Undo.DestroyObjectImmediate(procedural);
            animator.applyRootMotion = false;

            so = new SerializedObject(Obtener<ReaccionAnimada>(raiz));
            so.FindProperty("animator").objectReferenceValue = animator;
            so.ApplyModifiedProperties();

            // Bloqueo (RF-07): la guardia se hace con IK de manos, que necesita IK Pass en la capa base
            so = new SerializedObject(Obtener<DefensaOponente>(raiz));
            so.FindProperty("animator").objectReferenceValue = animator;
            so.ApplyModifiedProperties();
            ActivarIKPass(animator);

            CrearHitboxesEnHuesos(raiz.transform, animator);
        }
        else
        {
            var reaccion = Obtener<ReaccionProcedural>(raiz);
            so = new SerializedObject(reaccion);
            so.FindProperty("cuerpo").objectReferenceValue = cuerpo;
            so.ApplyModifiedProperties();

            CrearHitboxes(cuerpo);
        }
        Selection.activeGameObject = raiz;
    }

    // Los AnimatorOverrideController usan las capas de su controller base: se activa ahí
    private static void ActivarIKPass(Animator animator)
    {
        RuntimeAnimatorController rac = animator.runtimeAnimatorController;
        while (rac is AnimatorOverrideController aoc) rac = aoc.runtimeAnimatorController;
        if (rac is not UnityEditor.Animations.AnimatorController controller || controller.layers.Length == 0) return;

        var capas = controller.layers;
        if (capas[0].iKPass) return;
        Undo.RecordObject(controller, "Activar IK Pass");
        capas[0].iKPass = true;
        controller.layers = capas;
        EditorUtility.SetDirty(controller);
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

    /// <summary>
    /// Hitboxes de un personaje Humanoid, en la pose actual: cada una abarca los vértices que mueve su hueso
    /// (y sus hijos) y cuelga de él. Por alturas no sirve: en guardia los guantes están a la altura de la cara.
    /// </summary>
    private static void CrearHitboxesEnHuesos(Transform raiz, Animator animator)
    {
        foreach (var vieja in raiz.GetComponentsInChildren<ZonaGolpe>(true))
            Undo.DestroyObjectImmediate(vieja.gameObject);

        Transform cabeza = animator.GetBoneTransform(HumanBodyBones.Head);
        Transform pecho = animator.GetBoneTransform(HumanBodyBones.Chest);
        var huesosCabeza = new HashSet<Transform>(cabeza.GetComponentsInChildren<Transform>());
        var huesosTorso = new HashSet<Transform>();
        foreach (var hueso in new[] { HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest })
            if (animator.GetBoneTransform(hueso) != null) huesosTorso.Add(animator.GetBoneTransform(hueso));

        bool hayCabeza = false, hayTorso = false;
        Bounds cajaCabeza = default, cajaTorso = default;
        foreach (var smr in animator.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (smr.sharedMesh == null) continue;
            var horneada = new Mesh();
            smr.BakeMesh(horneada, true);
            Vector3[] vertices = horneada.vertices;
            BoneWeight[] pesos = smr.sharedMesh.boneWeights;
            Transform[] huesos = smr.bones;
            for (int i = 0; i < vertices.Length && i < pesos.Length; i++)
            {
                Transform principal = huesos[pesos[i].boneIndex0];
                Vector3 p = smr.transform.TransformPoint(vertices[i]);
                if (huesosCabeza.Contains(principal)) Encapsular(ref cajaCabeza, ref hayCabeza, p);
                else if (huesosTorso.Contains(principal)) Encapsular(ref cajaTorso, ref hayTorso, p);
            }
            Object.DestroyImmediate(horneada);
        }
        if (!hayCabeza || !hayTorso) { Debug.LogWarning("No se encontraron vértices de la cabeza o del torso."); return; }

        Vector3 t = cajaCabeza.size;
        Hitbox<SphereCollider>("Hitbox_Cabeza", cabeza, cajaCabeza.center, TipoZona.Cabeza, 1.5f)
            .radius = (t.x + t.y + t.z) / 6f;
        Hitbox<BoxCollider>("Hitbox_Torso", pecho, cajaTorso.center, TipoZona.Torso, 1f)
            .size = cajaTorso.size;
    }

    private static void Encapsular(ref Bounds caja, ref bool hay, Vector3 p)
    {
        if (hay) caja.Encapsulate(p);
        else { caja = new Bounds(p, Vector3.zero); hay = true; }
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
