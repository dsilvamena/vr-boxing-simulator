using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Evita que las suelas atraviesen la lona cuando un clip se retargetea a un personaje de otras proporciones.
/// El retarget humanoide coloca el cuerpo por su centro de masa y no sabe cuánto calzado hay bajo el tobillo:
/// Mr. Sandman tiene 22 cm de bota bajo el tobillo y, con las animaciones de Soda, la hundía en la lona.
/// Después de la animación mide las suelas y sube la cadera lo que se hayan hundido. Si no se hunden
/// (tendido en la lona, por ejemplo) no hace nada. Va en el objeto del Animator.
/// Las suelas se calculan en el Editor (menú contextual) y quedan guardadas, así la malla no necesita Read/Write.
/// </summary>
[DefaultExecutionOrder(-50)]
public class ApoyoEnSuelo : MonoBehaviour
{
    [System.Serializable]
    private class Suela
    {
        public Transform hueso;
        public Vector3[] puntos; // en el espacio del hueso
    }

    [SerializeField] private Animator animator;
    [Tooltip("Hundimiento que se tolera antes de corregir (m)")]
    [SerializeField] private float tolerancia = 0.005f;
    [SerializeField] private Suela[] suelas = new Suela[0];

    private Transform cadera;
    private Vector3 caderaEscrita, caderaAnimada;
    private bool corregida;

    public float UltimaCorreccion { get; private set; }

    private void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        cadera = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;
    }

    private void LateUpdate()
    {
        if (cadera == null || suelas.Length == 0) return;

        // Si el Animator no escribió este frame (pausa, culling), se deshace la corrección anterior para no acumularla
        if (corregida && cadera.localPosition == caderaEscrita) cadera.localPosition = caderaAnimada;
        corregida = false;

        float minimo = float.MaxValue;
        foreach (var suela in suelas)
        {
            if (suela.hueso == null) continue;
            Matrix4x4 m = suela.hueso.localToWorldMatrix;
            foreach (var p in suela.puntos)
                minimo = Mathf.Min(minimo, m.m10 * p.x + m.m11 * p.y + m.m12 * p.z + m.m13);
        }

        float hundido = transform.position.y - minimo;
        UltimaCorreccion = hundido > tolerancia ? hundido : 0f;
        if (UltimaCorreccion == 0f) return;

        caderaAnimada = cadera.localPosition;
        cadera.position += Vector3.up * hundido;
        caderaEscrita = cadera.localPosition;
        corregida = true;
    }

#if UNITY_EDITOR
    private void Reset() => CalcularSuelas();

    /// <summary>
    /// Toma los vértices de cada pie (pesados a Foot o Toes) que en la pose de reposo están a menos de
    /// 4 cm de su punto más bajo, y los guarda en el espacio de su hueso.
    /// </summary>
    [ContextMenu("Calcular suelas")]
    public void CalcularSuelas()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (animator == null || !animator.isHuman) { Debug.LogWarning("ApoyoEnSuelo necesita un Animator Humanoid."); return; }

        var huesosPie = new List<Transform>();
        foreach (var b in new[] { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot, HumanBodyBones.LeftToes, HumanBodyBones.RightToes })
            if (animator.GetBoneTransform(b) != null) huesosPie.Add(animator.GetBoneTransform(b));

        // Altura de reposo (sin skinning) y punto en el espacio del hueso, por hueso
        var candidatos = new Dictionary<Transform, List<(float altura, Vector3 local)>>();
        foreach (var smr in animator.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            Mesh malla = smr.sharedMesh;
            if (malla == null) continue;
            Vector3[] vertices = malla.vertices;
            BoneWeight[] pesos = malla.boneWeights;
            Matrix4x4[] bindposes = malla.bindposes;
            Transform[] huesos = smr.bones;
            for (int i = 0; i < vertices.Length && i < pesos.Length; i++)
            {
                if (pesos[i].weight0 < 0.5f) continue;
                Transform hueso = huesos[pesos[i].boneIndex0];
                if (!huesosPie.Contains(hueso)) continue;
                float altura = transform.InverseTransformPoint(smr.transform.TransformPoint(vertices[i])).y;
                if (!candidatos.TryGetValue(hueso, out var lista)) candidatos[hueso] = lista = new List<(float, Vector3)>();
                lista.Add((altura, bindposes[pesos[i].boneIndex0].MultiplyPoint3x4(vertices[i])));
            }
        }

        var resultado = new List<Suela>();
        foreach (var par in candidatos)
        {
            float minimo = float.MaxValue;
            foreach (var c in par.Value) minimo = Mathf.Min(minimo, c.altura);
            var puntos = new List<Vector3>();
            foreach (var c in par.Value) if (c.altura < minimo + 0.04f) puntos.Add(c.local);
            resultado.Add(new Suela { hueso = par.Key, puntos = puntos.ToArray() });
        }
        UnityEditor.Undo.RecordObject(this, "Calcular suelas");
        suelas = resultado.ToArray();
        UnityEditor.EditorUtility.SetDirty(this);
        int total = 0;
        foreach (var s in suelas) total += s.puntos.Length;
        Debug.Log($"ApoyoEnSuelo: {suelas.Length} huesos, {total} puntos de suela.");
    }
#endif
}
