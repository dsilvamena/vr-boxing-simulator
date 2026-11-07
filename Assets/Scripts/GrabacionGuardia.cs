using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Trayectoria de las dos manos al subir la guardia, grabada con el visor por GrabadoraGuardia (la actividad "Hand Grab
/// Recorder en los bloqueos" del cronograma). DefensaOponente la reproduce al bloquear: hacia delante al subir la guardia y
/// hacia atrás al bajarla, en vez de mover los guantes en línea recta.
/// Las posiciones van en el marco de la cabeza sin inclinación: x a la derecha, y hacia arriba (vertical del mundo) y z hacia
/// delante (frente de la cabeza, en horizontal), en metros. Así el rival, que siempre mira al jugador, las usa tal cual.
/// Las rotaciones se guardan en ese mismo marco para un uso posterior; todavía no se aplican al rival (hace falta calibrar
/// el giro entre el control y la mano del modelo con el visor).
/// </summary>
[CreateAssetMenu(menuName = "Boxeo/Grabación de guardia", fileName = "Guardia")]
public class GrabacionGuardia : ScriptableObject
{
    [Serializable]
    public struct Muestra
    {
        public float t;
        public Vector3 izquierda;
        public Vector3 derecha;
        public Quaternion rotacionIzquierda;
        public Quaternion rotacionDerecha;
    }

    [Tooltip("Quién o cómo se grabó; una provisional lo dice aquí")]
    public string origen;
    [Tooltip("Altura de la cabeza de quien grabó, sobre el piso, en metros. El rival escala la trayectoria a su altura")]
    public float alturaCabeza = 1.7f;
    public List<Muestra> muestras = new List<Muestra>();

    public float Duracion => muestras.Count > 0 ? muestras[muestras.Count - 1].t : 0f;
    public bool Valida => muestras != null && muestras.Count >= 2 && Duracion > 0f;

    /// <summary>Posición de cada mano en el avance dado de la subida (0 = manos abajo, 1 = guardia arriba).</summary>
    public void Evaluar(float avance, out Vector3 izquierda, out Vector3 derecha)
    {
        float t = Mathf.Clamp01(avance) * Duracion;
        int i = 1;
        while (i < muestras.Count - 1 && muestras[i].t < t) i++;
        Muestra a = muestras[i - 1];
        Muestra b = muestras[i];
        float k = b.t > a.t ? Mathf.Clamp01((t - a.t) / (b.t - a.t)) : 1f;
        izquierda = Vector3.Lerp(a.izquierda, b.izquierda, k);
        derecha = Vector3.Lerp(a.derecha, b.derecha, k);
    }

    /// <summary>
    /// Deja solo la subida: empieza cuando alguna mano pasa de velocidadInicio y termina cuando, después del pico, las dos
    /// se quedan por debajo de velocidadFin durante muestrasQuietas seguidas. El tiempo vuelve a empezar en 0.
    /// </summary>
    public void Recortar(float velocidadInicio = 0.25f, float velocidadFin = 0.15f, int muestrasQuietas = 4)
    {
        if (muestras.Count < 3) return;
        var rapidez = new float[muestras.Count];
        int pico = 0;
        for (int i = 1; i < muestras.Count; i++)
        {
            float dt = Mathf.Max(0.0001f, muestras[i].t - muestras[i - 1].t);
            rapidez[i] = Mathf.Max((muestras[i].izquierda - muestras[i - 1].izquierda).magnitude,
                                   (muestras[i].derecha - muestras[i - 1].derecha).magnitude) / dt;
            if (rapidez[i] > rapidez[pico]) pico = i;
        }
        int inicio = 0;
        for (int i = 1; i <= pico; i++)
        {
            if (rapidez[i] <= velocidadInicio) continue;
            inicio = i - 1;
            break;
        }
        int fin = muestras.Count - 1;
        int quietas = 0;
        for (int i = pico + 1; i < muestras.Count; i++)
        {
            quietas = rapidez[i] < velocidadFin ? quietas + 1 : 0;
            if (quietas < muestrasQuietas) continue;
            fin = i - muestrasQuietas + 1;
            break;
        }
        float t0 = muestras[inicio].t;
        var recortadas = new List<Muestra>();
        for (int i = inicio; i <= fin; i++)
        {
            Muestra m = muestras[i];
            m.t -= t0;
            recortadas.Add(m);
        }
        muestras = recortadas;
    }
}
