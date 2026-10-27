using System;
using System.Collections;
using UnityEngine;

public enum ResultadoAtaque { Conectado, Bloqueado, Esquivado }

/// <summary>
/// Salud del jugador. Va en el OVRCameraRig. Resuelve los ataques del oponente:
/// esquive (cabeza fuera de la línea del golpe), bloqueo (ambas manos en guardia frente a la cara) o impacto.
/// </summary>
public class SaludJugador : MonoBehaviour
{
    [SerializeField] private float saludMaxima = 100f;
    [Tooltip("Distancia máxima (m) entre cada mano y la cabeza para contar como guardia")]
    [SerializeField] private float distanciaGuardia = 0.4f;
    [Tooltip("Fracción del daño que pasa al bloquear (0 = nada, 1 = todo)")]
    [SerializeField, Range(0f, 1f)] private float danoTrasBloqueo = 0.25f;
    [Tooltip("Escribe cada ataque en la consola")]
    [SerializeField] private bool registrarAtaques = true;
    [Tooltip("Añade posiciones y desvíos al log (solo para calibrar)")]
    [SerializeField] private bool registroDetallado = false;

    public event Action<float, float> AlCambiarSalud;       // salud, máxima
    public event Action<ResultadoAtaque> AlResolverAtaque;
    public event Action AlQuedarseSinSalud;

    public float Salud { get; private set; }
    public float SaludMaxima => saludMaxima;
    public float SaludNormalizada => saludMaxima > 0f ? Salud / saludMaxima : 0f;
    public bool SinSalud => Salud <= 0f;
    /// <summary>Posición de la cabeza del jugador en mundo (el oponente apunta aquí al empezar su ataque).</summary>
    public Vector3 PosicionCabeza => rig != null ? rig.centerEyeAnchor.position : transform.position;

    // Patrones de vibración: x = amplitud (0 = pausa), y = segundos
    private static readonly Vector2[] PatronRecibido = { new(1f, 0.25f) };
    private static readonly Vector2[] PatronBloqueado = { new(0.6f, 0.05f), new(0f, 0.04f), new(0.6f, 0.05f) };

    private OVRCameraRig rig;
    private Coroutine vibracion;

    private void Awake()
    {
        rig = GetComponentInParent<OVRCameraRig>();
        if (rig == null) Debug.LogWarning($"{name}: SaludJugador necesita estar en el OVRCameraRig.", this);
        Salud = saludMaxima;
    }

    private void OnDisable() => DetenerVibracion();

    /// <summary>
    /// El golpe viaja en línea recta desde origen en la dirección dada, hasta alcance metros.
    /// Si la cabeza queda a más de radio de esa línea, el golpe falla (esquive o fuera de alcance).
    /// </summary>
    public ResultadoAtaque RecibirAtaque(Vector3 origen, Vector3 direccion, float alcance, float radio, float dano)
    {
        // Sin salud o sin rig no hay nada que golpear
        if (SinSalud || rig == null) return ResultadoAtaque.Esquivado;

        Transform cabeza = rig.centerEyeAnchor;
        Vector3 dir = direccion.normalized;
        Vector3 fin = origen + dir * alcance;
        Vector3 masCercano = PuntoMasCercano(origen, fin, cabeza.position);
        float distancia = Vector3.Distance(cabeza.position, masCercano);

        ResultadoAtaque resultado;
        if (distancia > radio) resultado = ResultadoAtaque.Esquivado;
        else if (EnGuardia(cabeza)) resultado = ResultadoAtaque.Bloqueado;
        else resultado = ResultadoAtaque.Conectado;

        if (resultado == ResultadoAtaque.Bloqueado) dano *= danoTrasBloqueo;
        if (resultado != ResultadoAtaque.Esquivado)
        {
            Salud = Mathf.Max(0f, Salud - dano);
            AlCambiarSalud?.Invoke(Salud, saludMaxima);
            Vibrar(resultado == ResultadoAtaque.Bloqueado ? PatronBloqueado : PatronRecibido);
        }

        if (registrarAtaques)
        {
            string resumen = $"Ataque: {resultado} → salud {Salud:F0}/{saludMaxima:F0}";
            if (!registroDetallado)
            {
                Debug.Log(resumen, this);
            }
            else
            {
                Vector3 d = cabeza.position - masCercano;
                float alLargo = Vector3.Dot(cabeza.position - origen, dir);
                Debug.Log($"{resumen} | a lo largo {alLargo:F2}/{alcance:F2} m | desvío vertical {d.y:F2}, horizontal {new Vector2(d.x, d.z).magnitude:F2} (radio {radio:F2}) | cabeza {cabeza.position.ToString("F2")}", this);
            }
        }

        AlResolverAtaque?.Invoke(resultado);
        if (SinSalud && resultado != ResultadoAtaque.Esquivado) AlQuedarseSinSalud?.Invoke();
        return resultado;
    }

    public void Reiniciar()
    {
        Salud = saludMaxima;
        AlCambiarSalud?.Invoke(Salud, saludMaxima);
    }

    private bool EnGuardia(Transform cabeza) =>
        ManoEnGuardia(rig.leftHandAnchor, cabeza) && ManoEnGuardia(rig.rightHandAnchor, cabeza);

    // Cerca de la cara y por delante de ella
    private bool ManoEnGuardia(Transform mano, Transform cabeza)
    {
        Vector3 aMano = mano.position - cabeza.position;
        return aMano.magnitude <= distanciaGuardia && Vector3.Dot(aMano, cabeza.forward) > 0f;
    }

    private static Vector3 PuntoMasCercano(Vector3 a, Vector3 b, Vector3 p)
    {
        Vector3 ab = b - a;
        float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 0.0001f));
        return a + ab * t;
    }

    private void Vibrar(Vector2[] patron)
    {
        float escala = PlayerPrefs.GetFloat("haptica", 1f);
        if (escala <= 0.01f) return;
        DetenerVibracion();
        vibracion = StartCoroutine(Vibracion(patron, escala));
    }

    private IEnumerator Vibracion(Vector2[] patron, float escala)
    {
        foreach (Vector2 pulso in patron)
        {
            float amplitud = pulso.x * escala;
            float frecuencia = amplitud > 0f ? 1f : 0f;
            OVRInput.SetControllerVibration(frecuencia, amplitud, OVRInput.Controller.LTouch);
            OVRInput.SetControllerVibration(frecuencia, amplitud, OVRInput.Controller.RTouch);
            yield return new WaitForSecondsRealtime(pulso.y);
        }
        ApagarControles();
        vibracion = null;
    }

    private void DetenerVibracion()
    {
        if (vibracion == null) return;
        StopCoroutine(vibracion);
        vibracion = null;
        ApagarControles();
    }

    private static void ApagarControles()
    {
        OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.LTouch);
        OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.RTouch);
    }
}