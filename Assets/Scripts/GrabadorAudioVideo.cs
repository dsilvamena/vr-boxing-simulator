#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;

/// <summary>
/// SOLO PARA EL EDITOR, lo usa DirectorVideo: graba en un WAV la mezcla final que oye el AudioListener
/// (OnAudioFilterRead, en el hilo de audio). Va en el objeto del AudioListener de cada escena; como el listener se
/// destruye al cambiar de escena, el reloj de audio (AudioSettings.dspTime) decide cuánto silencio rellenar para que el
/// audio siga alineado con el video.
/// </summary>
public class GrabadorAudioVideo : MonoBehaviour
{
    private static readonly object Cerrojo = new object();
    private static FileStream archivo;
    private static double dspInicio;
    private static long muestrasEscritas;   // por canal
    private static int canales, frecuencia;
    private static bool abierto;

    public static void Abrir(string ruta)
    {
        lock (Cerrojo)
        {
            archivo = new FileStream(ruta, FileMode.Create);
            archivo.Write(new byte[44], 0, 44);
            frecuencia = AudioSettings.outputSampleRate;
            canales = 0;
            muestrasEscritas = 0;
            dspInicio = AudioSettings.dspTime;
            abierto = true;
        }
    }

    public static void Cerrar()
    {
        lock (Cerrojo)
        {
            if (!abierto) return;
            abierto = false;
            if (canales == 0) canales = 2;
            long bytes = muestrasEscritas * canales * 2;
            archivo.Seek(0, SeekOrigin.Begin);
            var w = new BinaryWriter(archivo);
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write((int)(36 + bytes)); w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            w.Write(System.Text.Encoding.ASCII.GetBytes("fmt ")); w.Write(16); w.Write((short)1); w.Write((short)canales);
            w.Write(frecuencia); w.Write(frecuencia * canales * 2); w.Write((short)(canales * 2)); w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write((int)bytes);
            w.Flush();
            archivo.Dispose();
            archivo = null;
        }
    }

    private void OnAudioFilterRead(float[] datos, int ch)
    {
        lock (Cerrojo)
        {
            if (!abierto) return;
            if (canales == 0) canales = ch;
            if (ch != canales) return;
            int muestras = datos.Length / ch;
            // Lo que debería llevar escrito según el reloj de audio: si falta (cambio de escena), silencio
            long esperado = (long)((AudioSettings.dspTime - dspInicio) * frecuencia) - muestras;
            if (esperado > muestrasEscritas)
            {
                int hueco = (int)Math.Min(esperado - muestrasEscritas, frecuencia * 5L);
                var ceros = new byte[hueco * canales * 2];
                archivo.Write(ceros, 0, ceros.Length);
                muestrasEscritas += hueco;
            }
            var bytes = new byte[datos.Length * 2];
            for (int i = 0; i < datos.Length; i++)
            {
                short v = (short)(Mathf.Clamp(datos[i], -1f, 1f) * 32767f);
                bytes[2 * i] = (byte)(v & 0xff);
                bytes[2 * i + 1] = (byte)((v >> 8) & 0xff);
            }
            archivo.Write(bytes, 0, bytes.Length);
            muestrasEscritas += muestras;
        }
    }

    /// <summary>Segundos de audio escritos (para comprobar la grabación).</summary>
    public static double Segundos { get { lock (Cerrojo) return frecuencia > 0 ? muestrasEscritas / (double)frecuencia : 0; } }
}
#endif
