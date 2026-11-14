using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Maniquí para crear animaciones humanoides sin un programa de animación: una copia oculta de un personaje que se
/// coloca en una pose (se muestrea un clip y luego se mueven huesos: giros, IK de dos huesos para brazos y piernas),
/// se "fotografía" en espacio de músculos y con esas fotos se escribe un clip humanoide.
/// Como el clip queda en músculos, sirve para cualquier personaje Humanoid (se retargetea solo) y se mezcla con los
/// demás clips en el Animator. Los giros y posiciones se dan en el espacio del personaje (X = su derecha, Y = arriba,
/// Z = hacia donde mira), en metros del propio modelo.
/// Lo usan MenuBuilder (poses del menú), ReaccionesBuilder (reacciones a los golpes) y ArbitroBuilder (señas del réferi).
/// </summary>
public sealed class Maniqui : IDisposable
{
    /// <summary>Una pose completa en espacio de músculos.</summary>
    public struct Fotograma
    {
        public float[] musculos;
        public Vector3 posicion;
        public Quaternion giro;
    }

    public readonly GameObject Objeto;
    public readonly Animator Animador;
    private readonly HumanPoseHandler manejador;
    // Altura del tobillo más bajo con el modelo de pie en su pose de reposo: la referencia para apoyar los pies
    private readonly float alturaTobillo;

    public Maniqui(GameObject origen)
    {
        Objeto = UnityEngine.Object.Instantiate(origen);
        Objeto.hideFlags = HideFlags.HideAndDontSave;
        Objeto.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        Objeto.transform.localScale = Vector3.one;
        Animador = Objeto.GetComponentInChildren<Animator>();
        if (Animador == null || !Animador.isHuman) throw new ArgumentException($"{origen.name} no tiene un Animator Humanoid.");
        manejador = new HumanPoseHandler(Animador.avatar, Animador.transform);
        alturaTobillo = Mathf.Min(P(HumanBodyBones.LeftFoot).y, P(HumanBodyBones.RightFoot).y);
    }

    public void Dispose()
    {
        manejador.Dispose();
        UnityEngine.Object.DestroyImmediate(Objeto);
    }

    public Transform H(HumanBodyBones hueso) => Animador.GetBoneTransform(hueso);
    public Vector3 P(HumanBodyBones hueso) => H(hueso).position;

    /// <summary>Pecho alto si el esqueleto lo tiene; si no, el pecho.</summary>
    public Transform Pecho => H(HumanBodyBones.UpperChest) ?? H(HumanBodyBones.Chest) ?? H(HumanBodyBones.Spine);

    /// <summary>Punto medio entre los dos hombros (el nacimiento de los brazos).</summary>
    public Vector3 Hombros => (P(HumanBodyBones.LeftUpperArm) + P(HumanBodyBones.RightUpperArm)) * 0.5f;
    /// <summary>Distancia entre los dos hombros: la unidad para dar medidas que valgan en cualquier personaje.</summary>
    public float Ancho => Vector3.Distance(P(HumanBodyBones.LeftUpperArm), P(HumanBodyBones.RightUpperArm));

    /// <summary>Deja el maniquí en la pose de ese instante del clip.</summary>
    public void Muestrear(AnimationClip clip, float tiempo) => clip.SampleAnimation(Animador.gameObject, tiempo);

    /// <summary>Pone al maniquí en la pose de una foto.</summary>
    public void Aplicar(Fotograma f)
    {
        var pose = new HumanPose { muscles = (float[])f.musculos.Clone(), bodyPosition = f.posicion, bodyRotation = f.giro };
        manejador.SetHumanPose(ref pose);
    }

