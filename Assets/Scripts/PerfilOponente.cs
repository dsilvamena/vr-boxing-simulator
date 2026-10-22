using UnityEngine;

/// <summary>
/// Datos de un personaje (vida, daño, cómo reacciona, sonidos). Un asset por oponente.
/// </summary>
[CreateAssetMenu(menuName = "Boxeo/Perfil de oponente", fileName = "PerfilOponente")]
public class PerfilOponente : ScriptableObject
{
    [Header("Vida")]
    public float vidaMaxima = 100f;
    [Tooltip("Daño de un golpe completo al torso (en la cabeza se multiplica por la zona)")]
    public float danioGolpe = 12f;
    [Tooltip("Rapidez del puño (m/s) a partir de la cual el golpe ya es completo; más fuerte no suma")]
    public float rapidezGolpeCompleto = 2.5f;
    [Tooltip("Multiplicador del daño de los golpes laterales (ganchos), que no son jab, cross ni uppercut")]
    public float multiplicadorLateral = 0.5f;
    [Tooltip("Segundos tras un golpe en los que no recibe otro")]
    public float invulnerabilidad = 0.2f;
    [Tooltip("Segundos en el suelo antes de levantarse con la vida llena (0 = no se levanta)")]
    public float segundosKO = 4f;

    [Header("Reacción procedural")]
    [Tooltip("Cuánto lo inclina un golpe (más bajo = más pesado)")]
    public float fuerzaReaccion = 1f;
    [Tooltip("Rigidez del resorte que lo devuelve a su postura")]
    public float rigidez = 120f;
    [Tooltip("Amortiguación del resorte")]
    public float amortiguacion = 12f;
    [Tooltip("Inclinación máxima por golpe, en grados")]
    public float anguloMaximo = 25f;
    [Tooltip("Retroceso máximo por golpe, en metros")]
    public float retrocesoMaximo = 0.15f;

    [Header("Sonidos (opcionales)")]
    public AudioClip[] sonidosGolpe;
    public AudioClip sonidoKO;
}
