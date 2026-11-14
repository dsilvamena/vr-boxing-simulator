using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Tools > Boxeo > Crear reacciones a los golpes: genera con el Maniqui las reacciones que faltaban y las monta en el
/// controlador de los rivales. Hasta ahora todo golpe a la cabeza que no fuera un uppercut usaba el mismo clip (la
/// cabeza hacia atrás) y todo golpe al cuerpo, el mismo; un gancho de izquierda y uno de derecha se veían igual.
/// Clips nuevos, de cuerpo entero (cabeza, torso, cadera, rodillas y guardia, cada parte con su retraso):
/// - Cabeza_Derecha / Cabeza_Izquierda: un gancho le gira y le ladea la cabeza hacia donde va el guante, el torso la
///   sigue, la cadera se va de ese lado y la guardia se abre.
/// - Cuerpo_Derecha / Cuerpo_Izquierda: un golpe al costado lo dobla sobre el lado golpeado, baja el codo a cubrirse,
///   le ceden las rodillas y tarda más en recomponerse.
/// "Derecha" e "Izquierda" son hacia donde lo empuja el golpe, vistas desde el propio rival.
/// Cada clip parte de la guardia (Idle) de su personaje y vuelve a ella, así que hay un juego por cada guardia distinta:
/// Glass Joe (el controlador base), Bald Bull y Soda Popinski (sus override controllers; Mr. Sandman y Tyson usan el de
/// Soda). ReaccionAnimada elige el estado por la dirección del puño. Se puede repetir: reescribe los clips.
/// </summary>
public static class ReaccionesBuilder
{
    private const string Personajes = "Assets/_Project/Art/Characters/";
    private const string RutaControlador = Personajes + "GlassJoe/Animations/GlassJoe.controller";

    public static readonly string[] Estados = { "Cabeza_Derecha", "Cabeza_Izquierda", "Cuerpo_Derecha", "Cuerpo_Izquierda" };

    // Carpeta del personaje, modelo y override controller (vacío = usa el controlador base)
    private static readonly (string nombre, string modelo, string sobre)[] Bases =
    {
        ("GlassJoe", "GlassJoe/GlassJoe.fbx", ""),
        ("BaldBull", "BaldBull/BaldBull.prefab", "BaldBull/Animations/BaldBull.overrideController"),
        ("SodaPopinski", "SodaPopinski/SodaPopinski.prefab", "SodaPopinski/Animations/SodaPopinski.overrideController"),
    };

    [MenuItem("Tools/Boxeo/Crear reacciones a los golpes")]
    public static void Crear()
    {
        var controlador = AssetDatabase.LoadAssetAtPath<AnimatorController>(RutaControlador);
        if (controlador == null) { Debug.LogError($"Falta {RutaControlador}."); return; }
        AnimatorStateMachine maquina = controlador.layers[0].stateMachine;
        AnimatorState idle = maquina.states.First(s => s.state.name == "Idle").state;

        var clipsBase = new Dictionary<string, AnimationClip>();
        foreach ((string nombre, string modelo, string sobre) in Bases)
        {
            var origen = AssetDatabase.LoadAssetAtPath<GameObject>(Personajes + modelo);
            var controladorSobre = string.IsNullOrEmpty(sobre) ? null : AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(Personajes + sobre);
            AnimationClip guardia = MenuBuilder.ClipDeIdle(controladorSobre != null ? controladorSobre : (RuntimeAnimatorController)controlador);
            if (origen == null || guardia == null) { Debug.LogWarning($"Falta el modelo o la guardia de {nombre}."); continue; }

            using var maniqui = new Maniqui(origen);
            foreach (string estado in Estados)
            {
                float lado = estado.EndsWith("Derecha") ? 1f : -1f;
                List<(float, Maniqui.Fotograma)> claves = estado.StartsWith("Cabeza") ? GanchoALaCabeza(maniqui, guardia, lado) : GolpeAlCostado(maniqui, guardia, lado);
                AnimationClip clip = Maniqui.EscribirClip($"{Personajes}{nombre}/Animations/Generadas/{nombre}@{estado}.anim", claves, false);

                if (controladorSobre == null)
                {
                    clipsBase[estado] = clip;
                    AnimatorState nuevo = maquina.states.FirstOrDefault(s => s.state.name == estado).state;
                    if (nuevo == null)
                    {
                        nuevo = maquina.AddState(estado);
                        AnimatorStateTransition vuelta = nuevo.AddTransition(idle);
                        vuelta.hasExitTime = true;
                        vuelta.exitTime = 0.86f;
                        vuelta.hasFixedDuration = true;
                        vuelta.duration = 0.14f;
                    }
                    nuevo.motion = clip;
                }
                else
                {
                    // Un override cambia clips, no estados: la clave es el clip del controlador base
                    controladorSobre[clipsBase[estado]] = clip;
                    EditorUtility.SetDirty(controladorSobre);
                }
            }
        }
        EditorUtility.SetDirty(controlador);
        AssetDatabase.SaveAssets();
        Debug.Log("Reacciones a los golpes creadas y montadas en los controladores.");
    }