    /// <summary>
    /// De pie, erguido y de frente, con los brazos caídos a los lados y las piernas algo abiertas: el punto de partida
    /// de las poses que no salen de un clip. "apertura" abre las piernas (músculo Upper Leg In-Out).
    /// </summary>
    public void PoseDePie(float apertura = 0.12f, AnimationClip cabezaDe = null)
    {
        // Con todos los músculos a cero la cabeza no siempre queda derecha (depende de cómo se definió el avatar: la de
        // Bald Bull mira al techo). Se toma la de un clip en el que mira al frente: en su guardia el cuerpo va de lado,
        // pero la cabeza mira al rival
        Quaternion cabeza = Quaternion.identity, cuello = Quaternion.identity;
        if (cabezaDe != null)
        {
            Muestrear(cabezaDe, 0f);
            cabeza = H(HumanBodyBones.Head).rotation;
            if (H(HumanBodyBones.Neck) != null) cuello = H(HumanBodyBones.Neck).rotation;
        }
        var f = Capturar();
        for (int i = 0; i < f.musculos.Length; i++) f.musculos[i] = 0f;
        f = Sumar(f,
            ("Left Arm Down-Up", -0.55f), ("Right Arm Down-Up", -0.55f),
            ("Left Arm Front-Back", 0.35f), ("Right Arm Front-Back", 0.35f),
            ("Left Forearm Stretch", 0.8f), ("Right Forearm Stretch", 0.8f),
            ("Left Shoulder Down-Up", -0.1f), ("Right Shoulder Down-Up", -0.1f),
            ("Left Upper Leg In-Out", apertura), ("Right Upper Leg In-Out", apertura),
            ("Left Upper Leg Front-Back", 0.62f), ("Right Upper Leg Front-Back", 0.62f),
            ("Left Lower Leg Stretch", 0.95f), ("Right Lower Leg Stretch", 0.95f));
        f.giro = Quaternion.identity;
        f.posicion = new Vector3(0f, f.posicion.y, 0f);
        Aplicar(f);
        if (cabezaDe != null)
        {
            if (H(HumanBodyBones.Neck) != null) H(HumanBodyBones.Neck).rotation = cuello;
            H(HumanBodyBones.Head).rotation = cabeza;
        }
        Apoyar();
    }

    /// <summary>Sube o baja el cuerpo entero para que el tobillo más bajo quede a su altura de reposo.</summary>
    public void Apoyar()
    {
        float tobillo = Mathf.Min(P(HumanBodyBones.LeftFoot).y, P(HumanBodyBones.RightFoot).y);
        H(HumanBodyBones.Hips).position += Vector3.up * (alturaTobillo - tobillo);
    }

    /// <summary>Gira un hueso sobre un eje del personaje (vector = eje × grados), sin depender de los ejes del hueso.</summary>
    public void Girar(HumanBodyBones hueso, Vector3 ejeGrados)
    {
        Transform t = H(hueso);
        float grados = ejeGrados.magnitude;
        if (t == null || grados < 0.001f) return;
        t.rotation = Quaternion.AngleAxis(grados, ejeGrados / grados) * t.rotation;
    }

    /// <summary>Reparte un giro entre columna, pecho y pecho alto (los que existan).</summary>
    public void GirarTorso(Vector3 ejeGrados)
    {
        var huesos = new List<HumanBodyBones>();
        foreach (var h in new[] { HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest })
            if (H(h) != null) huesos.Add(h);
        foreach (var h in huesos) Girar(h, ejeGrados / huesos.Count);
    }

    /// <summary>Gira cuello y cabeza (40 % y 60 %).</summary>
    public void GirarCabeza(Vector3 ejeGrados)
    {
        bool hayCuello = H(HumanBodyBones.Neck) != null;
        if (hayCuello) Girar(HumanBodyBones.Neck, ejeGrados * 0.4f);
        Girar(HumanBodyBones.Head, ejeGrados * (hayCuello ? 0.6f : 1f));
    }

    /// <summary>Lleva la mano a un punto con el codo apuntando hacia "codo" (otro punto). La mano conserva su giro
    /// respecto al antebrazo.</summary>
    public void Mano(bool izquierda, Vector3 objetivo, Vector3 codo)
    {
        DosHuesos(
            H(izquierda ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm),
            H(izquierda ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm),
            H(izquierda ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand),
            objetivo, codo);
    }

