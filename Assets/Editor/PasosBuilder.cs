using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Tools > Boxeo > Poner pasos procedurales: añade PasosProcedurales al Animator de cada rival (pasos solo en Idle) y del
/// réferi (pasos siempre) en las escenas de pelea de Build Settings, y activa el IK Pass de la capa base de sus
/// controladores (el de Glass Joe ya lo tenía; los override controllers lo heredan). No cambia nada que ya esté puesto.
/// </summary>
public static class PasosBuilder
{
    [MenuItem("Tools/Boxeo/Poner pasos procedurales")]
    private static void Poner()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("No en Play."); return; }
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) { Debug.LogError("Guarda o descarta los cambios de las escenas abiertas antes."); return; }

        SceneSetup[] abiertas = EditorSceneManager.GetSceneManagerSetup();
        int anadidos = 0;
        try
        {
            foreach (string ruta in EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path))
            {
                Scene escena = EditorSceneManager.OpenScene(ruta, OpenSceneMode.Single);
                bool cambio = false;
                foreach (GameObject raiz in escena.GetRootGameObjects())
                {
                    foreach (Oponente oponente in raiz.GetComponentsInChildren<Oponente>(true))
                        cambio |= Poner(oponente.GetComponentInChildren<Animator>(true), soloIdle: true, ref anadidos);
                    foreach (CuerpoArbitro cuerpo in raiz.GetComponentsInChildren<CuerpoArbitro>(true))
                        cambio |= Poner(cuerpo.GetComponentInChildren<Animator>(true), soloIdle: false, ref anadidos);
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
        AssetDatabase.SaveAssets();
        Debug.Log($"Pasos procedurales: {anadidos} componentes añadidos.");
    }

    private static bool Poner(Animator animator, bool soloIdle, ref int anadidos)
    {
        if (animator == null || animator.avatar == null || !animator.avatar.isHuman) return false;
        ActivarIKPass(animator.runtimeAnimatorController);
        if (animator.GetComponent<PasosProcedurales>() != null) return false;
        var pasos = animator.gameObject.AddComponent<PasosProcedurales>();
        if (!soloIdle)
        {
            var so = new SerializedObject(pasos);
            so.FindProperty("estados").arraySize = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        anadidos++;
        return true;
    }

    private static void ActivarIKPass(RuntimeAnimatorController controlador)
    {
        if (controlador is AnimatorOverrideController o) controlador = o.runtimeAnimatorController;
        if (controlador is not AnimatorController c || c.layers.Length == 0 || c.layers[0].iKPass) return;
        AnimatorControllerLayer[] capas = c.layers;
        capas[0].iKPass = true;
        c.layers = capas;
        EditorUtility.SetDirty(c);
        Debug.Log($"IK Pass activado en la capa base de {c.name}.");
    }
}
