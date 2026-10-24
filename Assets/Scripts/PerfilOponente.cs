using UnityEngine;

/// <summary>
/// Datos de un personaje (cuántos golpes aguanta, cómo reacciona, sonidos). Un asset por oponente.
/// </summary>
[CreateAssetMenu(menuName = "Boxeo/Perfil de oponente", fileName = "PerfilOponente")]
public class PerfilOponente : ScriptableObject
{
    [Header("Caídas")]
    [Tooltip("Golpes (de cualquier tipo) que aguanta antes de caer")]
    public int golpesPorCaida = 10;
    [Tooltip("La caída número N es la derrota y ya no se levanta; las anteriores son knockdowns")]
    public int caidasParaDerrota = 3;
    [Tooltip("Segundos de un knockdown hasta que se levanta (con animación, lo que dura el clip)")]
    public float segundosKnockdown = 3.7f;
    [Tooltip("Segundos tras un golpe en los que no recibe otro")]
    public float invulnerabilidad = 0.2f;
    [Tooltip("Rapidez del puño (m/s) a partir de la cual el golpe ya es completo; más fuerte no suma")]
    public float rapidezGolpeCompleto = 2.5f;

    [Header("Ataques")]
    [Tooltip("Segundos entre ataques (se elige uno al azar en el rango)")]
    public Vector2 intervaloAtaque = new Vector2(4f, 6f);
    [Tooltip("Cada cuántos ataques uno es uppercut; el resto son jabs (0 = nunca)")]
    public int uppercutCada = 4;
    [Tooltip("Tras recibir un golpe, segundos hasta que puede volver a atacar (reinicia la espera)")]
    public Vector2 esperaTrasGolpe = new Vector2(1.5f, 2.5f);
    [Tooltip("Segundos tras levantarse de un knockdown antes del primer ataque")]
    public float esperaTrasLevantarse = 3f;

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
