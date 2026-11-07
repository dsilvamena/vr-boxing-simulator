using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Tools > Boxeo > Montar réferi provisional: crea (si falta) un réferi hecho con primitivas, camisa blanca, pantalón y
/// pajarita negros, como en el boceto del informe, y lo monta en el Arbitro de la escena abierta:
/// Arbitro > Cuerpo (CuerpoArbitro) > Modelo (el prefab) + Voz + CartelPulsar.
/// Es un marcador de posición: cuando llegue el modelo definitivo se borra Modelo, se pone el nuevo como hijo de Cuerpo y
/// se asigna su hombro (o su Animator) en CuerpoArbitro. No hace falta tocar código.
/// </summary>
public static class ArbitroBuilder
{
    private const string Carpeta = "Assets/_Project/Art/Characters/ArbitroProvisional";
    private const string RutaPrefab = Carpeta + "/ArbitroProvisional.prefab";
    private const string RutaFuente = "Assets/_Project/Fonts/Bangers SDF.asset";

    [MenuItem("Tools/Boxeo/Montar réferi provisional")]
    public static void Montar()
    {
        var arbitro = Object.FindAnyObjectByType<Arbitro>();
        if (arbitro == null)
        {
            Debug.LogWarning("No hay un Arbitro en la escena abierta.");
            return;
        }
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RutaPrefab) ?? CrearPrefab();
        Undo.RegisterFullObjectHierarchyUndo(arbitro.gameObject, "Montar réferi provisional");

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

        Transform modelo = cuerpo.Find("Modelo");
        if (modelo == null)
        {
            var instancia = (GameObject)PrefabUtility.InstantiatePrefab(prefab, cuerpo);
            instancia.name = "Modelo";
            modelo = instancia.transform;
            modelo.localPosition = Vector3.zero;
            modelo.localRotation = Quaternion.identity;
        }

        // La voz sale de la cabeza y se mueve con él
        if (voz != null)
        {
            voz.SetParent(cuerpo, false);
            voz.localPosition = new Vector3(0f, 1.62f, 0.05f);
            voz.localRotation = Quaternion.identity;
        }

        Transform cartel = ConfigurarCartel(cuerpo.Find("CartelPulsar") ?? CrearCartel(cuerpo));

