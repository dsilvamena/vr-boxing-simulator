using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Tools > Boxeo > Crear caminar del réferi: el FBX del réferi no trae ningún clip de caminar, y con solo los pasos
/// procedurales se veía deslizarse agachado y rígido, moviendo nada más que los pies. Esto genera con el Maniqui dos
/// ciclos en el sitio (la raíz no avanza: la mueve CuerpoArbitro):
/// - Referee_Caminar.anim: caminar hacia delante (talón, apoyo, despegue), con el vaivén de la cadera, el giro de la
///   pelvis contra el de los hombros y el braceo cruzado. Hacia atrás es el mismo clip al revés.
/// - Referee_Lateral.anim: el paso lateral del réferi de boxeo hacia su derecha (abre un pie y junta el otro, sin
///   cruzarlos), algo agachado. Hacia la izquierda es el mismo clip en espejo.
/// y cambia el estado de la capa base de Referee.controller por un blend tree 2D (Freeform Directional) en "AndarX" /
/// "AndarZ" (velocidad local dividida por la del clip: 1 = la velocidad para la que está hecho el paso) con la postura de
/// siempre (el blend tree "Postura" en "Agachado") en el centro. "RitmoPaso" acelera el estado cuando va más deprisa que
/// el clip. CuerpoArbitro manda los tres parámetros. Además pone en el PasosProcedurales del réferi de cada pelea la
/// velocidad a partir de la cual manda el clip (los pasos procedurales quedan para los giros en el sitio).
/// Se puede repetir: reescribe los clips (mismo GUID) y rehace el blend tree.
/// </summary>
public static class CaminarArbitroBuilder
{
    private const string Carpeta = "Assets/_Project/Art/Characters/Referee";
    private const string RutaModelo = Carpeta + "/Referee.fbx";
    private const string Animaciones = Carpeta + "/Animations";
    private const string RutaControlador = Animaciones + "/Referee.controller";
    private const string RutaDePie = Animaciones + "/Referee_DePie.anim";
    private const string RutaCaminar = Animaciones + "/Referee_Caminar.anim";
    private const string RutaLateral = Animaciones + "/Referee_Lateral.anim";

    // Medidas del paso. Las velocidades que salen de aquí son las de CuerpoArbitro (velocidadClipCaminar/Lateral)
    private const float Paso = 0.6f;            // m entre talón y talón al caminar
    private const float CicloCaminar = 1f;      // s por ciclo (dos pasos)  → 2 · 0,6 / 1 = 1,2 m/s
    private const float PasoLateral = 0.34f;    // m que avanza de lado en cada ciclo (abrir y juntar)
    private const float CicloLateral = 0.7f;    // s por ciclo              → 0,34 / 0,7 ≈ 0,49 m/s
    private const int Claves = 16;

