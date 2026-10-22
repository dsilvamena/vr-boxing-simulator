using UnityEditor;
using UnityEngine;

/// <summary>
/// Menú Tools/Boxeo para crear el dummy físico de pruebas y los puños en el OVRCameraRig.
/// </summary>
public static class DummyBuilder
{
    [MenuItem("Tools/Boxeo/Crear dummy físico")]
    public static void CrearDummy()
    {
        var raiz = new GameObject("Dummy");
        Undo.RegisterCreatedObjectUndo(raiz, "Crear dummy");
        raiz.transform.position = SpawnPos();

        // Base anclada al suelo
        var baseObj = Pieza("Base", PrimitiveType.Cylinder, raiz.transform, new Vector3(0, 0.1f, 0), new Vector3(0.6f, 0.1f, 0.6f));
        baseObj.GetComponent<Rigidbody>().isKinematic = true;

        // Torso (cápsula) unido a la base
        var torso = Pieza("Torso", PrimitiveType.Capsule, raiz.transform, new Vector3(0, 0.95f, 0), new Vector3(0.45f, 0.7f, 0.3f), 20f);
        // El ancla va en espacio local del hijo: -1 en una cápsula es su extremo inferior
        Unir(torso, baseObj.GetComponent<Rigidbody>(), new Vector3(0, -1f, 0), 25f, 2500f, 150f);

        // Cabeza unida al torso (-0.5 en una esfera es su punto más bajo)
        var cabeza = Pieza("Cabeza", PrimitiveType.Sphere, raiz.transform, new Vector3(0, 1.8f, 0), Vector3.one * 0.28f, 4f);
        Unir(cabeza, torso.GetComponent<Rigidbody>(), new Vector3(0, -0.5f, 0), 35f, 250f, 15f);

        Selection.activeGameObject = raiz;
    }

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

    private static Vector3 SpawnPos()
    {
        var cam = Camera.main;
        if (cam == null) return Vector3.zero;
        // Con tracking FloorLevel el suelo está a la altura del OVRCameraRig, no en y = 0
        var rig = Object.FindAnyObjectByType<OVRCameraRig>();
        float sueloY = rig != null ? rig.transform.position.y : 0f;
        Vector3 f = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
        return new Vector3(cam.transform.position.x, sueloY, cam.transform.position.z) + f * 1.0f;
    }

    private static GameObject Pieza(string nombre, PrimitiveType tipo, Transform padre, Vector3 localPos, Vector3 escala, float masa = 1f)
    {
        var go = GameObject.CreatePrimitive(tipo);
        go.name = nombre;
        go.transform.SetParent(padre, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = escala;
        var rb = go.AddComponent<Rigidbody>();
        rb.mass = masa;
        rb.angularDamping = 1f;
        rb.linearDamping = 0.2f;
        return go;
    }

    private static void Unir(GameObject pieza, Rigidbody padre, Vector3 ancla, float limite, float resorte, float amortiguacion)
    {
        var j = pieza.AddComponent<ConfigurableJoint>();
        j.connectedBody = padre;
        j.autoConfigureConnectedAnchor = true;
        j.anchor = ancla;
        j.xMotion = j.yMotion = j.zMotion = ConfigurableJointMotion.Locked;
        j.angularXMotion = j.angularYMotion = j.angularZMotion = ConfigurableJointMotion.Limited;
        j.lowAngularXLimit = new SoftJointLimit { limit = -limite };
        j.highAngularXLimit = new SoftJointLimit { limit = limite };
        j.angularYLimit = new SoftJointLimit { limit = limite };
        j.angularZLimit = new SoftJointLimit { limit = limite };
        j.rotationDriveMode = RotationDriveMode.Slerp;
        j.slerpDrive = new JointDrive { positionSpring = resorte, positionDamper = amortiguacion, maximumForce = float.MaxValue };
    }
}
