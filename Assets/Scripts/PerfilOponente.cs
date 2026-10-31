using UnityEngine;

/// <summary>
/// Datos de un personaje (salud, ataques, cómo reacciona, sonidos). Un asset por oponente.
/// </summary>
[CreateAssetMenu(menuName = "Boxeo/Perfil de oponente", fileName = "PerfilOponente")]
public class PerfilOponente : ScriptableObject
{
    [Tooltip("Nombre que ve el jugador (panel de salud, victoria); vacío = nombre del objeto")]
    public string nombre = "";

    [Header("Salud (RF-04)")]
    public float saludMaxima = 100f;
    [Tooltip("Daño base de cada tipo de golpe del jugador, antes de rapidez y zona")]
    public float danoJab = 3f;
    public float danoCross = 5f;
    public float danoUppercut = 6f;
    public float danoLateral = 4f;
    [Tooltip("Multiplicador si el golpe da en la cabeza")]
    public float multiplicadorCabeza = 1.5f;
    [Tooltip("Fracción del daño que se aplica con un golpe lento (0 a 1)")]
    [Range(0f, 1f)] public float danoMinimoPorRapidez = 0.4f;

    [Header("Caídas")]
    [Tooltip("La caída número N es la derrota y ya no se levanta; las anteriores son knockdowns")]
    public int caidasParaDerrota = 3;
    [Tooltip("Segundos de un knockdown hasta que se levanta (con animación, lo que dura el clip)")]
    public float segundosKnockdown = 3.7f;
    [Tooltip("Segundos tras un golpe en los que no recibe otro")]
    public float invulnerabilidad = 0.2f;
    [Tooltip("Rapidez del puño (m/s) a partir de la cual el golpe ya es completo; más fuerte no suma")]
    public float rapidezGolpeCompleto = 2.5f;

    [Header("Defensa (RF-07)")]
    [Tooltip("Probabilidad de bloquear un golpe del jugador cuando está en guardia (0 = nunca)")]
    [Range(0f, 1f)] public float probabilidadBloqueo = 0.1f;
    [Tooltip("Cuánto sube la probabilidad de bloqueo por cada golpe seguido que recibe (aprende la combinación)")]
    [Range(0f, 0.5f)] public float bloqueoPorGolpeSeguido = 0.05f;
    [Tooltip("Probabilidad máxima de bloqueo, aun recibiendo muchos golpes seguidos")]
    [Range(0f, 1f)] public float bloqueoMaximo = 0.5f;
    [Tooltip("Segundos entre golpes para que cuenten como seguidos")]
    public float ventanaGolpesSeguidos = 1.5f;
    [Tooltip("Segundos que mantiene la guardia alta tras bloquear; los golpes en ese tiempo también se bloquean")]
    public float segundosGuardia = 0.7f;
    [Tooltip("Fracción del daño que pasa al bloquear (0 = nada, 1 = todo)")]
    [Range(0f, 1f)] public float danoTrasBloqueo = 0.15f;

    [Header("Velocidad")]
    [Tooltip("Multiplica la velocidad de sus animaciones de ataque (más alto = menos tiempo para esquivar)")]
    [Range(0.5f, 2f)] public float velocidadAtaque = 1f;
    [Tooltip("Metros por segundo a los que se acerca al jugador si este se aleja (0 = no se mueve)")]
    public float velocidadDesplazamiento = 0.5f;
    [Tooltip("Distancia al jugador que intenta mantener, en metros (debe quedar dentro de alcanceAtaque)")]
    public float distanciaCombate = 1.1f;

    [Header("Ataques")]
    [Tooltip("Segundos entre ataques (se elige uno al azar en el rango)")]
    public Vector2 intervaloAtaque = new Vector2(4f, 6f);
    [Tooltip("Cada cuántos ataques uno es uppercut; el resto son jabs (0 = nunca)")]
    public int uppercutCada = 4;
    [Tooltip("Tras recibir un golpe, segundos hasta que puede volver a atacar (reinicia la espera)")]
    public Vector2 esperaTrasGolpe = new Vector2(1.5f, 2.5f);
    [Tooltip("Segundos tras levantarse de un knockdown antes del primer ataque")]
    public float esperaTrasLevantarse = 3f;
    [Tooltip("Tras bloquear, segundos hasta el contraataque (más corto que la espera normal)")]
    public Vector2 contraataqueTrasBloqueo = new Vector2(0.6f, 1.2f);
    [Tooltip("Con la guardia del jugador baja, cuánto más rápido corre la espera hasta el próximo ataque (0 = igual, 1 = el doble)")]
    [Range(0f, 3f)] public float castigoGuardiaBaja = 0.5f;

    [Header("Daño al jugador (RF-04, RF-05, RF-06)")]
    public float danoAtaqueJab = 8f;
    public float danoAtaqueUppercut = 14f;
    [Tooltip("Segundos desde que empieza la animación hasta que el puño llega (míralo en el clip)")]
    public float retardoImpactoJab = 0.35f;
    public float retardoImpactoUppercut = 0.45f;
    [Tooltip("Hasta dónde llega el golpe desde el oponente, en metros")]
    public float alcanceAtaque = 1.5f;
    [Tooltip("Qué tan lejos de la línea del golpe puede estar la cabeza y aún recibirlo, en metros")]
    public float radioImpacto = 0.3f;
    [Tooltip("Altura del golpe sobre el suelo, en metros")]
    public float alturaAtaque = 1.45f;

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

    [Header("Sonidos (opcionales, RF-16)")]
    public AudioClip[] sonidosGolpe;
    public AudioClip sonidoBloqueo;
    public AudioClip sonidoKO;
}