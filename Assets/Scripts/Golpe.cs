using UnityEngine;

public enum Mano { Izquierda, Derecha }

public enum TipoZona { Cabeza, Torso }

/// <summary>
/// Jab, Cross y Uppercut son los golpes del juego. Lateral (ganchos y golpes de lado) cuenta, pero vale menos.
/// </summary>
public enum TipoGolpe { Jab, Cross, Uppercut, Lateral }

/// <summary>
/// Datos de un golpe que un Puno le conecta a una ZonaGolpe.
/// </summary>
public struct Golpe
{
    public Mano mano;
    public TipoGolpe tipo;
    public ZonaGolpe zona;
    /// <summary>Punto de contacto aproximado, en mundo.</summary>
    public Vector3 punto;
    /// <summary>Velocidad de la mano relativa al jugador (sin la locomoción con joystick), en mundo.</summary>
    public Vector3 velocidad;
    /// <summary>0-1, cuánto de "golpe completo" fue según el perfil. Lo rellena el Oponente.</summary>
    public float intensidad;
    /// <summary>Lo rellena el Oponente al aplicar el daño.</summary>
    public float danio;

    public float Rapidez => velocidad.magnitude;

    /// <summary>Ángulo máximo (grados) sobre la horizontal para que un golpe hacia arriba cuente como uppercut.</summary>
    private const float AnguloUppercut = 50f;
    /// <summary>Desviación lateral máxima (grados) respecto al frente para que un golpe sea recto (jab o cross).</summary>
    private const float AnguloRecto = 40f;

    /// <summary>
    /// Clasifica por la dirección de la mano respecto a hacia dónde mira el jugador. Guardia diestra:
    /// el recto con la izquierda (adelantada) es jab y con la derecha es cross.
    /// </summary>
    public static TipoGolpe Clasificar(Mano mano, Vector3 velocidad, Vector3 frenteJugador)
    {
        if (Vector3.Angle(velocidad, Vector3.up) < AnguloUppercut) return TipoGolpe.Uppercut;

        Vector3 frente = Vector3.ProjectOnPlane(frenteJugador, Vector3.up).normalized;
        Vector3 horizontal = Vector3.ProjectOnPlane(velocidad, Vector3.up);
        if (Vector3.Angle(horizontal, frente) > AnguloRecto) return TipoGolpe.Lateral;

        return mano == Mano.Izquierda ? TipoGolpe.Jab : TipoGolpe.Cross;
    }
}
