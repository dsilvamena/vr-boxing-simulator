using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Tools > Boxeo > Poner rivales en el menú: en Main_Scene coloca a los cinco rivales del juego (los mismos modelos con
/// esqueleto de las peleas) en arco frente al jugador, en orden de nivel de izquierda a derecha, cada uno con su pose:
/// Glass Joe en su guardia nerviosa y Soda Popinski en la suya (sus clips de Idle), Bald Bull chocando los guantes,
/// Mr. Sandman con los guantes en la cintura y Iron Mike Tyson agazapado en su guardia cerrada, balanceándose.
/// Las tres poses propias son clips humanoides generados aquí con el Maniqui (Art/Characters/Menu/Menu_*.anim), cada uno
/// con su controlador de un solo estado, "Idle". CuerpoVivo les añade la respiración y la mirada al jugador.
/// Además deja a los cinco posados en la escena guardada (primer fotograma de su pose), para que en el Editor no se
/// vean en T fuera de Play. Son solo adorno: no llevan Oponente ni colisiones. Se puede repetir.
/// </summary>
public static class MenuBuilder
{
    private const string Escena = "Assets/Scenes/Main_Scene.unity";
    private const string Personajes = "Assets/_Project/Art/Characters/";
    private const string CarpetaPoses = Personajes + "Menu/";

    private delegate List<(float, Maniqui.Fotograma)> Pose(Maniqui m, AnimationClip idle);

    // Nombre en la escena, prefab (vacío = ya está en la escena), escala, x, z, pose propia (null = su Idle)
    private static readonly (string nombre, string prefab, float escala, float x, float z, Pose pose)[] Rivales =
    {
        ("GlassJoe", "", 1f, -2.1f, 0.25f, null),
        ("BaldBull", "BaldBull/BaldBull.prefab", 1.15f, -1.1f, 0.95f, ChocarGuantes),
        ("SodaPopinski", "SodaPopinski/SodaPopinski.prefab", 0.8f, 0f, 1.3f, null),
        ("MrSandman", "MrSandman/MrSandman.prefab", 1f, 1.1f, 0.95f, EnJarras),
        ("MikeTyson", "MikeTyson/MikeTyson.prefab", 1f, 2.1f, 0.25f, GuardiaCerrada),
    };

    [MenuItem("Tools/Boxeo/Poner rivales en el menú")]
    public static void Poner()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("No en Play."); return; }
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) { Debug.LogError("Guarda o descarta los cambios de las escenas abiertas antes."); return; }

        SceneSetup[] abiertas = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            Scene escena = EditorSceneManager.OpenScene(Escena, OpenSceneMode.Single);
            GameObject[] raices = escena.GetRootGameObjects();
            GameObject joe = raices.FirstOrDefault(g => g.name == "GlassJoe");
            var rig = Object.FindAnyObjectByType<OVRCameraRig>();
            if (joe == null || rig == null) { Debug.LogError("Main_Scene no tiene GlassJoe u OVRCameraRig."); return; }
            float suelo = joe.transform.position.y;
            Vector3 jugador = rig.transform.position;

            foreach (GameObject viejo in raices.Where(g => g.name == "bald_bull" || g.name == "mr_sandman"))
                Object.DestroyImmediate(viejo);

            foreach ((string nombre, string prefab, float escala, float x, float z, Pose pose) in Rivales)
            {
                GameObject go = escena.GetRootGameObjects().FirstOrDefault(g => g.name == nombre);
                if (go == null)
                {
                    var origen = AssetDatabase.LoadAssetAtPath<GameObject>(Personajes + prefab);
                    if (origen == null) { Debug.LogWarning($"Falta {prefab}."); continue; }
                    go = (GameObject)PrefabUtility.InstantiatePrefab(origen, escena);
                    go.name = nombre;
                }
                go.transform.position = new Vector3(x, suelo, z);
                Vector3 haciaJugador = jugador - go.transform.position;
                haciaJugador.y = 0f;
                go.transform.rotation = Quaternion.LookRotation(haciaJugador);
                go.transform.localScale = Vector3.one * escala;

                var animator = go.GetComponentInChildren<Animator>();
                if (animator == null) continue;
                if (animator.GetComponent<CuerpoVivo>() == null) animator.gameObject.AddComponent<CuerpoVivo>();

                // La guardia del personaje sale del controlador de su prefab: en la escena puede tener ya el de su pose
                var original = PrefabUtility.GetCorrespondingObjectFromSource(animator);
                AnimationClip idle = ClipDeIdle((original != null ? original : animator).runtimeAnimatorController);
                if (pose != null)
                {
                    AnimationClip propio = CrearPose(nombre, animator.gameObject, idle, pose);
                    animator.runtimeAnimatorController = ControladorDePose(nombre, propio);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
                    idle = propio;
                }
                Posar(animator, idle);
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(escena);
            EditorSceneManager.SaveScene(escena);
            Debug.Log("Rivales del menú colocados y posados.");
        }
        finally
        {
            if (abiertas.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(abiertas);
        }
    }

    /// <summary>
    /// Tools > Boxeo > Posar rivales en las peleas: en las escenas de pelea deja a cada rival en el primer fotograma de
    /// su guardia, para que fuera de Play no se vea en T (los que se importan así: Bald Bull, Mr. Sandman, Tyson). En
    /// Play no cambia nada: el Animator escribe la pose cada fotograma. Las hitboxes cuelgan de los huesos y los siguen.
    /// </summary>
    [MenuItem("Tools/Boxeo/Posar rivales en las peleas")]
    public static void PosarEnPeleas()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("No en Play."); return; }
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) { Debug.LogError("Guarda o descarta los cambios de las escenas abiertas antes."); return; }

        SceneSetup[] abiertas = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes.Where(s => s.enabled))
            {
                Scene escena = EditorSceneManager.OpenScene(s.path, OpenSceneMode.Single);
                var oponente = Object.FindAnyObjectByType<Oponente>();
                Animator animator = oponente != null ? oponente.GetComponentInChildren<Animator>() : null;
                if (animator == null || !animator.isHuman) continue;
                Posar(animator, ClipDeIdle(animator.runtimeAnimatorController));
                EditorSceneManager.MarkSceneDirty(escena);
                EditorSceneManager.SaveScene(escena);
            }
            Debug.Log("Rivales posados en las escenas de pelea.");
        }
        finally
        {
            if (abiertas.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(abiertas);
        }
    }

    /// <summary>El clip del estado Idle de un controlador (resuelto a través del override, si lo es).</summary>
    public static AnimationClip ClipDeIdle(RuntimeAnimatorController controlador)
    {
        var sobre = controlador as AnimatorOverrideController;
        var basico = (sobre != null ? sobre.runtimeAnimatorController : controlador) as AnimatorController;
        if (basico == null) return null;
        var estado = basico.layers[0].stateMachine.states.FirstOrDefault(s => s.state.name == "Idle").state;
        var clip = estado != null ? estado.motion as AnimationClip : null;
        return sobre != null && clip != null ? sobre[clip] : clip;
    }

    /// <summary>
    /// Deja el esqueleto de la escena en el primer fotograma del clip, para que se vea posado fuera de Play. En Play el
    /// Animator escribe los huesos cada fotograma, así que esto solo cambia lo que se ve en el Editor.
    /// </summary>
    public static void Posar(Animator animator, AnimationClip clip)
    {
        if (clip == null) return;
        clip.SampleAnimation(animator.gameObject, 0f);
        var apoyo = animator.GetComponent<ApoyoEnSuelo>();
        if (apoyo != null)
        {
            float hundido = apoyo.MedirHundimiento();
            if (hundido > 0f) animator.GetBoneTransform(HumanBodyBones.Hips).position += Vector3.up * hundido;
        }
        foreach (Transform hueso in animator.GetComponentsInChildren<Transform>())
        {
            EditorUtility.SetDirty(hueso);
            if (PrefabUtility.IsPartOfPrefabInstance(hueso)) PrefabUtility.RecordPrefabInstancePropertyModifications(hueso);
        }
    }

    private static AnimationClip CrearPose(string nombre, GameObject personaje, AnimationClip idle, Pose pose)
    {
        using var maniqui = new Maniqui(personaje);
        return Maniqui.EscribirClip($"{CarpetaPoses}Menu_{nombre}.anim", pose(maniqui, idle), true);
    }

    // Un controlador con un solo estado, "Idle" (el nombre que busca CuerpoVivo), con IK Pass para la mirada
    private static AnimatorController ControladorDePose(string nombre, AnimationClip clip)
    {
        string ruta = $"{CarpetaPoses}Menu_{nombre}.controller";
        var controlador = AssetDatabase.LoadAssetAtPath<AnimatorController>(ruta);
        if (controlador == null)
        {
            controlador = AnimatorController.CreateAnimatorControllerAtPath(ruta);
            controlador.layers[0].stateMachine.AddState("Idle");
        }
        AnimatorControllerLayer[] capas = controlador.layers;
        capas[0].iKPass = true;
        capas[0].stateMachine.states.First(s => s.state.name == "Idle").state.motion = clip;
        controlador.layers = capas;
        EditorUtility.SetDirty(controlador);
        return controlador;
    }

    // ---------- Poses ----------

    // Bald Bull: plantado, sacando pecho, choca los guantes delante de él dos veces y los separa despacio
    private static List<(float, Maniqui.Fotograma)> ChocarGuantes(Maniqui m, AnimationClip idle)
    {
        Maniqui.Fotograma Foto(float separacion, float pecho)
        {
            m.PoseDePie(0.2f, idle);
            m.GirarTorso(Vector3.right * -pecho);
            m.GirarCabeza(Vector3.right * (pecho + 6f));
            Vector3 c = m.Hombros;
            float a = m.Ancho;
            foreach (bool izquierda in new[] { true, false })
            {
                float lado = izquierda ? -1f : 1f;
                m.Mano(izquierda,
                    c + new Vector3(lado * (0.24f * a + separacion), -0.18f * a, 0.8f * a),
                    c + new Vector3(lado * 1.7f * a, -0.6f * a, 0f));
            }
            return m.Capturar();
        }

        Maniqui.Fotograma abiertos = Foto(0.17f, 7f), juntos = Foto(0f, 3f), rebote = Foto(0.06f, 4f);
        return new List<(float, Maniqui.Fotograma)>
        {
            (0f, juntos), (0.5f, juntos), (1.7f, abiertos), (2.1f, abiertos), (2.24f, juntos), (2.44f, rebote), (2.58f, juntos), (3f, juntos),
        };
    }

    // Mr. Sandman: guantes en la cintura, piernas abiertas y la barbilla alta, quieto con las dos botas plantadas.
    // Antes cambiaba el peso de una pierna a otra, pero con su esqueleto (no es de Mixamo) el retarget de ese vaivén le
    // levantaba un pie del suelo con la rodilla doblada. Quieto, la respiración y la mirada se las pone CuerpoVivo.
    private static List<(float, Maniqui.Fotograma)> EnJarras(Maniqui m, AnimationClip idle)
    {
        m.PoseDePie(0.2f, idle);
        m.GirarTorso(Vector3.right * -6f);
        m.GirarCabeza(Vector3.right * 6f);
        Vector3 cadera = m.P(HumanBodyBones.Hips);
        float a = m.Ancho;
        foreach (bool izquierda in new[] { true, false })
        {
            float lado = izquierda ? -1f : 1f;
            m.Mano(izquierda,
                cadera + new Vector3(lado * 0.62f * a, 0.5f * a, 0.12f * a),
                cadera + new Vector3(lado * 2.4f * a, 1.1f * a, -0.7f * a));
        }
        Maniqui.Fotograma quieto = m.Capturar();
        return new List<(float, Maniqui.Fotograma)> { (0f, quieto), (4f, quieto) };
    }

    // Iron Mike Tyson: su guardia cerrada ("peek-a-boo"), agazapado con los guantes pegados a los pómulos, y el
    // balanceo de lado a lado con el que entraba
    private static List<(float, Maniqui.Fotograma)> GuardiaCerrada(Maniqui m, AnimationClip idle)
    {
        Maniqui.Fotograma Foto(float lado, float hundido)
        {
            m.Muestrear(idle, 0f);
            m.MoverCadera(new Vector3(lado * 0.07f, -0.07f - hundido, 0f));
            m.GirarTorso(Vector3.right * 14f + Vector3.forward * (-lado * 13f) + Vector3.up * (lado * 8f));
            m.GirarCabeza(Vector3.right * -8f + Vector3.forward * (lado * 9f));
            Vector3 cabeza = m.P(HumanBodyBones.Head);
            Vector3 pecho = m.Pecho.position;
            float ancho = Vector3.Distance(m.P(HumanBodyBones.LeftUpperArm), m.P(HumanBodyBones.RightUpperArm));
            foreach (bool izquierda in new[] { true, false })
            {
                float l = izquierda ? -1f : 1f;
                m.Mano(izquierda,
                    cabeza + new Vector3(l * 0.25f * ancho, -0.3f * ancho, 0.4f * ancho),
                    pecho + new Vector3(l * 0.5f * ancho, -1.4f * ancho, 0.3f * ancho));
            }
            return m.Capturar();
        }

        Maniqui.Fotograma izquierda = Foto(-1f, 0f), centro = Foto(0f, 0.03f), derecha = Foto(1f, 0f);
        return new List<(float, Maniqui.Fotograma)>
        {
            (0f, izquierda), (0.45f, centro), (0.9f, derecha), (1.5f, derecha), (1.95f, centro), (2.4f, izquierda), (3f, izquierda),
        };
    }
}
