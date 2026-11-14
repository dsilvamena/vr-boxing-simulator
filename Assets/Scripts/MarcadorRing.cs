using TMPro;
using UnityEngine;

/// <summary>
/// Marcador de la arena: los paneles luminosos colgados de la estructura de focos, uno por cada lado del ring, con el
/// round, el reloj y los nombres. Es parte del escenario, no un HUD (RF-14): para saber cuánto queda hay que levantar la
/// vista, como en una velada de verdad. Solo lee de ControlRondas y del Oponente; no cambia nada del combate.
/// El texto se rehace cuando cambia el segundo o el estado, no cada fotograma.
/// Lo monta Tools > Boxeo > Poner reglamento en las cinco escenas de pelea.
/// </summary>
public class MarcadorRing : MonoBehaviour
{
    [Tooltip("Un texto por panel; todos muestran lo mismo")]
    [SerializeField] private TextMeshPro[] textos = new TextMeshPro[0];
    [Tooltip("Vacío = el ControlRondas de la escena")]
    [SerializeField] private ControlRondas rondas;
    [Tooltip("Vacío = el Oponente de la escena")]
    [SerializeField] private Oponente oponente;
    [Tooltip("Cómo se llama al jugador en el marcador")]
    [SerializeField] private string nombreJugador = "TÚ";
    [Tooltip("Color del reloj en los últimos diez segundos del round")]
    [SerializeField] private Color colorUltimosSegundos = new Color(1f, 0.25f, 0.2f);
    [SerializeField] private Color colorNormal = new Color(1f, 0.78f, 0.25f);

    private int ultimoSegundo = -1;
    private EstadoRondas ultimoEstado = (EstadoRondas)(-1);
    private int ultimoRound = -1;

    private void Awake()
    {
        if (rondas == null) rondas = FindAnyObjectByType<ControlRondas>();
        if (oponente == null) oponente = FindAnyObjectByType<Oponente>();
        if (rondas == null) enabled = false;
    }

    private void Update()
    {
        int segundo = Mathf.CeilToInt(Mathf.Max(0f, rondas.SegundosRestantes));
        if (segundo == ultimoSegundo && rondas.Estado == ultimoEstado && rondas.RoundActual == ultimoRound) return;
        ultimoSegundo = segundo;
        ultimoEstado = rondas.Estado;
        ultimoRound = rondas.RoundActual;

        string reloj = $"{segundo / 60}:{segundo % 60:00}";
        string arriba;
        bool apurado = false;
        switch (rondas.Estado)
        {
            case EstadoRondas.Preparacion:
                arriba = "ROUND 1";
                break;
            case EstadoRondas.Round:
                arriba = $"ROUND {rondas.RoundActual}   {reloj}";
                apurado = segundo <= 10;
                break;
            case EstadoRondas.Descanso:
                arriba = $"DESCANSO   {reloj}";
                break;
            default:
                arriba = "FIN DEL COMBATE";
                break;
        }
        string rival = oponente != null ? oponente.Nombre.ToUpperInvariant() : "";
        string texto = $"{arriba}\n<size=52%><color=#FFFFFF>{nombreJugador}  VS  {rival}</color></size>";
        foreach (TextMeshPro t in textos)
        {
            if (t == null) continue;
            t.color = apurado ? colorUltimosSegundos : colorNormal;
            t.text = texto;
        }
    }
}