    [MenuItem("Tools/Boxeo/Crear caminar del réferi")]
    public static void Crear()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("No en Play."); return; }
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(RutaModelo);
        var dePie = AssetDatabase.LoadAssetAtPath<AnimationClip>(RutaDePie);
        var controlador = AssetDatabase.LoadAssetAtPath<AnimatorController>(RutaControlador);
        if (fbx == null || dePie == null || controlador == null)
        {
            Debug.LogError("Faltan el FBX, Referee_DePie o Referee.controller: usa antes Tools > Boxeo > Montar réferi.");
            return;
        }

        AnimationClip caminar, lateral;
        using (var m = new Maniqui(fbx))
        {
            caminar = Maniqui.EscribirClip(RutaCaminar, Ciclo(CicloCaminar, f => PoseCaminar(m, dePie, f)), true);
            lateral = Maniqui.EscribirClip(RutaLateral, Ciclo(CicloLateral, f => PoseLateral(m, dePie, f)), true);
        }
        MontarBlendTree(controlador, caminar, lateral);
        AssetDatabase.SaveAssets();
        PonerEnPeleas();
        Debug.Log($"Caminar del réferi creado: {RutaCaminar}, {RutaLateral} y el blend tree \"Locomocion\" en {RutaControlador}.");
    }

    private static List<(float, Maniqui.Fotograma)> Ciclo(float duracion, System.Func<float, Maniqui.Fotograma> pose)
    {
        var claves = new List<(float, Maniqui.Fotograma)>();
        for (int i = 0; i < Claves; i++) claves.Add((duracion * i / Claves, pose((float)i / Claves)));
        claves.Add((duracion, claves[0].Item2));
        return claves;
    }

    // ---------- Caminar ----------

    // Un pie que pisa con el talón en la fase 0: apoyo en [0, 0,5) (el pie va de delante a atrás a velocidad constante,
    // porque el cuerpo avanza sobre él) y vuelo en [0,5, 1) (vuelve adelante levantándose).
    // Devuelve el avance (z) respecto a la cadera, la altura y el cabeceo del pie (positivo = punta abajo)
    private static (float z, float alto, float cabeceo) PieCaminando(float f)
    {
        f = Mathf.Repeat(f, 1f);
        if (f < 0.5f)
        {
            float s = f / 0.5f;
            float z = Paso * 0.5f - Paso * s;
            // Entra con la punta arriba, se aplana enseguida y al final del apoyo despega el talón
            float cabeceo = s < 0.15f ? Mathf.Lerp(-14f, 0f, s / 0.15f)
                : s > 0.65f ? Mathf.Lerp(0f, 28f, Mathf.SmoothStep(0f, 1f, (s - 0.65f) / 0.35f)) : 0f;
            return (z, 0f, cabeceo);
        }
        else
        {
            float s = (f - 0.5f) / 0.5f;
            float z = -Paso * 0.5f + Paso * Mathf.SmoothStep(0f, 1f, s);
            // Sube antes de la mitad del vuelo y baja a buscar el suelo
            float alto = 0.075f * Mathf.Sin(Mathf.Pow(s, 0.8f) * Mathf.PI);
            float cabeceo = s < 0.4f ? Mathf.Lerp(28f, 0f, s / 0.4f) : Mathf.Lerp(0f, -14f, (s - 0.4f) / 0.6f);
            return (z, alto, cabeceo);
        }
    }

    private static Maniqui.Fotograma PoseCaminar(Maniqui m, AnimationClip dePie, float f)
    {
        m.Muestrear(dePie, 0f);
        m.Apoyar();
        var (pieI, pieD, giroI, giroD, largoPie) = PiesDeReposo(m);
        float ciclo = Mathf.Cos(2f * Mathf.PI * f);   // 1 = pie derecho delante (pisa en f = 0), -1 = el izquierdo

        // Pelvis: gira con la pierna que va delante y se desplaza hacia la que apoya; el pecho gira al revés
        Vector3 cadera = new Vector3(0.022f * Mathf.Sin(2f * Mathf.PI * f), 0f, 0f);
        m.H(HumanBodyBones.Hips).position += cadera;
        m.Girar(HumanBodyBones.Hips, Vector3.up * (-7f * ciclo) + Vector3.forward * (-2.5f * Mathf.Sin(2f * Mathf.PI * f)));
        // Un poco hacia delante, como quien camina con decisión, y la barbilla algo recogida (en positivo, hacia abajo)
        m.GirarTorso(Vector3.up * (11f * ciclo) + Vector3.right * 9f);
        m.GirarCabeza(Vector3.up * (-4f * ciclo) + Vector3.right * 6f);

        // Pies: a la anchura de una pisada normal (más juntos que de pie) y con la trayectoria del ciclo
        var d = PieCaminando(f);
        var i = PieCaminando(f + 0.5f);
        Vector3 objetivoD = new Vector3(Mathf.Lerp(pieD.x, 0.09f, 0.6f), pieD.y + d.alto, pieD.z + d.z);
        Vector3 objetivoI = new Vector3(Mathf.Lerp(pieI.x, -0.09f, 0.6f), pieI.y + i.alto, pieI.z + i.z);
        PlantarConCadera(m, objetivoI, giroI, i.cabeceo, objetivoD, giroD, d.cabeceo, largoPie, 0.012f);

        // Brazos: cada uno va con la pierna contraria; el codo se dobla más al ir delante
        Bracear(m, true, ciclo, 17f);
        Bracear(m, false, -ciclo, 17f);
        return m.Capturar();
    }

    private static void Bracear(Maniqui m, bool izquierdo, float delante, float amplitud)
    {
        // Girar sobre la derecha del personaje en negativo lleva el brazo hacia delante
        m.Girar(izquierdo ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm, Vector3.right * (-amplitud * delante));
        m.Girar(izquierdo ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm,
            Vector3.right * -(10f + 14f * Mathf.Max(0f, delante)));
    }

    // ---------- Paso lateral (hacia su derecha) ----------

    private static Maniqui.Fotograma PoseLateral(Maniqui m, AnimationClip dePie, float f)
    {
        m.Muestrear(dePie, 0f);
        m.Apoyar();
        var (pieI, pieD, giroI, giroD, largoPie) = PiesDeReposo(m);

        // El cuerpo avanza PasoLateral · f; cada pie, en el mundo, se queda quieto salvo en su vuelo: el derecho abre en
        // [0, 0,4] y el izquierdo junta en [0,5, 0,9]. En el sitio, el pie es su recorrido en el mundo menos el del cuerpo
        float cuerpo = PasoLateral * f;
        float vueloD = Mathf.Clamp01(f / 0.4f), vueloI = Mathf.Clamp01((f - 0.5f) / 0.4f);
        float xD = PasoLateral * Mathf.SmoothStep(0f, 1f, vueloD) - cuerpo;
        float xI = PasoLateral * Mathf.SmoothStep(0f, 1f, vueloI) - cuerpo;
        float altoD = f < 0.4f ? 0.045f * Mathf.Sin(vueloD * Mathf.PI) : 0f;
        float altoI = f >= 0.5f && f < 0.9f ? 0.04f * Mathf.Sin(vueloI * Mathf.PI) : 0f;

        // Un poco agachado, el peso entre los dos pies y el tronco algo inclinado hacia donde va
        m.GirarTorso(Vector3.right * 9f + Vector3.forward * (-2f));
        m.GirarCabeza(Vector3.right * 3f + Vector3.forward * 2f);
        Vector3 objetivoD = new Vector3(pieD.x + 0.03f + xD, pieD.y + altoD, pieD.z);
        Vector3 objetivoI = new Vector3(pieI.x - 0.03f + xI, pieI.y + altoI, pieI.z);
        PlantarConCadera(m, objetivoI, giroI, 0f, objetivoD, giroD, 0f, largoPie, 0.05f);

        // Los brazos cuelgan algo separados y se mecen poco
        float mecer = Mathf.Sin(2f * Mathf.PI * f);
        m.Girar(HumanBodyBones.LeftUpperArm, Vector3.forward * (-6f - 3f * mecer));
        m.Girar(HumanBodyBones.RightUpperArm, Vector3.forward * (6f - 3f * mecer));
        m.Girar(HumanBodyBones.LeftLowerArm, Vector3.right * -18f);
        m.Girar(HumanBodyBones.RightLowerArm, Vector3.right * -18f);
        return m.Capturar();
    }

    // ---------- Comunes ----------

    private static (Vector3 i, Vector3 d, Quaternion gi, Quaternion gd, float largo) PiesDeReposo(Maniqui m)
    {
        Transform pieI = m.H(HumanBodyBones.LeftFoot), pieD = m.H(HumanBodyBones.RightFoot);
        Transform puntaD = m.H(HumanBodyBones.RightToes);
        float largo = puntaD != null ? Vector3.Distance(pieD.position, puntaD.position) : 0.12f;
        return (pieI.position, pieD.position, pieI.rotation, pieD.rotation, largo);
    }

    // Planta los dos pies y baja la cadera lo justo para que la pierna más estirada llegue (con las rodillas apenas
    // dobladas): así el cuerpo sube y baja solo, más bajo con los pies abiertos y más alto al pasar un pie junto al otro.
    // "agachado" baja la cadera además esa cantidad.
    private static void PlantarConCadera(Maniqui m, Vector3 objetivoI, Quaternion giroI, float cabeceoI,
        Vector3 objetivoD, Quaternion giroD, float cabeceoD, float largoPie, float agachado)
    {
        Vector3 tobilloI = Despegar(objetivoI, cabeceoI, largoPie), tobilloD = Despegar(objetivoD, cabeceoD, largoPie);
        Transform cadera = m.H(HumanBodyBones.Hips);
        float bajar = agachado;
        foreach (bool izquierdo in new[] { true, false })
        {
            Vector3 muslo = m.P(izquierdo ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg);
            float pierna = Vector3.Distance(muslo, m.P(izquierdo ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg))
                           + Vector3.Distance(m.P(izquierdo ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg),
                               m.P(izquierdo ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot));
            Vector3 tobillo = izquierdo ? tobilloI : tobilloD;
            Vector3 delta = tobillo - muslo;
            float horizontal = new Vector2(delta.x, delta.z).magnitude;
            float alcance = pierna * 0.975f;
            float vertical = Mathf.Sqrt(Mathf.Max(0.01f, alcance * alcance - horizontal * horizontal));
            bajar = Mathf.Max(bajar, (muslo.y - tobillo.y) - vertical + agachado);
        }
        cadera.position += Vector3.down * Mathf.Max(0f, bajar);

        Quaternion cabeceadoI = Quaternion.AngleAxis(cabeceoI, Vector3.right) * giroI;
        Quaternion cabeceadoD = Quaternion.AngleAxis(cabeceoD, Vector3.right) * giroD;
        m.Pie(true, tobilloI, cabeceadoI, m.P(HumanBodyBones.LeftLowerLeg) + Vector3.forward * 0.5f);
        m.Pie(false, tobilloD, cabeceadoD, m.P(HumanBodyBones.RightLowerLeg) + Vector3.forward * 0.5f);
    }

    // Con la punta abajo el pie gira sobre la punta (el talón sube); con la punta arriba, sobre el tobillo
    private static Vector3 Despegar(Vector3 tobillo, float cabeceo, float largoPie)
    {
        if (cabeceo <= 0f) return tobillo;
        Vector3 punta = tobillo + Vector3.forward * largoPie;
        return punta + Quaternion.AngleAxis(cabeceo, Vector3.right) * (tobillo - punta);
    }

    // ---------- Controlador ----------

    private static void MontarBlendTree(AnimatorController controlador, AnimationClip caminar, AnimationClip lateral)
    {
        foreach (string nombre in new[] { "AndarX", "AndarZ", "RitmoPaso" })
            if (controlador.parameters.All(p => p.name != nombre))
                controlador.AddParameter(new AnimatorControllerParameter
                {
                    name = nombre, type = AnimatorControllerParameterType.Float, defaultFloat = nombre == "RitmoPaso" ? 1f : 0f,
                });

        AnimatorStateMachine maquina = controlador.layers[0].stateMachine;
        AnimatorState estado = maquina.defaultState ?? maquina.states[0].state;
        // La postura de siempre: el blend tree en "Agachado" (si ya se había envuelto, se saca de dentro)
        var actual = estado.motion as BlendTree;
        BlendTree postura = actual != null && actual.name == "Locomocion"
            ? actual.children.Select(c => c.motion).OfType<BlendTree>().First(b => b.name == "Postura")
            : actual;
        if (postura == null) { Debug.LogError("El estado del réferi no tiene el blend tree Postura."); return; }

        BlendTree arbol = actual != null && actual.name == "Locomocion" ? actual : null;
        if (arbol == null)
        {
            arbol = new BlendTree { name = "Locomocion", hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(arbol, controlador);
        }
        arbol.blendType = BlendTreeType.FreeformDirectional2D;
        arbol.blendParameter = "AndarX";
        arbol.blendParameterY = "AndarZ";
        arbol.useAutomaticThresholds = false;
        arbol.children = new[]
        {
            new ChildMotion { motion = postura, position = Vector2.zero, timeScale = 1f },
            new ChildMotion { motion = caminar, position = new Vector2(0f, 1f), timeScale = 1f },
            new ChildMotion { motion = caminar, position = new Vector2(0f, -1f), timeScale = -1f },
            new ChildMotion { motion = lateral, position = new Vector2(1f, 0f), timeScale = 1f },
            new ChildMotion { motion = lateral, position = new Vector2(-1f, 0f), timeScale = 1f, mirror = true },
        };
        estado.motion = arbol;
        estado.name = "Locomocion";
        estado.speedParameter = "RitmoPaso";
        estado.speedParameterActive = true;
        EditorUtility.SetDirty(arbol);
        EditorUtility.SetDirty(estado);
        EditorUtility.SetDirty(controlador);
    }

    // En cada pelea: a partir de esta velocidad los pies los pone el clip; la IK de pasos queda para los giros
    private static void PonerEnPeleas()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) { Debug.LogWarning("Hay escenas sin guardar: no se tocan los PasosProcedurales del réferi."); return; }
        SceneSetup[] abiertas = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            foreach (string ruta in EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path))
            {
                Scene escena = EditorSceneManager.OpenScene(ruta, OpenSceneMode.Single);
                bool cambio = false;
                foreach (GameObject raiz in escena.GetRootGameObjects())
                    foreach (CuerpoArbitro cuerpo in raiz.GetComponentsInChildren<CuerpoArbitro>(true))
                    {
                        var pasos = cuerpo.GetComponentInChildren<PasosProcedurales>(true);
                        if (pasos == null) continue;
                        var so = new SerializedObject(pasos);
                        SerializedProperty p = so.FindProperty("velocidadConClip");
                        if (Mathf.Approximately(p.floatValue, 0.2f)) continue;
                        p.floatValue = 0.2f;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        cambio = true;
                    }
                if (!cambio) continue;
                EditorSceneManager.MarkSceneDirty(escena);
                EditorSceneManager.SaveScene(escena);
            }
        }
        finally
        {
            if (abiertas.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(abiertas);
        }
    }
}
