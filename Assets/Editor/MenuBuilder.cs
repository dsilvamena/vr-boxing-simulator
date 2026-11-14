using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Tools > Boxeo > Poner rivales en el menú: en Main_Scene cambia los dos modelos estáticos antiguos (bald_bull y
/// mr_sandman, .glb sin esqueleto) por los cinco rivales del juego, los mismos modelos con esqueleto de las peleas,
/// en arco frente al jugador y en orden de nivel de izquierda a derecha. Cada uno queda en su guardia (Idle), respira
/// y sigue al jugador con la mirada (CuerpoVivo, que además desfasa su Idle para que no se muevan a la vez).
/// Son solo adorno: no llevan Oponente ni colisiones. Se puede repetir: recoloca los que ya estén.
/// </summary>
public static class MenuBuilder
{
    private const string Escena = "Assets/Scenes/Main_Scene.unity";
    private const string Personajes = "Assets/_Project/Art/Characters/";

    // Nombre en la escena, prefab (vacío = ya está en la escena), escala, x, z
    private static readonly (string nombre, string prefab, float escala, float x, float z)[] Rivales =
    {
        ("GlassJoe", "", 1f, -2.1f, 0.25f),
        ("BaldBull", "BaldBull/BaldBull.prefab", 1.15f, -1.1f, 0.95f),
        ("SodaPopinski", "SodaPopinski/SodaPopinski.prefab", 0.8f, 0f, 1.3f),
        ("MrSandman", "MrSandman/MrSandman.prefab", 1f, 1.1f, 0.95f),
        ("MikeTyson", "MikeTyson/MikeTyson.prefab", 1f, 2.1f, 0.25f),
    };

    [MenuItem("Tools/Boxeo/Poner rivales en el menú")]
    private static void Poner()
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

            foreach ((string nombre, string prefab, float escala, float x, float z) in Rivales)
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
                if (animator != null && animator.GetComponent<CuerpoVivo>() == null) animator.gameObject.AddComponent<CuerpoVivo>();
            }
            EditorSceneManager.MarkSceneDirty(escena);
            EditorSceneManager.SaveScene(escena);
            Debug.Log("Rivales del menú colocados.");
        }
        finally
        {
            if (abiertas.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(abiertas);
        }
    }
}