        var componente = cuerpo.GetComponent<CuerpoArbitro>() ?? cuerpo.gameObject.AddComponent<CuerpoArbitro>();
        var so = new SerializedObject(componente);
        so.FindProperty("arbitro").objectReferenceValue = arbitro;
        so.FindProperty("brazoCuenta").objectReferenceValue = modelo.Find("HombroDerecho");
        so.FindProperty("cartelPulsar").objectReferenceValue = cartel.gameObject;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(arbitro.gameObject.scene);
        Selection.activeGameObject = cuerpo.gameObject;
        Debug.Log($"Réferi provisional montado en {arbitro.gameObject.scene.name}, en {cuerpo.position}.");
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
        cartel.localPosition = new Vector3(0f, 2.0f, 0f);
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
        const string ruta = Carpeta + "/Materials/Arbitro_Cartel.mat";
        var existente = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (existente != null) return existente;
        Directory.CreateDirectory(Carpeta + "/Materials");
        var material = new Material(fuente.material);
        material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.2f);
        material.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
        material.EnableKeyword(ShaderUtilities.Keyword_Outline);
        AssetDatabase.CreateAsset(material, ruta);
        return material;
    }

    private static GameObject CrearPrefab()
    {
        Directory.CreateDirectory(Carpeta + "/Materials");
        Material camisa = Material(Carpeta + "/Materials/Arbitro_Camisa.mat", new Color(0.93f, 0.93f, 0.93f));
        Material pantalon = Material(Carpeta + "/Materials/Arbitro_Pantalon.mat", new Color(0.08f, 0.08f, 0.09f));
        Material piel = Material(Carpeta + "/Materials/Arbitro_Piel.mat", new Color(0.86f, 0.66f, 0.52f));
        Material negro = Material(Carpeta + "/Materials/Arbitro_Negro.mat", new Color(0.02f, 0.02f, 0.02f));

        var raiz = new GameObject("ArbitroProvisional");
        // Medidas de una persona de 1,75 m, con el origen en los pies y mirando a +Z
        Pieza(raiz.transform, "PiernaIzquierda", PrimitiveType.Capsule, new Vector3(-0.1f, 0.43f, 0f), new Vector3(0.15f, 0.43f, 0.15f), pantalon);
        Pieza(raiz.transform, "PiernaDerecha", PrimitiveType.Capsule, new Vector3(0.1f, 0.43f, 0f), new Vector3(0.15f, 0.43f, 0.15f), pantalon);
        Pieza(raiz.transform, "ZapatoIzquierdo", PrimitiveType.Cube, new Vector3(-0.1f, 0.035f, 0.04f), new Vector3(0.11f, 0.07f, 0.26f), negro);
        Pieza(raiz.transform, "ZapatoDerecho", PrimitiveType.Cube, new Vector3(0.1f, 0.035f, 0.04f), new Vector3(0.11f, 0.07f, 0.26f), negro);
        Pieza(raiz.transform, "Torso", PrimitiveType.Capsule, new Vector3(0f, 1.17f, 0f), new Vector3(0.4f, 0.34f, 0.25f), camisa);
        Pieza(raiz.transform, "Cinturon", PrimitiveType.Cylinder, new Vector3(0f, 0.9f, 0f), new Vector3(0.38f, 0.03f, 0.25f), negro);
        Pieza(raiz.transform, "Cuello", PrimitiveType.Cylinder, new Vector3(0f, 1.5f, 0f), new Vector3(0.1f, 0.05f, 0.1f), piel);
        Pieza(raiz.transform, "Cabeza", PrimitiveType.Sphere, new Vector3(0f, 1.63f, 0f), new Vector3(0.2f, 0.24f, 0.22f), piel);
        Pieza(raiz.transform, "Pelo", PrimitiveType.Sphere, new Vector3(0f, 1.7f, -0.015f), new Vector3(0.205f, 0.13f, 0.21f), negro);
        // Ojos y nariz: dejan ver hacia dónde mira
        Pieza(raiz.transform, "OjoIzquierdo", PrimitiveType.Sphere, new Vector3(-0.045f, 1.655f, 0.095f), Vector3.one * 0.03f, negro);
        Pieza(raiz.transform, "OjoDerecho", PrimitiveType.Sphere, new Vector3(0.045f, 1.655f, 0.095f), Vector3.one * 0.03f, negro);
        Pieza(raiz.transform, "Nariz", PrimitiveType.Sphere, new Vector3(0f, 1.62f, 0.11f), new Vector3(0.035f, 0.05f, 0.04f), piel);
        Pieza(raiz.transform, "Pajarita", PrimitiveType.Cube, new Vector3(0f, 1.455f, 0.115f), new Vector3(0.13f, 0.045f, 0.03f), negro);

        Brazo(raiz.transform, "HombroIzquierdo", new Vector3(-0.25f, 1.42f, 0f), camisa, piel);
        Brazo(raiz.transform, "HombroDerecho", new Vector3(0.25f, 1.42f, 0f), camisa, piel);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(raiz, RutaPrefab);
        Object.DestroyImmediate(raiz);
        Debug.Log($"Creado {RutaPrefab}");
        return prefab;
    }

    // Hombro (pivote que gira) con la manga y la mano colgando hacia abajo
    private static void Brazo(Transform padre, string nombre, Vector3 hombro, Material manga, Material mano)
    {
        var pivote = new GameObject(nombre).transform;
        pivote.SetParent(padre, false);
        pivote.localPosition = hombro;
        Pieza(pivote, "Brazo", PrimitiveType.Capsule, new Vector3(0f, -0.3f, 0f), new Vector3(0.11f, 0.3f, 0.11f), manga);
        Pieza(pivote, "Mano", PrimitiveType.Sphere, new Vector3(0f, -0.63f, 0f), new Vector3(0.09f, 0.11f, 0.06f), mano);
    }

    private static void Pieza(Transform padre, string nombre, PrimitiveType tipo, Vector3 posicion, Vector3 escala, Material material)
    {
        var go = GameObject.CreatePrimitive(tipo);
        go.name = nombre;
        // Sin colliders: el réferi no debe frenar los puños ni empujar a nadie
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(padre, false);
        go.transform.localPosition = posicion;
        go.transform.localScale = escala;
        var render = go.GetComponent<MeshRenderer>();
        render.sharedMaterial = material;
        render.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
    }

    private static Material Material(string ruta, Color color)
    {
        var existente = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (existente != null) return existente;
        var material = new Material(Shader.Find("Standard")) { color = color };
        material.SetFloat("_Glossiness", 0.2f);
        AssetDatabase.CreateAsset(material, ruta);
        return material;
    }
}
