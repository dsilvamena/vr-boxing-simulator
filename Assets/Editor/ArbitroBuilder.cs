using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Tools > Boxeo > Montar réferi: monta el modelo del réferi (Art/Characters/Referee/Referee.fbx, Humanoid) en el Arbitro
/// de la escena abierta: Arbitro > Cuerpo (CuerpoArbitro) > Modelo (el FBX con su Animator) + Voz + CartelPulsar.
/// Si faltan, crea antes en Referee/Animations la pose de pie (Referee_DePie.anim, generada en espacio de músculos a partir
/// de la TPose del FBX, con respiración) y el controlador Referee.controller: un blend tree en el float "Agachado" entre
/// esa pose (0) y la postura de vigilar del FBX, Pose_Referee (1). CuerpoArbitro mueve "Agachado" según la fase y hace el
/// gesto de la cuenta con el brazo derecho del esqueleto. Si ya había un modelo (el provisional de primitivas), lo cambia.
/// El FBX trae solo esas dos poses, sin caminar: el réferi se desliza.
/// </summary>
public static class ArbitroBuilder
{
    private const string Carpeta = "Assets/_Project/Art/Characters/Referee";
    private const string RutaModelo = Carpeta + "/Referee.fbx";
    private const string Animaciones = Carpeta + "/Animations";
    private const string RutaDePie = Animaciones + "/Referee_DePie.anim";
    private const string RutaControlador = Animaciones + "/Referee.controller";
    private const string RutaCartel = Carpeta + "/Materials/Arbitro_Cartel.mat";
    private const string RutaFuente = "Assets/_Project/Fonts/Bangers SDF.asset";

    // Altura de la boca y del cartel con el réferi de pie (mide 1,82 m con el pelo)
    private const float AlturaVoz = 1.6f;
    private const float AlturaCartel = 2.15f;

