#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class GlassJoeSetup
{
    [MenuItem("Tools/Glass Joe Boxing/Setup Selected Glass Joe", false, 10)]
    static void SetupSelectedJoe()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null) { EditorUtility.DisplayDialog("Glass Joe", "Selecciona la instancia de Glass Joe en la Hierarchy.", "OK"); return; }
        if (PrefabUtility.IsPartOfPrefabAsset(selected))
        {
            EditorUtility.DisplayDialog("Glass Joe", "Selecciona la instancia del modelo dentro de la escena (Hierarchy), no el archivo del Project.", "OK");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(selected, "Setup Glass Joe");
        GlassJoeTarget target = selected.GetComponent<GlassJoeTarget>();
        if (target == null) target = Undo.AddComponent<GlassJoeTarget>(selected);
        target.visualRoot = FindVisualRoot(selected.transform);

        Rigidbody rb = selected.GetComponent<Rigidbody>();
        if (rb == null) rb = Undo.AddComponent<Rigidbody>(selected);
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        Bounds localBounds;
        if (TryGetLocalRenderBounds(selected.transform, out localBounds))
        {
            CapsuleCollider capsule = selected.GetComponent<CapsuleCollider>();
            if (capsule == null) capsule = Undo.AddComponent<CapsuleCollider>(selected);
            capsule.direction = 1;
            capsule.center = localBounds.center;
            capsule.height = Mathf.Max(localBounds.size.y, 0.3f);
            capsule.radius = Mathf.Max(0.12f, Mathf.Min(localBounds.size.x, localBounds.size.z) * 0.28f);
            capsule.isTrigger = false;
        }
        else
        {
            EditorUtility.DisplayDialog("Glass Joe", "No encontré Renderer debajo del objeto. Se añadieron los componentes base, pero crea/ajusta un CapsuleCollider manualmente.", "OK");
        }

        EditorUtility.SetDirty(selected);
        Selection.activeGameObject = selected;
        EditorUtility.DisplayDialog("Glass Joe listo", "Se añadieron GlassJoeTarget, Rigidbody cinemático y CapsuleCollider. La barra de vida se crea al entrar en Play.\n\nAhora coloca BoxingFistHitbox en cada mano rastreada y ajusta el radio.", "OK");
    }

    [MenuItem("Tools/Glass Joe Boxing/Setup Selected Fist Hitbox", false, 11)]
    static void SetupSelectedFist()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null) { EditorUtility.DisplayDialog("Puño", "Selecciona el transform de la mano en la Hierarchy.", "OK"); return; }
        if (PrefabUtility.IsPartOfPrefabAsset(selected)) { EditorUtility.DisplayDialog("Puño", "Selecciona el objeto de la mano dentro de la escena.", "OK"); return; }
        Undo.RegisterFullObjectHierarchyUndo(selected, "Setup Boxing Fist Hitbox");
        BoxingFistHitbox fist = selected.GetComponent<BoxingFistHitbox>();
        if (fist == null) fist = Undo.AddComponent<BoxingFistHitbox>(selected);
        SphereCollider sphere = selected.GetComponent<SphereCollider>();
        if (sphere == null) sphere = Undo.AddComponent<SphereCollider>(selected);
        sphere.isTrigger = true;
        sphere.radius = fist.radius;
        Rigidbody rb = selected.GetComponent<Rigidbody>();
        if (rb == null) rb = Undo.AddComponent<Rigidbody>(selected);
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        EditorUtility.SetDirty(selected);
        EditorUtility.DisplayDialog("Puño configurado", "Se añadió una esfera Trigger y Rigidbody cinemático. Asegúrate de que este objeto siga la posición de la mano real en Play.", "OK");
    }

    static Transform FindVisualRoot(Transform root)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.GetComponentInChildren<Renderer>() != null) return child;
        }
        return root;
    }

    static bool TryGetLocalRenderBounds(Transform root, out Bounds result)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        bool found = false;
        result = new Bounds(Vector3.zero, Vector3.one);
        foreach (Renderer renderer in renderers)
        {
            Bounds b = renderer.bounds;
            Vector3 c = b.center, e = b.extents;
            for (int i = 0; i < 8; i++)
            {
                Vector3 world = c + Vector3.Scale(e, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 local = root.InverseTransformPoint(world);
                if (!found) { result = new Bounds(local, Vector3.zero); found = true; }
                else result.Encapsulate(local);
            }
        }
        return found;
    }
}
#endif