    // ---------- Gancho a la cabeza ----------

    // lado = +1 lo empuja hacia su derecha. Cada parte del cuerpo lleva su propio peso para poder retrasarlas
    private static Maniqui.Fotograma PoseGancho(Maniqui m, AnimationClip guardia, float lado, float cabeza, float torso, float cadera, float brazos)
    {
        m.Muestrear(guardia, 0f);
        // La cadera se va con el golpe y se hunde: las rodillas ceden
        m.MoverCadera(new Vector3(lado * 0.07f, -0.05f, -0.03f) * cadera, Vector3.up * (lado * 7f * cadera));
        // El torso gira con el golpe, se ladea hacia ese lado y se echa algo atrás
        m.GirarTorso((Vector3.up * (lado * 22f) + Vector3.forward * (-lado * 12f) + Vector3.right * -6f) * torso);
        // La cabeza: gira hacia donde va el guante y se ladea
        m.GirarCabeza((Vector3.up * (lado * 40f) + Vector3.forward * (-lado * 16f) + Vector3.right * 6f) * cabeza);
        // La guardia se abre: el guante del lado hacia el que sale despedido se va fuera y abajo, el otro baja
        bool manoLejana = lado < 0f;   // true = izquierda
        m.MoverMano(manoLejana, new Vector3(lado * 0.15f, -0.11f, 0.03f) * brazos);
        m.MoverMano(!manoLejana, new Vector3(lado * 0.06f, -0.07f, 0f) * brazos);
        return m.Capturar();
    }

    private static List<(float, Maniqui.Fotograma)> GanchoALaCabeza(Maniqui m, AnimationClip guardia, float lado)
    {
        // tiempo, cabeza, torso, cadera, brazos: la cabeza llega primero y rebota; el cuerpo la sigue con retraso
        (float t, float cabeza, float torso, float cadera, float brazos)[] curva =
        {
            (0f, 0f, 0f, 0f, 0f),
            (0.06f, 1f, 0.45f, 0.2f, 0.3f),
            (0.14f, 0.85f, 1f, 0.7f, 0.9f),
            (0.27f, 0.35f, 0.8f, 1f, 1f),
            (0.43f, -0.12f, 0.3f, 0.6f, 0.6f),
            (0.63f, 0.03f, 0.05f, 0.2f, 0.2f),
            (0.85f, 0f, 0f, 0f, 0f),
        };
        return curva.Select(c => (c.t, PoseGancho(m, guardia, lado, c.cabeza, c.torso, c.cadera, c.brazos))).ToList();
    }

    // ---------- Golpe al costado ----------

    // lado = +1: el guante entra por su costado izquierdo y lo empuja hacia su derecha
    private static Maniqui.Fotograma PoseCostado(Maniqui m, AnimationClip guardia, float lado, float cabeza, float torso, float cadera, float brazos)
    {
        m.Muestrear(guardia, 0f);
        // La cintura se va con el golpe y las rodillas ceden más que con un golpe a la cabeza
        m.MoverCadera(new Vector3(lado * 0.06f, -0.08f, -0.05f) * cadera, Vector3.up * (lado * 4f * cadera));
        // Se dobla hacia delante y sobre el lado golpeado (el hombro de ese lado baja)
        m.GirarTorso((Vector3.right * 20f + Vector3.forward * (lado * 15f) + Vector3.up * (lado * 6f)) * torso);
        // La cabeza cae hacia delante y hacia el lado golpeado
        m.GirarCabeza((Vector3.right * 15f + Vector3.forward * (lado * 8f)) * cabeza);
        // El codo del lado golpeado baja a cubrir el costado; la otra mano baja un poco
        bool manoGolpeada = lado > 0f;   // true = izquierda
        m.MoverMano(manoGolpeada, new Vector3(lado * 0.04f, -0.2f, -0.06f) * brazos, new Vector3(0f, -0.1f, -0.1f) * brazos);
        m.MoverMano(!manoGolpeada, new Vector3(0f, -0.07f, 0f) * brazos);
        return m.Capturar();
    }

    private static List<(float, Maniqui.Fotograma)> GolpeAlCostado(Maniqui m, AnimationClip guardia, float lado)
    {
        (float t, float cabeza, float torso, float cadera, float brazos)[] curva =
        {
            (0f, 0f, 0f, 0f, 0f),
            (0.08f, 0.4f, 0.8f, 0.45f, 0.3f),
            (0.2f, 0.9f, 1f, 0.9f, 1f),
            (0.45f, 1f, 0.85f, 1f, 0.95f),
            (0.75f, 0.5f, 0.42f, 0.55f, 0.5f),
            (1.02f, 0.1f, 0.08f, 0.15f, 0.12f),
            (1.25f, 0f, 0f, 0f, 0f),
        };
        return curva.Select(c => (c.t, PoseCostado(m, guardia, lado, c.cabeza, c.torso, c.cadera, c.brazos))).ToList();
    }
}