    /// <summary>Desplaza la mano desde donde está (y el codo, de paso, hacia donde ya apuntaba más "codo").</summary>
    public void MoverMano(bool izquierda, Vector3 delta, Vector3 codo = default)
    {
        Vector3 mano = P(izquierda ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
        Vector3 codoActual = P(izquierda ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
        Mano(izquierda, mano + delta, codoActual + codo);
    }

    /// <summary>Mueve la cadera y vuelve a plantar los pies donde estaban (las rodillas se doblan o estiran).</summary>
    public void MoverCadera(Vector3 delta, Vector3 giroEjeGrados = default)
    {
        Transform pieI = H(HumanBodyBones.LeftFoot), pieD = H(HumanBodyBones.RightFoot);
        Vector3 pI = pieI.position, pD = pieD.position;
        Quaternion qI = pieI.rotation, qD = pieD.rotation;
        Vector3 rodillaI = P(HumanBodyBones.LeftLowerLeg), rodillaD = P(HumanBodyBones.RightLowerLeg);

        Transform cadera = H(HumanBodyBones.Hips);
        cadera.position += delta;
        Girar(HumanBodyBones.Hips, giroEjeGrados);

        Pie(true, pI, qI, rodillaI + Vector3.forward * 0.3f);
        Pie(false, pD, qD, rodillaD + Vector3.forward * 0.3f);
    }

    /// <summary>Planta un pie en un punto con ese giro; la rodilla apunta hacia "rodilla".</summary>
    public void Pie(bool izquierdo, Vector3 objetivo, Quaternion giro, Vector3 rodilla)
    {
        Transform pie = H(izquierdo ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);
        DosHuesos(
            H(izquierdo ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg),
            H(izquierdo ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg),
            pie, objetivo, rodilla);
        pie.rotation = giro;
    }

    /// <summary>Mueve un pie desde donde está (un paso), girándolo "grados" sobre la vertical.</summary>
    public void MoverPie(bool izquierdo, Vector3 delta, float grados = 0f)
    {
        Transform pie = H(izquierdo ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);
        Vector3 rodilla = P(izquierdo ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg);
        Pie(izquierdo, pie.position + delta, Quaternion.AngleAxis(grados, Vector3.up) * pie.rotation,
            rodilla + delta + Vector3.forward * 0.3f);
    }

    // IK analítica de dos huesos: dobla la articulación del medio lo justo para la distancia, orienta el primero
    // hacia el objetivo y lo rueda para que la articulación mire al polo
    private static void DosHuesos(Transform a, Transform b, Transform c, Vector3 objetivo, Vector3 polo)
    {
        float lab = Vector3.Distance(a.position, b.position);
        float lbc = Vector3.Distance(b.position, c.position);
        Vector3 hacia = objetivo - a.position;
        float distancia = Mathf.Clamp(hacia.magnitude, Mathf.Abs(lab - lbc) + 0.005f, (lab + lbc) * 0.999f);
        objetivo = a.position + hacia.normalized * distancia;

        Vector3 ba = a.position - b.position, bc = c.position - b.position;
        float actual = Vector3.Angle(ba, bc);
        float deseado = Mathf.Acos(Mathf.Clamp((lab * lab + lbc * lbc - distancia * distancia) / (2f * lab * lbc), -1f, 1f)) * Mathf.Rad2Deg;
        Vector3 eje = Vector3.Cross(ba, bc);
        if (eje.sqrMagnitude < 1e-8f) eje = Vector3.Cross(c.position - a.position, polo - a.position);
        if (eje.sqrMagnitude < 1e-8f) eje = Vector3.right;
        b.rotation = Quaternion.AngleAxis(deseado - actual, eje.normalized) * b.rotation;

        a.rotation = Quaternion.FromToRotation(c.position - a.position, objetivo - a.position) * a.rotation;

        Vector3 linea = (c.position - a.position).normalized;
        Vector3 codoActual = Vector3.ProjectOnPlane(b.position - a.position, linea);
        Vector3 codoDeseado = Vector3.ProjectOnPlane(polo - a.position, linea);
        if (codoActual.sqrMagnitude > 1e-8f && codoDeseado.sqrMagnitude > 1e-8f)
            a.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(codoActual, codoDeseado, linea), linea) * a.rotation;
    }

    /// <summary>Fotografía la pose actual en espacio de músculos.</summary>
    public Fotograma Capturar()
    {
        var pose = new HumanPose();
        manejador.GetHumanPose(ref pose);
        return new Fotograma { musculos = (float[])pose.muscles.Clone(), posicion = pose.bodyPosition, giro = pose.bodyRotation };
    }

    /// <summary>Cambia músculos de una foto por nombre (los de HumanTrait.MuscleName), sumando al valor que tenían.</summary>
    public static Fotograma Sumar(Fotograma f, params (string musculo, float delta)[] cambios)
    {
        var copia = new Fotograma { musculos = (float[])f.musculos.Clone(), posicion = f.posicion, giro = f.giro };
        foreach ((string musculo, float delta) in cambios)
        {
            int i = Array.IndexOf(HumanTrait.MuscleName, musculo);
            if (i < 0) throw new ArgumentException($"No existe el músculo '{musculo}'.");
            copia.musculos[i] += delta;
        }
        return copia;
    }

    /// <summary>Mezcla dos fotos (0 = a, 1 = b).</summary>
    public static Fotograma Mezclar(Fotograma a, Fotograma b, float t)
    {
        var m = new float[a.musculos.Length];
        for (int i = 0; i < m.Length; i++) m[i] = Mathf.LerpUnclamped(a.musculos[i], b.musculos[i], t);
        return new Fotograma
        {
            musculos = m,
            posicion = Vector3.LerpUnclamped(a.posicion, b.posicion, t),
            giro = Quaternion.SlerpUnclamped(a.giro, b.giro, t),
        };
    }

    /// <summary>
    /// Escribe (o reescribe, conservando el GUID) un clip humanoide con esas fotos como fotogramas clave, con tangentes
    /// suaves. En bucle, la primera y la última foto deben ser la misma.
    /// </summary>
    public static AnimationClip EscribirClip(string ruta, IList<(float tiempo, Fotograma foto)> claves, bool bucle)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ruta);
        bool nuevo = clip == null;
        if (nuevo) clip = new AnimationClip { frameRate = 30f };
        else clip.ClearCurves();
        clip.name = System.IO.Path.GetFileNameWithoutExtension(ruta);

