using UnityEditor;
using UnityEngine;

/// <summary>
/// Menú Tools/Boxeo para crear los puños en el OVRCameraRig. Después, "Poner guantes en las manos" les pone los guantes.
/// </summary>
public static class PunosBuilder
{
    [MenuItem("Tools/Boxeo/Añadir puños a OVRCameraRig")]
    public static void AnadirPunos()
    {
        foreach (string ancla in new[] { "LeftHandAnchor", "RightHandAnchor" })
        {
            var objetivo = GameObject.Find(ancla);
            if (objetivo == null) { Debug.LogWarning($"No se encontró {ancla} en la escena."); continue; }
            if (objetivo.transform.Find("Puno") != null) continue;

            var puno = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            puno.name = "Puno";
            Undo.RegisterCreatedObjectUndo(puno, "Añadir puño");
            puno.transform.SetParent(objetivo.transform, false);
            puno.transform.localScale = Vector3.one * 0.12f;
            puno.GetComponent<SphereCollider>().radius = 0.5f;
            puno.AddComponent<Rigidbody>();
            puno.AddComponent<Puno>();
        }
    }
}