    [MenuItem("Tools/Boxeo/Montar réferi")]
    public static void Montar()
    {
        var arbitro = Object.FindAnyObjectByType<Arbitro>();
        if (arbitro == null)
        {
            Debug.LogWarning("No hay un Arbitro en la escena abierta.");
            return;
        }
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(RutaModelo);
        if (fbx == null)
        {
            Debug.LogError($"No se encuentra {RutaModelo}.");
            return;
        }
        var controlador = AssetDatabase.LoadAssetAtPath<AnimatorController>(RutaControlador) ?? CrearControlador();
        Undo.RegisterFullObjectHierarchyUndo(arbitro.gameObject, "Montar réferi");

        // Cuerpo: lo que se mueve. Arranca en el sitio de la voz, a ras del piso del Arbitro
        Transform cuerpo = arbitro.transform.Find("Cuerpo");
        Transform voz = arbitro.transform.Find("Voz") ?? (cuerpo != null ? cuerpo.Find("Voz") : null);
        if (cuerpo == null)
        {
            cuerpo = new GameObject("Cuerpo").transform;
            cuerpo.SetParent(arbitro.transform, false);
            Vector3 sitio = voz != null ? voz.position : arbitro.transform.position;
            cuerpo.position = new Vector3(sitio.x, arbitro.transform.position.y, sitio.z);
            // Mira al centro del ring al empezar
            Vector3 alCentro = -new Vector3(cuerpo.position.x, 0f, cuerpo.position.z);
            if (alCentro.sqrMagnitude > 0.01f) cuerpo.rotation = Quaternion.LookRotation(alCentro, Vector3.up);
        }

        // El modelo anterior (el provisional u otro) se cambia por el FBX
        Transform modelo = cuerpo.Find("Modelo");
        if (modelo != null && PrefabUtility.GetCorrespondingObjectFromSource(modelo.gameObject) != fbx)
        {
            Undo.DestroyObjectImmediate(modelo.gameObject);
            modelo = null;
        }
        if (modelo == null)
        {
            var instancia = (GameObject)PrefabUtility.InstantiatePrefab(fbx, cuerpo);
            instancia.name = "Modelo";
            modelo = instancia.transform;
            modelo.localPosition = Vector3.zero;
            modelo.localRotation = Quaternion.identity;
            modelo.localScale = Vector3.one;
        }
        var animador = modelo.GetComponent<Animator>();
        animador.runtimeAnimatorController = controlador;
        animador.applyRootMotion = false;
        // Fuera de la vista no hace falta animarlo; el gesto de la cuenta se aplica encima en LateUpdate
        animador.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        foreach (var piel in modelo.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            piel.updateWhenOffscreen = false;
            piel.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        // La voz sale de la cabeza y se mueve con él
        if (voz != null)
        {
            voz.SetParent(cuerpo, false);
            voz.localPosition = new Vector3(0f, AlturaVoz, 0.05f);
            voz.localRotation = Quaternion.identity;
        }

        Transform cartel = ConfigurarCartel(cuerpo.Find("CartelPulsar") ?? CrearCartel(cuerpo));

        var componente = cuerpo.GetComponent<CuerpoArbitro>() ?? cuerpo.gameObject.AddComponent<CuerpoArbitro>();
        var so = new SerializedObject(componente);
        so.FindProperty("arbitro").objectReferenceValue = arbitro;
        // Vacío: con un Animator humanoide el gesto se hace con el brazo derecho del esqueleto
        so.FindProperty("brazoCuenta").objectReferenceValue = null;
        so.FindProperty("animador").objectReferenceValue = animador;
        so.FindProperty("cartelPulsar").objectReferenceValue = cartel.gameObject;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(arbitro.gameObject.scene);
        Selection.activeGameObject = cuerpo.gameObject;
        Debug.Log($"Réferi montado en {arbitro.gameObject.scene.name}, en {cuerpo.position}.");
    }

    // Blend tree en "Agachado": 0 = de pie (respirando), 1 = la postura de vigilar del FBX
    private static AnimatorController CrearControlador()
    {
        Directory.CreateDirectory(Animaciones);
        AnimationClip dePie = AssetDatabase.LoadAssetAtPath<AnimationClip>(RutaDePie) ?? CrearPoseDePie();
        AnimationClip vigilar = ClipDelModelo("Pose_Referee");

        var controlador = AnimatorController.CreateAnimatorControllerAtPath(RutaControlador);
        controlador.AddParameter("Agachado", AnimatorControllerParameterType.Float);
        controlador.AddParameter("Contando", AnimatorControllerParameterType.Bool);
        controlador.AddParameter("Contar", AnimatorControllerParameterType.Trigger);

        controlador.CreateBlendTreeInController("Postura", out BlendTree arbol, 0);
        arbol.blendType = BlendTreeType.Simple1D;
        arbol.blendParameter = "Agachado";
        arbol.useAutomaticThresholds = false;
        arbol.AddChild(dePie, 0f);
        arbol.AddChild(vigilar, 1f);
        // CreateBlendTreeInController añade un parámetro "Blend" que no se usa
        foreach (var sobra in controlador.parameters.Where(p => p.name == "Blend").ToArray())
            controlador.RemoveParameter(sobra);

        EditorUtility.SetDirty(controlador);
        AssetDatabase.SaveAssets();
        Debug.Log($"Creado {RutaControlador}");
        return controlador;
    }

    private static AnimationClip ClipDelModelo(string nombre) =>
        AssetDatabase.LoadAllAssetsAtPath(RutaModelo).OfType<AnimationClip>().First(c => c.name == nombre);

    // De pie y relajado: la TPose del FBX con los brazos caídos a los lados, los codos algo doblados y las piernas un poco
    // abiertas. Respira: el pecho y los brazos se mueven un poco en un ciclo de 4 s
    private static AnimationClip CrearPoseDePie()
    {
        AnimationClip tpose = ClipDelModelo("TPose");
        const float ciclo = 4f;
        var clip = new AnimationClip { name = "Referee_DePie", frameRate = 30f };

        var fijos = new System.Collections.Generic.Dictionary<string, float>
        {
            { "Left Arm Down-Up", -0.55f }, { "Right Arm Down-Up", -0.55f },
            { "Left Arm Front-Back", 0.35f }, { "Right Arm Front-Back", 0.35f },
            { "Left Forearm Stretch", 0.8f }, { "Right Forearm Stretch", 0.8f },
            { "Left Shoulder Down-Up", -0.1f }, { "Right Shoulder Down-Up", -0.1f },
            { "Left Upper Leg In-Out", 0.12f }, { "Right Upper Leg In-Out", 0.12f },
            { "Left Upper Leg Front-Back", 0.62f }, { "Right Upper Leg Front-Back", 0.62f },
            { "Left Lower Leg Stretch", 0.95f }, { "Right Lower Leg Stretch", 0.95f },
            { "Head Nod Down-Up", -0.05f },
        };
        // Amplitud de la respiración en cada músculo
        var respiracion = new System.Collections.Generic.Dictionary<string, float>
        {
            { "Chest Front-Back", 0.025f }, { "UpperChest Front-Back", 0.02f },
            { "Left Arm Down-Up", 0.015f }, { "Right Arm Down-Up", 0.015f },
        };

        foreach (var enlace in AnimationUtility.GetCurveBindings(tpose))
        {
            string n = enlace.propertyName;
            // Los objetivos de IK de manos y pies no hacen falta en una pose
            if (n.StartsWith("LeftHand") || n.StartsWith("RightHand") || n.StartsWith("LeftFoot") || n.StartsWith("RightFoot"))
                continue;
            float valor = fijos.TryGetValue(n, out float v) ? v : AnimationUtility.GetEditorCurve(tpose, enlace).Evaluate(0f);
            AnimationCurve curva;
            if (respiracion.TryGetValue(n, out float amplitud))
            {
                curva = new AnimationCurve();
                for (int i = 0; i <= 8; i++)
                {
                    float t = ciclo * i / 8f;
                    curva.AddKey(t, valor + amplitud * Mathf.Sin(2f * Mathf.PI * t / ciclo));
                }
                for (int i = 0; i < curva.length; i++) curva.SmoothTangents(i, 0f);
            }
            else
            {
                curva = AnimationCurve.Constant(0f, ciclo, valor);
            }
            AnimationUtility.SetEditorCurve(clip, enlace, curva);
        }

        var ajustes = AnimationUtility.GetAnimationClipSettings(clip);
        ajustes.loopTime = true;
        ajustes.keepOriginalPositionY = true;
        ajustes.keepOriginalOrientation = true;
        ajustes.keepOriginalPositionXZ = true;
        ajustes.loopBlendOrientation = true;
        ajustes.loopBlendPositionY = true;
        ajustes.loopBlendPositionXZ = true;
        AnimationUtility.SetAnimationClipSettings(clip, ajustes);

        AssetDatabase.CreateAsset(clip, RutaDePie);
        Debug.Log($"Creado {RutaDePie}");
        return clip;
    }

    private static Transform CrearCartel(Transform cuerpo)
    {
        var go = new GameObject("CartelPulsar", typeof(RectTransform), typeof(TextMeshPro));
        go.transform.SetParent(cuerpo, false);
        go.SetActive(false);
        return go.transform;
    }

    // Se aplica también si ya existía, para que volver a montar actualice el cartel
    private static Transform ConfigurarCartel(Transform cartel)
    {
        cartel.localPosition = new Vector3(0f, AlturaCartel, 0f);
        // El texto de TextMeshPro se lee desde -Z: se gira para que lo lea quien está delante del réferi
        cartel.localRotation = Quaternion.Euler(0f, 180f, 0f);
        cartel.localScale = Vector3.one;
        var rt = (RectTransform)cartel;
        rt.sizeDelta = new Vector2(0.8f, 0.3f);
        var texto = cartel.GetComponent<TextMeshPro>();
        texto.text = "¡ARRIBA!\n<size=55%>PULSA  A  B  X  Y</size>";
        texto.fontSize = 1f;
        texto.alignment = TextAlignmentOptions.Center;
        texto.color = new Color(1f, 0.82f, 0.25f);
        texto.textWrappingMode = TextWrappingModes.NoWrap;
        var fuente = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RutaFuente);
        if (fuente != null)
        {
            texto.font = fuente;
            // El contorno va en un material propio: cambiar outlineWidth en modo edición crearía una copia del
            // material dentro de la escena
            texto.fontSharedMaterial = MaterialCartel(fuente);
        }
        EditorUtility.SetDirty(texto);
        return cartel;
    }

    private static Material MaterialCartel(TMP_FontAsset fuente)
    {
        var existente = AssetDatabase.LoadAssetAtPath<Material>(RutaCartel);
        if (existente != null) return existente;
        Directory.CreateDirectory(Path.GetDirectoryName(RutaCartel));
        var material = new Material(fuente.material);
        material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.2f);
        material.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
        material.EnableKeyword(ShaderUtilities.Keyword_Outline);
        AssetDatabase.CreateAsset(material, RutaCartel);
        return material;
    }
}