        int n = claves.Count;
        for (int m = 0; m < HumanTrait.MuscleCount; m++)
        {
            var valores = new float[n];
            for (int k = 0; k < n; k++) valores[k] = claves[k].foto.musculos[m];
            Poner(clip, PropiedadMusculo(m), claves, valores, bucle);
        }

        var componentes = new float[7][];
        for (int i = 0; i < 7; i++) componentes[i] = new float[n];
        Quaternion anterior = claves[0].foto.giro;
        for (int k = 0; k < n; k++)
        {
            Vector3 p = claves[k].foto.posicion;
            Quaternion q = claves[k].foto.giro;
            // Mismo hemisferio que la clave anterior: interpolar componente a componente no da la vuelta larga
            if (Quaternion.Dot(anterior, q) < 0f) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
            anterior = q;
            componentes[0][k] = p.x; componentes[1][k] = p.y; componentes[2][k] = p.z;
            componentes[3][k] = q.x; componentes[4][k] = q.y; componentes[5][k] = q.z; componentes[6][k] = q.w;
        }
        string[] nombres = { "RootT.x", "RootT.y", "RootT.z", "RootQ.x", "RootQ.y", "RootQ.z", "RootQ.w" };
        for (int i = 0; i < 7; i++) Poner(clip, nombres[i], claves, componentes[i], bucle);

        var ajustes = AnimationUtility.GetAnimationClipSettings(clip);
        ajustes.loopTime = bucle;
        // La raíz no se mueve con el clip (applyRootMotion va apagado): todo el movimiento queda en la pose
        ajustes.keepOriginalPositionY = true;
        ajustes.keepOriginalOrientation = true;
        ajustes.keepOriginalPositionXZ = true;
        ajustes.loopBlendOrientation = true;
        ajustes.loopBlendPositionY = true;
        ajustes.loopBlendPositionXZ = true;
        AnimationUtility.SetAnimationClipSettings(clip, ajustes);

        if (nuevo)
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(ruta));
            AssetDatabase.CreateAsset(clip, ruta);
        }
        else EditorUtility.SetDirty(clip);
        return clip;
    }

    private static void Poner(AnimationClip clip, string propiedad, IList<(float tiempo, Fotograma foto)> claves, float[] valores, bool bucle)
    {
        var curva = new AnimationCurve();
        for (int k = 0; k < valores.Length; k++) curva.AddKey(new Keyframe(claves[k].tiempo, valores[k]));
        for (int k = 0; k < curva.length; k++) curva.SmoothTangents(k, 0f);
        if (bucle && curva.length > 2)
        {
            // La pendiente al cerrar el bucle es la de la curva que pasa por la penúltima, la primera y la segunda clave
            int u = curva.length - 1;
            float dt = (curva[1].time - curva[0].time) + (curva[u].time - curva[u - 1].time);
            float pendiente = dt > 0f ? (curva[1].value - curva[u - 1].value) / dt : 0f;
            Keyframe primera = curva[0], ultima = curva[u];
            primera.inTangent = primera.outTangent = pendiente;
            ultima.inTangent = ultima.outTangent = pendiente;
            curva.MoveKey(0, primera);
            curva.MoveKey(u, ultima);
        }
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), propiedad), curva);
    }

    // HumanTrait llama a los dedos "Left Index 1 Stretched"; en las curvas de un clip son "LeftHand.Index.1 Stretched"
    private static string PropiedadMusculo(int indice)
    {
        string nombre = HumanTrait.MuscleName[indice];
        foreach (string lado in new[] { "Left", "Right" })
            foreach (string dedo in new[] { "Thumb", "Index", "Middle", "Ring", "Little" })
            {
                string prefijo = lado + " " + dedo + " ";
                if (nombre.StartsWith(prefijo)) return lado + "Hand." + dedo + "." + nombre.Substring(prefijo.Length);
            }
        return nombre;
    }
}
